using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.Capture;
using PocketWilds.Editor.Environment;
using PocketWilds.Editor.Events;
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
    /// catalog). T49: the 8 pieces of L'Éveil des Terriers (PF_Acc_SproutHat... PF_Acc_OverallsStraps) with a default and a gold
    /// row each (pw_accessories.py tables). <see cref="RenderIcons"/>: the shop icons rendered from the real assets, the 7 burrows
    /// outfits worn on the mole or the field hamster (run after the fitting pipeline). Idempotent.
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

        /// <summary>T47 palette names (meadow, blossom, frost); the T49 pieces use default and gold (BurrowsEventPipeline).</summary>
        public static readonly string[] Palettes = { "meadow", "blossom", "frost" };

        /// <summary>One accessory: id, visual key (FBX name), prefab, palette names with their rows (base, R, G, B, A, occlusion, accent;
        /// missing columns: B and A = base, occlusion = base toward a warm shade, accent white), the palette its icon shows.</summary>
        public sealed class Piece
        {
            public string Id;
            public string Visual;
            public string Prefab;
            public string[] PaletteNames;
            public string[][] Rows;
            public string IconPalette;

            /// <summary>ICO_Accessory_&lt;Name&gt; (FlowerCrown, SeedBasket, SproutHat...).</summary>
            public string Icon => "ICO_Accessory_" + Visual.Substring("ACC_".Length);
        }

        /// <summary>pw_accessories.py docstring table of the T49 pieces: base, R, G (, B), default row then gold row.</summary>
        private static readonly Dictionary<string, (string[] plain, string[] gold)> BurrowsRows = new Dictionary<string, (string[], string[])>
        {
            ["SproutHat"] = (new[] { "A8744E", "F4EBD6", "86CC5A" }, new[] { "E2B04A", "FFF0C4", "F2D27A" }),
            ["SeedSack"] = (new[] { "D8B98A", "7A5A3E", "F0D9A0", "8DC26F" }, new[] { "E3B955", "B8862C", "FFF0C2", "F5D77A" }),
            ["BurrowGoggles"] = (new[] { "8A5A3C", "D7A24A", "A9DCE6" }, new[] { "B9832F", "E8BE55", "FFF0BF" }),
            ["GardenerScarf"] = (new[] { "E07B5A", "A9D47E", "F4E3C1" }, new[] { "E2B54C", "FFF1C9", "C2902F" }),
            ["MinerHelmet"] = (new[] { "F2C14E", "D9A33A", "6E6A66", "FFF2B3" }, new[] { "E6B43F", "C08A2A", "F0D27A", "FFF6D6" }),
            ["GoldenShovel"] = (new[] { "8A5A3C", "C9925A", "B8C2C8" }, new[] { "A8742E", "E2B04A", "F5D06A" }),
            ["OverallsBib"] = (new[] { "7E9FC4", "E0B04E", "6B8DB5" }, new[] { "D9AE4C", "FFF0C4", "C4962F" }),
            ["OverallsStraps"] = (new[] { "7E9FC4", "E0B04E", "6B8DB5" }, new[] { "D9AE4C", "FFF0C4", "C4962F" }),
        };

        /// <summary>Every accessory with a visual: the T47 two, then the 8 pieces of the burrows event (BurrowsEventPipeline.Accessories).</summary>
        public static readonly Piece[] Accessories = BuildPieces();

        private static Piece[] BuildPieces()
        {
            var list = new List<Piece>
            {
                // pw_accessories.py: base = ring and leaves, R = petals, G = flower centers.
                new Piece
                {
                    Id = "cosmetic.flower_crown", Visual = "ACC_FlowerCrown", Prefab = "PF_Acc_FlowerCrown", PaletteNames = Palettes, IconPalette = "meadow",
                    Rows = new[]
                    {
                        new[] { "7FB069", "F8F4EA", "F2C94C", "7FB069", "7FB069", "4E6D5D", "FFFFFF" },
                        new[] { "8CBF7A", "F3BDD3", "F7DF7A", "8CBF7A", "8CBF7A", "5A7A62", "FFFFFF" },
                        new[] { "A9C9C4", "DDEBFA", "C9B8F0", "A9C9C4", "A9C9C4", "62807E", "FFFFFF" },
                    },
                },
                // pw_accessories.py: base = weave, R = rim and foot band, G = seeds (accent in B). The table gives one set (meadow);
                // blossom and frost are soft shifts of it (rosy wood, pale birch).
                new Piece
                {
                    Id = "accessory.fieldhamster.seedbasket", Visual = "ACC_SeedBasket", Prefab = "PF_Acc_SeedBasket", PaletteNames = Palettes, IconPalette = "meadow",
                    Rows = new[]
                    {
                        new[] { "C99A62", "A87A48", "F0D9A0", "5E4A3A", "C99A62", "6F5A47", "FFFFFF" },
                        new[] { "D6A68C", "B57E6A", "F3E2B0", "6A4C44", "D6A68C", "7A5C52", "FFFFFF" },
                        new[] { "CDBFA4", "A99A80", "EFE6CC", "5F5446", "CDBFA4", "6E6656", "FFFFFF" },
                    },
                },
            };
            foreach (var row in BurrowsEventPipeline.Accessories)
            {
                var (plain, gold) = BurrowsRows[row.Name];
                list.Add(new Piece
                {
                    Id = row.Id, Visual = "ACC_" + row.Name, Prefab = "PF_Acc_" + row.Name,
                    PaletteNames = new[] { BurrowsEventPipeline.DefaultPalette, BurrowsEventPipeline.GoldPalette },
                    Rows = new[] { plain, gold },
                    // Gold pieces are gold by default (the shop's palette 0).
                    IconPalette = row.Gold ? BurrowsEventPipeline.GoldPalette : BurrowsEventPipeline.DefaultPalette,
                });
            }
            return list.ToArray();
        }

        public static Piece FindPiece(string accessoryId) => Accessories.FirstOrDefault(p => p.Id == accessoryId);

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
            foreach (var piece in Accessories)
            {
                string model = $"{AccessoryModels}/{piece.Visual}.fbx";
                var meshes = ModelMeshes(model, null);
                if (meshes.Count == 0)
                {
                    report.Add($"{piece.Visual}: model missing ({model})");
                    continue;
                }
                var prefab = SavePrefab(piece.Prefab, $"{PrefabFolder}/{piece.Prefab}.prefab", meshes, material, ShadowCastingMode.Off);
                var entry = catalog.Find(piece.Id);
                if (entry == null)
                {
                    entry = new CosmeticVisualCatalog.AccessoryEntry { accessoryId = piece.Id };
                    catalog.accessories.Add(entry);
                }
                entry.visualKey = piece.Visual;
                entry.prefab = prefab;
                entry.palettes = new List<string>(piece.PaletteNames);
                entry.paletteRows = piece.PaletteNames.Select(p => PaletteRow(piece.Id, p)).ToList();
                entry.warm = 2;
                int tris = meshes.Sum(m => (int)m.mesh.GetIndexCount(0) / 3);
                int budget = BurrowsEventPipeline.Accessories.Where(r => r.Id == piece.Id).Select(r => r.Tris).DefaultIfEmpty(0).First();
                report.Add($"{piece.Prefab}: {tris} tris" + (budget > 0 && tris > budget ? $" OVER BUDGET {budget}" : string.Empty));
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
            foreach (var piece in Accessories)
                for (int p = 0; p < piece.PaletteNames.Length; p++)
                    Add(set, PaletteRow(piece.Id, piece.PaletteNames[p]), piece.Rows[p]);
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

        /// <summary>A palette row from its hex columns (base, R, G, B, A, occlusion, accent); missing B and A take the base, a missing
        /// occlusion is the base toward a warm shade, a missing accent is white.</summary>
        public static void Add(PaletteSetAsset set, string id, string[] hex)
        {
            var baseColor = Hex(hex[0]);
            string At(int i) => i < hex.Length && !string.IsNullOrEmpty(hex[i]) ? hex[i] : null;
            set.palettes.Add(new PaletteSetAsset.Entry
            {
                sourceId = id,
                baseColor = baseColor,
                regionR = At(1) != null ? Hex(At(1)) : baseColor,
                regionG = At(2) != null ? Hex(At(2)) : baseColor,
                regionB = At(3) != null ? Hex(At(3)) : baseColor,
                regionA = At(4) != null ? Hex(At(4)) : baseColor,
                occlusion = At(5) != null ? Hex(At(5)) : Color.Lerp(baseColor, Hex("3B2A22"), 0.55f),
                accent = At(6) != null ? Hex(At(6)) : Color.white,
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

        public static GameObject SavePrefab(string name, string path, List<(Mesh mesh, Matrix4x4 matrix, string name)> meshes, Material material,
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

        /// <summary>Who wears an outfit on its icon: the first of these fitted by every piece, else any species fitted by every piece.</summary>
        public static readonly string[] OutfitWearers = { "species.mole", "species.fieldhamster" };

        /// <summary>
        /// The studio shared by the cosmetic and decoration icons and the fitting captures: an empty scene, the Prairie day light,
        /// the animal thumbnail camera and its framing rules, the palette table. <see cref="Shoot"/> writes Art/UI/Shop/&lt;icon&gt;.png
        /// (listed under generated in SourceArt/Handmade_Sources.json). Dispose restores the scene and imports the written icons.
        /// </summary>
        public sealed class IconStudio : IDisposable
        {
            public ContentDatabaseAsset Database { get; private set; }
            public AnimalVisualCatalog Visuals { get; private set; }
            public CosmeticVisualCatalog Catalog { get; private set; }
            public ContentRegistry Registry { get; private set; }
            public PaletteTable Palettes { get; private set; }
            public Camera Camera { get; private set; }
            public readonly List<string> Written = new List<string>();
            private bool _warmed;
            private bool _fog;
            private bool _async;
            private string _returnScene;

            /// <summary>Null with the reason when the open scene has unsaved changes or the data is missing.</summary>
            public static IconStudio Open(out string refusal)
            {
                var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if (!string.IsNullOrEmpty(active.path) && active.isDirty)
                {
                    refusal = "refused: save or discard changes in " + active.path;
                    return null;
                }
                // Load after the scene switch: switching can unload what was loaded before it (the animal prefabs then read null).
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath);
                var catalog = AssetDatabase.LoadAssetAtPath<CosmeticVisualCatalog>(CatalogPath);
                if (database == null || catalog == null)
                {
                    refusal = "run AccessoryVisualPipeline.BuildAll first";
                    return null;
                }
                var studio = new IconStudio
                {
                    _returnScene = active.path, Database = database, Catalog = catalog,
                    Visuals = AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>(AnimalPipeline.VisualCatalogPath),
                };
                studio.Registry = database.CreateBuilder().Build();
                studio._fog = RenderSettings.fog;
                studio._async = ShaderUtil.allowAsyncCompilation;
                ShaderUtil.allowAsyncCompilation = false;
                Directory.CreateDirectory(IconsFolder);
                studio.Palettes = new PaletteTable(studio.Registry);
                var sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
                studio.Camera = AnimalThumbnailPipeline.CreateCamera();
                var lighting = AssetDatabase.LoadAssetAtPath<LightingProfile>("Assets/_Game/Art/Lighting/Grassland/LP_Grassland_Day.asset");
                if (lighting != null)
                    lighting.Apply(sun, studio.Camera, QualityTier.High);
                RenderSettings.fog = false;
                studio.Camera.clearFlags = CameraClearFlags.SolidColor;
                DynamicGI.UpdateEnvironment();
                refusal = null;
                return studio;
            }

            /// <summary>Every mesh renderer of the subject on one palette row (the shared material's Shader User Value).</summary>
            public void Tint(GameObject subject, string paletteRow)
            {
                uint value = AccessoryWardrobe.Packed(Palettes, paletteRow);
                foreach (var r in subject.GetComponentsInChildren<MeshRenderer>(true))
                    r.SetShaderUserValue(value);
            }

            /// <summary>Three-quarter front view from slightly above, cropped around the subject, written as a shop icon.</summary>
            public void Shoot(GameObject subject, string icon, float yaw)
            {
                Frame(Camera, subject, yaw);
                CaptureTo($"{IconsFolder}/{icon}.png");
            }

            /// <summary>Captures what the camera frames now (cropped around the alpha) to a PNG.</summary>
            public void CaptureTo(string path)
            {
                if (!_warmed)
                {
                    AnimalThumbnailPipeline.Capture(Camera, out _, out _);
                    _warmed = true;
                }
                AnimalThumbnailPipeline.Capture(Camera, out var premultiplied, out var alpha);
                var crop = AnimalThumbnailPipeline.CropAround(alpha);
                AnimalThumbnailPipeline.WritePng(path, AnimalThumbnailPipeline.Resample(premultiplied, alpha, crop, false));
                Written.Add(path);
            }

            /// <summary>An animal of a species in its first variant at the idle pose (LOD0, no blob shadow), at the origin facing +Z.</summary>
            public AnimalView Animal(string speciesId)
            {
                var asset = Database.species.FirstOrDefault(s => s != null && s.sourceId == speciesId);
                if (asset == null || asset.variants.Count == 0 || Visuals == null)
                    return null;
                var species = asset.ToRuntime();
                if (!Visuals.TryGet(species.VisualKey, out var visual) || visual.prefab == null)
                    return null;
                var resolver = new AnimalVisualResolver(Registry, Palettes);
                var view = Object.Instantiate(visual.prefab);
                var animal = new AnimalInstance
                {
                    InstanceId = 1, Seed = 3u, Species = species.Id, Variant = ContentId.FromSource(asset.variants[0].sourceId),
                };
                resolver.TryResolve(animal, Vector3.zero, 0f, out var snapshot);
                view.Configure(snapshot);
                view.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
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
                return view;
            }

            public void Dispose()
            {
                Palettes?.Dispose();
                ShaderUtil.allowAsyncCompilation = _async;
                RenderSettings.fog = _fog;
                if (!string.IsNullOrEmpty(_returnScene))
                    EditorSceneManager.OpenScene(_returnScene, OpenSceneMode.Single);
                foreach (var path in Written)
                    if (path.StartsWith("Assets/", StringComparison.Ordinal))
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
        }

        /// <summary>A piece on its socket with the species' measured fit, tinted with a palette row (null without prefab or socket).</summary>
        public static GameObject Wear(IconStudio studio, AnimalView view, ShopCatalogAsset.Accessory accessory, ShopCatalogAsset.SpeciesFit fit, string paletteRow)
        {
            var entry = studio.Catalog.Find(accessory.sourceId);
            var socket = accessory.socket switch
            {
                AccessorySocket.HeadTop => view.SocketHeadTop,
                AccessorySocket.Neck => view.SocketNeck,
                AccessorySocket.Back => view.SocketBack,
                _ => view.SocketTailBase,
            };
            if (entry == null || entry.prefab == null || socket == null)
                return null;
            var piece = Object.Instantiate(entry.prefab, socket, false);
            piece.transform.localPosition = fit.localPosition;
            piece.transform.localRotation = Quaternion.Euler(fit.localEuler);
            piece.transform.localScale = fit.localScale.sqrMagnitude > 1e-8f ? fit.localScale : Vector3.one;
            studio.Tint(piece, paletteRow);
            return piece;
        }

        /// <summary>
        /// The shop icons from the real assets, same light and framing rules as the animal thumbnails: every accessory alone in the
        /// palette the shop shows first (gold pieces in gold), the Cozy Burrow, the companion in its base look, and the 7 burrows
        /// outfits (ICO_Outfit_Burrows_01..07) worn on the mole, else the field hamster, three-quarter view, with their measured fits
        /// (run after AccessoryFitPipeline). Decoration icons: BurrowsDecorPipeline.RenderIcons.
        /// </summary>
        public static string RenderIcons()
        {
            var studio = IconStudio.Open(out string refusal);
            if (studio == null)
                return refusal;
            var missing = new List<string>();
            using (studio)
            {
                foreach (var piece in Accessories)
                {
                    var entry = studio.Catalog.Find(piece.Id);
                    if (entry == null || entry.prefab == null)
                    {
                        missing.Add(piece.Icon);
                        continue;
                    }
                    var go = Object.Instantiate(entry.prefab);
                    try
                    {
                        studio.Tint(go, PaletteRow(piece.Id, piece.IconPalette));
                        studio.Shoot(go, piece.Icon, 28f);
                    }
                    finally
                    {
                        Object.DestroyImmediate(go);
                    }
                }

                if (studio.Catalog.burrowPrefab != null)
                {
                    var burrow = Object.Instantiate(studio.Catalog.burrowPrefab);
                    try
                    {
                        studio.Tint(burrow, studio.Catalog.burrowPalette);
                        studio.Shoot(burrow, BurrowIcon, 30f);
                    }
                    finally
                    {
                        Object.DestroyImmediate(burrow);
                    }
                }

                var companion = CompanionCatalog.FieldHamster;
                if (studio.Visuals != null && studio.Registry.Species.TryGet(companion.Species, out var species)
                    && studio.Visuals.TryGet(species.VisualKey, out var visual) && visual.prefab != null)
                {
                    var resolver = new AnimalVisualResolver(studio.Registry, studio.Palettes);
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
                        studio.Shoot(view.gameObject, CompanionIcon, 32f);
                    }
                    finally
                    {
                        Object.DestroyImmediate(view.gameObject);
                    }
                }

                var accessories = AccessoryFitMeasure.ShopCatalogs(studio.Database).SelectMany(c => c.accessories).Where(a => a != null).ToList();
                for (int i = 0; i < BurrowsEventPipeline.Outfits.Length; i++)
                {
                    string icon = OutfitIcon(i + 1);
                    var (pieces, palette) = BurrowsEventPipeline.Outfits[i];
                    var worn = pieces.Select(p => accessories.FirstOrDefault(a => a.sourceId == BurrowsEventPipeline.Accessories[p].Id)).ToList();
                    if (worn.Any(a => a == null))
                    {
                        missing.Add(icon + " (pieces not in a shop catalog)");
                        continue;
                    }
                    bool FitsAll(string s) => worn.All(a => a.speciesFits.Any(f => f.speciesId == s));
                    string wearer = OutfitWearers.FirstOrDefault(FitsAll) ?? worn[0].speciesFits.Select(f => f.speciesId).FirstOrDefault(FitsAll);
                    var view = wearer != null ? studio.Animal(wearer) : null;
                    if (view == null)
                    {
                        missing.Add(icon + (wearer == null ? " (no species fits every piece)" : " (no visual for " + wearer + ")"));
                        continue;
                    }
                    try
                    {
                        foreach (var a in worn)
                            Wear(studio, view, a, a.speciesFits.First(f => f.speciesId == wearer), PaletteRow(a.sourceId, palette));
                        studio.Shoot(view.gameObject, icon, 32f);
                    }
                    finally
                    {
                        Object.DestroyImmediate(view.gameObject);
                    }
                }
            }
            return studio.Written.Count + " icons: " + string.Join(", ", studio.Written.Select(Path.GetFileNameWithoutExtension))
                   + (missing.Count > 0 ? "; MISSING: " + string.Join(", ", missing) : string.Empty);
        }

        /// <summary>ICO_Outfit_Burrows_01..07 (the key BurrowsEventPipeline writes on each outfit).</summary>
        public static string OutfitIcon(int n) => "ICO_Outfit_Burrows_" + n.ToString("00");

        /// <summary>
        /// Review captures (« tenues ajustées »): every measured (accessory, species) fit worn at the idle pose, framed on the accessory
        /// and the body around it, three-quarter front view; written to TestResults/Captures/T47_fit_* (T47 pieces) and T49_fit_*.
        /// </summary>
        public static string RenderFittings()
        {
            var studio = IconStudio.Open(out string refusal);
            if (studio == null)
                return refusal;
            const string folder = "TestResults/Captures";
            Directory.CreateDirectory(folder);
            var names = new List<string>();
            using (studio)
            {
                var camera = studio.Camera;
                foreach (var accessory in AccessoryFitMeasure.ShopCatalogs(studio.Database).SelectMany(c => c.accessories).Where(a => a != null))
                {
                    var piece = FindPiece(accessory.sourceId);
                    if (piece == null)
                        continue;
                    foreach (var fit in accessory.speciesFits)
                    {
                        var view = studio.Animal(fit.speciesId);
                        if (view == null)
                            continue;
                        try
                        {
                            var worn = Wear(studio, view, accessory, fit, PaletteRow(piece.Id, piece.IconPalette));
                            if (worn == null)
                                continue;
                            var bounds = new Bounds();
                            bool first = true;
                            foreach (var r in worn.GetComponentsInChildren<MeshRenderer>(true))
                            {
                                if (first)
                                    bounds = r.bounds;
                                else
                                    bounds.Encapsulate(r.bounds);
                                first = false;
                            }
                            // The accessory and the body around it.
                            bounds.Expand(Mathf.Max(0.12f, bounds.size.magnitude * 2.2f));
                            float radius = bounds.extents.magnitude;
                            float distance = radius / Mathf.Sin(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                            var direction = Quaternion.Euler(-28f, 32f, 0f) * Vector3.forward;
                            camera.transform.position = bounds.center + direction * distance;
                            camera.transform.LookAt(bounds.center);
                            camera.nearClipPlane = Mathf.Max(0.005f, distance - radius * 2f);
                            camera.farClipPlane = distance + radius * 3f;
                            string prefix = piece.Id.Contains(".burrows.") ? "T49" : "T47";
                            string path = $"{folder}/{prefix}_fit_{Short(piece.Id)}_{Short(fit.speciesId)}.png";
                            studio.CaptureTo(path);
                            names.Add(Path.GetFileName(path));
                        }
                        finally
                        {
                            Object.DestroyImmediate(view.gameObject);
                        }
                    }
                }
            }
            return names.Count + " fittings: " + string.Join(", ", names);
        }

        /// <summary>Three-quarter front view from slightly above (accessories, decorations and animals face +Z).</summary>
        public static void Frame(Camera camera, GameObject subject, float yaw)
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
