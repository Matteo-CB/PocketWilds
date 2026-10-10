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
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.Activities
{
    /// <summary>
    /// T42 « Expéditions » screen pipeline (doc 01 Contenu section 7, A18 and A19; doc 05 sheet 23): PF_UI_Expeditions
    /// (ExpeditionScreenView: a screen-space canvas at sorting order 23, over the hub and under the result card) with its
    /// title and close, the Gems chip, the map card and its 16 pooled destination cards in a horizontal row, the 4 slot cards
    /// (empty, away with the ring, the timer and « Finir maintenant », back with its glow, locked), « Tout réclamer », the
    /// « Défis » row (10 cells), the planning sheet (40 pooled portraits, « Équipe conseillée », the rating's stars and 3
    /// reasons, 4 duration chips, 7 loot items, the note, « Partir »), the welcome layer (the dim and 4 postcards, each with
    /// its frame, picture, stamp, postmark, soft ring and 6 round petals), the note pill and the tutorial paw. Atlas_Expeditions
    /// packs agent EA's sprites (Art/UI/Activities/Expeditions, found by role from their names) and agent EP's destination
    /// card art (Destinations/EXP_*), apart from every other atlas; the Addressables group Activity_Expeditions holds the
    /// screen and its atlas. Nothing of the scene is referenced: the UI kit's sprites and icons, the fonts and their
    /// materials are named in the prefab and borrowed at run time from what the scene already loaded (CommonContentTests:
    /// nothing duplicated between the bundle and the scene); the portraits are the codex thumbnails, borrowed too. Labels
    /// are built under an inactive object so their font references can be cleared before the save. Missing sprites are
    /// listed and left empty (the view shows plain shapes), never invented. Idempotent; rerun when EA's or EP's art changes.
    /// </summary>
    public static class ExpeditionPipeline
    {
        public const string PrefabPath = ExpeditionLayout.PrefabPath;
        public const string AtlasPath = "Assets/_Game/Art/UI/Atlases/Atlas_Expeditions.spriteatlasv2";
        public const string AtlasAddress = "activity.expeditions.atlas";

        // The screen's colours (doc 05 palette, the activity UI's warm tints).
        public static readonly Color Cream = UiColorTokens.Hex(0xFFF8EC);
        public static readonly Color Card = UiColorTokens.Hex(0xFFFCF4);
        public static readonly Color CardLocked = UiColorTokens.Hex(0xEDE6DA);
        public static readonly Color Text = UiColorTokens.Hex(0x4A3A2C);
        public static readonly Color TextSoft = UiColorTokens.Hex(0x8A7660);
        public static readonly Color Accent = UiColorTokens.Hex(0xFFB15E);
        public static readonly Color Gold = UiColorTokens.Hex(0xF2B544);
        public static readonly Color StarOff = UiColorTokens.Hex(0xE3DCCF);
        public static readonly Color Leaf = UiColorTokens.Hex(0x78B860);
        public static readonly Color Disabled = UiColorTokens.Hex(0xCFC6B8);
        public static readonly Color Petal = UiColorTokens.Hex(0xF6C6D3);
        public static readonly Color Shadow = new Color(0.42f, 0.30f, 0.20f, 0.12f);

        // The scene's UI kit, borrowed by name (UiPipeline's kit and icons).
        public const string KitPanel = "UI_Panel_Primary_9Slice";
        public const string KitButton = "UI_Button_Primary_9Slice";
        public const string KitPill = "UI_Pill_9Slice";
        public const string KitCircle = "UI_Button_IconCircle";
        public const string KitDot = "UI_Dot";
        public const string KitRing = "UI_Ring_Soft";
        public const string KitGlow = "UI_Glow_Soft";
        public const string KitPaw = "UI_Gesture_Tap";
        public const string KitClose = "ICO_Close_X";

        /// <summary>The loot preview's and the screen's icons by <see cref="ExpeditionScreenView.LootIcon"/>, borrowed by name.</summary>
        public static readonly string[] IconNames =
        {
            "ICO_Currency_Light", "ICO_Treat", "ICO_Seed", "ICO_Sticker|ICO_Gift", "UI_Egg_Shell|ICO_Hatch_Egg", "ICO_Ticket",
            "ICO_Ingredient_Hazelnut|ICO_Treat", "UI_Star_Result_Full|ICO_Rarity_Star", "ICO_Currency_Gems", "ICO_Lock|ICO_Unlock", "ICO_Leaf|ICO_BiomeMastery",
            "ICO_Paw", "ICO_Dots|UI_Dot", "ICO_Check", "ICO_Postcard|ICO_Act_Expeditions", "UI_Star_Result_Empty", "ICO_Expedition_Back|ICO_Timer",
        };

        [MenuItem("Pocket Wilds/Activities/Build Expeditions (T42)", priority = 244)]
        public static void BuildMenu() => Debug.Log("[ExpeditionPipeline] " + BuildAll());

        public static string BuildAll()
        {
            var report = new List<string>();
            var art = LoadArt();
            report.Add(art.Missing.Count > 0
                ? "WARNING: agent EA's sprites missing (left empty, plain shapes shown): " + string.Join(", ", art.Missing)
                : "agent EA's sprites: all found");
            report.Add(art.Arts.Count > 0
                ? "destination art: " + string.Join(", ", art.Arts.Select(a => a.name))
                : "WARNING: no destination art yet (agent EP's " + PostcardLayout.CardArtFolder + "/EXP_*.png)");
            var atlas = EnsureAtlas(art, report);
            var view = BuildPrefab(art, report);
            if (view != null)
                report.Add(EnsureAddressables(view.gameObject, atlas));
            AssetDatabase.SaveAssets();
            return string.Join("; ", report);
        }

        // ---------------------------------------------------------------- sprites

        /// <summary>Agent EA's sprites by role (null when not drawn yet) and agent EP's destination art.</summary>
        public sealed class Art
        {
            public Sprite Map, LockedDestination, SlotEmpty, SlotAway, SlotBack, SlotLocked, SlotGlow, RingTrack, RingFill, Plus;
            public Sprite Chip, ChipSelected, PostcardFrame, Stamp, Postmark, Petal, Leaf, FamilyPaw, Dots, PostcardIcon;
            public readonly Sprite[] Durations = new Sprite[ExpeditionLayout.DurationCount];
            public readonly List<Sprite> Arts = new List<Sprite>();
            public readonly List<string> Missing = new List<string>();

            public IEnumerable<Sprite> All()
            {
                foreach (var sprite in new[]
                         {
                             Map, LockedDestination, SlotEmpty, SlotAway, SlotBack, SlotLocked, SlotGlow, RingTrack, RingFill, Plus, Chip,
                             ChipSelected, PostcardFrame, Stamp, Postmark, Petal, Leaf, FamilyPaw, Dots, PostcardIcon,
                         })
                    if (sprite != null)
                        yield return sprite;
                foreach (var sprite in Durations)
                    if (sprite != null)
                        yield return sprite;
                foreach (var sprite in Arts)
                    if (sprite != null)
                        yield return sprite;
            }
        }

        // Agent EA's sprites (Art/UI/Activities/Expeditions, drawn by SourceArt/UI/Tools/pw_ui_sprites_t42.py).
        public const string MapPaper = "UI_Exp_MapPaper";
        public const string DestLocked = "UI_Exp_DestLocked";
        public const string SlotEmpty = "UI_Exp_Slot_Empty";
        public const string SlotAway = "UI_Exp_Slot_Away";
        public const string SlotBack = "UI_Exp_Slot_Back";
        public const string SlotLocked = "UI_Exp_Slot_Locked";
        public const string RingTrack = "UI_Exp_Ring_Track";
        public const string RingFill = "UI_Exp_Ring_Fill";
        public const string Plus = "UI_Exp_Plus";
        public const string Chip = "UI_Exp_Chip";
        public const string ChipSelected = "UI_Exp_Chip_Selected";
        public const string ClockPrefix = "ICO_Exp_Clock_";
        public const string PostcardFrame = "UI_Exp_PostcardFrame";
        public const string Stamp = "UI_Exp_Stamp_FlowerMeadows";
        public const string Postmark = "UI_Exp_Postmark";
        public const string PetalSprite = "UI_Exp_Petal";
        public const string ReasonBiome = "ICO_Exp_Reason_Biome";
        public const string ReasonFamily = "ICO_Exp_Reason_Family";
        public const string ReasonSpecies = "ICO_Exp_Reason_Species";
        public const string PostcardIcon = "ICO_Postcard";
        /// <summary>EA's bell for the refuge's toast: the scene's (FeedbackUiPipeline), never referenced by this bundle.</summary>
        public const string BackIcon = "ICO_Expedition_Back";

        /// <summary>The postcard frame (Tiled) at exactly this size; the 512 × 768 picture sits 40 px in.</summary>
        public static readonly Vector2 PostcardFrameSize = new Vector2(592f, 848f);

        public static string SpritePath(string name) => ExpeditionLayout.SpriteFolder + "/" + name + ".png";

        /// <summary>EA's sprites by name (a missing one is listed and left empty); the destination art is every EXP_*.png of EP's folder.</summary>
        public static Art LoadArt()
        {
            var art = new Art();

            Sprite Load(string name, bool required = true)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath(name));
                if (sprite == null && required)
                    art.Missing.Add(name);
                return sprite;
            }

            art.Map = Load(MapPaper);
            art.LockedDestination = Load(DestLocked);
            art.SlotEmpty = Load(SlotEmpty);
            art.SlotAway = Load(SlotAway);
            art.SlotBack = Load(SlotBack);
            art.SlotLocked = Load(SlotLocked);
            art.RingTrack = Load(RingTrack);
            art.RingFill = Load(RingFill);
            art.Plus = Load(Plus);
            art.Chip = Load(Chip);
            art.ChipSelected = Load(ChipSelected);
            string[] hours = { "1h", "4h", "8h", "12h" };
            for (int i = 0; i < hours.Length; i++)
                art.Durations[i] = Load(ClockPrefix + hours[i]);
            art.PostcardFrame = Load(PostcardFrame);
            art.Stamp = Load(Stamp);
            art.Postmark = Load(Postmark);
            art.Petal = Load(PetalSprite);
            art.Leaf = Load(ReasonBiome);
            art.FamilyPaw = Load(ReasonFamily);
            art.Dots = Load(ReasonSpecies);
            art.PostcardIcon = Load(PostcardIcon);
            if (Directory.Exists(PostcardLayout.CardArtFolder))
            {
                foreach (var file in Directory.GetFiles(PostcardLayout.CardArtFolder, "EXP_*.png", SearchOption.TopDirectoryOnly))
                {
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(file.Replace('\\', '/'));
                    if (sprite != null)
                        art.Arts.Add(sprite);
                }
            }
            return art;
        }

        // ---------------------------------------------------------------- atlas

        /// <summary>Atlas_Expeditions: EA's sprites and EP's destination art, packed apart (no other atlas takes them).</summary>
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
                report.Add("no atlas (no sprite yet)");
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
            report.Add($"Atlas_Expeditions: {textures.Count} sprites");
            return atlas;
        }

        // ---------------------------------------------------------------- prefab

        /// <summary>What the prefab borrows from the scene, gathered while it is built.</summary>
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

        /// <summary>A label and every text it can show (both languages, the longest values), measured before the save.</summary>
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

        /// <summary>A label box's height for this many lines (the fonts' line height is 1.2 em; a little room above).</summary>
        private static float LineBox(float size, int lines) => Mathf.Ceil(size * (1.2f * lines + 0.15f));

        public static ExpeditionScreenView BuildPrefab(Art art, List<string> report)
        {
            Fits.Clear();
            var root = new GameObject(ExpeditionLayout.ScreenPrefabName, typeof(RectTransform));
            root.layer = 5;
            try
            {
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = ExpeditionLayout.SortingOrder;
                canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1;
                UiPipeline.AddScaler(root);
                root.AddComponent<GraphicRaycaster>();
                var view = root.AddComponent<ExpeditionScreenView>();
                var borrow = new Borrow();
                var p = new ExpeditionScreenParts { canvas = canvas };

                // Everything under an inactive Content: no label wakes up (its font references are cleared before the save).
                var content = UiPipeline.Rect("Content", root.transform);
                content.gameObject.SetActive(false);
                UiPipeline.Stretch(content);
                p.content = content.gameObject;

                var main = UiPipeline.Rect("Main", content);
                UiPipeline.Stretch(main);
                p.contentGroup = main.gameObject.AddComponent<CanvasGroup>();
                p.background = UiPipeline.AddImage("Background", main, null, Cream, Image.Type.Simple);
                p.background.preserveAspect = false;
                UiPipeline.Stretch(p.background.rectTransform);
                var safe = UiPipeline.Rect("SafeArea", main);
                UiPipeline.Stretch(safe);
                p.safeArea = safe;

                BuildTop(p, safe, borrow);
                BuildMap(p, safe, art, borrow);
                BuildSlots(p, safe, art, borrow);
                BuildChallenges(p, safe, borrow);
                BuildSheet(p, safe, art, borrow);
                BuildWelcome(p, content, art, borrow);
                BuildNoteAndPaw(p, content, borrow);

                // Nothing may end in « … »: every label is measured in its real font with every text it can show.
                var overflows = Measure();
                if (overflows.Count > 0)
                {
                    report.Add("ERROR: labels too long for their boxes, the prefab is not saved: " + string.Join("; ", overflows));
                    Debug.LogError("[ExpeditionPipeline] labels overflow:\n" + string.Join("\n", overflows));
                    return null;
                }
                report.Add($"{Fits.Count} labels measured, every text fits");

                view.SetParts(p);
                var arts = art.Arts.ToArray();
                view.SetArt(art.SlotEmpty, art.SlotAway, art.SlotBack, art.SlotLocked, art.LockedDestination, art.Chip, art.ChipSelected,
                    (Sprite[])art.Durations.Clone(), arts.Select(a => a.name).ToArray(), arts);
                var own = new Sprite[IconNames.Length];
                own[(int)ExpeditionScreenView.LootIcon.Leaf] = art.Leaf;
                own[(int)ExpeditionScreenView.LootIcon.Paw] = art.FamilyPaw;
                own[(int)ExpeditionScreenView.LootIcon.Dots] = art.Dots;
                own[(int)ExpeditionScreenView.LootIcon.Postcard] = art.PostcardIcon;

                // The labels' fonts and materials by name, then their references cleared: the bundle holds no font.
                var labels = root.GetComponentsInChildren<TMP_Text>(true);
                var fonts = new string[labels.Length];
                var materials = new string[labels.Length];
                for (int i = 0; i < labels.Length; i++)
                {
                    fonts[i] = labels[i].font != null ? labels[i].font.name : string.Empty;
                    materials[i] = labels[i].fontSharedMaterial != null ? labels[i].fontSharedMaterial.name : string.Empty;
                    ClearFont(labels[i]);
                }
                view.SetBorrowed(borrow.Images.ToArray(), borrow.Names.ToArray(), labels, fonts, materials, (string[])IconNames.Clone(), own);
                view.SetColors(Cream, Card, CardLocked, Text, TextSoft, Accent, Gold, StarOff, Leaf, Disabled);
                canvas.enabled = false;

                Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
                var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath).GetComponent<ExpeditionScreenView>();
                report.Add($"{ExpeditionLayout.ScreenPrefabName}: {p.slots.Length} slots, {p.destinations.Length} destination cards, {p.animals.Length} portraits, "
                           + $"{p.challenges.Length} challenges, {p.postcards.Length} postcards, {labels.Length} labels, {borrow.Names.Distinct().Count()} kit sprites borrowed"
                           + (saved.Valid ? "" : " (INCOMPLETE)"));
                report.Add(CheckDependencies());
                return saved;
            }
            finally
            {
                Fits.Clear();
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>The prefab references nothing of the scene's UI (kit, icons, HUD atlas, fonts, tokens, codex thumbnails).</summary>
        public static string CheckDependencies()
        {
            var bad = AssetDatabase.GetDependencies(PrefabPath, true)
                .Where(d => d.Contains("/Fonts/") || d.Contains("Atlas_CommonHUD") || d.Contains("/Art/UI/Kit/") || d.Contains("/Art/UI/Icons/")
                            || d.Contains("/Thumbnails/") || d.Contains("DEF_UiColorTokens") || d.Contains("DEF_CodexVisuals"))
                .ToList();
            return bad.Count == 0 ? "no scene asset referenced" : "WARNING: the prefab references scene assets: " + string.Join(", ", bad);
        }

        /// <summary>Clears a label's font and material references (the label never woke up: nothing reassigns them).</summary>
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

        /// <summary>
        /// Each registered label with each of its texts, on an awake copy of the label (same font, material, size, spacing,
        /// wrapping): TMP's preferred size within the box's width must fit the box (its lines included). Returns the misses.
        /// </summary>
        private static List<string> Measure()
        {
            var problems = new List<string>();
            var probe = new GameObject("ExpeditionMeasure", typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave };
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

        /// <summary>The texts of keys in English and French (ExpeditionTexts, else ActivityTexts).</summary>
        private static string[] T(params string[] keys)
        {
            var found = new List<string>();
            foreach (var key in keys)
            {
                foreach (var table in new[] { ExpeditionTexts.Texts, ActivityTexts.Texts })
                {
                    for (int i = 0; i < table.GetLength(0); i++)
                    {
                        if (table[i, 0] != key)
                            continue;
                        found.Add(table[i, 1]);
                        found.Add(table[i, 2]);
                    }
                }
            }
            return found.ToArray();
        }

        /// <summary>A counted template's texts with {n} filled by the longest value.</summary>
        private static string[] Filled(string key, string n) =>
            T(key, key + ".one", key + ".other").Select(t => t.Replace("{n}", n)).ToArray();

        private static string[] Names()
        {
            var keys = new List<string>();
            for (int i = 0; i < ExpeditionTexts.BiomeDestinations.GetLength(0); i++)
                keys.Add(ExpeditionText.DestinationName(ExpeditionTexts.BiomeDestinations[i, 0]));
            for (int i = 0; i < ExpeditionTexts.EventDestinations.GetLength(0); i++)
                keys.Add(ExpeditionText.DestinationName(ExpeditionTexts.EventDestinations[i, 0]));
            return T(keys.ToArray());
        }

        private static string[] ChallengeNames() => T(Enumerable.Range(0, 10).Select(ExpeditionText.ChallengeName).ToArray());

        private static string[] ChallengeGoals() => T(Enumerable.Range(0, 10).Select(ExpeditionText.ChallengeGoal).ToArray());

        /// <summary>« De retour dans » with the time on its own line, as the slot writes it (ExpeditionTimeText, line break).</summary>
        private static string[] Timers()
        {
            var times = new[] { "12" + Nbsp + "h" + Nbsp + "59", "59" + Nbsp + "min", "59" + Nbsp + "s", "12 h 59", "59 min" };
            var result = new List<string>();
            foreach (var template in T(ExpeditionText.BackIn))
            {
                int at = template.IndexOf("{time}", StringComparison.Ordinal);
                string prefix = (at >= 0 ? template.Substring(0, at) : template).TrimEnd(' ', ' ');
                string suffix = at >= 0 ? template.Substring(at + 6) : string.Empty;
                foreach (var time in times)
                    result.Add(prefix + "\n" + time + suffix);
            }
            return result.ToArray();
        }

        private const string Nbsp = " ";

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

        /// <summary>A horizontal scrolling row: its viewport (masked) and the content its pooled views sit in.</summary>
        private static RectTransform ScrollRow(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, int count, float pitch,
            out ScrollRect scroll)
        {
            var viewport = UiPipeline.Rect(name, parent);
            UiPipeline.Place(viewport, anchor, Center, position, size);
            viewport.gameObject.AddComponent<RectMask2D>();
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0f);
            var contentRect = UiPipeline.Rect("Cells", viewport);
            contentRect.anchorMin = new Vector2(0f, 0f);
            contentRect.anchorMax = new Vector2(0f, 1f);
            contentRect.pivot = new Vector2(0f, 0.5f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(count * pitch + 2f * ExpeditionScreenView.RowPadding, 0f);
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            UiPipeline.ConfigureScroll(scroll, viewport, contentRect);
            scroll.horizontal = true;
            scroll.vertical = false;
            return contentRect;
        }

        // ---------------------------------------------------------------- layout (reference 1080 × 2340)
        //
        // From the top of the safe area: the title bar (0 to 170), the map card (170 to 710) with « Tout réclamer » on its
        // edge, then the 2 × 2 slots filling what is left down to the « Défis » block (its 400 at the bottom). The planning
        // sheet covers everything under the map card. Sizes never go under their role's minimum (Label, NumberSmall and
        // Caption 36, Body 38, Subheading and Button 44).

        private const float MapTop = 170f;
        private const float MapHeight = 540f;
        private const float SlotsTop = MapTop + MapHeight + 56f;
        private const float ChallengesHeight = 400f;
        private const float SheetTop = MapTop + MapHeight - 10f;

        /// <summary>The widest a reason's words may be (its chip grows with them; a second row takes the rest).</summary>
        private const float ReasonMaxText = 420f;

        // ---- top bar: title, close, Gems
        private static void BuildTop(ExpeditionScreenParts p, RectTransform safe, Borrow borrow)
        {
            p.title = Label("Title", safe, TextRole.Heading, 64f, Text, TextAlignmentOptions.Center, TopCenter, new Vector2(0f, -90f),
                new Vector2(600f, LineBox(64f, 1)), 1, T(ExpeditionText.Title));
            var closeBack = borrow.Kit(Img("Close", safe, null, Card, Image.Type.Simple, TopRight, new Vector2(-90f, -90f), new Vector2(120f, 120f)), KitCircle);
            p.close = ButtonOn(closeBack);
            p.closeIcon = borrow.Kit(Img("Icon", closeBack.transform, null, TextSoft, Image.Type.Simple, Center, Vector2.zero, new Vector2(56f, 56f)), KitClose);
            p.gemsChip = borrow.Kit(Img("Gems", safe, null, Card, Image.Type.Sliced, TopLeft, new Vector2(180f, -90f), new Vector2(280f, 88f)), KitPill);
            p.gemsIcon = Img("Icon", p.gemsChip.transform, null, Color.white, Image.Type.Simple, LeftMiddle, new Vector2(48f, 0f), new Vector2(60f, 60f));
            p.gems = Label("Amount", p.gemsChip.transform, TextRole.NumberSmall, 40f, Text, TextAlignmentOptions.MidlineLeft, LeftMiddle,
                new Vector2(174f, 2f), new Vector2(184f, LineBox(40f, 1)), 1, new[] { "99999" });
        }

        // ---- the map card and its destination cards (8:5 art, the name on two lines)
        private static void BuildMap(ExpeditionScreenParts p, RectTransform safe, Art art, Borrow borrow)
        {
            p.map = Img("Map", safe, art.Map, art.Map != null ? Color.white : Card, art.Map != null ? Image.Type.Simple : Image.Type.Sliced, TopCenter,
                new Vector2(0f, -(MapTop + MapHeight * 0.5f)), new Vector2(1000f, MapHeight));
            p.map.preserveAspect = false;
            if (art.Map == null)
                borrow.Kit(p.map, KitPanel);
            var viewport = UiPipeline.Rect("Viewport", p.map.transform);
            UiPipeline.Stretch(viewport);
            viewport.offsetMin = new Vector2(24f, 24f);
            viewport.offsetMax = new Vector2(-24f, -24f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0f);
            var contentRect = UiPipeline.Rect("Cards", viewport);
            contentRect.anchorMin = new Vector2(0f, 0f);
            contentRect.anchorMax = new Vector2(0f, 1f);
            contentRect.pivot = new Vector2(0f, 0.5f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(ExpeditionLayout.DestinationCapacity * ExpeditionScreenView.DestinationPitch + 40f, 0f);
            var scroll = p.map.gameObject.AddComponent<ScrollRect>();
            UiPipeline.ConfigureScroll(scroll, viewport, contentRect);
            scroll.horizontal = true;
            scroll.vertical = false;
            p.mapScroll = scroll;
            p.mapContent = contentRect;
            p.destinations = new ExpeditionScreenParts.Destination[ExpeditionLayout.DestinationCapacity];
            var size = new Vector2(400f, 384f);
            var names = Names();
            for (int i = 0; i < p.destinations.Length; i++)
            {
                var d = new ExpeditionScreenParts.Destination();
                var cardRoot = UiPipeline.Rect("Destination" + i.ToString("00"), contentRect);
                UiPipeline.Place(cardRoot, LeftMiddle, Center,
                    new Vector2(ExpeditionScreenView.RowPadding + ExpeditionScreenView.DestinationPitch * (i + 0.5f), 0f), size);
                d.root = cardRoot;
                d.outline = borrow.Kit(Img("Outline", cardRoot, null, Accent, Image.Type.Sliced, Center, Vector2.zero, size + new Vector2(16f, 16f)), KitPanel);
                d.outline.gameObject.SetActive(false);
                d.frame = borrow.Kit(Img("Frame", cardRoot, null, Card, Image.Type.Sliced, Center, Vector2.zero, size), KitPanel);
                d.button = ButtonOn(d.frame);
                // The art: 8:5 like EP's EXP_* render (512 × 320); EA's 4:3 locked silhouette fits inside (preserveAspect).
                d.art = Img("Art", cardRoot, null, Color.white, Image.Type.Simple, TopCenter, new Vector2(0f, -131f), new Vector2(380f, 238f));
                d.art.preserveAspect = true;
                d.art.enabled = false;
                d.lockIcon = Img("Lock", cardRoot, null, TextSoft, Image.Type.Simple, TopCenter, new Vector2(0f, -131f), new Vector2(72f, 72f));
                d.lockIcon.gameObject.SetActive(false);
                d.name = Label("Name", cardRoot, TextRole.Label, 36f, Text, TextAlignmentOptions.Center, BottomCenter, new Vector2(0f, 64f),
                    new Vector2(384f, LineBox(36f, 2)), 2, i == 0 ? names : Array.Empty<string>());
                p.destinations[i] = d;
            }
        }

        // ---- the 4 slot cards (a 2 × 2 grid that fills the space) and « Tout réclamer »
        private static void BuildSlots(ExpeditionScreenParts p, RectTransform safe, Art art, Borrow borrow)
        {
            var area = UiPipeline.Rect("Slots", safe);
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            area.offsetMin = new Vector2(40f, ChallengesHeight);
            area.offsetMax = new Vector2(-40f, -SlotsTop);
            Nested(area.gameObject, true);
            p.slots = new ExpeditionScreenParts.Slot[ExpeditionLayout.SlotCapacity];
            const float gap = 12f;
            const float inner = 450f;
            var names = Names();
            for (int i = 0; i < p.slots.Length; i++)
            {
                var s = new ExpeditionScreenParts.Slot();
                var rootRect = UiPipeline.Rect("Slot" + i, area);
                // Each slot takes a quarter of the area (its height follows the screen; its parts hold to its top and bottom).
                bool left = i % 2 == 0, top = i < 2;
                rootRect.anchorMin = new Vector2(left ? 0f : 0.5f, top ? 0.5f : 0f);
                rootRect.anchorMax = new Vector2(left ? 0.5f : 1f, top ? 1f : 0.5f);
                rootRect.pivot = Center;
                rootRect.offsetMin = new Vector2(left ? 0f : gap, top ? gap : 0f);
                rootRect.offsetMax = new Vector2(left ? -gap : 0f, top ? 0f : -gap);
                s.root = rootRect;
                s.glow = UiPipeline.AddImage("Glow", rootRect, null, Gold, Image.Type.Sliced);
                UiPipeline.Stretch(s.glow.rectTransform);
                s.glow.rectTransform.offsetMin = new Vector2(-20f, -20f);
                s.glow.rectTransform.offsetMax = new Vector2(20f, 20f);
                borrow.Kit(s.glow, KitGlow);
                s.glow.enabled = false;
                s.frame = UiPipeline.AddImage("Frame", rootRect, art.SlotEmpty, art.SlotEmpty != null ? Color.white : Card, Image.Type.Sliced);
                UiPipeline.Stretch(s.frame.rectTransform);
                if (art.SlotEmpty == null)
                    borrow.Kit(s.frame, KitPanel);
                s.button = ButtonOn(s.frame);

                // Empty: a « + » and « Choisis ton équipe ».
                var empty = UiPipeline.Rect("Empty", rootRect);
                UiPipeline.Stretch(empty);
                s.empty = empty.gameObject;
                s.plus = Img("Plus", empty, art.Plus, art.Plus != null ? Color.white : Accent, Image.Type.Simple, Center, new Vector2(0f, 60f), new Vector2(130f, 130f));
                if (art.Plus == null)
                    borrow.Kit(s.plus, KitCircle);
                s.emptyLabel = Label("Label", empty, TextRole.Label, 40f, TextSoft, TextAlignmentOptions.Center, Center, new Vector2(0f, -80f),
                    new Vector2(inner, LineBox(40f, 2)), 2, T(ExpeditionText.PickTeam));

                // Away or back, at the top: the team, a small row of stars, the destination on up to two lines.
                var team = UiPipeline.Rect("Team", rootRect);
                UiPipeline.Stretch(team);
                s.team = team.gameObject;
                s.portraits = new Image[ExpeditionLayout.MaxTeam];
                for (int k = 0; k < s.portraits.Length; k++)
                {
                    var disc = borrow.Kit(Img("Disc" + k, team, null, Cream, Image.Type.Simple, TopCenter, new Vector2((k - 1) * 128f, -82f), new Vector2(116f, 116f)),
                        KitCircle);
                    s.portraits[k] = Img("Portrait", disc.transform, null, Color.white, Image.Type.Simple, Center, Vector2.zero, new Vector2(104f, 104f));
                    s.portraits[k].preserveAspect = true;
                }
                s.stars = new Image[3];
                for (int k = 0; k < 3; k++)
                    s.stars[k] = Img("Star" + k, team, null, StarOff, Image.Type.Simple, TopCenter, new Vector2((k - 1) * 44f, -152f), new Vector2(40f, 40f));
                s.destination = Label("Destination", team, TextRole.Label, 36f, Text, TextAlignmentOptions.Center, TopCenter, new Vector2(0f, -222f),
                    new Vector2(inner, LineBox(36f, 2)), 2, i == 0 ? names : Array.Empty<string>());

                // Away, at the bottom: the ring with « De retour dans » and the time on its own line, the speed-ups left, then
                // « Finir maintenant » with its Gems on a second line of the button.
                var away = UiPipeline.Rect("Away", rootRect);
                UiPipeline.Stretch(away);
                s.away = away.gameObject;
                s.ringTrack = Img("RingTrack", away, art.RingTrack, art.RingTrack != null ? Color.white : StarOff, Image.Type.Simple, BottomCenter,
                    new Vector2(-180f, 232f), new Vector2(76f, 76f));
                if (art.RingTrack == null)
                    borrow.Kit(s.ringTrack, KitRing);
                s.ringFill = Img("RingFill", away, art.RingFill, Accent, Image.Type.Filled, BottomCenter, new Vector2(-180f, 232f), new Vector2(76f, 76f));
                s.ringFill.fillMethod = Image.FillMethod.Radial360;
                s.ringFill.fillOrigin = (int)Image.Origin360.Top;
                s.ringFill.fillClockwise = true;
                s.ringFill.fillAmount = 0f;
                if (art.RingFill == null)
                    borrow.Kit(s.ringFill, KitRing);
                s.timer = Label("Timer", away, TextRole.Label, 36f, Text, TextAlignmentOptions.MidlineLeft, BottomCenter, new Vector2(46f, 232f),
                    new Vector2(350f, LineBox(36f, 2)), 2, i == 0 ? Timers() : Array.Empty<string>());
                s.speedUpsLeft = Label("Left", away, TextRole.Caption, 36f, TextSoft, TextAlignmentOptions.Center, BottomCenter, new Vector2(0f, 148f),
                    new Vector2(inner, LineBox(36f, 1)), 1, Filled(ExpeditionText.SpeedUpsLeft, "3"), T(ExpeditionText.SpeedUpsNone));
                s.speedUpBack = borrow.Kit(Img("SpeedUp", away, null, Accent, Image.Type.Sliced, BottomCenter, new Vector2(0f, 68f), new Vector2(inner, 108f)), KitPill);
                s.speedUp = ButtonOn(s.speedUpBack);
                s.speedUpLabel = Label("Label", s.speedUpBack.transform, TextRole.Label, 36f, Text, TextAlignmentOptions.Center, Center, new Vector2(0f, 23f),
                    new Vector2(inner - 30f, LineBox(36f, 1)), 1, T(ExpeditionText.SpeedUp));
                s.speedUpGem = Img("Gem", s.speedUpBack.transform, null, Color.white, Image.Type.Simple, Center, new Vector2(-34f, -24f), new Vector2(40f, 40f));
                s.speedUpCost = Label("Cost", s.speedUpBack.transform, TextRole.NumberSmall, 36f, Text, TextAlignmentOptions.MidlineLeft, Center,
                    new Vector2(46f, -23f), new Vector2(110f, LineBox(36f, 1)), 1, new[] { "999" });

                // Back: the postcard icon and « Voir la carte postale » on up to two lines, the slot glowing.
                var back = UiPipeline.Rect("Back", rootRect);
                UiPipeline.Stretch(back);
                s.back = back.gameObject;
                s.backIcon = Img("Icon", back, null, Color.white, Image.Type.Simple, BottomCenter, new Vector2(-180f, 96f), new Vector2(80f, 80f));
                s.backLabel = Label("Label", back, TextRole.Label, 40f, Accent * 0.6f + Text * 0.4f, TextAlignmentOptions.Center, BottomCenter,
                    new Vector2(46f, 96f), new Vector2(350f, LineBox(40f, 2)), 2, T(ExpeditionText.OpenPostcard));

                // Locked: the lock, « Nouvel emplacement » or « Avec le Wild Club », the price.
                var locked = UiPipeline.Rect("Locked", rootRect);
                UiPipeline.Stretch(locked);
                s.locked = locked.gameObject;
                s.lockIcon = Img("Lock", locked, null, TextSoft, Image.Type.Simple, Center, new Vector2(0f, 96f), new Vector2(96f, 96f));
                s.lockedLabel = Label("Label", locked, TextRole.Label, 40f, TextSoft, TextAlignmentOptions.Center, Center, new Vector2(0f, -10f),
                    new Vector2(inner, LineBox(40f, 2)), 2, T(ExpeditionText.SlotLocked, ExpeditionText.SlotClub));
                s.priceGem = Img("Gem", locked, null, Color.white, Image.Type.Simple, Center, new Vector2(-54f, -110f), new Vector2(52f, 52f));
                s.price = Label("Price", locked, TextRole.NumberSmall, 40f, Text, TextAlignmentOptions.MidlineLeft, Center, new Vector2(44f, -108f),
                    new Vector2(140f, LineBox(40f, 1)), 1, new[] { "800" });

                // H-VFX-ACT-005-01: 4 soft dust dots at the team's feet.
                s.dust = new Image[ExpeditionLayout.DustDots];
                for (int k = 0; k < s.dust.Length; k++)
                {
                    s.dust[k] = borrow.Kit(Img("Dust" + k, rootRect, null, new Color(0.86f, 0.78f, 0.64f, 0f), Image.Type.Simple, TopCenter,
                        new Vector2((k - 1.5f) * 60f, -140f), new Vector2(26f, 26f)), KitDot);
                    s.dust[k].enabled = false;
                }
                p.slots[i] = s;
            }

            // « Tout réclamer » rides on the map card's lower edge.
            p.claimAllBack = borrow.Kit(Img("ClaimAll", safe, null, Accent, Image.Type.Sliced, TopCenter, new Vector2(0f, -(MapTop + MapHeight)),
                new Vector2(460f, 96f)), KitButton);
            p.claimAll = ButtonOn(p.claimAllBack);
            p.claimAllLabel = Label("Label", p.claimAllBack.transform, TextRole.Button, 44f, Text, TextAlignmentOptions.Center, Center, new Vector2(0f, 2f),
                new Vector2(420f, LineBox(44f, 1)), 1, T(ExpeditionText.ClaimAll));
            p.claimAll.gameObject.SetActive(false);
        }

        // ---- the « Défis » row: each cell its name and its constraint, each on up to two lines
        private static void BuildChallenges(ExpeditionScreenParts p, RectTransform safe, Borrow borrow)
        {
            p.challengesTitle = Label("ChallengesTitle", safe, TextRole.Subheading, 44f, Text, TextAlignmentOptions.MidlineLeft, BottomCenter,
                new Vector2(-170f, 362f), new Vector2(620f, LineBox(44f, 1)), 1, T(ExpeditionText.Challenges));
            var size = new Vector2(420f, 300f);
            p.challengeContent = ScrollRow("Challenges", safe, BottomCenter, new Vector2(0f, 176f), new Vector2(1000f, 312f),
                ExpeditionLayout.ChallengeCapacity, ExpeditionScreenView.ChallengePitch, out p.challengeScroll);
            p.challenges = new ExpeditionScreenParts.Challenge[ExpeditionLayout.ChallengeCapacity];
            for (int i = 0; i < p.challenges.Length; i++)
            {
                var c = new ExpeditionScreenParts.Challenge();
                var rootRect = UiPipeline.Rect("Challenge" + i.ToString("00"), p.challengeContent);
                UiPipeline.Place(rootRect, LeftMiddle, Center, new Vector2(ExpeditionScreenView.RowPadding + ExpeditionScreenView.ChallengePitch * (i + 0.5f), 0f), size);
                c.root = rootRect;
                c.back = borrow.Kit(Img("Back", rootRect, null, Card, Image.Type.Sliced, Center, Vector2.zero, size), KitPanel);
                c.button = ButtonOn(c.back);
                c.name = Label("Name", rootRect, TextRole.Label, 36f, Text, TextAlignmentOptions.Center, TopCenter, new Vector2(0f, -64f),
                    new Vector2(390f, LineBox(36f, 2)), 2, i == 0 ? ChallengeNames() : Array.Empty<string>());
                c.goal = Label("Goal", rootRect, TextRole.Caption, 36f, TextSoft, TextAlignmentOptions.Center, TopCenter, new Vector2(0f, -166f),
                    new Vector2(390f, LineBox(36f, 2)), 2, i == 0 ? ChallengeGoals() : Array.Empty<string>());
                c.stars = new Image[3];
                for (int k = 0; k < 3; k++)
                    c.stars[k] = Img("Star" + k, rootRect, null, StarOff, Image.Type.Simple, BottomCenter, new Vector2((k - 1) * 44f, 34f), new Vector2(38f, 38f));
                p.challenges[i] = c;
            }
        }

        // ---- the planning sheet (under the map card)
        private static void BuildSheet(ExpeditionScreenParts p, RectTransform safe, Art art, Borrow borrow)
        {
            var sheet = UiPipeline.Rect("Sheet", safe);
            sheet.anchorMin = Vector2.zero;
            sheet.anchorMax = Vector2.one;
            sheet.offsetMin = new Vector2(0f, -40f);
            sheet.offsetMax = new Vector2(0f, -SheetTop);
            Nested(sheet.gameObject, true);
            p.sheet = sheet;
            p.sheetGroup = sheet.gameObject.AddComponent<CanvasGroup>();
            p.sheetBack = borrow.Kit(UiPipeline.AddImage("Back", sheet, null, Card, Image.Type.Sliced), KitPanel);
            UiPipeline.Stretch(p.sheetBack.rectTransform);
            p.sheetBack.raycastTarget = true;
            p.sheetTitle = Label("Title", sheet, TextRole.Subheading, 46f, Text, TextAlignmentOptions.MidlineLeft, TopLeft, new Vector2(420f, -62f),
                new Vector2(720f, LineBox(46f, 1)), 1, T(ExpeditionText.PickTeam));
            p.sheetDestination = Label("Destination", sheet, TextRole.Label, 36f, TextSoft, TextAlignmentOptions.MidlineLeft, TopLeft, new Vector2(420f, -120f),
                new Vector2(720f, LineBox(36f, 1)), 1, Names());
            var closeBack = borrow.Kit(Img("Close", sheet, null, Cream, Image.Type.Simple, TopRight, new Vector2(-80f, -76f), new Vector2(104f, 104f)), KitCircle);
            p.sheetClose = ButtonOn(closeBack);
            p.sheetCloseIcon = borrow.Kit(Img("Icon", closeBack.transform, null, TextSoft, Image.Type.Simple, Center, Vector2.zero, new Vector2(48f, 48f)), KitClose);

            // The free animals' portraits in one row.
            p.animalContent = ScrollRow("Animals", sheet, TopCenter, new Vector2(0f, -262f), new Vector2(1000f, 196f), ExpeditionLayout.AnimalCapacity,
                ExpeditionScreenView.AnimalPitch, out p.animalScroll);
            p.animals = new ExpeditionScreenParts.Animal[ExpeditionLayout.AnimalCapacity];
            for (int i = 0; i < p.animals.Length; i++)
            {
                var a = new ExpeditionScreenParts.Animal();
                var cell = UiPipeline.Rect("Animal" + i.ToString("00"), p.animalContent);
                UiPipeline.Place(cell, LeftMiddle, Center, new Vector2(ExpeditionScreenView.RowPadding + ExpeditionScreenView.AnimalPitch * (i + 0.5f), 0f),
                    new Vector2(170f, 170f));
                a.root = cell;
                a.ring = borrow.Kit(Img("Ring", cell, null, Accent, Image.Type.Simple, Center, Vector2.zero, new Vector2(176f, 176f)), KitRing);
                a.ring.enabled = false;
                a.disc = borrow.Kit(Img("Disc", cell, null, Card, Image.Type.Simple, Center, Vector2.zero, new Vector2(150f, 150f)), KitCircle);
                a.button = ButtonOn(a.disc);
                a.portrait = Img("Portrait", cell, null, Color.white, Image.Type.Simple, Center, Vector2.zero, new Vector2(130f, 130f));
                a.portrait.preserveAspect = true;
                a.check = Img("Check", cell, null, Leaf, Image.Type.Simple, Center, new Vector2(56f, 56f), new Vector2(48f, 48f));
                a.check.enabled = false;
                a.leaf = Img("Leaf", cell, art.Leaf, Leaf, Image.Type.Simple, Center, new Vector2(-56f, -56f), new Vector2(44f, 44f));
                a.leaf.enabled = false;
                p.animals[i] = a;
            }

            // « Équipe conseillée » on the left; the rating's word and its stars on the right.
            p.suggestedBack = borrow.Kit(Img("Suggested", sheet, null, Cream, Image.Type.Sliced, TopLeft, new Vector2(310f, -428f), new Vector2(500f, 96f)), KitPill);
            p.suggested = ButtonOn(p.suggestedBack);
            p.suggestedLabel = Label("Label", p.suggestedBack.transform, TextRole.Label, 40f, Text, TextAlignmentOptions.Center, Center, new Vector2(0f, 2f),
                new Vector2(470f, LineBox(40f, 1)), 1, T(ExpeditionText.Suggested));
            p.ratingLabel = Label("Rating", sheet, TextRole.Caption, 36f, TextSoft, TextAlignmentOptions.Center, TopRight, new Vector2(-230f, -384f),
                new Vector2(400f, LineBox(36f, 1)), 1, T(ExpeditionText.Rating));
            p.ratingStars = new Image[3];
            for (int k = 0; k < 3; k++)
                p.ratingStars[k] = Img("Star" + k, sheet, null, StarOff, Image.Type.Simple, TopRight, new Vector2(-310f + k * 80f, -448f), new Vector2(64f, 64f));

            // The reasons: chips as wide as their words (the view lays them out, a second row when needed).
            var reasons = UiPipeline.Rect("Reasons", sheet);
            reasons.anchorMin = reasons.anchorMax = TopLeft;
            reasons.pivot = new Vector2(0f, 0.5f);
            reasons.anchoredPosition = new Vector2(60f, -526f);
            reasons.sizeDelta = new Vector2(960f, 64f);
            p.reasons = new ExpeditionScreenParts.Reason[3];
            var reasonIcons = new[] { art.Leaf, art.FamilyPaw, art.Dots };
            var reasonTexts = new[] { Filled(ExpeditionText.ReasonBiome, "3"), T(ExpeditionText.ReasonFamily), T(ExpeditionText.ReasonSpecies) };
            for (int k = 0; k < 3; k++)
            {
                var r = new ExpeditionScreenParts.Reason();
                var reasonRoot = UiPipeline.Rect("Reason" + k, reasons);
                reasonRoot.anchorMin = reasonRoot.anchorMax = LeftMiddle;
                reasonRoot.pivot = LeftMiddle;
                reasonRoot.anchoredPosition = new Vector2(k * 318f, 0f);
                reasonRoot.sizeDelta = new Vector2(300f, 64f);
                r.root = reasonRoot;
                var chip = borrow.Kit(UiPipeline.AddImage("Back", reasonRoot, null, Cream, Image.Type.Sliced), KitPill);
                UiPipeline.Stretch(chip.rectTransform);
                r.icon = Img("Icon", reasonRoot, reasonIcons[k], k == 0 ? Leaf : Color.white, Image.Type.Simple, LeftMiddle, new Vector2(36f, 0f), new Vector2(44f, 44f));
                r.label = Label("Label", reasonRoot, TextRole.Label, 36f, Text, TextAlignmentOptions.MidlineLeft, LeftMiddle, Vector2.zero,
                    new Vector2(ReasonMaxText, LineBox(36f, 1)), 1, reasonTexts[k]);
                r.label.rectTransform.pivot = LeftMiddle;
                r.label.rectTransform.anchoredPosition = new Vector2(ExpeditionScreenView.ReasonTextLeft, 1f);
                reasonRoot.gameObject.SetActive(false);
                p.reasons[k] = r;
            }

            // The durations and their chips (one wide chip for the first stroll).
            p.durationTitle = Label("DurationTitle", sheet, TextRole.Subheading, 44f, Text, TextAlignmentOptions.MidlineLeft, TopLeft, new Vector2(370f, -676f),
                new Vector2(620f, LineBox(44f, 1)), 1, T(ExpeditionText.Duration));
            var chips = UiPipeline.Rect("Durations", sheet);
            UiPipeline.Place(chips, TopCenter, Center, new Vector2(0f, -760f), new Vector2(960f, 100f));
            p.durations = new ExpeditionScreenParts.Chip[ExpeditionLayout.DurationCount];
            for (int i = 0; i < p.durations.Length; i++)
            {
                var c = new ExpeditionScreenParts.Chip();
                var chipRoot = UiPipeline.Rect("Chip" + i, chips);
                UiPipeline.Place(chipRoot, Center, Center, new Vector2((i - 1.5f) * 252f, 0f), new Vector2(240f, 100f));
                c.root = chipRoot;
                c.back = UiPipeline.AddImage("Back", chipRoot, art.Chip, art.Chip != null ? Color.white : Card, Image.Type.Sliced);
                UiPipeline.Stretch(c.back.rectTransform);
                if (art.Chip == null)
                    borrow.Kit(c.back, KitPill);
                c.button = ButtonOn(c.back);
                c.icon = Img("Clock", chipRoot, art.Durations[i], Color.white, Image.Type.Simple, LeftMiddle, new Vector2(46f, 0f), new Vector2(56f, 56f));
                c.icon.enabled = art.Durations[i] != null;
                c.label = UiPipeline.AddText("Label", chipRoot, TextRole.Label, 36f, Text, string.Empty);
                c.label.alignment = TextAlignmentOptions.Center;
                c.label.overflowMode = TextOverflowModes.Overflow;
                UiPipeline.Stretch(c.label.rectTransform);
                c.label.rectTransform.offsetMin = new Vector2(80f, 0f);
                c.label.rectTransform.offsetMax = new Vector2(-12f, 0f);
                // The usual chip (240 wide: « 12 h », the weekend's « 45 min »), and the first stroll's (700 wide).
                Fits.Add(new Fit { Label = c.label, Box = new Vector2(240f - 92f, 100f), Lines = 1, Texts = new[] { "12" + Nbsp + "h", "45" + Nbsp + "min", "12 h", "45 min" } });
                if (i == 0)
                    Fits.Add(new Fit { Label = c.label, Box = new Vector2(ExpeditionScreenView.FirstStrollChip - 92f, 100f), Lines = 1, Texts = T(ExpeditionText.FirstStroll) });
                p.durations[i] = c;
            }

            // The loot preview: each icon with its value under it.
            p.lootTitle = Label("LootTitle", sheet, TextRole.Caption, 36f, TextSoft, TextAlignmentOptions.MidlineLeft, TopLeft, new Vector2(510f, -852f),
                new Vector2(900f, LineBox(36f, 1)), 1, T(ExpeditionText.Loot));
            var loot = UiPipeline.Rect("Loot", sheet);
            UiPipeline.Place(loot, TopCenter, Center, new Vector2(0f, -950f), new Vector2(1040f, 124f));
            p.loot = new ExpeditionScreenParts.LootItem[ExpeditionLayout.LootCapacity];
            for (int i = 0; i < p.loot.Length; i++)
            {
                var item = new ExpeditionScreenParts.LootItem();
                var itemRoot = UiPipeline.Rect("Item" + i, loot);
                UiPipeline.Place(itemRoot, Center, Center, new Vector2((i - 3f) * ExpeditionScreenView.LootPitch, 0f), new Vector2(ExpeditionScreenView.LootPitch, 124f));
                item.root = itemRoot;
                item.icon = Img("Icon", itemRoot, null, Color.white, Image.Type.Simple, Center, new Vector2(0f, 30f), new Vector2(60f, 60f));
                item.amount = Label("Amount", itemRoot, TextRole.NumberSmall, 36f, Text, TextAlignmentOptions.Center, Center, new Vector2(0f, -32f),
                    new Vector2(ExpeditionScreenView.LootPitch - 4f, LineBox(36f, 1)), 1, i == 0
                        ? new[] { "59" + Nbsp + "min", "2" + Nbsp + "h" + Nbsp + "48", "2 h 48", "60" + Nbsp + "%", "100%", "×12" }
                        : Array.Empty<string>());
                itemRoot.gameObject.SetActive(false);
                p.loot[i] = item;
            }
            p.note = Label("Note", sheet, TextRole.Caption, 36f, TextSoft, TextAlignmentOptions.Center, TopCenter, new Vector2(0f, -1072f),
                new Vector2(960f, LineBox(36f, 2)), 2, Filled(ExpeditionText.DiscoveryGuarantee, "12"), ChallengeGoals());

            // « Partir » at the thumb.
            p.goBack = borrow.Kit(Img("Go", sheet, null, Accent, Image.Type.Sliced, BottomCenter, new Vector2(0f, 120f), new Vector2(620f, 130f)), KitButton);
            p.go = ButtonOn(p.goBack);
            p.goLabel = Label("Label", p.goBack.transform, TextRole.Button, 52f, Text, TextAlignmentOptions.Center, Center, new Vector2(0f, 3f),
                new Vector2(580f, LineBox(52f, 1)), 1, T(ExpeditionText.Go));
            sheet.gameObject.SetActive(false);
        }

        // ---- the welcome: the dim and the postcards
        private static void BuildWelcome(ExpeditionScreenParts p, RectTransform content, Art art, Borrow borrow)
        {
            var layer = UiPipeline.Rect("Welcome", content);
            UiPipeline.Stretch(layer);
            Nested(layer.gameObject, false);
            p.dim = UiPipeline.AddImage("Dim", layer, null, new Color(0.11f, 0.13f, 0.12f, 0f), Image.Type.Simple);
            p.dim.preserveAspect = false;
            UiPipeline.Stretch(p.dim.rectTransform);
            p.dim.enabled = false;
            var cards = UiPipeline.Rect("Postcards", layer);
            UiPipeline.Stretch(cards);
            p.postcardLayer = cards;
            p.postcards = new ExpeditionScreenParts.Postcard[ExpeditionLayout.PostcardCapacity];
            var frameSize = PostcardFrameSize;
            var names = Names();
            for (int i = 0; i < p.postcards.Length; i++)
            {
                var c = new ExpeditionScreenParts.Postcard();
                var cardRoot = UiPipeline.Rect("Postcard" + i, cards);
                UiPipeline.Place(cardRoot, Center, Center, new Vector2(0f, 60f), frameSize);
                c.root = cardRoot;
                c.group = cardRoot.gameObject.AddComponent<CanvasGroup>();
                c.group.blocksRaycasts = false;
                c.group.interactable = false;
                // The soft ring spreads behind the card; the 6 round petals drift out around it.
                c.ring = borrow.Kit(Img("Ring", cardRoot, null, new Color(1f, 0.96f, 0.86f, 0f), Image.Type.Simple, Center, Vector2.zero, new Vector2(760f, 760f)),
                    KitRing);
                c.ring.enabled = false;
                c.petals = new Image[ExpeditionLayout.Petals];
                for (int k = 0; k < c.petals.Length; k++)
                {
                    c.petals[k] = Img("Petal" + k, cardRoot, art.Petal, Petal, Image.Type.Simple, Center, Vector2.zero, new Vector2(44f, 44f));
                    if (art.Petal == null)
                        borrow.Kit(c.petals[k], KitDot);
                    c.petals[k].enabled = false;
                }
                var card = UiPipeline.Rect("Card", cardRoot);
                UiPipeline.Stretch(card);
                c.card = card;
                var shadow = Img("Shadow", card, art.PostcardFrame, Shadow, art.PostcardFrame != null ? Image.Type.Tiled : Image.Type.Sliced, Center,
                    new Vector2(6f, -12f), frameSize);
                shadow.preserveAspect = false;
                if (art.PostcardFrame == null)
                    borrow.Kit(shadow, KitPanel);
                c.frame = Img("Frame", card, art.PostcardFrame, Color.white, art.PostcardFrame != null ? Image.Type.Tiled : Image.Type.Sliced, Center,
                    Vector2.zero, frameSize);
                c.frame.preserveAspect = false;
                if (art.PostcardFrame == null)
                    borrow.Kit(c.frame, KitPanel);
                // The 512 × 768 picture in the deckled frame's window, 40 px in.
                var window = new Vector2(PostcardLayout.Width, PostcardLayout.Height);
                c.placeholder = Img("Placeholder", card, null, Cream, Image.Type.Simple, Center, Vector2.zero, window);
                c.placeholder.preserveAspect = false;
                var picture = UiPipeline.Rect("Picture", card);
                UiPipeline.Place(picture, Center, Center, Vector2.zero, window);
                c.picture = picture.gameObject.AddComponent<RawImage>();
                c.picture.raycastTarget = false;
                c.picture.color = Color.white;
                c.picture.uvRect = new Rect(0f, 0f, 1f, 1f);
                c.picture.enabled = false;
                c.stamp = Img("Stamp", card, art.Stamp, Color.white, Image.Type.Simple, Center, new Vector2(181f, 309f), new Vector2(128f, 128f));
                c.stamp.enabled = art.Stamp != null;
                c.postmark = Img("Postmark", card, art.Postmark, Color.white, Image.Type.Simple, Center, new Vector2(122f, 282f), new Vector2(150f, 150f));
                c.postmark.enabled = art.Postmark != null;
                var plate = borrow.Kit(Img("Plate", card, null, Card, Image.Type.Sliced, Center, new Vector2(0f, -490f), new Vector2(600f, 112f)), KitPill);
                c.destination = Label("Destination", plate.transform, TextRole.Label, 36f, Text, TextAlignmentOptions.Center, Center, new Vector2(0f, 2f),
                    new Vector2(570f, LineBox(36f, 2)), 2, i == 0 ? names : Array.Empty<string>());
                cardRoot.gameObject.SetActive(false);
                p.postcards[i] = c;
            }
        }

        // ---- the note pill and the tutorial paw
        private static void BuildNoteAndPaw(ExpeditionScreenParts p, RectTransform content, Borrow borrow)
        {
            var note = UiPipeline.Rect("Note", content);
            UiPipeline.Place(note, BottomCenter, Center, new Vector2(0f, 440f), new Vector2(900f, 128f));
            Nested(note.gameObject, false);
            p.notePill = note;
            p.noteGroup = note.gameObject.AddComponent<CanvasGroup>();
            p.noteGroup.blocksRaycasts = false;
            p.noteGroup.interactable = false;
            var back = borrow.Kit(UiPipeline.AddImage("Back", note, null, new Color(0.29f, 0.23f, 0.17f, 0.88f), Image.Type.Sliced), KitPill);
            UiPipeline.Stretch(back.rectTransform);
            p.noteLabel = Label("Label", note, TextRole.Body, 38f, Color.white, TextAlignmentOptions.Center, Center, new Vector2(0f, 2f),
                new Vector2(850f, LineBox(38f, 2)), 2,
                T(ExpeditionText.NeedGems, ExpeditionText.ClubSoon, ExpeditionText.NoSlot, ExpeditionText.DestinationLocked, ExpeditionText.AnimalAway,
                    ExpeditionText.TeamFull, ExpeditionText.NoAnimal, ExpeditionText.SlotConfirm, ExpeditionText.SpeedUpsNone), ChallengeGoals());
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

        /// <summary>
        /// Group Activity_Expeditions (shared with agent EP's postcard sets): the screen and its atlas, loaded when the screen
        /// opens. The portraits, the kit, the icons and the fonts stay out (borrowed from the scene).
        /// </summary>
        public static string EnsureAddressables(GameObject prefab, SpriteAtlasAsset atlas)
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var group = EnsureGroup(settings, ExpeditionLayout.Group);
            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab)), group, false, false);
            entry.address = ExpeditionLayout.ScreenAddress;
            string atlasGuid = AssetDatabase.AssetPathToGUID(AtlasPath);
            string result = $"Addressables {ExpeditionLayout.Group}: {ExpeditionLayout.ScreenAddress}";
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
