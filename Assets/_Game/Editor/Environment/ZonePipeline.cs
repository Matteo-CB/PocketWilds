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

        /// <summary>Optional sign mesh (exported by pw_grassland_t35a_props.py), used once it exists.</summary>
        public const string SignMeshPath = "Assets/_Game/Art/Environment/Grassland/Baked/GL_ZoneSign.asset";

        [MenuItem("Pocket Wilds/World/Build Zone Kit (T35A)", priority = 185)]
        public static void BuildMenu() => Debug.Log("[ZonePipeline] " + Build());

        public static string Build()
        {
            var start = AssetDatabase.LoadAssetAtPath<WorldChunk>(GrasslandPipeline.ChunkPath);
            if (start == null)
                throw new InvalidOperationException("Start chunk missing: " + GrasslandPipeline.ChunkPath);
            // The Prairie's low round bushes as the start chunk draws them: mesh, material, palette, brightness.
            WorldChunk.PaletteBinding bushA = default, bushB = default;
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
            }
            if (bushA.renderer == null || bushB.renderer == null)
                throw new InvalidOperationException("The start chunk has no Bush_A and Bush_B renderers to copy.");
            var meshA = bushA.renderer.GetComponent<MeshFilter>().sharedMesh;
            var meshB = bushB.renderer.GetComponent<MeshFilter>().sharedMesh;

            var kit = AssetDatabase.LoadAssetAtPath<ZoneKit>(KitPath);
            if (kit == null)
            {
                kit = ScriptableObject.CreateInstance<ZoneKit>();
                AssetDatabase.CreateAsset(kit, KitPath);
            }
            kit.bushes = new[] { meshA, meshB };
            kit.material = bushA.renderer.sharedMaterial;
            kit.paletteId = bushA.paletteId;
            kit.brightness = bushA.brightness;
            kit.bushHeight = Mathf.Max(meshA.bounds.max.y, meshB.bounds.max.y);
            kit.bushRadius = Mathf.Max(Radius(meshA), Radius(meshB));
            kit.spacing = 2f * kit.bushRadius;
            var sign = AssetDatabase.LoadAssetAtPath<Mesh>(SignMeshPath);
            kit.signMesh = sign;
            if (sign != null)
            {
                kit.signRadius = Radius(sign);
                kit.signHeight = sign.bounds.max.y;
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

            // The bush meshes must already be shared kit assets: then the zone bundle carries nothing but the kit.
            var kitGroup = settings.FindGroup(WorldContentPipeline.KitGroup);
            bool shared = kitGroup != null && new[] { meshA, meshB }.All(m =>
                kitGroup.entries.Any(e => e.AssetPath == AssetDatabase.GetAssetPath(m)));
            return $"{KitPath}: bushes {meshA.name} and {meshB.name} (radius {kit.bushRadius:F2} m, height {kit.bushHeight:F2} m), " +
                   $"palette {kit.paletteId}, sign {(sign != null ? sign.name : "not modeled yet")}, meshes in the kit bundle: {shared}.";
        }

        private static float Radius(Mesh mesh)
        {
            var b = mesh.bounds;
            return Mathf.Max(Mathf.Max(Mathf.Abs(b.min.x), Mathf.Abs(b.max.x)), Mathf.Max(Mathf.Abs(b.min.z), Mathf.Abs(b.max.z)));
        }
    }
}
