using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PocketWilds.Editor.UI;
using PocketWilds.Presentation;
using PocketWilds.UI;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.Activities
{
    /// <summary>
    /// T43 « Jardin » overlay pipeline (doc 01 Contenu section 8 and S-ACT-006; doc 05 sheet 24): PF_UI_Garden
    /// (GardenScreenView: a screen-space canvas at sorting order 16, over the garden set) with its top bar (« Jardin », the
    /// level pill and its progress, the Gems, close), 5 pooled bed labels on a full-screen layer (the presenter places them
    /// where the set camera sees each bed), the bottom tools (the can, « Tout récolter », « Finir maintenant » with its Gems
    /// and the speed-ups left), the « Défis » row (10 cells), the seed sheet (25 pooled packets in a 4-column grid), 6 product
    /// chips, the level-up banner, the note pill and the tutorial paw. Atlas_Garden packs agent GA's sprites found in
    /// Art/UI/Activities/Garden (UI_Garden_* and the garden's own ICO_*: packets, products, dyes, can), apart from every
    /// other atlas; the Addressables group Activity_Garden holds the overlay and its atlas (beside agent GP's set and plants).
    /// Nothing of the scene is referenced: the UI kit, the scene's icons (Gems, stars, heart, lock), the fonts and their
    /// materials are named in the prefab and borrowed at run time from what the scene already loaded. Every label is
    /// measured in its real font with every text it can show (English and French): one overflow and the prefab is not
    /// saved. Missing sprites are listed and left empty (plain kit shapes and role icons stand in), never invented.
    /// Idempotent; rerun when GA's sprites or the texts change.
    /// </summary>
    public static class GardenScreenPipeline
    {
        public const string PrefabPath = GardenLayout.ScreenPrefabPath;
        public const string AtlasPath = "Assets/_Game/Art/UI/Atlases/Atlas_Garden.spriteatlasv2";
        public const string AtlasAddress = "activity.garden.atlas";

        // The overlay's colours (the expeditions' warm tints, a garden green).
        public static readonly Color Card = UiColorTokens.Hex(0xFFFCF4);
        public static readonly Color CardLocked = UiColorTokens.Hex(0xEDE6DA);
        public static readonly Color Text = UiColorTokens.Hex(0x4A3A2C);
        public static readonly Color TextSoft = UiColorTokens.Hex(0x8A7660);
        public static readonly Color Accent = UiColorTokens.Hex(0xFFB15E);
        public static readonly Color Gold = UiColorTokens.Hex(0xF2B544);
        public static readonly Color StarOff = UiColorTokens.Hex(0xE3DCCF);
        public static readonly Color Leaf = UiColorTokens.Hex(0x78B860);
        public static readonly Color Water = UiColorTokens.Hex(0x74B3EA);
        public static readonly Color Disabled = UiColorTokens.Hex(0xCFC6B8);
        public static readonly Color Track = UiColorTokens.Hex(0xE8E0D2);

        // The scene's UI kit, borrowed by name (UiPipeline's kit).
        public const string KitPanel = "UI_Panel_Primary_9Slice";
        public const string KitButton = "UI_Button_Primary_9Slice";
        public const string KitPill = "UI_Pill_9Slice";
        public const string KitCircle = "UI_Button_IconCircle";
        public const string KitRing = "UI_Ring_Soft";
        public const string KitPaw = "UI_Gesture_Tap";
        public const string KitClose = "ICO_Close_X";

        /// <summary>The overlay's role icons by <see cref="GardenScreenView.Icon"/>, borrowed by name (the bundle's own sprite wins).</summary>
        public static readonly string[] IconNames =
        {
            "ICO_Currency_Gems", "UI_Star_Result_Full|ICO_Rarity_Star", "UI_Star_Result_Empty", "ICO_Lock|ICO_Unlock",
            "UI_Garden_Heart|ICO_Favorite_Heart|ICO_Heart", "UI_Garden_Drop|ICO_Garden_Drop|ICO_Drop", "ICO_WateringCan", "ICO_Seed", "ICO_Check",
        };

        // Agent GA's sprites (Art/UI/Activities/Garden).
        public const string LevelPill = "UI_Garden_LevelPill";
        public const string BedLabel = "UI_Garden_BedLabel";
        public const string CoverSign = "UI_Garden_CoverSign";
        public const string PacketCard = "UI_Garden_SeedPacketCard";

        /// <summary>
        /// The garden's own icons drawn by GA into the scene's icon folder (Art/UI/Icons): packed into Atlas_Garden with the
        /// overlay unless another atlas already packs them (then borrowed from the scene by name, never duplicated).
        /// </summary>
        public static readonly string[] GardenIconPrefixes = { "ICO_Seed_", "ICO_Crop_", "ICO_Dye_", "ICO_Ingredient_", "ICO_WateringCan" };

        public const string IconFolder = "Assets/_Game/Art/UI/Icons";

        /// <summary>The garden level of each starter plant (doc 01 table 8.3), in <see cref="GardenText.Plants"/> order (0: a biome plant).</summary>
        private static readonly int[] PlantLevels = { 1, 1, 1, 1, 2, 2, 3, 3, 4, 5, 6, 7, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

        [MenuItem("Pocket Wilds/Activities/Build Garden Screen (T43)", priority = 246)]
        public static void BuildMenu() => Debug.Log("[GardenScreenPipeline] " + BuildAll());

        public static string BuildAll()
        {
            var report = new List<string>();
            var art = LoadArt();
            report.Add(art.Missing.Count > 0
                ? "WARNING: sprites not drawn yet (plain shapes and role icons shown): " + string.Join(", ", art.Missing)
                : "agent GA's sprites: all found");
            var atlas = EnsureAtlas(art, report);
            var view = BuildPrefab(art, report);
            if (view != null)
                report.Add(EnsureAddressables(view.gameObject, atlas));
            AssetDatabase.SaveAssets();
            return string.Join("; ", report);
        }

        // ---------------------------------------------------------------- sprites

        /// <summary>GA's sprites by role, the garden's own icons (in the bundle) and the item table (seeds, products, dyes).</summary>
        public sealed class Art
        {
            public Sprite LevelPill, BedLabel, CoverSign, PacketCard;
            public readonly Sprite[] OwnIcons = new Sprite[IconNames.Length];
            public string[] ItemIds = Array.Empty<string>();
            public string[] ItemNames = Array.Empty<string>();
            public Sprite[] ItemOwn = Array.Empty<Sprite>();
            public readonly List<string> Missing = new List<string>();

            public IEnumerable<Sprite> All()
            {
                foreach (var sprite in new[] { LevelPill, BedLabel, CoverSign, PacketCard })
                    if (sprite != null)
                        yield return sprite;
                foreach (var sprite in OwnIcons)
                    if (sprite != null)
                        yield return sprite;
                foreach (var sprite in ItemOwn)
                    if (sprite != null)
                        yield return sprite;
            }
        }

        /// <summary>
        /// GA's sprites: every sprite under Art/UI/Activities/Garden by name (bundled in Atlas_Garden); a name not there is
        /// borrowed at run time if the scene's atlases have it (Art/UI/Icons), else listed as missing.
        /// </summary>
        public static Art LoadArt()
        {
            var art = new Art();
            var own = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            if (AssetDatabase.IsValidFolder(GardenLayout.SpriteFolder))
                foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { GardenLayout.SpriteFolder }))
                    foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
                        if (asset is Sprite sprite && !own.ContainsKey(sprite.name))
                            own.Add(sprite.name, sprite);
            // GA's garden icons in the scene's icon folder: bundled, unless another atlas packs them already.
            var atlases = new List<SpriteAtlas>();
            foreach (var guid in AssetDatabase.FindAssets("t:SpriteAtlas"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path == AtlasPath)
                    continue;
                var other = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);
                if (other != null)
                    atlases.Add(other);
            }
            var scene = new HashSet<string>(StringComparer.Ordinal);
            foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { IconFolder, "Assets/_Game/Art/UI/Kit" }))
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
                    if (asset is Sprite sprite)
                    {
                        scene.Add(sprite.name);
                        if (!IsGardenIcon(sprite.name) || own.ContainsKey(sprite.name) || atlases.Any(a => a.CanBindTo(sprite)))
                            continue;
                        own.Add(sprite.name, sprite);
                    }

            Sprite Own(string names, bool report)
            {
                foreach (var name in names.Split('|'))
                    if (own.TryGetValue(name, out var sprite))
                        return sprite;
                if (report && !names.Split('|').Any(scene.Contains))
                    art.Missing.Add(names.Split('|')[0]);
                return null;
            }

            art.LevelPill = Own(LevelPill, true);
            art.BedLabel = Own(BedLabel, true);
            art.CoverSign = Own(CoverSign, true);
            art.PacketCard = Own(PacketCard, true);
            for (int i = 0; i < IconNames.Length; i++)
                art.OwnIcons[i] = Own(IconNames[i], i == (int)GardenScreenView.Icon.Can || i == (int)GardenScreenView.Icon.Drop
                                                    || i == (int)GardenScreenView.Icon.Heart);
            var ids = new List<string>();
            var names = new List<string>();
            foreach (var plant in GardenText.Plants)
            {
                ids.Add(plant);
                names.Add(GardenText.SeedIcon(plant));
            }
            foreach (var product in GardenText.Products)
            {
                ids.Add(product);
                names.Add(GardenText.ProductIcon(product));
            }
            foreach (var dye in GardenText.Dyes)
            {
                ids.Add(dye);
                names.Add(GardenText.DyeIcon(dye));
            }
            art.ItemIds = ids.ToArray();
            art.ItemNames = names.ToArray();
            art.ItemOwn = new Sprite[ids.Count];
            for (int i = 0; i < ids.Count; i++)
                art.ItemOwn[i] = Own(art.ItemNames[i], true);
            return art;
        }

        public static bool IsGardenIcon(string name) =>
            !string.IsNullOrEmpty(name) && GardenIconPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal));

        // ---------------------------------------------------------------- atlas

        /// <summary>Atlas_Garden: GA's sprites of the garden, packed apart (no other atlas takes them).</summary>
        private static SpriteAtlasAsset EnsureAtlas(Art art, List<string> report)
        {
            var textures = new List<Object>();
            foreach (var sprite in art.All())
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GetAssetPath(sprite));
                if (texture != null && !textures.Contains(texture))
                    textures.Add(texture);
            }
            if (textures.Count == 0)
            {
                report.Add("no atlas (no garden sprite yet)");
                return null;
            }
            if (EditorSettings.spritePackerMode == SpritePackerMode.Disabled)
                EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;
            Directory.CreateDirectory(Path.GetDirectoryName(AtlasPath));
            var atlas = new SpriteAtlasAsset();
            atlas.Add(textures.ToArray());
            SpriteAtlasAsset.Save(atlas, AtlasPath);
            AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceUpdate);
            var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(AtlasPath);
            importer.includeInBuild = true;
            var packing = importer.packingSettings;
            packing.enableRotation = false;
            packing.enableTightPacking = false;
            packing.padding = 4;
            importer.packingSettings = packing;
            var textureSettings = importer.textureSettings;
            textureSettings.generateMipMaps = false;
            textureSettings.filterMode = FilterMode.Bilinear;
            textureSettings.sRGB = true;
            importer.textureSettings = textureSettings;
            foreach (var platform in new[] { "Android", "iPhone" })
            {
                var settings = importer.GetPlatformSettings(platform);
                settings.overridden = true;
                settings.maxTextureSize = 2048;
                settings.format = TextureImporterFormat.ASTC_4x4;
                importer.SetPlatformSettings(settings);
            }
            importer.SaveAndReimport();
            report.Add($"Atlas_Garden: {textures.Count} sprites");
            return atlas;
        }

        // ---------------------------------------------------------------- prefab

        private sealed class Borrow
        {
            public readonly List<Image> Images = new List<Image>();
            public readonly List<string> Names = new List<string>();

            public Image Kit(Image image, string name)
            {
                Images.Add(image);
                Names.Add(name);
                return image;
            }
        }

        /// <summary>A label and every text it can show (both languages), measured before the save.</summary>
        private sealed class Fit
        {
            public TMP_Text Label;
            public Vector2 Box;
            public int Lines;
            public string[] Texts;
        }

        private static readonly List<Fit> Fits = new List<Fit>();

        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        private static readonly Vector2 TopRight = new Vector2(1f, 1f);
        private static readonly Vector2 BottomCenter = new Vector2(0.5f, 0f);
        private static readonly Vector2 LeftMiddle = new Vector2(0f, 0.5f);

        private static float LineBox(float size, int lines) => Mathf.Ceil(size * (1.2f * lines + 0.15f));

        public static GardenScreenView BuildPrefab(Art art, List<string> report)
        {
            Fits.Clear();
            var root = new GameObject(GardenLayout.ScreenPrefabName, typeof(RectTransform));
            root.layer = 5;
            try
            {
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = GardenLayout.SortingOrder;
                canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1;
                UiPipeline.AddScaler(root);
                root.AddComponent<GraphicRaycaster>();
                var view = root.AddComponent<GardenScreenView>();
                var borrow = new Borrow();
                var p = new GardenScreenParts { canvas = canvas };

                // Everything under an inactive Content: no label wakes up (its font references are cleared before the save).
                var content = UiPipeline.Rect("Content", root.transform);
                content.gameObject.SetActive(false);
                UiPipeline.Stretch(content);
                p.content = content.gameObject;

                // The bed labels first (under the bars and the sheet), over the whole screen: they follow the set camera.
                var labels = UiPipeline.Rect("BedLabels", content);
                UiPipeline.Stretch(labels);
                labels.pivot = Center;
                p.labelLayer = labels;
                BuildBeds(p, labels, art, borrow);

                var safe = UiPipeline.Rect("SafeArea", content);
                UiPipeline.Stretch(safe);
                p.safeArea = safe;
                BuildTop(p, safe, art, borrow);
                BuildTools(p, safe, borrow);
                BuildChallenges(p, safe, borrow);
                BuildProducts(p, safe, borrow);
                BuildSheet(p, safe, art, borrow);
                BuildBannerNoteAndPaw(p, content, borrow);

                var overflows = Measure();
                if (overflows.Count > 0)
                {
                    report.Add("ERROR: labels too long for their boxes, the prefab is not saved: " + string.Join("; ", overflows));
                    Debug.LogError("[GardenScreenPipeline] labels overflow:\n" + string.Join("\n", overflows));
                    return null;
                }
                report.Add($"{Fits.Count} labels measured, every text fits");

                view.SetParts(p);
                view.SetArt(art.LevelPill, art.BedLabel, art.CoverSign);
                view.SetItems(art.ItemIds, art.ItemNames, art.ItemOwn);

                // The labels' fonts and materials by name, then their references cleared: the bundle holds no font.
                var texts = root.GetComponentsInChildren<TMP_Text>(true);
                var fonts = new string[texts.Length];
                var materials = new string[texts.Length];
                for (int i = 0; i < texts.Length; i++)
                {
                    fonts[i] = texts[i].font != null ? texts[i].font.name : string.Empty;
                    materials[i] = texts[i].fontSharedMaterial != null ? texts[i].fontSharedMaterial.name : string.Empty;
                    ClearFont(texts[i]);
                }
                view.SetBorrowed(borrow.Images.ToArray(), borrow.Names.ToArray(), texts, fonts, materials, (string[])IconNames.Clone(),
                    (Sprite[])art.OwnIcons.Clone());
                view.SetColors(Card, CardLocked, Text, TextSoft, Accent, Gold, StarOff, Leaf, Water, Disabled);
                canvas.enabled = false;

                Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
                var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath).GetComponent<GardenScreenView>();
                report.Add($"{GardenLayout.ScreenPrefabName}: {p.beds.Length} bed labels, {p.packets.Length} packets, {p.challenges.Length} challenges, "
                           + $"{p.products.Length} product chips, {texts.Length} labels, {borrow.Names.Distinct().Count()} kit sprites borrowed, "
                           + $"{art.ItemOwn.Count(s => s != null)}/{art.ItemIds.Length} item sprites in the bundle" + (saved.Valid ? "" : " (INCOMPLETE)"));
                report.Add(CheckDependencies());
                return saved;
            }
            finally
            {
                Fits.Clear();
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// The prefab references nothing of the scene's UI (kit, icons, HUD atlas, fonts, tokens); GA's garden icons of the
        /// icon folder that no other atlas packs are the garden's own (<see cref="IsGardenIcon"/>).
        /// </summary>
        public static string CheckDependencies()
        {
            var bad = AssetDatabase.GetDependencies(PrefabPath, true)
                .Where(d => d.Contains("/Fonts/") || d.Contains("Atlas_CommonHUD") || d.Contains("/Art/UI/Kit/")
                            || d.Contains("/Art/UI/Icons/") && !IsGardenIcon(Path.GetFileNameWithoutExtension(d))
                            || d.Contains("/Thumbnails/") || d.Contains("DEF_UiColorTokens") || d.Contains("DEF_CodexVisuals"))
                .ToList();
            return bad.Count == 0 ? "no scene asset referenced" : "WARNING: the prefab references scene assets: " + string.Join(", ", bad);
        }

        private static void ClearFont(TMP_Text label)
        {
            var so = new SerializedObject(label);
            foreach (var name in new[] { "m_fontAsset", "m_sharedMaterial", "m_fontMaterial", "m_Material", "m_baseMaterial" })
            {
                var property = so.FindProperty(name);
                if (property != null && property.propertyType == SerializedPropertyType.ObjectReference)
                    property.objectReferenceValue = null;
            }
            foreach (var name in new[] { "m_fontSharedMaterials", "m_fontMaterials" })
            {
                var property = so.FindProperty(name);
                if (property != null && property.isArray)
                    property.arraySize = 0;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- measuring

        private static List<string> Measure()
        {
            var problems = new List<string>();
            var probe = new GameObject("GardenMeasure", typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var m = probe.AddComponent<TextMeshProUGUI>();
                m.richText = false;
                m.margin = Vector4.zero;
                foreach (var fit in Fits)
                {
                    var label = fit.Label;
                    m.font = label.font;
                    m.fontSharedMaterial = label.fontSharedMaterial;
                    m.fontStyle = label.fontStyle;
                    m.characterSpacing = label.characterSpacing;
                    m.wordSpacing = label.wordSpacing;
                    m.lineSpacing = label.lineSpacing;
                    m.enableAutoSizing = false;
                    m.fontSize = label.fontSize;
                    m.textWrappingMode = fit.Lines > 1 ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
                    foreach (var text in fit.Texts)
                    {
                        if (string.IsNullOrEmpty(text))
                            continue;
                        var size = m.GetPreferredValues(text, fit.Box.x, 10000f);
                        if (size.x > fit.Box.x + 0.5f || size.y > fit.Box.y + 0.5f)
                            problems.Add($"{label.name} « {text.Replace("\n", " / ")} » {size.x:0}×{size.y:0} in {fit.Box.x:0}×{fit.Box.y:0}");
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }
            return problems;
        }

        /// <summary>The texts of keys in English and French (GardenTexts, then ActivityTexts, ExpeditionTexts and the UI table).</summary>
        private static string[] T(params string[] keys)
        {
            var found = new List<string>();
            foreach (var key in keys)
            {
                foreach (var table in new[] { GardenTexts.Texts, ActivityTexts.Texts, ExpeditionTexts.Texts, UiPipeline.Texts })
                {
                    bool hit = false;
                    for (int i = 0; i < table.GetLength(0); i++)
                    {
                        if (table[i, 0] != key)
                            continue;
                        found.Add(table[i, 1]);
                        found.Add(table[i, 2]);
                        hit = true;
                    }
                    if (hit)
                        break;
                }
            }
            return found.ToArray();
        }

        private static string[] Filled(string key, string n) =>
            T(key, key + ".one", key + ".other").Select(t => t.Replace("{n}", n)).ToArray();

        private const string Nbsp = " ";

        /// <summary>« Prêt dans » with the longest times (« 7 h 59 », « 59 min », « 59 s »), both languages.</summary>
        private static string[] ReadyTimes()
        {
            var times = new[] { "7" + Nbsp + "h" + Nbsp + "59", "59" + Nbsp + "min", "59" + Nbsp + "s", "7 h 59", "59 min", "59 s" };
            var result = new List<string>();
            foreach (var template in T(GardenText.ReadyIn))
                foreach (var time in times)
                    result.Add(template.Replace("{time}", time));
            return result.ToArray();
        }

        private static string[] PlantNames() => T(GardenText.Plants.Select(GardenText.PlantName).ToArray());

        /// <summary>A locked packet's line: « Jardin niveau 7 » or a biome's name.</summary>
        private static string[] LockTexts()
        {
            var keys = new List<string>();
            for (int i = 0; i < UiPipeline.Texts.GetLength(0); i++)
            {
                string key = UiPipeline.Texts[i, 0];
                if (key.StartsWith("biome.", StringComparison.Ordinal) && key.EndsWith(".name", StringComparison.Ordinal))
                    keys.Add(key);
            }
            keys.Add(GardenText.PlantBiome);
            return Filled(GardenText.LevelUp, "7").Concat(T(keys.ToArray())).ToArray();
        }

        /// <summary>The level-up banner's second line for each level that opens plants (2 to 7), both languages.</summary>
        private static string[] OpenedLines()
        {
            var lines = new List<string>();
            for (int language = 1; language <= 2; language++)
            {
                for (int level = 2; level <= 7; level++)
                {
                    var names = new List<string>();
                    for (int i = 0; i < PlantLevels.Length; i++)
                        if (PlantLevels[i] == level)
                            names.Add(GardenTexts.Plants[i, language]);
                    if (level == 4)
                        names.Add(T(GardenText.OpenedDyes)[language - 1]);
                    string template = T(GardenText.Opened)[language - 1];
                    lines.Add(template.Replace("{list}", string.Join(", ", names)));
                }
            }
            return lines.ToArray();
        }

        private static string[] ChallengeNames() => T(Enumerable.Range(0, 10).Select(GardenText.ChallengeName).ToArray());

        private static string[] ChallengeGoals() => T(Enumerable.Range(0, 10).Select(GardenText.ChallengeGoal).ToArray());

        // ---------------------------------------------------------------- building helpers

        private static TextMeshProUGUI Label(string name, Transform parent, TextRole role, float size, Color color, TextAlignmentOptions align,
            Vector2 anchor, Vector2 position, Vector2 box, int lines, params string[][] texts)
        {
            var label = UiPipeline.AddText(name, parent, role, size, color, string.Empty);
            label.alignment = align;
            label.textWrappingMode = lines > 1 ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            UiPipeline.Place(label.rectTransform, anchor, Center, position, box);
            var all = texts.SelectMany(t => t).ToArray();
            if (all.Length > 0)
                Fits.Add(new Fit { Label = label, Box = box, Lines = lines, Texts = all });
            return label;
        }

        private static Image Img(string name, Transform parent, Sprite sprite, Color color, Image.Type type, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var image = UiPipeline.AddImage(name, parent, sprite, color, type);
            UiPipeline.Place(image.rectTransform, anchor, Center, position, size);
            return image;
        }

        private static Button ButtonOn(Image target)
        {
            target.raycastTarget = true;
            var button = target.gameObject.AddComponent<Button>();
            UiPipeline.ConfigureButton(button, target);
            return button;
        }

        private static void Nested(GameObject go, bool raycast)
        {
            var nested = go.AddComponent<Canvas>();
            nested.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1;
            if (raycast)
                go.AddComponent<GraphicRaycaster>();
        }

        // ---------------------------------------------------------------- layout (reference 1080 × 2340)
        //
        // From the top of the safe area: the title bar (0 to 170) and the level pill (200). The set shows through the middle,
        // each bed with its floating label. From the bottom: the « Défis » row (16 to 244) and its title (280), the tools
        // (the can and « Finir maintenant » at 410, the speed-ups left at 512), « Tout récolter » (620), the product chips
        // (760). The seed sheet rises from the bottom over the lower 1250. Sizes never go under their role's minimum (Label,
        // NumberSmall and Caption 36, Body 38, Subheading and Button 44).

        private const float ChallengeRowY = 130f;
        private const float ToolsY = 410f;
        private const float SheetHeight = 1250f;

        // ---- the bed labels (pivot at their top centre: placed under each bed's near edge)
        private static void BuildBeds(GardenScreenParts p, RectTransform layer, Art art, Borrow borrow)
        {
            p.beds = new GardenScreenParts.Bed[GardenLayout.PlotCount];
            var ownedSize = new Vector2(500f, 88f);
            var signSize = new Vector2(420f, 150f);
            for (int i = 0; i < p.beds.Length; i++)
            {
                var b = new GardenScreenParts.Bed();
                var rootRect = UiPipeline.Rect("Bed" + i, layer);
                rootRect.anchorMin = rootRect.anchorMax = Center;
                rootRect.pivot = TopCenter;
                rootRect.sizeDelta = new Vector2(500f, 150f);
                rootRect.anchoredPosition = new Vector2(0f, -300f + i * 260f);
                b.root = rootRect;
                b.group = rootRect.gameObject.AddComponent<CanvasGroup>();
                b.group.blocksRaycasts = false;
                b.group.interactable = false;

                var owned = UiPipeline.Rect("Owned", rootRect);
                UiPipeline.Stretch(owned);
                b.owned = owned.gameObject;
                b.outline = borrow.Kit(Img("Outline", owned, null, Accent, Image.Type.Sliced, TopCenter, new Vector2(0f, -44f), ownedSize + new Vector2(16f, 16f)),
                    KitPill);
                b.outline.enabled = false;
                b.back = Img("Back", owned, art.BedLabel, art.BedLabel != null ? Color.white : Card, Image.Type.Sliced, TopCenter, new Vector2(0f, -44f), ownedSize);
                if (art.BedLabel == null)
                    borrow.Kit(b.back, KitPill);
                b.drop = Img("Drop", owned, null, Water, Image.Type.Simple, TopCenter, new Vector2(-215f, -44f), new Vector2(40f, 40f));
                b.state = Label("State", owned, TextRole.Label, 36f, Text, TextAlignmentOptions.Center, TopCenter, new Vector2(0f, -45f),
                    new Vector2(350f, LineBox(36f, 1)), 1, i == 0 ? T(GardenText.Plant, GardenText.Ready) : Array.Empty<string>(),
                    i == 0 ? ReadyTimes() : Array.Empty<string>());
                b.heart0 = Img("Heart0", owned, null, Leaf, Image.Type.Simple, TopCenter, new Vector2(198f, -36f), new Vector2(26f, 26f));
                b.heart1 = Img("Heart1", owned, null, Leaf, Image.Type.Simple, TopCenter, new Vector2(222f, -52f), new Vector2(24f, 24f));

                var locked = UiPipeline.Rect("Locked", rootRect);
                UiPipeline.Stretch(locked);
                b.locked = locked.gameObject;
                b.sign = Img("Sign", locked, art.CoverSign, art.CoverSign != null ? Color.white : Card, Image.Type.Sliced, TopCenter, new Vector2(0f, -75f), signSize);
                if (art.CoverSign == null)
                    borrow.Kit(b.sign, KitPanel);
                b.signLabel = Label("Label", locked, TextRole.Label, 36f, Text, TextAlignmentOptions.Center, TopCenter, new Vector2(0f, -46f),
                    new Vector2(380f, LineBox(36f, 1)), 1, i == 0 ? T(GardenText.NewPlot, GardenText.PlotClub) : Array.Empty<string>());
                b.priceGem = Img("Gem", locked, null, Color.white, Image.Type.Simple, TopCenter, new Vector2(-44f, -108f), new Vector2(44f, 44f));
                b.price = Label("Price", locked, TextRole.NumberSmall, 36f, Text, TextAlignmentOptions.MidlineLeft, TopCenter, new Vector2(40f, -108f),
                    new Vector2(120f, LineBox(36f, 1)), 1, i == 0 ? new[] { "400", "9999" } : Array.Empty<string>());
                locked.gameObject.SetActive(false);
                p.beds[i] = b;
            }
        }

        // ---- top bar: title, close, Gems, the level pill
        private static void BuildTop(GardenScreenParts p, RectTransform safe, Art art, Borrow borrow)
        {
            p.title = Label("Title", safe, TextRole.Heading, 64f, Text, TextAlignmentOptions.Center, TopCenter, new Vector2(0f, -90f),
                new Vector2(560f, LineBox(64f, 1)), 1, T(GardenText.Title));
            var closeBack = borrow.Kit(Img("Close", safe, null, Card, Image.Type.Simple, TopRight, new Vector2(-90f, -90f), new Vector2(120f, 120f)), KitCircle);
            p.close = ButtonOn(closeBack);
            p.closeIcon = borrow.Kit(Img("Icon", closeBack.transform, null, TextSoft, Image.Type.Simple, Center, Vector2.zero, new Vector2(56f, 56f)), KitClose);
            p.gemsChip = borrow.Kit(Img("Gems", safe, null, Card, Image.Type.Sliced, TopLeft, new Vector2(180f, -90f), new Vector2(280f, 88f)), KitPill);
            p.gemsIcon = Img("Icon", p.gemsChip.transform, null, Color.white, Image.Type.Simple, LeftMiddle, new Vector2(48f, 0f), new Vector2(60f, 60f));
            p.gems = Label("Amount", p.gemsChip.transform, TextRole.NumberSmall, 40f, Text, TextAlignmentOptions.MidlineLeft, LeftMiddle,
                new Vector2(174f, 2f), new Vector2(184f, LineBox(40f, 1)), 1, new[] { "99999" });

            p.levelPill = Img("LevelPill", safe, art.LevelPill, art.LevelPill != null ? Color.white : Card, Image.Type.Sliced, TopCenter,
                new Vector2(0f, -212f), new Vector2(560f, 104f));
            if (art.LevelPill == null)
                borrow.Kit(p.levelPill, KitPill);
            p.levelLabel = Label("Level", p.levelPill.transform, TextRole.Label, 38f, Text, TextAlignmentOptions.Center, Center, new Vector2(0f, 14f),
                new Vector2(500f, LineBox(38f, 1)), 1, Filled(GardenText.LevelUp, "10"));
            p.levelTrack = borrow.Kit(Img("Track", p.levelPill.transform, null, Track, Image.Type.Sliced, Center, new Vector2(0f, -28f), new Vector2(440f, 16f)),
                KitPill);
            p.levelFill = Img("Fill", p.levelPill.transform, null, Leaf, Image.Type.Filled, Center, new Vector2(0f, -28f), new Vector2(440f, 16f));
            p.levelFill.fillMethod = Image.FillMethod.Horizontal;
            p.levelFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            p.levelFill.fillAmount = 0f;
            borrow.Kit(p.levelFill, KitPill);
        }

        // ---- the bottom tools: the can, « Finir maintenant » (and the speed-ups left), « Tout récolter »
        private static void BuildTools(GardenScreenParts p, RectTransform safe, Borrow borrow)
        {
            var tools = UiPipeline.Rect("Tools", safe);
            UiPipeline.Stretch(tools);
            Nested(tools.gameObject, true);
            p.canRing = borrow.Kit(Img("CanRing", tools, null, Accent, Image.Type.Simple, BottomCenter, new Vector2(-380f, ToolsY), new Vector2(176f, 176f)), KitRing);
            p.canRing.enabled = false;
            p.canBack = borrow.Kit(Img("Can", tools, null, Card, Image.Type.Simple, BottomCenter, new Vector2(-380f, ToolsY), new Vector2(150f, 150f)), KitCircle);
            p.can = ButtonOn(p.canBack);
            p.canIcon = Img("Icon", p.canBack.transform, null, Color.white, Image.Type.Simple, Center, Vector2.zero, new Vector2(104f, 104f));

            p.speedUpBack = borrow.Kit(Img("SpeedUp", tools, null, Accent, Image.Type.Sliced, BottomCenter, new Vector2(250f, ToolsY), new Vector2(420f, 120f)),
                KitPill);
            p.speedUp = ButtonOn(p.speedUpBack);
            p.speedUpLabel = Label("Label", p.speedUpBack.transform, TextRole.Label, 36f, Text, TextAlignmentOptions.Center, Center, new Vector2(0f, 23f),
                new Vector2(390f, LineBox(36f, 1)), 1, T(GardenText.SpeedUp));
            p.speedUpGem = Img("Gem", p.speedUpBack.transform, null, Color.white, Image.Type.Simple, Center, new Vector2(-34f, -26f), new Vector2(40f, 40f));
            p.speedUpCost = Label("Cost", p.speedUpBack.transform, TextRole.NumberSmall, 36f, Text, TextAlignmentOptions.MidlineLeft, Center,
                new Vector2(46f, -25f), new Vector2(110f, LineBox(36f, 1)), 1, new[] { "999" });
            p.speedUpsLeft = Label("Left", tools, TextRole.Caption, 36f, TextSoft, TextAlignmentOptions.Center, BottomCenter, new Vector2(250f, ToolsY + 102f),
                new Vector2(420f, LineBox(36f, 1)), 1, Filled(GardenText.SpeedUpsLeft, "3"), T(GardenText.SpeedUpsNone));
            p.speedUp.gameObject.SetActive(false);

            p.harvestAllBack = borrow.Kit(Img("HarvestAll", tools, null, Accent, Image.Type.Sliced, BottomCenter, new Vector2(0f, ToolsY + 210f),
                new Vector2(480f, 112f)), KitButton);
            p.harvestAll = ButtonOn(p.harvestAllBack);
            p.harvestAllLabel = Label("Label", p.harvestAllBack.transform, TextRole.Button, 44f, Text, TextAlignmentOptions.Center, Center, new Vector2(0f, 2f),
                new Vector2(440f, LineBox(44f, 1)), 1, T(GardenText.HarvestAll));
            p.harvestAll.gameObject.SetActive(false);
        }

        // ---- the « Défis » row: each cell its name, its goal on up to two lines, its stars and the running progress
        private static void BuildChallenges(GardenScreenParts p, RectTransform safe, Borrow borrow)
        {
            p.challengesTitle = Label("ChallengesTitle", safe, TextRole.Subheading, 44f, Text, TextAlignmentOptions.MidlineLeft, BottomCenter,
                new Vector2(-170f, 280f), new Vector2(620f, LineBox(44f, 1)), 1, T(GardenText.Challenges));
            var viewport = UiPipeline.Rect("Challenges", safe);
            UiPipeline.Place(viewport, BottomCenter, Center, new Vector2(0f, ChallengeRowY), new Vector2(1000f, 228f));
            viewport.gameObject.AddComponent<RectMask2D>();
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0f);
            var cells = UiPipeline.Rect("Cells", viewport);
            cells.anchorMin = new Vector2(0f, 0f);
            cells.anchorMax = new Vector2(0f, 1f);
            cells.pivot = new Vector2(0f, 0.5f);
            cells.anchoredPosition = Vector2.zero;
            cells.sizeDelta = new Vector2(GardenScreenLayout.ChallengeCapacity * GardenScreenView.ChallengePitch + 2f * GardenScreenView.RowPadding, 0f);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            UiPipeline.ConfigureScroll(scroll, viewport, cells);
            scroll.horizontal = true;
            scroll.vertical = false;
            p.challengeScroll = scroll;
            p.challengeContent = cells;
            Nested(viewport.gameObject, true);
            var size = new Vector2(400f, 216f);
            p.challenges = new GardenScreenParts.Challenge[GardenScreenLayout.ChallengeCapacity];
            for (int i = 0; i < p.challenges.Length; i++)
            {
                var c = new GardenScreenParts.Challenge();
                var rootRect = UiPipeline.Rect("Challenge" + i.ToString("00"), cells);
                UiPipeline.Place(rootRect, LeftMiddle, Center, new Vector2(GardenScreenView.RowPadding + GardenScreenView.ChallengePitch * (i + 0.5f), 0f), size);
                c.root = rootRect;
                c.back = borrow.Kit(Img("Back", rootRect, null, Card, Image.Type.Sliced, Center, Vector2.zero, size), KitPanel);
                c.button = ButtonOn(c.back);
                c.name = Label("Name", rootRect, TextRole.Label, 36f, Text, TextAlignmentOptions.Center, TopCenter, new Vector2(0f, -40f),
                    new Vector2(380f, LineBox(36f, 1)), 1, i == 0 ? ChallengeNames() : Array.Empty<string>());
                c.goal = Label("Goal", rootRect, TextRole.Caption, 36f, TextSoft, TextAlignmentOptions.Center, TopCenter, new Vector2(0f, -112f),
                    new Vector2(380f, LineBox(36f, 2)), 2, i == 0 ? ChallengeGoals() : Array.Empty<string>());
                c.stars = new Image[3];
                for (int k = 0; k < 3; k++)
                    c.stars[k] = Img("Star" + k, rootRect, null, StarOff, Image.Type.Simple, BottomCenter, new Vector2(-150f + k * 40f, 24f), new Vector2(32f, 32f));
                c.progress = Label("Progress", rootRect, TextRole.NumberSmall, 36f, Text, TextAlignmentOptions.MidlineRight, BottomCenter, new Vector2(80f, 25f),
                    new Vector2(200f, LineBox(36f, 1)), 1, i == 0 ? T(GardenText.ChallengeProgress).Select(t => t.Replace("{n}", "18").Replace("{total}", "18")).ToArray()
                        : Array.Empty<string>());
                c.progress.gameObject.SetActive(false);
                p.challenges[i] = c;
            }
        }

        // ---- the harvest's product chips (an icon and « +3 », counting up)
        private static void BuildProducts(GardenScreenParts p, RectTransform safe, Borrow borrow)
        {
            var row = UiPipeline.Rect("Products", safe);
            UiPipeline.Place(row, BottomCenter, Center, new Vector2(0f, 760f), new Vector2(1040f, 104f));
            Nested(row.gameObject, false);
            p.productRow = row;
            p.products = new GardenScreenParts.Product[GardenScreenLayout.ProductCapacity];
            for (int i = 0; i < p.products.Length; i++)
            {
                var d = new GardenScreenParts.Product();
                var chip = UiPipeline.Rect("Product" + i, row);
                UiPipeline.Place(chip, Center, Center, new Vector2((i - 2.5f) * GardenScreenView.ProductPitch, 0f), new Vector2(200f, 96f));
                d.root = chip;
                d.group = chip.gameObject.AddComponent<CanvasGroup>();
                d.group.blocksRaycasts = false;
                d.group.interactable = false;
                d.back = borrow.Kit(UiPipeline.AddImage("Back", chip, null, Card, Image.Type.Sliced), KitPill);
                UiPipeline.Stretch(d.back.rectTransform);
                d.icon = Img("Icon", chip, null, Color.white, Image.Type.Simple, LeftMiddle, new Vector2(52f, 0f), new Vector2(68f, 68f));
                d.amount = Label("Amount", chip, TextRole.NumberSmall, 36f, Text, TextAlignmentOptions.MidlineLeft, LeftMiddle, new Vector2(146f, 2f),
                    new Vector2(100f, LineBox(36f, 1)), 1, i == 0 ? new[] { "+99" } : Array.Empty<string>());
                chip.gameObject.SetActive(false);
                p.products[i] = d;
            }
        }

        // ---- the seed sheet: a 4-column grid of packets
        private static void BuildSheet(GardenScreenParts p, RectTransform safe, Art art, Borrow borrow)
        {
            var sheet = UiPipeline.Rect("Sheet", safe);
            sheet.anchorMin = new Vector2(0f, 0f);
            sheet.anchorMax = new Vector2(1f, 0f);
            sheet.pivot = new Vector2(0.5f, 0f);
            sheet.offsetMin = new Vector2(0f, -40f);
            sheet.offsetMax = new Vector2(0f, SheetHeight);
            Nested(sheet.gameObject, true);
            p.sheet = sheet;
            p.sheetGroup = sheet.gameObject.AddComponent<CanvasGroup>();
            p.sheetBack = borrow.Kit(UiPipeline.AddImage("Back", sheet, null, Card, Image.Type.Sliced), KitPanel);
            UiPipeline.Stretch(p.sheetBack.rectTransform);
            p.sheetBack.raycastTarget = true;
            p.sheetTitle = Label("Title", sheet, TextRole.Subheading, 46f, Text, TextAlignmentOptions.MidlineLeft, TopLeft, new Vector2(330f, -64f),
                new Vector2(540f, LineBox(46f, 1)), 1, T(GardenText.Seeds));
            var closeBack = borrow.Kit(Img("Close", sheet, null, Card, Image.Type.Simple, TopRight, new Vector2(-80f, -70f), new Vector2(104f, 104f)), KitCircle);
            p.sheetClose = ButtonOn(closeBack);
            p.sheetCloseIcon = borrow.Kit(Img("Icon", closeBack.transform, null, TextSoft, Image.Type.Simple, Center, Vector2.zero, new Vector2(48f, 48f)), KitClose);

            var viewport = UiPipeline.Rect("Packets", sheet);
            viewport.anchorMin = new Vector2(0.5f, 0f);
            viewport.anchorMax = new Vector2(0.5f, 1f);
            viewport.pivot = new Vector2(0.5f, 1f);
            float width = GardenScreenView.PacketColumns * GardenScreenView.PacketPitchX + 2f * GardenScreenView.RowPadding;
            viewport.offsetMin = new Vector2(-width * 0.5f, 60f);
            viewport.offsetMax = new Vector2(width * 0.5f, -130f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0f);
            var cells = UiPipeline.Rect("Cells", viewport);
            cells.anchorMin = new Vector2(0f, 1f);
            cells.anchorMax = new Vector2(1f, 1f);
            cells.pivot = new Vector2(0.5f, 1f);
            cells.anchoredPosition = Vector2.zero;
            int rows = (GardenScreenLayout.PacketCapacity + GardenScreenView.PacketColumns - 1) / GardenScreenView.PacketColumns;
            cells.sizeDelta = new Vector2(0f, rows * GardenScreenView.PacketPitchY + 2f * GardenScreenView.RowPadding);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            UiPipeline.ConfigureScroll(scroll, viewport, cells);
            scroll.horizontal = false;
            scroll.vertical = true;
            p.packetScroll = scroll;
            p.packetContent = cells;

            var size = new Vector2(244f, 330f);
            var names = PlantNames();
            var locks = LockTexts();
            p.packets = new GardenScreenParts.Packet[GardenScreenLayout.PacketCapacity];
            for (int i = 0; i < p.packets.Length; i++)
            {
                var k = new GardenScreenParts.Packet();
                int column = i % GardenScreenView.PacketColumns, row = i / GardenScreenView.PacketColumns;
                var rootRect = UiPipeline.Rect("Packet" + i.ToString("00"), cells);
                UiPipeline.Place(rootRect, TopLeft, Center, new Vector2(GardenScreenView.RowPadding + GardenScreenView.PacketPitchX * (column + 0.5f),
                    -(GardenScreenView.RowPadding + GardenScreenView.PacketPitchY * (row + 0.5f))), size);
                k.root = rootRect;
                k.back = Img("Back", rootRect, art.PacketCard, art.PacketCard != null ? Color.white : Card, Image.Type.Sliced, Center, Vector2.zero, size);
                if (art.PacketCard == null)
                    borrow.Kit(k.back, KitPanel);
                k.button = ButtonOn(k.back);
                k.icon = Img("Icon", rootRect, null, Color.white, Image.Type.Simple, Center, new Vector2(0f, 98f), new Vector2(124f, 124f));
                k.lockIcon = Img("Lock", rootRect, null, TextSoft, Image.Type.Simple, Center, new Vector2(70f, 140f), new Vector2(48f, 48f));
                k.lockIcon.gameObject.SetActive(false);
                // Up to 3 lines: « Snow Lingonberry » breaks in three at the role size.
                k.name = Label("Name", rootRect, TextRole.Label, 36f, Text, TextAlignmentOptions.Center, Center, new Vector2(0f, -20f),
                    new Vector2(244f, LineBox(36f, 3)), 3, i == 0 ? names : Array.Empty<string>());
                k.count = Label("Count", rootRect, TextRole.NumberSmall, 36f, Text, TextAlignmentOptions.Center, Center, new Vector2(0f, -132f),
                    new Vector2(236f, LineBox(36f, 1)), 1, i == 0 ? new[] { "×99" } : Array.Empty<string>());
                k.lockLabel = Label("Lock", rootRect, TextRole.Caption, 36f, TextSoft, TextAlignmentOptions.Center, Center, new Vector2(0f, -132f),
                    new Vector2(236f, LineBox(36f, 2)), 2, i == 0 ? locks : Array.Empty<string>());
                k.lockLabel.gameObject.SetActive(false);
                p.packets[i] = k;
            }
            sheet.gameObject.SetActive(false);
        }

        // ---- the level-up banner, the note pill and the tutorial paw
        private static void BuildBannerNoteAndPaw(GardenScreenParts p, RectTransform content, Borrow borrow)
        {
            var banner = UiPipeline.Rect("Banner", content);
            UiPipeline.Place(banner, TopCenter, Center, new Vector2(0f, -420f), new Vector2(920f, 200f));
            Nested(banner.gameObject, false);
            p.banner = banner;
            p.bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            p.bannerGroup.blocksRaycasts = false;
            p.bannerGroup.interactable = false;
            var back = borrow.Kit(UiPipeline.AddImage("Back", banner, null, Card, Image.Type.Sliced), KitPanel);
            UiPipeline.Stretch(back.rectTransform);
            p.bannerTitle = Label("Title", banner, TextRole.Subheading, 48f, Leaf * 0.6f + Text * 0.4f, TextAlignmentOptions.Center, Center, new Vector2(0f, 46f),
                new Vector2(860f, LineBox(48f, 1)), 1, Filled(GardenText.LevelUp, "10"));
            p.bannerLine = Label("Line", banner, TextRole.Body, 38f, Text, TextAlignmentOptions.Center, Center, new Vector2(0f, -36f),
                new Vector2(860f, LineBox(38f, 2)), 2, OpenedLines());
            banner.gameObject.SetActive(false);

            var note = UiPipeline.Rect("Note", content);
            UiPipeline.Place(note, BottomCenter, Center, new Vector2(0f, 900f), new Vector2(900f, 128f));
            Nested(note.gameObject, false);
            p.notePill = note;
            p.noteGroup = note.gameObject.AddComponent<CanvasGroup>();
            p.noteGroup.blocksRaycasts = false;
            p.noteGroup.interactable = false;
            var noteBack = borrow.Kit(UiPipeline.AddImage("Back", note, null, new Color(0.29f, 0.23f, 0.17f, 0.88f), Image.Type.Sliced), KitPill);
            UiPipeline.Stretch(noteBack.rectTransform);
            p.noteLabel = Label("Label", note, TextRole.Body, 38f, Color.white, TextAlignmentOptions.Center, Center, new Vector2(0f, 2f),
                new Vector2(850f, LineBox(38f, 2)), 2,
                T(GardenText.NeedGems, GardenText.ClubSoon, GardenText.PlotConfirm, GardenText.NoSeeds, GardenText.SpeedUpsNone, GardenText.CanHint,
                    GardenText.AlreadyWatered, GardenText.Happy, GardenText.ChallengeLocked, GardenText.ChallengeStarted, GardenText.ChallengeStopped,
                    GardenText.PlantBiome), LockTexts());
            note.gameObject.SetActive(false);

            var hintRoot = UiPipeline.Rect("Paw", content);
            Nested(hintRoot.gameObject, false);
            var group = hintRoot.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            group.alpha = 0f;
            UiPipeline.Place(hintRoot, Center, Center, Vector2.zero, new Vector2(320f, 320f));
            var ring = borrow.Kit(Img("Ring", hintRoot, null, new Color(1f, 1f, 1f, 0f), Image.Type.Simple, Center, Vector2.zero, new Vector2(220f, 220f)), KitRing);
            var hand = borrow.Kit(Img("Paw", hintRoot, null, Color.white, Image.Type.Simple, Center, new Vector2(0f, 150f), new Vector2(170f, 170f)), KitPaw);
            var tap = hintRoot.gameObject.AddComponent<TapHint>();
            tap.SetReferences(hintRoot, group, hand.rectTransform, ring);
            hintRoot.gameObject.SetActive(false);
            p.paw = tap;
        }

        // ---------------------------------------------------------------- Addressables

        /// <summary>Group Activity_Garden (shared with agent GP's set and plants): the overlay and its atlas, loaded when the garden opens.</summary>
        public static string EnsureAddressables(GameObject prefab, SpriteAtlasAsset atlas)
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var group = EnsureGroup(settings, GardenLayout.Group);
            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab)), group, false, false);
            entry.address = GardenLayout.ScreenAddress;
            string atlasGuid = AssetDatabase.AssetPathToGUID(AtlasPath);
            string result = $"Addressables {GardenLayout.Group}: {GardenLayout.ScreenAddress}";
            if (atlas != null && !string.IsNullOrEmpty(atlasGuid))
            {
                var atlasEntry = settings.CreateOrMoveEntry(atlasGuid, group, false, false);
                atlasEntry.address = AtlasAddress;
                result += ", " + AtlasAddress;
            }
            EditorUtility.SetDirty(group);
            EditorUtility.SetDirty(settings);
            return result;
        }

        private static AddressableAssetGroup EnsureGroup(AddressableAssetSettings settings, string name)
        {
            var group = settings.FindGroup(name) ?? settings.CreateGroup(name, false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var bundled = group.GetSchema<BundledAssetGroupSchema>();
            bundled.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            bundled.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
            bundled.IncludeInBuild = true;
            bundled.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            bundled.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            group.GetSchema<ContentUpdateGroupSchema>().StaticContent = true;
            EditorUtility.SetDirty(group);
            return group;
        }
    }
}
