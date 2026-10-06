using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PocketWilds.Bootstrap;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.Build
{
    /// <summary>
    /// T34 shader preparation data. A Windows benchmark run with "-pw-trace-graphics &lt;file&gt;" records every graphics
    /// state the game draws (all scenarios, the three tiers and every adaptive quality step); this pipeline imports
    /// the file as a GraphicsStateCollection (one per platform and graphics API), rebuilds the shader variant
    /// collection used as a fallback on platforms without a trace yet, and wires DEF_ShaderWarmup into the bootstrap.
    /// Retrace after any shader change: a stale trace only misses the changed variants.
    /// </summary>
    public static class ShaderWarmupPipeline
    {
        public const string Folder = "Assets/_Game/Content/Graphics";
        public const string SetPath = Folder + "/DEF_ShaderWarmup.asset";
        public const string VariantsPath = Folder + "/SVC_Game.shadervariants";
        public const string BootstrapScenePath = "Assets/_Game/Scenes/SC_Bootstrap.unity";
        public const string DefaultTracePath = "C:/tmp/pw/trace.graphicsstate";

        [MenuItem("Pocket Wilds/Build/Import Graphics State Trace (T34)", priority = 312)]
        public static void ImportMenu() => Debug.Log("[ShaderWarmup] " + ImportTrace(DefaultTracePath));

        public static ShaderWarmupSet EnsureSet()
        {
            Directory.CreateDirectory(Folder);
            var set = AssetDatabase.LoadAssetAtPath<ShaderWarmupSet>(SetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<ShaderWarmupSet>();
                AssetDatabase.CreateAsset(set, SetPath);
            }
            return set;
        }

        /// <summary>
        /// Imports a player run's traces (one file per boot, &lt;file&gt;_NN, merged into one collection), updates
        /// DEF_ShaderWarmup and the fallback variants, wires the bootstrap.
        /// </summary>
        public static string ImportTrace(string basePath)
        {
            string folder = Path.GetDirectoryName(basePath) ?? ".";
            string stem = Path.GetFileNameWithoutExtension(basePath);
            string extension = Path.GetExtension(basePath);
            var files = Directory.Exists(folder)
                ? Directory.GetFiles(folder, stem + "_??" + extension).OrderBy(f => f, StringComparer.Ordinal).ToList()
                : new List<string>();
            if (files.Count == 0 && File.Exists(basePath))
                files.Add(basePath);
            if (files.Count == 0)
                throw new FileNotFoundException("No graphics state trace (run the benchmark player with -pw-trace-graphics).", basePath);
            string tracePath = Path.Combine(folder, stem + "_merged" + extension);
            var merged = new GraphicsStateCollection();
            var variants = new List<GraphicsStateCollection.ShaderVariant>();
            var states = new List<GraphicsStateCollection.GraphicsState>();
            for (int f = 0; f < files.Count; f++)
            {
                var part = new GraphicsStateCollection();
                if (!part.LoadFromFile(files[f]))
                    throw new InvalidOperationException("Not a graphics state collection: " + files[f]);
                if (f == 0)
                {
                    merged.runtimePlatform = part.runtimePlatform;
                    merged.graphicsDeviceType = part.graphicsDeviceType;
                    merged.version = part.version;
                    merged.qualityLevelName = part.qualityLevelName;
                }
                variants.Clear();
                part.GetVariants(variants);
                foreach (var variant in variants)
                {
                    if (variant.shader == null)
                        continue;
                    merged.AddVariant(variant.shader, variant.passId, variant.keywords);
                    states.Clear();
                    part.GetGraphicsStatesForVariant(variant, states);
                    foreach (var state in states)
                        merged.AddGraphicsStateForVariant(variant, state);
                }
                Object.DestroyImmediate(part);
            }
            if (!merged.SaveToFile(tracePath))
                throw new IOException("Could not write " + tracePath);
            Object.DestroyImmediate(merged);

            var probe = new GraphicsStateCollection();
            if (!probe.LoadFromFile(tracePath))
                throw new InvalidOperationException("Not a graphics state collection: " + tracePath);
            string name = $"GSC_{probe.runtimePlatform}_{probe.graphicsDeviceType}";
            string assetPath = $"{Folder}/{name}.graphicsstate";
            Directory.CreateDirectory(Folder);
            File.Copy(tracePath, assetPath, true);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            var collection = AssetDatabase.LoadAssetAtPath<GraphicsStateCollection>(assetPath);
            if (collection == null)
                throw new InvalidOperationException("Unity did not import " + assetPath + " as a GraphicsStateCollection.");

            var set = EnsureSet();
            set.collections = set.collections
                .Where(c => c != null && !(c.runtimePlatform == collection.runtimePlatform && c.graphicsDeviceType == collection.graphicsDeviceType))
                .Append(collection).ToArray();
            int fallback = RebuildVariants(set);
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            WireBootstrap(set);
            Object.DestroyImmediate(probe);
            // T34C: each Addressables group warms its own copy of its shaders: the new trace is split by owner.
            string split = PocketWilds.Editor.World.CommonContentPipeline.SplitAfterImport(collection.runtimePlatform, collection.graphicsDeviceType);
            return $"{name} from {files.Count} traces: {collection.variantCount} variants, {collection.totalGraphicsStateCount} states; fallback {fallback} variants; {split}";
        }

        /// <summary>The traced variants as a ShaderVariantCollection (compiles them where no traced states exist).</summary>
        public static int RebuildVariants(ShaderWarmupSet set) => RebuildVariants(set, VariantsPath);

        /// <summary>T34C: one fallback collection per warmup set (built-in, world, animals).</summary>
        public static int RebuildVariants(ShaderWarmupSet set, string path)
        {
            var svc = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(path);
            if (svc == null)
            {
                svc = new ShaderVariantCollection();
                AssetDatabase.CreateAsset(svc, path);
            }
            svc.Clear();
            var list = new List<GraphicsStateCollection.ShaderVariant>();
            foreach (var collection in set.collections)
            {
                if (collection == null)
                    continue;
                list.Clear();
                collection.GetVariants(list);
                foreach (var variant in list)
                {
                    if (variant.shader == null)
                        continue;
                    var passType = PassTypeOf(variant.shader, variant.passId);
                    var keywords = variant.keywords?.Select(k => k.name).Where(n => !string.IsNullOrEmpty(n)).ToArray() ?? Array.Empty<string>();
                    try
                    {
                        svc.Add(new ShaderVariantCollection.ShaderVariant(variant.shader, passType, keywords));
                    }
                    catch (ArgumentException)
                    {
                        // A combination the pass type cannot express: the traced state still covers it on its platform.
                    }
                }
            }
            set.variants = svc;
            EditorUtility.SetDirty(svc);
            return svc.variantCount;
        }

        private static PassType PassTypeOf(Shader shader, PassIdentifier pass)
        {
            var data = ShaderUtil.GetShaderData(shader);
            if (pass.SubshaderIndex >= data.SubshaderCount)
                return PassType.ScriptableRenderPipeline;
            var subshader = data.GetSubshader((int)pass.SubshaderIndex);
            if (pass.PassIndex >= subshader.PassCount)
                return PassType.ScriptableRenderPipeline;
            string mode = subshader.GetPass((int)pass.PassIndex).FindTagValue(new ShaderTagId("LightMode")).name;
            return mode switch
            {
                "ShadowCaster" => PassType.ShadowCaster,
                "Meta" => PassType.Meta,
                null or "" or "SRPDefaultUnlit" => PassType.ScriptableRenderPipelineDefaultUnlit,
                _ => PassType.ScriptableRenderPipeline,
            };
        }

        /// <summary>DEF_ShaderWarmup on the bootstrap's GameBootstrap (scene saved).</summary>
        public static void WireBootstrap(ShaderWarmupSet set)
        {
            var active = EditorSceneManager.GetActiveScene();
            string previous = active.path;
            var scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
            var bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            if (bootstrap == null)
                throw new InvalidOperationException("SC_Bootstrap has no GameBootstrap.");
            // SerializedObject refused this reference from editor tool calls: the field is set directly, then saved.
            typeof(GameBootstrap).GetField("_shaderWarmup", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(bootstrap, set);
            EditorUtility.SetDirty(bootstrap);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (!string.IsNullOrEmpty(previous) && previous != BootstrapScenePath)
                EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        }
    }
}
