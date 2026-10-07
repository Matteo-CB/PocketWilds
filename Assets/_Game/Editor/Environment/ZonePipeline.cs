using System;
using System.Linq;
using PocketWilds.Editor.World;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace PocketWilds.Editor.Environment
{
    /// <summary>
    /// T35A: builds DEF_ZoneKit_Grassland, the hedge and sign that close the next zone of the Prairie (doc 02
    /// Améliorations, section 4.6). The hedge uses the very bush meshes, material and palette of the Prairie's own low
    /// bushes (read from the start chunk), so it looks like the meadow and adds no asset to the build. Addressable in its
    /// own group (the kit group is rewritten by the world setup).
    /// </summary>
    public static class ZonePipeline
    {
        public const string KitPath = "Assets/_Game/Content/World/DEF_ZoneKit_Grassland.asset";
        public const string Address = "zones.grassland";
        public const string Group = "Biome_Grassland_Zones";

        /// <summary>The hedge segment and the sign (pw_grassland_t35a_props.py). Bushes stand in until they exist.</summary>
        public const string ModelPath = "Assets/_Game/Art/Environment/Grassland/Models/GL_Props_T35A.fbx";

        [MenuItem("Pocket Wilds/World/Build Zone Kit (T35A)", priority = 185)]
        public static void BuildMenu() => Debug.Log("[ZonePipeline] " + Build());

        public static string Build()
        {
            var start = AssetDatabase.LoadAssetAtPath<WorldChunk>(GrasslandPipeline.ChunkPath);
            if (start == null)
                throw new InvalidOperationException("Start chunk missing: " + GrasslandPipeline.ChunkPath);
            // The Prairie's low round bushes as the start chunk draws them: mesh, material, palette, brightness.
            WorldChunk.PaletteBinding bushA = default, bushB = default, tree = default;
            for (int i = 0; i < start.BindingCount; i++)
            {
                var binding = start.GetBinding(i);
                if (binding.renderer == null || binding.vertexBrightness)
                    continue;
                string name = binding.renderer.name;
                if (bushA.renderer == null && name.Contains("Bush_A"))
                    bushA = binding;
                else if (bushB.renderer == null && name.Contains("Bush_B"))
                    bushB = binding;
                else if (tree.renderer == null && name.StartsWith("GL_Tree_A", StringComparison.Ordinal))
                    tree = binding;
            }
            if (bushA.renderer == null || bushB.renderer == null)
                throw new InvalidOperationException("The start chunk has no Bush_A and Bush_B renderers to copy.");
            // The sign's wood is the meadow fence's: its palette and brightness.
            WorldChunk.PaletteBinding fence = default;
            foreach (var path in new[] { GrasslandPipeline.ChunkPath, GrasslandPipeline.MeadowChunkPath })
            {
                var chunk = AssetDatabase.LoadAssetAtPath<WorldChunk>(path);
                for (int i = 0; chunk != null && i < chunk.BindingCount && fence.renderer == null; i++)
                {
                    var binding = chunk.GetBinding(i);
                    if (binding.renderer != null && !binding.vertexBrightness && binding.renderer.name.Contains("Fence"))
                        fence = binding;
                }
            }

            // T35A pieces modeled for the zones (pw_grassland_t35a_props.py): the hedge segment and the sign, as
            // standalone meshes (never the FBX itself in a bundle, see GrasslandPipeline.KitMesh).
            var models = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Mesh>().ToArray();
            var hedgeSource = models.FirstOrDefault(m => m.name == "GL_ZoneHedge");
            var signSource = models.FirstOrDefault(m => m.name == "GL_ZoneSign");
            var gateSource = models.FirstOrDefault(m => m.name == "GL_BiomeGate_Forest");
            var hedge = hedgeSource != null ? GrasslandPipeline.KitMesh(hedgeSource) : null;
            var sign = signSource != null ? GrasslandPipeline.KitMesh(signSource) : null;
            var gate = gateSource != null ? GrasslandPipeline.KitMesh(gateSource) : null;

            var kit = AssetDatabase.LoadAssetAtPath<ZoneKit>(KitPath);
            if (kit == null)
            {
                kit = ScriptableObject.CreateInstance<ZoneKit>();
                AssetDatabase.CreateAsset(kit, KitPath);
            }
            var bushMeshA = bushA.renderer.GetComponent<MeshFilter>().sharedMesh;
            var bushMeshB = bushB.renderer.GetComponent<MeshFilter>().sharedMesh;
            kit.bushes = hedge != null ? new[] { hedge } : new[] { bushMeshA, bushMeshB };
            kit.material = bushA.renderer.sharedMaterial;
            kit.paletteId = bushA.paletteId;
            kit.brightness = bushA.brightness;
            kit.bushHeight = kit.bushes.Max(m => m.bounds.max.y);
            kit.bushRadius = kit.bushes.Max(m => Radius(m));
            kit.spacing = 2f * kit.bushRadius;
            kit.signMesh = sign;
            if (sign != null)
            {
                kit.signRadius = Radius(sign);
                kit.signHeight = sign.bounds.max.y;
                kit.signPaletteId = fence.renderer != null ? fence.paletteId : bushA.paletteId;
                kit.signBrightness = fence.renderer != null ? fence.brightness : (byte)WorldChunk.NeutralBrightness;
            }
            // The Forest's gate: the Prairie trees' wood and leaves (their material and palette).
            kit.gateMesh = gate;
            if (gate != null)
            {
                var treeBinding = tree.renderer != null ? tree : bushA;
                kit.gateMaterial = treeBinding.renderer.sharedMaterial;
                kit.gatePaletteId = treeBinding.paletteId;
                kit.gateBrightness = treeBinding.brightness;
                kit.gateHalfSpan = 1.25f;
                kit.gateTrunkRadius = 0.3f;
                kit.gateHeight = gate.bounds.max.y;
            }
            EditorUtility.SetDirty(kit);
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var group = settings.FindGroup(Group) ?? settings.CreateGroup(Group, false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var bundled = group.GetSchema<BundledAssetGroupSchema>();
            bundled.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            bundled.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
            bundled.IncludeInBuild = true;
            bundled.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            bundled.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            group.GetSchema<ContentUpdateGroupSchema>().StaticContent = true;
            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(KitPath), group, false, false);
            entry.address = Address;
            entry.SetLabel(WorldContentPipeline.BiomeLabel, true, true, false);
            EditorUtility.SetDirty(group);
            AssetDatabase.SaveAssets();

            return $"{KitPath}: hedge {string.Join(", ", kit.bushes.Select(m => m.name))} (half length {kit.bushRadius:F2} m, " +
                   $"height {kit.bushHeight:F2} m, {kit.bushes[0].GetIndexCount(0) / 3} triangles), palette {kit.paletteId}, " +
                   $"sign {(sign != null ? $"{sign.name} ({sign.GetIndexCount(0) / 3} triangles, palette {kit.signPaletteId})" : "not modeled yet")}, " +
                   $"gate {(gate != null ? $"{gate.name} ({gate.GetIndexCount(0) / 3} triangles, {kit.gateHeight:F2} m, palette {kit.gatePaletteId})" : "not modeled yet")}.";
        }

        private static float Radius(Mesh mesh)
        {
            var b = mesh.bounds;
            return Mathf.Max(Mathf.Max(Mathf.Abs(b.min.x), Mathf.Abs(b.max.x)), Mathf.Max(Mathf.Abs(b.min.z), Mathf.Abs(b.max.z)));
        }
    }
}
