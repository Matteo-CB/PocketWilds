using System;
using System.Collections.Generic;
using System.Text;
using PocketWilds.Content;
using PocketWilds.Editor.UI;
using PocketWilds.Presentation;
using PocketWilds.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.Environment
{
    /// <summary>
    /// T48B the season looks (DEF_SeasonLooks), built from the Grassland base palettes (DEF_Palettes_Grassland) with hand-tuned
    /// transforms per palette class, never by a generator: ground, grass (and its tips), crowns (trees, bushes, leaves, clover,
    /// roots) and the flowers' greens change; rocks, pebbles, wood, hay, stone, water, windmill, well, fences, mushrooms and life
    /// never do, and inside a changed palette only its green colors move (bark, petals, fruit and paths keep theirs). Soleil is
    /// the validated look: no row. Also the shared quad (MSH_SeasonQuad, Baked/Weather), MAT_SeasonDrift and MAT_Firefly, the
    /// fireflies' species (at the butterflies' anchors) and the wiring of the vertical slice. Rerun after a base palette change.
    /// </summary>
    public static class SeasonLookPipeline
    {
        public const string LooksPath = "Assets/_Game/Content/World/DEF_SeasonLooks.asset";
        public const string QuadPath = GrasslandPipeline.WeatherFolder + "/MSH_SeasonQuad.asset";
        public const string DriftMaterialPath = "Assets/_Game/Art/Materials/MAT_SeasonDrift.mat";
        public const string FireflyMaterialPath = "Assets/_Game/Art/Materials/MAT_Firefly.mat";

        private enum PaletteClass : byte
        {
            None,
            Ground,
            Grass,
            Crown,
            Flower,
        }

        /// <summary>Palette names (after the biome) that never change with the seasons.</summary>
        private static readonly string[] Fixed = { "rock", "pebble", "wood", "hay", "stone", "water", "windmill", "well", "fence", "mushroom", "life_" };

        private static readonly Color FrostWhite = new Color(0.88f, 0.93f, 0.97f);
        private static readonly Color FrostTip = new Color(0.92f, 0.95f, 0.98f);

        [MenuItem("Pocket Wilds/World/Build Season Looks (T48B)", priority = 231)]
        public static void BuildMenu() => Debug.Log("[SeasonLookPipeline] " + BuildAll());

        public static string BuildAll()
        {
            var quad = EnsureQuad();
            var drift = EnsureMaterial(DriftMaterialPath, "PocketWilds/SeasonDrift");
            var firefly = EnsureMaterial(FireflyMaterialPath, "PocketWilds/Firefly");
            var palettes = AssetDatabase.LoadAssetAtPath<PaletteSetAsset>(GrasslandPipeline.PalettesPath)
                           ?? throw new InvalidOperationException("Missing " + GrasslandPipeline.PalettesPath);
            var set = AssetDatabase.LoadAssetAtPath<SeasonLookSet>(LooksPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<SeasonLookSet>();
                AssetDatabase.CreateAsset(set, LooksPath);
            }
            set.quadMesh = quad;
            set.driftMaterial = drift;
            set.fireflyMaterial = firefly;
            set.maxDrift = SeasonDrift.MaxInstances;
            set.driftBox = new Vector3(14f, 5f, 14f);
            set.fireflies = new AmbientLifeKit.Species
            {
                id = "life.firefly", mesh = quad, paletteId = 0, behaviour = AmbientLifeBehaviour.Firefly, anchorPrefix = ButterflyAnchors(),
                perAnchor = 2, spawnChance = 0.8f, scale = 0.1f, flap = Vector3.zero, flapGrounded = Vector3.zero, shoulder = Vector2.zero,
            };
            var report = new StringBuilder();
            set.looks = new[]
            {
                Awakening(Rows(palettes, RefugeSeason.Awakening, report)),
                Sun(),
                Harvest(Rows(palettes, RefugeSeason.Harvest, report)),
                Frost(Rows(palettes, RefugeSeason.Frost, report)),
            };
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            WireScene(set);
            return "DEF_SeasonLooks built and wired; rows " + report;
        }

        // ---------------- looks (hand-tuned) ----------------

        private static SeasonLookSet.Look Awakening(SeasonLookSet.PaletteRow[] rows) => new SeasonLookSet.Look
        {
            season = RefugeSeason.Awakening, palettes = rows,
            sunTint = new Color(0.98f, 1f, 0.97f), sunIntensity = 1f, ambientTint = new Color(0.98f, 1.02f, 0.99f), skyTint = new Color(0.97f, 1f, 1.03f),
            cloudTint = Color.white, morningSky = Color.white, morningSkyWeight = 0f, fogTint = new Color(0.86f, 0.92f, 0.86f), fogTintWeight = 0.12f,
            morningMist = 0.15f, showerFrequency = 1.8f, rain = true, rainbow = true,
            drift = SeasonDriftKind.Petals, driftColorA = Hex("F6C6D6"), driftColorB = Hex("FBF1DC"), driftAmount = 0.55f, driftSize = 0.055f,
            driftFall = 0.35f, driftSway = 0.45f, fireflies = false,
        };

        private static SeasonLookSet.Look Sun() => new SeasonLookSet.Look
        {
            season = RefugeSeason.Sun, palettes = Array.Empty<SeasonLookSet.PaletteRow>(),
            sunTint = Color.white, sunIntensity = 1f, ambientTint = Color.white, skyTint = Color.white, cloudTint = Color.white, morningSky = Color.white,
            morningSkyWeight = 0f, fogTint = Color.white, fogTintWeight = 0f, morningMist = 0f, showerFrequency = 1f, rain = true, rainbow = true,
            drift = SeasonDriftKind.None, driftAmount = 0f, fireflies = true,
        };

        private static SeasonLookSet.Look Harvest(SeasonLookSet.PaletteRow[] rows) => new SeasonLookSet.Look
        {
            season = RefugeSeason.Harvest, palettes = rows,
            sunTint = new Color(1.04f, 0.98f, 0.9f), sunIntensity = 0.98f, ambientTint = new Color(1.03f, 0.99f, 0.93f), skyTint = new Color(1.02f, 0.99f, 0.95f),
            cloudTint = new Color(1.02f, 0.98f, 0.94f), morningSky = Hex("F6DDBF"), morningSkyWeight = 0.25f, fogTint = Hex("F1E3CC"), fogTintWeight = 0.25f,
            morningMist = 0.75f, showerFrequency = 1.1f, rain = true, rainbow = true,
            drift = SeasonDriftKind.Leaves, driftColorA = Hex("E3A04A"), driftColorB = Hex("D9783C"), driftAmount = 0.45f, driftSize = 0.075f,
            driftFall = 0.55f, driftSway = 0.6f, fireflies = false,
        };

        private static SeasonLookSet.Look Frost(SeasonLookSet.PaletteRow[] rows) => new SeasonLookSet.Look
        {
            season = RefugeSeason.Frost, palettes = rows,
            sunTint = new Color(0.93f, 0.97f, 1.05f), sunIntensity = 0.95f, ambientTint = new Color(0.94f, 0.98f, 1.06f), skyTint = new Color(0.95f, 0.99f, 1.06f),
            cloudTint = new Color(0.98f, 0.99f, 1.02f), morningSky = Hex("F4C9D6"), morningSkyWeight = 0.55f, fogTint = Hex("E6EEF5"), fogTintWeight = 0.35f,
            morningMist = 0.3f, showerFrequency = 1f, rain = false, rainbow = false,
            drift = SeasonDriftKind.Snow, driftColorA = Hex("FFFFFF"), driftColorB = Hex("E8F1FA"), driftAmount = 0.8f, driftSize = 0.035f,
            driftFall = 0.45f, driftSway = 0.25f, fireflies = false,
        };

        // ---------------- palette transforms ----------------

        private static SeasonLookSet.PaletteRow[] Rows(PaletteSetAsset set, RefugeSeason season, StringBuilder report)
        {
            var rows = new List<SeasonLookSet.PaletteRow>();
            foreach (var entry in set.palettes)
            {
                if (entry == null || string.IsNullOrEmpty(entry.sourceId))
                    continue;
                var kind = Classify(entry.sourceId);
                if (kind == PaletteClass.None)
                    continue;
                var colors = entry.ToRuntime().Colors;
                var seasonal = new Color32[colors.Length];
                bool changed = false;
                for (int c = 0; c < colors.Length; c++)
                {
                    seasonal[c] = Transform(colors[c], kind, c, season);
                    changed |= !Same(seasonal[c], colors[c]);
                }
                if (changed)
                    rows.Add(new SeasonLookSet.PaletteRow { paletteId = entry.sourceId, colors = seasonal });
            }
            report.Append(season).Append(' ').Append(rows.Count).Append("; ");
            return rows.ToArray();
        }

        private static PaletteClass Classify(string id)
        {
            string name = id.Substring(id.LastIndexOf('.') + 1);
            foreach (var word in Fixed)
            {
                if (name.Contains(word))
                    return PaletteClass.None;
            }
            if (name.Contains("ground"))
                return PaletteClass.Ground;
            if (name.Contains("grass"))
                return PaletteClass.Grass;
            if (name.Contains("flower") && !name.Contains("bush"))
                return PaletteClass.Flower;
            foreach (var word in new[] { "tree", "bush", "birch", "leaf", "clover", "roots", "hedge", "moss", "fern" })
            {
                if (name.Contains(word))
                    return PaletteClass.Crown;
            }
            return PaletteClass.None;
        }

        private static bool IsGreen(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            return h >= 0.16f && h <= 0.48f && s >= 0.15f && v >= 0.12f;
        }

        /// <summary>One color of a palette in a season. Only greens move (and the grass tips, column 2 of the grass palettes).</summary>
        private static Color32 Transform(Color32 source, PaletteClass kind, int column, RefugeSeason season)
        {
            Color c = source;
            bool tip = kind == PaletteClass.Grass && column == 2;
            bool green = IsGreen(c);
            if (!green && !tip)
            {
                // Éveil: the flowers' petals a little more vivid.
                if (season == RefugeSeason.Awakening && kind == PaletteClass.Flower && column != 5)
                    return Rebuild(c, 0f, 0f, 1.08f, 1f);
                return source;
            }
            Color.RGBToHSV(c, out float h, out float s, out float v);
            switch (season)
            {
                case RefugeSeason.Awakening:
                    // Fresher and greener: a step toward pure spring green, a touch more saturated and light.
                    return tip ? Rebuild(c, 0.2f, 0.35f, 1.05f, 1.02f) : Rebuild(c, 0.31f, 0.18f, 1.1f, 1.04f);
                case RefugeSeason.Harvest:
                    if (tip)
                        return Color.HSVToRGB(0.115f, Mathf.Max(s, 0.48f), Mathf.Clamp(v, 0.78f, 0.9f));
                    switch (kind)
                    {
                        case PaletteClass.Crown:
                            // Light leaves turn golden, deep ones amber: the crowns keep their shading.
                            return v > 0.62f ? Rebuild(c, 0.12f, 0.72f, 1.18f, 1.05f) : Rebuild(c, 0.075f, 0.68f, 1.2f, 1.02f);
                        case PaletteClass.Grass:
                            return Rebuild(c, 0.14f, 0.36f, 1.06f, 1.04f);
                        case PaletteClass.Ground:
                            return Rebuild(c, 0.15f, 0.32f, 1.02f, 1.05f);
                        default:
                            return Rebuild(c, 0.2f, 0.18f, 1f, 1f);
                    }
                case RefugeSeason.Frost:
                    if (tip)
                        return Color.Lerp(c, FrostTip, 0.65f);
                    var cool = Rebuild(c, 0.36f, 0.15f, kind == PaletteClass.Ground ? 0.5f : 0.58f, 1.04f);
                    float frost = kind == PaletteClass.Ground ? 0.3f : v > 0.7f ? 0.38f : 0.16f;
                    return Color.Lerp(cool, FrostWhite, frost);
                default:
                    return source;
            }
        }

        /// <summary>Hue a share of the way toward a target (0 target: hue kept), saturation and value scaled.</summary>
        private static Color32 Rebuild(Color c, float targetHue, float toward, float saturation, float value)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            if (toward > 0f)
                h = Mathf.Repeat(h + Mathf.DeltaAngle(h * 360f, targetHue * 360f) / 360f * toward, 1f);
            var rgb = Color.HSVToRGB(h, Mathf.Clamp01(s * saturation), Mathf.Clamp01(v * value));
            rgb.a = 1f;
            return rgb;
        }

        private static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

        private static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : Color.magenta;

        // ---------------- assets ----------------

        private static string ButterflyAnchors()
        {
            var kit = AssetDatabase.LoadAssetAtPath<AmbientLifeKit>(GrasslandPipeline.LifeKitPath);
            if (kit != null)
            {
                foreach (var species in kit.species)
                {
                    if (species.behaviour == AmbientLifeBehaviour.Butterfly && !string.IsNullOrEmpty(species.anchorPrefix))
                        return species.anchorPrefix;
                }
            }
            return "VFX_Butterfly";
        }

        /// <summary>The unit quad (corners at plus or minus 0.5 in x and y) shared by the drift and the fireflies.</summary>
        private static Mesh EnsureQuad()
        {
            var mesh = new Mesh
            {
                name = "MSH_SeasonQuad",
                vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) },
                uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) },
                triangles = new[] { 0, 2, 1, 0, 3, 2 },
            };
            mesh.RecalculateBounds();
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(QuadPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, QuadPath);
                return mesh;
            }
            EditorUtility.CopySerialized(mesh, existing);
            existing.name = "MSH_SeasonQuad";
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static Material EnsureMaterial(string path, string shaderName)
        {
            var shader = Shader.Find(shaderName) ?? throw new InvalidOperationException("Shader not found: " + shaderName);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>The vertical slice's entry reads DEF_SeasonLooks (palette rows, light, drift, fireflies).</summary>
        public static void WireScene(SeasonLookSet set)
        {
            var scene = EditorSceneManager.OpenScene(UiPipeline.ScenePath, OpenSceneMode.Single);
            // Load again after the scene switch: it can unload assets loaded before it (the reference then saved as none).
            string path = AssetDatabase.GetAssetPath(set);
            set = AssetDatabase.LoadAssetAtPath<SeasonLookSet>(string.IsNullOrEmpty(path) ? LooksPath : path)
                  ?? throw new InvalidOperationException("Missing " + LooksPath);
            VerticalSliceEntry entry = null;
            foreach (var root in scene.GetRootGameObjects())
                entry = entry != null ? entry : root.GetComponentInChildren<VerticalSliceEntry>(true);
            if (entry == null)
                throw new InvalidOperationException("SceneEntry (VerticalSliceEntry) missing in " + UiPipeline.ScenePath);
            var serialized = new SerializedObject(entry);
            serialized.FindProperty("_seasonLooks").objectReferenceValue = set;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
