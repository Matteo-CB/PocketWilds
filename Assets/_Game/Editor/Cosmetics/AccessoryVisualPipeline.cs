using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.Capture;
using PocketWilds.Editor.Environment;
using PocketWilds.Presentation;
using PocketWilds.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.Cosmetics
{
    /// <summary>
    /// T47 accessory and decoration visuals. <see cref="BuildAll"/>: the palette rows (DEF_Palettes_Cosmetics: each accessory
    /// palette of the pw_accessories.py table, the Cozy Burrow row of pw_grassland_t47_props.py), ONE shared accessory material
    /// (MAT_Accessory_Common, SH_EnvironmentOpaque: vertex-color regions tinted by the palette row in the per-renderer Shader User
    /// Value, so every wearer shares it and the SRP Batcher keeps batching), PF_Acc_FlowerCrown and PF_Acc_SeedBasket (no shadow
    /// cast: tiny parts), PF_Decor_CozyBurrow on MAT_Environment_Common, and DEF_CosmeticVisualCatalog (hooked on the animal visual
    /// catalog). <see cref="RenderIcons"/>: the shop icons rendered from the real assets (run after the fitting pipeline).
    /// Idempotent.
    /// </summary>
    public static class AccessoryVisualPipeline
    {
        public const string AccessoryModels = "Assets/_Game/Art/Props/Accessories";
        public const string PrefabFolder = "Assets/_Game/Prefabs/Accessories";
        public const string MaterialPath = "Assets/_Game/Art/Materials/MAT_Accessory_Common.mat";
        public const string PalettesPath = "Assets/_Game/Content/Palettes/DEF_Palettes_Cosmetics.asset";
        public const string CatalogPath = "Assets/_Game/Content/Visuals/DEF_CosmeticVisualCatalog.asset";
        public const string BurrowModel = "Assets/_Game/Art/Environment/Grassland/Models/GL_Props_T47.fbx";
        public const string BurrowObject = "Decor_CozyBurrow";
        public const string BurrowPrefabPath = "Assets/_Game/Prefabs/Environment/Grassland/PF_Decor_CozyBurrow.prefab";
        public const string BurrowPalette = "palette.grassland.cozy_burrow";
        public const string IconsFolder = "Assets/_Game/Art/UI/Shop";
        public const string FlowerCrownIcon = "ICO_Accessory_FlowerCrown";
        public const string SeedBasketIcon = "ICO_Accessory_SeedBasket";
        public const string BurrowIcon = "ICO_Decor_CozyBurrow";
        public const string CompanionIcon = "ICO_Companion_FieldHamster";

        public static readonly string[] Palettes = { "meadow", "blossom", "frost" };

        /// <summary>(accessory id, visual key = FBX name, prefab name, palette rows (base, R, G, B, A, occlusion, accent) per palette).</summary>
        private static readonly (string Id, string Visual, string Prefab, string[][] Rows)[] Accessories =
        {
            // pw_accessories.py: base = ring and leaves, R = petals, G = flower centers.
            ("cosmetic.flower_crown", "ACC_FlowerCrown", "PF_Acc_FlowerCrown", new[]
            {
                new[] { "7FB069", "F8F4EA", "F2C94C", "7FB069", "7FB069", "4E6D5D", "FFFFFF" },
                new[] { "8CBF7A", "F3BDD3", "F7DF7A", "8CBF7A", "8CBF7A", "5A7A62", "FFFFFF" },
                new[] { "A9C9C4", "DDEBFA", "C9B8F0", "A9C9C4", "A9C9C4", "62807E", "FFFFFF" },
            }),
            // pw_accessories.py: base = weave, R = rim and foot band, G = seeds (accent in B). The table gives one set (meadow);
            // blossom and frost are soft shifts of it (rosy wood, pale birch).
            ("accessory.fieldhamster.seedbasket", "ACC_SeedBasket", "PF_Acc_SeedBasket", new[]
            {
                new[] { "C99A62", "A87A48", "F0D9A0", "5E4A3A", "C99A62", "6F5A47", "FFFFFF" },
                new[] { "D6A68C", "B57E6A", "F3E2B0", "6A4C44", "D6A68C", "7A5C52", "FFFFFF" },
                new[] { "CDBFA4", "A99A80", "EFE6CC", "5F5446", "CDBFA4", "6E6656", "FFFFFF" },
            }),
        };

        public static string PaletteRow(string accessoryId, string palette) => "palette.accessory." + Short(accessoryId) + "." + palette;

        private static string Short(string id) => id.Substring(id.LastIndexOf('.') + 1);

        [MenuItem("Pocket Wilds/Cosmetics/Build Accessory Visuals (T47)", priority = 192)]
        public static void BuildAllMenu() => Debug.Log("[AccessoryVisualPipeline] " + BuildAll());

        [MenuItem("Pocket Wilds/Cosmetics/Render Cosmetic Icons (T47)", priority = 195)]
        public static void RenderIconsMenu() => Debug.Log("[AccessoryVisualPipeline] " + RenderIcons());

        public static string BuildAll()
        {
            var report = new List<string>();
            EnsurePalettes();
            var material = EnsureMaterial();
            var environment = AssetDatabase.LoadAssetAtPath<Material>(GrasslandPipeline.EnvironmentMaterialPath);
            Directory.CreateDirectory(PrefabFolder);
            var catalog = LoadOrCreate<CosmeticVisualCatalog>(CatalogPath);
            catalog.accessories.RemoveAll(e => e == null || Accessories.All(a => a.Id != e.accessoryId));
            foreach (var (id, visual, prefabName, _) in Accessories)
            {
                string model = $"{AccessoryModels}/{visual}.fbx";
                var meshes = ModelMeshes(model, null);
                if (meshes.Count == 0)
                {
                    report.Add($"{visual}: model missing ({model})");
                    continue;
                }
                var prefab = SavePrefab(prefabName, $"{PrefabFolder}/{prefabName}.prefab", meshes, material, ShadowCastingMode.Off);
                var entry = catalog.Find(id);
                if (entry == null)
                {
                    entry = new CosmeticVisualCatalog.AccessoryEntry { accessoryId = id };
                    catalog.accessories.Add(entry);
                }
                entry.visualKey = visual;
                entry.prefab = prefab;
                entry.palettes = new List<string>(Palettes);
                entry.paletteRows = Palettes.Select(p => PaletteRow(id, p)).ToList();
                entry.warm = 2;
                int tris = meshes.Sum(m => (int)m.mesh.GetIndexCount(0) / 3);
                report.Add($"{prefabName}: {tris} tris");
            }

            var burrowMeshes = ModelMeshes(BurrowModel, BurrowObject);
            if (burrowMeshes.Count == 0 || environment == null)
            {
                report.Add("PF_Decor_CozyBurrow: model or MAT_Environment_Common missing");
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(BurrowPrefabPath));
                catalog.burrowPrefab = SavePrefab("PF_Decor_CozyBurrow", BurrowPrefabPath, burrowMeshes, environment, ShadowCastingMode.On);
                catalog.burrowPalette = BurrowPalette;
                catalog.burrowItem = "decor.cozy_burrow";
                report.Add($"PF_Decor_CozyBurrow: {burrowMeshes.Sum(m => (int)m.mesh.GetIndexCount(0) / 3)} tris");
            }
            EditorUtility.SetDirty(catalog);

            var visuals = AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>(AnimalPipeline.VisualCatalogPath);
            if (visuals != null && visuals.cosmetics != catalog)
            {
                visuals.cosmetics = catalog;
                EditorUtility.SetDirty(visuals);
            }
            AssetDatabase.SaveAssets();
            return string.Join("; ", report);
        }

        // ---------------------------------------------------------------- palettes, material, prefabs

        public static void EnsurePalettes()
        {
            var set = LoadOrCreate<PaletteSetAsset>(PalettesPath);
            set.palettes.Clear();
            foreach (var (id, _, _, rows) in Accessories)
                for (int p = 0; p < Palettes.Length; p++)
                    Add(set, PaletteRow(id, Palettes[p]), rows[p]);
            // pw_grassland_t47_props.T47_PALETTES: earth, R moss, G honey wood and daisy hearts, B entrance shade, A petals.
            Add(set, BurrowPalette, new[] { "8A6646", "7FAF5A", "D9A85A", "3B2A22", "F8F4EA", "4A3A2E", "FFFFFF" });
            EditorUtility.SetDirty(set);
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath);
            if (database != null && !database.paletteSets.Contains(set))
            {
                database.paletteSets.Add(set);
                EditorUtility.SetDirty(database);
            }
        }

        private static void Add(PaletteSetAsset set, string id, string[] hex)
        {
            set.palettes.Add(new PaletteSetAsset.Entry
            {
                sourceId = id,
                baseColor = Hex(hex[0]),
                regionR = Hex(hex[1]),
                regionG = Hex(hex[2]),
                regionB = Hex(hex[3]),
                regionA = Hex(hex[4]),
                occlusion = Hex(hex[5]),
                accent = Hex(hex[6]),
            });
        }

        private static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out var color) ? color : Color.magenta;

        /// <summary>The one accessory material: the props' shader and lighting values, SRP Batcher compatible, no instancing.</summary>
        public static Material EnsureMaterial()
        {
            var shader = Shader.Find(GrasslandPipeline.EnvironmentShader);
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
                material = new Material(shader) { name = "MAT_Accessory_Common" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.shader = shader;
            var common = AssetDatabase.LoadAssetAtPath<Material>(GrasslandPipeline.EnvironmentMaterialPath);
            if (common != null && common.shader == shader)
                material.CopyPropertiesFromMaterial(common);
            material.enableInstancing = false;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Meshes of a model with their placement under its root (one object by name, or all).</summary>
        public static List<(Mesh mesh, Matrix4x4 matrix, string name)> ModelMeshes(string modelPath, string objectName)
        {
            var result = new List<(Mesh, Matrix4x4, string)>();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
                return result;
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                    continue;
                if (objectName != null && filter.name != objectName && filter.sharedMesh.name != objectName)
                    continue;
                result.Add((filter.sharedMesh, model.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix, filter.name));
            }
            return result;
        }

        private static GameObject SavePrefab(string name, string path, List<(Mesh mesh, Matrix4x4 matrix, string name)> meshes, Material material,
            ShadowCastingMode shadows)
        {
            var root = new GameObject(name);
            try
            {
                foreach (var (mesh, matrix, childName) in meshes)
                {
                    var child = new GameObject(childName);
                    child.transform.SetParent(root.transform, false);
                    child.transform.localPosition = matrix.GetColumn(3);
                    child.transform.localRotation = matrix.rotation;
                    child.transform.localScale = matrix.lossyScale;
                    child.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = child.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = shadows;
                    renderer.receiveShadows = true;
                    renderer.lightProbeUsage = LightProbeUsage.Off;
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                }
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        // ---------------------------------------------------------------- icons

        /// <summary>
        /// The shop icons from the real assets, same light and framing rules as the animal thumbnails: the crown and the basket as
        /// fitted (meadow palette), the burrow, the companion in its base look. Written to Art/UI/Shop (listed under generated in
        /// SourceArt/Handmade_Sources.json).
        /// </summary>
        public static string RenderIcons()
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(active.path) && active.isDirty)
                return "refused: save or discard changes in " + active.path;
            string returnScene = active.path;
            var written = new List<string>();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath);
            var visuals = AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>(AnimalPipeline.VisualCatalogPath);
            var catalog = AssetDatabase.LoadAssetAtPath<CosmeticVisualCatalog>(CatalogPath);
            var lighting = AssetDatabase.LoadAssetAtPath<LightingProfile>("Assets/_Game/Art/Lighting/Grassland/LP_Grassland_Day.asset");
            if (database == null || catalog == null)
                return "run BuildAll first";
            var registry = database.CreateBuilder().Build();
            bool fog = RenderSettings.fog;
            bool asyncShaders = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            Directory.CreateDirectory(IconsFolder);
            try
            {
                using (var palettes = new PaletteTable(registry))
                {
                    var sun = new GameObject("Sun").AddComponent<Light>();
                    sun.type = LightType.Directional;
                    var camera = AnimalThumbnailPipeline.CreateCamera();
                    if (lighting != null)
                        lighting.Apply(sun, camera, QualityTier.High);
                    RenderSettings.fog = false;
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    DynamicGI.UpdateEnvironment();
                    bool warmed = false;

                    void Shoot(GameObject subject, string icon, float yaw)
                    {
                        Frame(camera, subject, yaw);
                        if (!warmed)
                        {
                            AnimalThumbnailPipeline.Capture(camera, out _, out _);
                            warmed = true;
                        }
                        AnimalThumbnailPipeline.Capture(camera, out var premultiplied, out var alpha);
                        var crop = AnimalThumbnailPipeline.CropAround(alpha);
                        string path = $"{IconsFolder}/{icon}.png";
                        AnimalThumbnailPipeline.WritePng(path, AnimalThumbnailPipeline.Resample(premultiplied, alpha, crop, false));
                        written.Add(path);
                    }

                    foreach (var (id, _, _, _) in Accessories)
                    {
                        var entry = catalog.Find(id);
                        if (entry == null || entry.prefab == null)
                            continue;
                        var piece = Object.Instantiate(entry.prefab);
                        try
                        {
                            uint value = AccessoryWardrobe.Packed(palettes, PaletteRow(id, Palettes[0]));
                            foreach (var r in piece.GetComponentsInChildren<MeshRenderer>(true))
                                r.SetShaderUserValue(value);
                            Shoot(piece, id == "cosmetic.flower_crown" ? FlowerCrownIcon : SeedBasketIcon, 28f);
                        }
                        finally
                        {
                            Object.DestroyImmediate(piece);
                        }
                    }

                    if (catalog.burrowPrefab != null)
                    {
                        var burrow = Object.Instantiate(catalog.burrowPrefab);
                        try
                        {
                            uint value = AccessoryWardrobe.Packed(palettes, catalog.burrowPalette);
                            foreach (var r in burrow.GetComponentsInChildren<MeshRenderer>(true))
                                r.SetShaderUserValue(value);
                            Shoot(burrow, BurrowIcon, 30f);
                        }
                        finally
                        {
                            Object.DestroyImmediate(burrow);
                        }
                    }

                    var companion = CompanionCatalog.FieldHamster;
                    if (visuals != null && registry.Species.TryGet(companion.Species, out var species)
                        && visuals.TryGet(species.VisualKey, out var visual) && visual.prefab != null)
                    {
                        var resolver = new AnimalVisualResolver(registry, palettes);
                        var view = Object.Instantiate(visual.prefab);
                        try
                        {
                            var animal = new AnimalInstance { InstanceId = 1, Seed = 3u, Species = companion.Species, Variant = companion.BaseLook };
                            resolver.TryResolve(animal, Vector3.zero, 0f, out var snapshot);
                            view.Configure(snapshot);
                            if (view.LodGroup != null)
                                view.LodGroup.ForceLOD(0);
                            foreach (var r in view.GetComponentsInChildren<Renderer>(true))
                                if (r.name.Contains("BlobShadow"))
                                    r.enabled = false;
                            if (view.Animator != null)
                            {
                                view.Animator.Rebind();
                                view.Animator.Update(0.6f);
                            }
                            Shoot(view.gameObject, CompanionIcon, 32f);
                        }
                        finally
                        {
                            Object.DestroyImmediate(view.gameObject);
                        }
                    }
                }
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = asyncShaders;
                RenderSettings.fog = fog;
                if (!string.IsNullOrEmpty(returnScene))
                    EditorSceneManager.OpenScene(returnScene, OpenSceneMode.Single);
            }
            foreach (var path in written)
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return written.Count + " icons: " + string.Join(", ", written.Select(Path.GetFileNameWithoutExtension));
        }

        /// <summary>Three-quarter front view from slightly above (accessories and the burrow face +Z like the animals).</summary>
        /// <summary>
        /// Review captures (T47 « tenues ajustées »): every fitted (accessory, species) pair worn at the idle pose, framed on the
        /// accessory and the head around it, three-quarter front view; written to TestResults/Captures/T47_fit_*.png.
        /// </summary>
        public static string RenderFittings()
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(active.path) && active.isDirty)
                return "refused: save or discard changes in " + active.path;
            const string folder = "TestResults/Captures";
            Directory.CreateDirectory(folder);
            var written = new List<string>();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath);
            var visuals = AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>(AnimalPipeline.VisualCatalogPath);
            var catalog = AssetDatabase.LoadAssetAtPath<CosmeticVisualCatalog>(CatalogPath);
            var lighting = AssetDatabase.LoadAssetAtPath<LightingProfile>("Assets/_Game/Art/Lighting/Grassland/LP_Grassland_Day.asset");
            if (database == null || catalog == null || visuals == null)
                return "run BuildAll first";
            var registry = database.CreateBuilder().Build();
            bool fog = RenderSettings.fog;
            bool asyncShaders = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            try
            {
                using (var palettes = new PaletteTable(registry))
                {
                    var resolver = new AnimalVisualResolver(registry, palettes);
                    var sun = new GameObject("Sun").AddComponent<Light>();
                    sun.type = LightType.Directional;
                    var camera = AnimalThumbnailPipeline.CreateCamera();
                    if (lighting != null)
                        lighting.Apply(sun, camera, QualityTier.High);
                    RenderSettings.fog = false;
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    DynamicGI.UpdateEnvironment();
                    bool warmed = false;
                    foreach (var (id, _, _, _) in Accessories)
                    {
                        var entry = catalog.Find(id);
                        if (entry == null || entry.prefab == null || registry.Shop == null
                            || !registry.Shop.Accessories.TryGet(ContentId.FromSource(id), out var accessory))
                            continue;
                        uint value = AccessoryWardrobe.Packed(palettes, PaletteRow(id, Palettes[0]));
                        foreach (var speciesAsset in database.species)
                        {
                            if (speciesAsset == null || speciesAsset.variants.Count == 0)
                                continue;
                            var species = speciesAsset.ToRuntime();
                            if (accessory.Excludes(species.Id) || !accessory.TryGetFit(species.Id, species.RigFamily, out var fit))
                                continue;
                            if (!visuals.TryGet(species.VisualKey, out var visual) || visual.prefab == null)
                                continue;
                            var view = Object.Instantiate(visual.prefab);
                            try
                            {
                                var animal = new AnimalInstance
                                {
                                    InstanceId = 1, Seed = 3u, Species = species.Id, Variant = ContentId.FromSource(speciesAsset.variants[0].sourceId),
                                };
                                resolver.TryResolve(animal, Vector3.zero, 0f, out var snapshot);
                                view.Configure(snapshot);
                                view.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                                if (view.LodGroup != null)
                                    view.LodGroup.ForceLOD(0);
                                foreach (var renderer in view.GetComponentsInChildren<Renderer>(true))
                                    if (renderer.name.Contains("BlobShadow"))
                                        renderer.enabled = false;
                                view.Animator.Rebind();
                                view.Animator.Update(0.6f);
                                var socket = accessory.Socket switch
                                {
                                    AccessorySocket.HeadTop => view.SocketHeadTop,
                                    AccessorySocket.Neck => view.SocketNeck,
                                    AccessorySocket.Back => view.SocketBack,
                                    _ => view.SocketTailBase,
                                };
                                if (socket == null)
                                    continue;
                                var piece = Object.Instantiate(entry.prefab, socket, false);
                                piece.transform.localPosition = fit.LocalPosition;
                                piece.transform.localRotation = Quaternion.Euler(fit.LocalEuler);
                                piece.transform.localScale = fit.LocalScale.sqrMagnitude > 1e-8f ? fit.LocalScale : Vector3.one;
                                var bounds = new Bounds();
                                bool first = true;
                                foreach (var r in piece.GetComponentsInChildren<MeshRenderer>(true))
                                {
                                    r.SetShaderUserValue(value);
                                    if (first)
                                        bounds = r.bounds;
                                    else
                                        bounds.Encapsulate(r.bounds);
                                    first = false;
                                }
                                // The accessory and the head around it.
                                bounds.Expand(Mathf.Max(0.12f, bounds.size.magnitude * 2.2f));
                                float radius = bounds.extents.magnitude;
                                float distance = radius / Mathf.Sin(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                                var direction = Quaternion.Euler(-28f, 32f, 0f) * Vector3.forward;
                                camera.transform.position = bounds.center + direction * distance;
                                camera.transform.LookAt(bounds.center);
                                camera.nearClipPlane = Mathf.Max(0.005f, distance - radius * 2f);
                                camera.farClipPlane = distance + radius * 3f;
                                if (!warmed)
                                {
                                    AnimalThumbnailPipeline.Capture(camera, out _, out _);
                                    warmed = true;
                                }
                                AnimalThumbnailPipeline.Capture(camera, out var premultiplied, out var alpha);
                                var crop = AnimalThumbnailPipeline.CropAround(alpha);
                                string path = $"{folder}/T47_fit_{Short(id)}_{species.VisualKey.Replace("PF_Animal_", string.Empty)}.png";
                                AnimalThumbnailPipeline.WritePng(path, AnimalThumbnailPipeline.Resample(premultiplied, alpha, crop, false));
                                written.Add(Path.GetFileName(path));
                            }
                            finally
                            {
                                Object.DestroyImmediate(view.gameObject);
                            }
                        }
                    }
                }
            }
            finally
            {
                RenderSettings.fog = fog;
                ShaderUtil.allowAsyncCompilation = asyncShaders;
            }
            return written.Count + " fittings: " + string.Join(", ", written);
        }

        private static void Frame(Camera camera, GameObject subject, float yaw)
        {
            var bounds = new Bounds();
            bool first = true;
            foreach (var renderer in subject.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || renderer.name.Contains("LOD1") || renderer.name.Contains("LOD2"))
                    continue;
                if (first)
                    bounds = renderer.bounds;
                else
                    bounds.Encapsulate(renderer.bounds);
                first = false;
            }
            float radius = Mathf.Max(0.01f, bounds.extents.magnitude);
            float distance = radius / Mathf.Sin(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.08f;
            var direction = Quaternion.Euler(-24f, yaw, 0f) * Vector3.forward;
            camera.transform.position = bounds.center + direction * distance;
            camera.transform.LookAt(bounds.center);
            camera.nearClipPlane = Mathf.Max(0.005f, distance - radius * 2f);
            camera.farClipPlane = distance + radius * 2f;
        }
    }
}
