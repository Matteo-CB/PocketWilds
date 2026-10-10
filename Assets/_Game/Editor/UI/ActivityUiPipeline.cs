using System;
using System.Collections.Generic;
using System.IO;
using PocketWilds.Content;
using PocketWilds.Editor.Import;
using PocketWilds.Presentation;
using PocketWilds.Simulation;
using PocketWilds.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.UI
{
    /// <summary>
    /// T37 activity screens build (doc « Mini-jeux & activités » sections 1 and 2; doc 01 Contenu sections 0, A15 and 12.0;
    /// doc 06 screens 029 and 030; doc 05 Sensations sheet 20): PF_UI_ActivityHub (bottom sheet: ticket chip, carousel of
    /// 8 recycled cards, theme pills, 10 challenges, practice toggle, « Jouer »), PF_UI_ActivityHud (score and its star
    /// marks, the soft time ring, streak, pause card), PF_UI_ActivityResult (stars, score, record, reward cards that turn
    /// over) and PF_UI_TicketsEmpty (the « no ticket » card), Atlas_Activities (the activity icons ICO_Act_&lt;Name&gt; and
    /// the screens' own icons, kept out of the full Atlas_CommonHUD), and the wiring of the vertical slice's entry. Every
    /// sprite is made in house (Art/UI, T36 kit and the T37 icons); every label uses a DEF_TextStyles role, sized so the
    /// longest English or French text plus 30 % fits at the role minimum; texts come from the localization tables
    /// (ActivityText keys). Layout targets the 1080 x 2340 portrait reference; touch targets are 124 units (48 dp) or more.
    /// Rerunnable. Sorting orders: HUD 20, hub 22 (Today's level: they never show together), result 30, « no ticket » 31.
    /// The HUD's Play button is built by <see cref="UiPipeline.BuildHudPrefab"/>.
    /// T38: PF_UI_ActivityHud also carries what an activity's stage shows over its game (the tip bubble, the tutorial paw,
    /// the round choices and the boost chip), and Atlas_Activities the T38 sprites of agent GI when drawn.
    /// T39: the HUD's item buttons (Hide and Seek's magnifier and whistle, agent HI's icons packed in Atlas_Activities and
    /// lent to the stage) and its night (one full screen Image with MAT_UI_Spotlight, PocketWilds/UISpotlight).
    /// </summary>
    public static class ActivityUiPipeline
    {
        public const string Folder = ProgressionUiPipeline.Folder;
        public const string HubPath = Folder + "PF_UI_ActivityHub.prefab";
        public const string HudPath = Folder + "PF_UI_ActivityHud.prefab";
        public const string ResultPath = Folder + "PF_UI_ActivityResult.prefab";
        public const string TicketsEmptyPath = Folder + "PF_UI_TicketsEmpty.prefab";
        public const string AtlasPath = TextureImportRules.UiRoot + "Atlases/Atlas_Activities.spriteatlasv2";
        public const string DiscoveryEggPath = "Assets/_Game/Content/Eggs/DEF_Egg_Discovery.asset";

        public const int HudSortingOrder = 20;
        public const int HubSortingOrder = 22;
        public const int ResultSortingOrder = 30;
        public const int TicketsEmptySortingOrder = 31;

        /// <summary>The activity icons (agent I, T37): ICO_Act_ and the ActivityId name.</summary>
        public const string ActivityIconPrefix = "ICO_Act_";

        /// <summary>
        /// Sprites used only by the activity screens (agent I, T37), packed in Atlas_Activities: the framework icons
        /// (Art/UI/Icons) and the two result stars (Art/UI/Kit, 192 px, same frame). ICO_Play stays in Atlas_CommonHUD (HUD).
        /// T38 (agent GI): the three bow choices and the golden brush (Icons); each is packed when drawn, nothing breaks while
        /// one is missing. The swipe paw UI_Gesture_Swipe stays in Atlas_CommonHUD next to UI_Gesture_Tap.
        /// T39 (agent HI): Hide and Seek's magnifier and whistle (Icons), packed when drawn.
        /// T40 (agent NA): the nut drop's three boost icons, the wide basket, the feather and the golden shower, and the
        /// result card's own reward icons (<see cref="RewardItemIcons"/>; Atlas_CommonHUD is full) (Icons).
        /// </summary>
        public static readonly string[] ScreenOnlyIcons =
        {
            "ICO_Practice", "ICO_Record", "ICO_Challenge", "ICO_Pause", "ICO_Timer", "ICO_Lock", "UI_Star_Result_Empty", "UI_Star_Result_Full",
            "ICO_Bow_Pink", "ICO_Bow_SkyBlue", "ICO_Bow_Cream", "ICO_GoldenBrush", ActivityText.IconMagnifier, ActivityText.IconWhistle,
            "ICO_WideBasket", "ICO_Feather", "ICO_GoldenShower",
            "ICO_Treat_MushroomBiscuit", "ICO_Treat_ToastedHazelnut", "ICO_Ingredient_Hazelnut", "ICO_Ingredient_Strawberry",
        };

        /// <summary>
        /// T40: the reward items with their own icon on the result card (RewardIconSet.items), beside T38's Clover Crunch:
        /// the 3-star treats of T39 (Mushroom Biscuit) and T40 (Toasted Hazelnut), and Nut Drop's ingredients. A missing
        /// sprite shows its kind's icon.
        /// </summary>
        public static readonly string[,] RewardItemIcons =
        {
            { "treat.mushroom_biscuit", "ICO_Treat_MushroomBiscuit" }, { "treat.toasted_hazelnut", "ICO_Treat_ToastedHazelnut" },
            { "ingredient.hazelnut", "ICO_Ingredient_Hazelnut" }, { "ingredient.strawberry", "ICO_Ingredient_Strawberry" },
        };

        /// <summary>
        /// The icons PF_UI_ActivityHud lends to the stages by name (IActivityHudView.Icon), so their bundles never copy the UI
        /// atlas: T38's bows and golden brush, T39's magnifier and whistle, T40's three boost icons. A missing one is left out
        /// (the stage falls back).
        /// </summary>
        public static readonly string[] LentIcons =
        {
            "ICO_Bow_Pink", "ICO_Bow_SkyBlue", "ICO_Bow_Cream", "ICO_GoldenBrush", ActivityText.IconMagnifier, ActivityText.IconWhistle,
            "ICO_WideBasket", "ICO_Feather", "ICO_GoldenShower",
        };

        /// <summary>T39 night (Hide and Seek challenge 7): the handmade UI shader and its material.</summary>
        public const string SpotlightShaderPath = "Assets/_Game/Art/Shaders/SH_UISpotlight.shader";

        public const string SpotlightMaterialPath = "Assets/_Game/Art/Materials/MAT_UI_Spotlight.mat";

        /// <summary>The night's look: a deep blue-violet (never black), 76 % opaque by the circle, 86 % far from it, a soft edge of 7 % of the width.</summary>
        public static readonly Color Night = UiColorTokens.Hex(0x211F47);

        public const float NightNear = 0.76f;
        public const float NightFar = 0.86f;
        public const float NightFeather = 0.07f;
        public const float NightReach = 0.8f;

        /// <summary>T38 HUD parts: the picker's choices (diameter, at least 150), their pitch and the height of their row's center.</summary>
        public const float PickerSize = 176f;
        public const float PickerPitch = 216f;
        public const float PickerY = 300f;

        /// <summary>The result stars' visual center in their frame: a pop centered on the star.</summary>
        public static readonly Vector2 ResultStarPivot = new Vector2(0.5f, 0.484f);

        // Hub layout (1080 x 2340 reference).
        public const float CardWidth = 340f;
        public const float CardHeight = 440f;
        public const float CardPitch = 364f;
        public const float CardPad = 64f;
        public const float HeaderHeight = 196f;
        /// <summary>Room for a lifted, outlined card and the tag on its top edge.</summary>
        public const float CarouselHeight = 530f;
        public const float FooterHeight = 338f;
        public const float NameRowHeight = 120f;
        public const float ThemesHeight = 112f;
        public const float ChallengesHeight = 460f;
        public const float PillWidth = 344f;
        public const float PillPitch = 360f;
        public const float KnobTravel = 22f;
        public const float ResultCardHeight = 1280f;
        public const float ResultChallengeSpace = 120f;
        public const float RewardPitch = 140f;

        // Warm tints (doc 05 palette; tokens for text and actions).
        private static readonly Color Warm = UiColorTokens.Hex(0xFFF1DA);
        private static readonly Color Gold = UiColorTokens.Hex(0xF2B544);
        private static readonly Color GoldGlow = new Color(1f, 0.83f, 0.42f, 0.6f);
        private static readonly Color PracticeOn = UiColorTokens.Hex(0xDEEFC8);
        private static readonly Color MarkOff = UiColorTokens.Hex(0xE3DCCF);
        private static readonly Color RingSoft = UiColorTokens.Hex(0xF6C98A);
        private static readonly Color SoftShadow = new Color(0.42f, 0.30f, 0.20f, 0.18f);
        private static readonly Color PickerShadow = new Color(0.36f, 0.24f, 0.14f, 0.32f);
        private static readonly Color TrailTint = new Color(0.965f, 0.788f, 0.541f, 0.6f);

        /// <summary>The biome themes the hub names (doc 01 section 12.0), with the soft color of their dot.</summary>
        private static readonly (string id, uint tint)[] Themes =
        {
            ("biome.grassland", 0x9BCB6B), ("biome.forest", 0x6FA36A), ("biome.lake", 0x7DBDE0), ("biome.mountain", 0xA9B6C6),
            ("biome.snow", 0xCFE4F2), ("biome.jungle", 0x5FB27C), ("biome.desert", 0xE8C489), ("biome.sakura", 0xF2B7C9),
            ("biome.mushroom", 0xD49A7A), ("biome.cloudlands", 0xC9DDF5), ("biome.lunar", 0xA7A6DA), ("biome.prehistoric", 0xC2A57A),
            ("biome.starlight", 0x8587C4),
        };

        public static bool IsAtlasSprite(string name) =>
            name != null && (name.StartsWith(ActivityIconPrefix, StringComparison.Ordinal) || Array.IndexOf(ScreenOnlyIcons, name) >= 0);

        private static string Icon(string name) => UiPipeline.IconFolder + "/" + name + ".png";
        private static string Kit(string name) => UiPipeline.KitFolder + "/" + name + ".png";

        private static Sprite Load(string path) =>
            AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? throw new FileNotFoundException("Run the UI sprite scripts (SourceArt/UI/Tools) first.", path);

        private static Sprite Optional(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        [MenuItem("Pocket Wilds/UI/Build Activities (T37)", priority = 206)]
        public static void BuildMenu() => Debug.Log("[ActivityUiPipeline] " + BuildAll());

        /// <summary>Standalone: tokens, texts, the HUD (its Play button), the four screens, the atlases and the wiring.</summary>
        public static string BuildAll()
        {
            UiPipeline.ReimportSprites();
            var tokens = UiPipeline.EnsureTokens();
            UiPipeline.EnsureLocalization();
            // The HUD with its Play button; the scene's HUD and the wish card's field are wired again after the rebuild.
            UiPipeline.BuildHudPrefab(tokens);
            AssetDatabase.SaveAssets();
            UiPipeline.SetupScene();
            return Extend(tokens);
        }

        /// <summary>
        /// The T37 part of a full UI build (UiPipeline.BuildAll calls it at its end, after the HUD with its Play button):
        /// Atlas_Activities, the four prefabs, the wiring.
        /// </summary>
        public static string Extend(UiColorTokens tokens)
        {
            ImportSprites();
            EnsureAtlases();
            var kit = new KitSprites(tokens);
            BuildHubPrefab(kit);
            BuildHudPrefab(kit);
            BuildResultPrefab(kit);
            BuildTicketsEmptyPrefab(kit);
            AssetDatabase.SaveAssets();
            WireScene();
            return "PF_UI_ActivityHub, PF_UI_ActivityHud, PF_UI_ActivityResult, PF_UI_TicketsEmpty and Atlas_Activities built and wired ("
                   + kit.ActivityIconsFound + "/18 activity icons)";
        }

        public static void ImportSprites()
        {
            foreach (var path in new[] { Icon("ICO_Play"), Icon("ICO_Ticket"), Icon("ICO_Paw"), Kit("UI_Ring_Soft"), Kit("UI_Glow_Soft"), Kit("UI_Gesture_Swipe") })
            {
                if (File.Exists(path))
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            foreach (var path in AtlasSpritePaths())
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        /// <summary>The Kit and Icons textures of Atlas_Activities (ICO_Act_* and <see cref="ScreenOnlyIcons"/>).</summary>
        private static IEnumerable<string> AtlasSpritePaths()
        {
            foreach (var folder in new[] { UiPipeline.KitFolder, UiPipeline.IconFolder })
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (Path.GetDirectoryName(path)?.Replace('\\', '/') != folder)
                        continue;
                    if (IsAtlasSprite(Path.GetFileNameWithoutExtension(path)))
                        yield return path;
                }
            }
        }

        // ---------------- Atlases ----------------

        /// <summary>
        /// Atlas_Activities packs the activity icons and the screens' own icons (doc 06: separate atlases for activities);
        /// Atlas_CommonHUD keeps every other Kit and Icons sprite (Today's art stays in Atlas_Today), so nothing is packed twice.
        /// </summary>
        public static void EnsureAtlases()
        {
            if (EditorSettings.spritePackerMode == SpritePackerMode.Disabled)
                EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;
            var own = new List<Object>();
            foreach (var path in AtlasSpritePaths())
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture != null)
                    own.Add(texture);
            }
            if (own.Count > 0)
                WriteAtlas(AtlasPath, own.ToArray());
            var common = new List<Object>();
            foreach (var texture in DailyVisitUiPipeline.CommonAtlasPackables())
            {
                if (!IsAtlasSprite(texture.name))
                    common.Add(texture);
            }
            WriteAtlas(UiPipeline.AtlasPath, common.ToArray());
        }

        private static void WriteAtlas(string path, Object[] packables)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var atlas = new SpriteAtlasAsset();
            atlas.Add(packables);
            SpriteAtlasAsset.Save(atlas, path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(path);
            importer.includeInBuild = true;
            var packing = importer.packingSettings;
            packing.enableRotation = false;
            packing.enableTightPacking = false;
            packing.padding = 4;
            importer.packingSettings = packing;
            var texture = importer.textureSettings;
            texture.generateMipMaps = false;
            texture.filterMode = FilterMode.Bilinear;
            texture.sRGB = true;
            importer.textureSettings = texture;
            foreach (var platform in new[] { "Android", "iPhone" })
            {
                var settings = importer.GetPlatformSettings(platform);
                settings.overridden = true;
                settings.maxTextureSize = 1024;
                settings.format = TextureImporterFormat.ASTC_4x4;
                importer.SetPlatformSettings(settings);
            }
            importer.SaveAndReimport();
        }

        // ---------------- Kit ----------------

        private sealed class KitSprites
        {
            public readonly UiColorTokens Tokens;
            public readonly Sprite Panel = Load(UiPipeline.PanelPath);
            public readonly Sprite Pill = Load(UiPipeline.PillPath);
            public readonly Sprite Circle = Load(UiPipeline.CirclePath);
            public readonly Sprite Button = Load(UiPipeline.ButtonPrimaryPath);
            public readonly Sprite Glow = Load(UiPipeline.GlowPath);
            public readonly Sprite Dot = Load(UiPipeline.DotPath);
            public readonly Sprite Star = Load(UiPipeline.StarPath);
            public readonly Sprite Close = Load(UiPipeline.CloseIconPath);
            public readonly Sprite Gems = Load(UiPipeline.GemsIconPath);
            public readonly Sprite Ring = Load(Kit("UI_Ring_Soft"));
            public readonly Sprite Ticket = Load(Icon("ICO_Ticket"));
            public readonly Sprite Paw = Load(Icon("ICO_Paw"));
            public readonly Sprite Heart = Load(Icon("ICO_Favorite_Heart"));
            public readonly Sprite Spots = Load(Kit("UI_Egg_Spots"));
            public readonly Sprite Play = UiPipeline.PlayIcon();
            public readonly Sprite Practice = Optional(Icon("ICO_Practice"));
            public readonly Sprite Challenge = Optional(Icon("ICO_Challenge"));
            public readonly Sprite Record = Optional(Icon("ICO_Record"));
            public readonly Sprite Lock = Optional(Icon("ICO_Lock"));
            public readonly Sprite Pause = Optional(Icon("ICO_Pause"));
            public readonly Sprite StarEmpty = Optional(Kit("UI_Star_Result_Empty"));
            public readonly Sprite StarFull = Optional(Kit("UI_Star_Result_Full"));
            // T38 (agent GI): the tutorial paws and the golden brush; the swipe falls back to the tap paw while undrawn.
            public readonly Sprite GestureTap = Load(Kit("UI_Gesture_Tap"));
            public readonly Sprite GestureSwipe = Optional(Kit("UI_Gesture_Swipe"));
            public readonly Sprite GoldenBrush = Optional(Icon("ICO_GoldenBrush"));
            // T39: the used item's small round check (Atlas_CommonHUD).
            public readonly Sprite Check = Optional(Icon("ICO_Check"));
            public readonly Sprite[] ActivityIcons;
            public readonly int ActivityIconsFound;

            public KitSprites(UiColorTokens tokens)
            {
                Tokens = tokens;
                // By ActivityId value (1 to 18); a missing icon falls back to ICO_Play at runtime.
                ActivityIcons = new Sprite[(int)ActivityId.GrandContest + 1];
                for (int i = 1; i < ActivityIcons.Length; i++)
                {
                    ActivityIcons[i] = Optional(Icon(ActivityText.IconName((ActivityId)i)));
                    if (ActivityIcons[i] != null)
                        ActivityIconsFound++;
                }
            }

            /// <summary>The reward sprites (doc 06 icon inventory), as the T36 screens show them; ICO_Sticker when drawn.</summary>
            public RewardIconSet Icons()
            {
                var egg = AssetDatabase.LoadAssetAtPath<EggDefinitionAsset>(DiscoveryEggPath);
                var sticker = Optional(Icon("ICO_Sticker"));
                var set = new RewardIconSet
                {
                    gems = Gems,
                    light = Load(UiPipeline.LightIconPath),
                    ticket = Ticket,
                    treat = Load(Icon("ICO_Treat")),
                    seed = Load(Icon("ICO_Seed")),
                    eggShell = Load(Kit("UI_Egg_Shell")),
                    eggSpots = Spots,
                    decoration = Load(Icon("ICO_Decoration")),
                    sticker = sticker != null ? sticker : Load(Icon("ICO_Gift")),
                    flowerSugar = Load(Icon("ICO_Treat")),
                    bond = Heart,
                    items = RewardItems(),
                    // By RewardKind value; T40 Ingredient (11): a soft apricot.
                    discTints = new[]
                    {
                        Tokens.panelSecondary, UiColorTokens.Hex(0xDCEAF6), UiColorTokens.Hex(0xFFF0C8), UiColorTokens.Hex(0xF6E2C4),
                        UiColorTokens.Hex(0xF9E0E4), UiColorTokens.Hex(0xDDEFC8), UiColorTokens.Hex(0xFFF4DE), UiColorTokens.Hex(0xF6E2C4),
                        UiColorTokens.Hex(0xF3EBDD), UiColorTokens.Hex(0xF9E0E4), UiColorTokens.Hex(0xF9E0E4), UiColorTokens.Hex(0xFBE8D4),
                    },
                };
                if (egg != null)
                {
                    set.eggShellColor = egg.shell;
                    set.eggSpotsColor = egg.spots;
                }
                return set;
            }

            /// <summary>T38's Clover Crunch, then <see cref="RewardItemIcons"/> (each one drawn: a missing one is left out).</summary>
            private static RewardIconSet.ItemIcon[] RewardItems()
            {
                var items = new List<RewardIconSet.ItemIcon>
                {
                    new RewardIconSet.ItemIcon { sourceId = "treat.clover_crunch", sprite = Load(Icon("ICO_Treat_CloverCrunch")) },
                };
                for (int i = 0; i < RewardItemIcons.GetLength(0); i++)
                {
                    var sprite = Optional(Icon(RewardItemIcons[i, 1]));
                    if (sprite != null)
                        items.Add(new RewardIconSet.ItemIcon { sourceId = RewardItemIcons[i, 0], sprite = sprite });
                }
                return items.ToArray();
            }

            public ActivityThemeSet ThemeSet()
            {
                var set = new ActivityThemeSet { entries = new ActivityThemeSet.Entry[Themes.Length], fallbackTint = Tokens.accentIdle };
                for (int i = 0; i < Themes.Length; i++)
                {
                    set.entries[i] = new ActivityThemeSet.Entry
                    {
                        sourceId = Themes[i].id, nameKey = Themes[i].id + ".name", tint = UiColorTokens.Hex(Themes[i].tint),
                    };
                }
                return set;
            }
        }

        // ---------------- PF_UI_ActivityHub (screen 029) ----------------

        public static ActivityHubScreen BuildHubPrefab(UiColorTokens tokens) => BuildHubPrefab(new KitSprites(tokens));

        private static ActivityHubScreen BuildHubPrefab(KitSprites kit)
        {
            var tokens = kit.Tokens;
            var center = new Vector2(0.5f, 0.5f);
            var root = new GameObject("PF_UI_ActivityHub", typeof(RectTransform));
            try
            {
                var canvas = Overlay(root, HubSortingOrder);
                var screen = root.AddComponent<ActivityHubScreen>();
                var content = Content(root, out var group);

                // A light dim above the sheet: a touch there closes the hub (one gesture).
                var outsideImage = UiPipeline.Rect("Outside", content).gameObject.AddComponent<Image>();
                UiPipeline.Stretch(outsideImage.rectTransform);
                var dim = tokens.darkOverlay;
                dim.a = 0.16f;
                outsideImage.color = dim;
                outsideImage.raycastTarget = true;
                outsideImage.maskable = false;
                var outside = PlainButton(outsideImage);

                var safe = UiPipeline.Rect("SafeArea", content);
                UiPipeline.Stretch(safe);
                var panel = UiPipeline.Rect("Panel", safe);
                panel.anchorMin = new Vector2(0f, 0f);
                panel.anchorMax = new Vector2(1f, 0f);
                panel.pivot = new Vector2(0.5f, 0f);
                panel.anchoredPosition = Vector2.zero;
                panel.sizeDelta = new Vector2(0f, 1600f);
                var back = UiPipeline.AddImage("Background", panel, kit.Panel, tokens.backgroundLight, Image.Type.Sliced);
                UiPipeline.Stretch(back.rectTransform);
                // The sheet runs under the gesture bar.
                back.rectTransform.offsetMin = new Vector2(0f, -260f);
                back.raycastTarget = true;
                var handle = UiPipeline.AddImage("Handle", panel, kit.Pill, tokens.panelSecondary, Image.Type.Sliced);
                handle.pixelsPerUnitMultiplier = 6f;
                UiPipeline.Place(handle.rectTransform, new Vector2(0.5f, 1f), center, new Vector2(0f, -24f), new Vector2(140f, 18f));

                // Header: the play icon and the title, the ticket chip (its « + » opens the packs), the round close.
                var headerIcon = UiPipeline.AddImage("Icon", panel, kit.Play, Color.white, Image.Type.Simple);
                UiPipeline.Place(headerIcon.rectTransform, new Vector2(0f, 1f), center, new Vector2(104f, -84f), new Vector2(88f, 88f));
                var title = UiPipeline.AddText("Title", panel, TextRole.Heading, 66f, tokens.textPrimary, "Activities", 56f);
                UiPipeline.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(160f, -84f), new Vector2(406f, 84f));
                var close = RoundClose(panel, kit, new Vector2(-82f, -84f));

                var chipBack = UiPipeline.AddImage("Tickets", panel, kit.Pill, tokens.panelPrimary, Image.Type.Sliced);
                chipBack.pixelsPerUnitMultiplier = 1.6f;
                chipBack.raycastTarget = true;
                UiPipeline.Place(chipBack.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-164f, -84f), new Vector2(340f, 112f));
                var chip = chipBack.gameObject.AddComponent<Button>();
                UiPipeline.ConfigureButton(chip, chipBack);
                var chipIcon = UiPipeline.AddImage("Icon", chipBack.rectTransform, kit.Ticket, Color.white, Image.Type.Simple);
                UiPipeline.Place(chipIcon.rectTransform, new Vector2(0f, 0.5f), center, new Vector2(50f, 2f), new Vector2(72f, 72f));
                var count = UiPipeline.AddText("Count", chipBack.rectTransform, TextRole.NumberSmall, 44f, tokens.textPrimary, "5/5");
                UiPipeline.Place(count.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(94f, 4f), new Vector2(156f, 60f));
                var plus = UiPipeline.AddImage("Plus", chipBack.rectTransform, kit.Circle, tokens.accentPrimary, Image.Type.Simple);
                UiPipeline.Place(plus.rectTransform, new Vector2(1f, 0.5f), center, new Vector2(-50f, 0f), new Vector2(76f, 76f));
                var plusLabel = UiPipeline.AddText("Label", plus.rectTransform, TextRole.NumberSmall, 52f, tokens.textPrimary, "+");
                plusLabel.alignment = TextAlignmentOptions.Center;
                UiPipeline.Place(plusLabel.rectTransform, center, center, new Vector2(0f, 4f), new Vector2(72f, 72f));
                // The real time to the next free ticket, under the chip; minutes only.
                var next = UiPipeline.AddText("Next", panel, TextRole.Caption, 38f, tokens.textSecondary, "Next ticket in 1 h 12 min", 36f);
                next.alignment = TextAlignmentOptions.MidlineRight;
                UiPipeline.Place(next.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-64f, -162f), new Vector2(952f, 48f));

                // The carousel: its own scroll view and canvas (scrolling rebuilds only the cards).
                var viewport = UiPipeline.Rect("Cards", panel);
                viewport.anchorMin = new Vector2(0f, 1f);
                viewport.anchorMax = new Vector2(1f, 1f);
                viewport.pivot = new Vector2(0.5f, 1f);
                viewport.anchoredPosition = new Vector2(0f, -HeaderHeight);
                viewport.sizeDelta = new Vector2(0f, CarouselHeight);
                viewport.gameObject.AddComponent<RectMask2D>();
                var drag = viewport.gameObject.AddComponent<Image>();
                drag.color = new Color(1f, 1f, 1f, 0f);
                drag.raycastTarget = true;
                var list = UiPipeline.Rect("List", viewport);
                list.anchorMin = new Vector2(0f, 0f);
                list.anchorMax = new Vector2(0f, 1f);
                list.pivot = new Vector2(0f, 0.5f);
                list.anchoredPosition = Vector2.zero;
                list.sizeDelta = new Vector2(CardPad * 2f + 17f * CardPitch + CardWidth, 0f);
                list.gameObject.AddComponent<Canvas>();
                list.gameObject.AddComponent<GraphicRaycaster>();
                var cardScroll = viewport.gameObject.AddComponent<ScrollRect>();
                Horizontal(cardScroll, viewport, list);
                var cards = new ActivityCardView[ActivityHubScreen.CardPool];
                for (int i = 0; i < cards.Length; i++)
                    cards[i] = BuildCard(list, i, kit);
                Maskable(list.gameObject);

                // The selected activity.
                var details = UiPipeline.Rect("Details", panel);
                details.anchorMin = new Vector2(0f, 1f);
                details.anchorMax = new Vector2(1f, 1f);
                details.pivot = new Vector2(0.5f, 1f);
                details.anchoredPosition = new Vector2(0f, -(HeaderHeight + CarouselHeight));
                details.sizeDelta = new Vector2(0f, NameRowHeight + ThemesHeight + ChallengesHeight);
                var name = UiPipeline.AddText("Name", details, TextRole.Subheading, 54f, tokens.textPrimary, "Grooming", 44f);
                UiPipeline.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(64f, -40f), new Vector2(952f, 70f));
                var best = UiPipeline.AddText("Best", details, TextRole.Caption, 40f, tokens.textSecondary, "Best 520", 36f);
                UiPipeline.Place(best.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(64f, -92f), new Vector2(600f, 52f));

                var themesRow = UiPipeline.Rect("Themes", details);
                themesRow.anchorMin = new Vector2(0f, 1f);
                themesRow.anchorMax = new Vector2(1f, 1f);
                themesRow.pivot = new Vector2(0.5f, 1f);
                themesRow.anchoredPosition = new Vector2(0f, -NameRowHeight);
                themesRow.sizeDelta = new Vector2(0f, ThemesHeight);
                themesRow.gameObject.AddComponent<RectMask2D>();
                var themesDrag = themesRow.gameObject.AddComponent<Image>();
                themesDrag.color = new Color(1f, 1f, 1f, 0f);
                themesDrag.raycastTarget = true;
                var themeList = UiPipeline.Rect("Pills", themesRow);
                themeList.anchorMin = new Vector2(0f, 0f);
                themeList.anchorMax = new Vector2(0f, 1f);
                themeList.pivot = new Vector2(0f, 0.5f);
                themeList.anchoredPosition = Vector2.zero;
                themeList.sizeDelta = new Vector2(CardPad * 2f + ActivityHubScreen.MaxThemes * PillPitch, 0f);
                themeList.gameObject.AddComponent<Canvas>();
                themeList.gameObject.AddComponent<GraphicRaycaster>();
                var themeScroll = themesRow.gameObject.AddComponent<ScrollRect>();
                Horizontal(themeScroll, themesRow, themeList);
                var pills = new ActivityThemePillView[ActivityHubScreen.MaxThemes];
                for (int i = 0; i < pills.Length; i++)
                    pills[i] = BuildPill(themeList, i, kit);
                Maskable(themeList.gameObject);
                themesRow.gameObject.SetActive(false);

                var challenges = UiPipeline.Rect("Challenges", details);
                challenges.anchorMin = new Vector2(0f, 1f);
                challenges.anchorMax = new Vector2(1f, 1f);
                challenges.pivot = new Vector2(0.5f, 1f);
                challenges.anchoredPosition = new Vector2(0f, -NameRowHeight);
                challenges.sizeDelta = new Vector2(0f, ChallengesHeight);
                var challengesIcon = UiPipeline.AddImage("Icon", challenges, kit.Challenge != null ? kit.Challenge : kit.Star,
                    kit.Challenge != null ? Color.white : Gold, Image.Type.Simple);
                UiPipeline.Place(challengesIcon.rectTransform, new Vector2(0f, 1f), center, new Vector2(92f, -34f), new Vector2(56f, 56f));
                var challengesTitle = UiPipeline.AddText("Title", challenges, TextRole.Subheading, 46f, tokens.textPrimary, "Challenges", 44f);
                UiPipeline.Place(challengesTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(132f, -34f), new Vector2(500f, 60f));
                var challengesCount = UiPipeline.AddText("Count", challenges, TextRole.NumberSmall, 40f, tokens.textSecondary, "0/10");
                challengesCount.alignment = TextAlignmentOptions.MidlineRight;
                UiPipeline.Place(challengesCount.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-64f, -34f), new Vector2(200f, 56f));
                var cells = new ActivityChallengeCellView[ActivityHubScreen.MaxChallenges];
                for (int i = 0; i < cells.Length; i++)
                {
                    int row = i / 5;
                    int column = i % 5;
                    cells[i] = BuildCell(challenges, i, new Vector2(CardPad + 86f + column * 190f, -(72f + 68f + row * 150f)), kit);
                }
                var goal = UiPipeline.AddText("Goal", challenges, TextRole.Body, 40f, tokens.textSecondary, "Tap a challenge to pick it", 38f);
                goal.alignment = TextAlignmentOptions.Center;
                goal.textWrappingMode = TextWrappingModes.Normal;
                goal.overflowMode = TextOverflowModes.Ellipsis;
                goal.lineSpacing = -10f;
                UiPipeline.Place(goal.rectTransform, new Vector2(0.5f, 1f), center, new Vector2(0f, -414f), new Vector2(952f, 88f));

                // Footer: the practice toggle (free, no reward, said on the row) and the big « Jouer » at the thumb.
                var practiceBack = UiPipeline.AddImage("Practice", panel, kit.Pill, Color.white, Image.Type.Sliced);
                practiceBack.pixelsPerUnitMultiplier = 1.6f;
                practiceBack.raycastTarget = true;
                UiPipeline.Place(practiceBack.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 34f + 150f + 18f),
                    new Vector2(952f, 112f));
                var practice = practiceBack.gameObject.AddComponent<Button>();
                UiPipeline.ConfigureButton(practice, practiceBack);
                var track = UiPipeline.AddImage("Track", practiceBack.rectTransform, kit.Pill, tokens.accentIdle, Image.Type.Sliced);
                track.pixelsPerUnitMultiplier = 3f;
                UiPipeline.Place(track.rectTransform, new Vector2(0f, 0.5f), center, new Vector2(82f, 0f), new Vector2(104f, 60f));
                var knob = UiPipeline.AddImage("Knob", track.rectTransform, kit.Circle, Color.white, Image.Type.Simple);
                UiPipeline.Place(knob.rectTransform, center, center, new Vector2(-KnobTravel, 0f), new Vector2(52f, 52f));
                var practiceLabel = UiPipeline.AddText("Label", practiceBack.rectTransform, TextRole.Label, 42f, tokens.textPrimary, "Practice", 36f);
                UiPipeline.Place(practiceLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(152f, 2f), new Vector2(380f, 56f));
                var practiceLine = UiPipeline.AddText("Line", practiceBack.rectTransform, TextRole.Caption, 36f, tokens.textSecondary, "Free, no rewards");
                practiceLine.alignment = TextAlignmentOptions.MidlineRight;
                practiceLine.textWrappingMode = TextWrappingModes.Normal;
                practiceLine.lineSpacing = -10f;
                UiPipeline.Place(practiceLine.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-28f, 0f), new Vector2(380f, 96f));

                var play = ProgressionUiPipeline.PrimaryButton(panel, tokens, "Play", new Vector2(0f, 34f), new Vector2(952f, 150f), out var playBack,
                    out var playLabel);
                playLabel.fontSizeMax = 60f;
                playLabel.fontSize = 60f;
                playLabel.fontSizeMin = 44f;
                UiPipeline.Place(playLabel.rectTransform, center, center, new Vector2(0f, 22f), new Vector2(900f, 80f));
                var costRow = UiPipeline.Rect("Cost", playBack.rectTransform);
                UiPipeline.Place(costRow, center, center, new Vector2(0f, -38f), new Vector2(600f, 52f));
                var costIcon = UiPipeline.AddImage("Icon", costRow, kit.Ticket, Color.white, Image.Type.Simple);
                UiPipeline.Place(costIcon.rectTransform, center, new Vector2(0f, 0.5f), new Vector2(-40f, 0f), new Vector2(48f, 48f));
                var costText = UiPipeline.AddText("Value", costRow, TextRole.Label, 40f, tokens.textPrimary, "1", 36f);
                UiPipeline.Place(costText.rectTransform, center, new Vector2(0f, 0.5f), new Vector2(18f, 2f), new Vector2(300f, 52f));

                screen.SetReferences(tokens, canvas, content.gameObject, group, safe, panel, outside, close, title, chip, count, next);
                screen.SetCardReferences(cardScroll, viewport, list, cards);
                screen.SetDetailReferences(details, name, best, themesRow, themeScroll, themeList, pills, challenges, challengesTitle, challengesCount, cells,
                    goal);
                screen.SetFooterReferences(practice, practiceBack, track, knob.rectTransform, practiceLabel, practiceLine, play, playBack, playLabel, costRow,
                    costIcon, costText);
                screen.SetArtReferences(kit.ActivityIcons, kit.Play, kit.ThemeSet(), Gold, Warm, PracticeOn);
                screen.SetLayout(CardWidth, CardPitch, CardPad, HeaderHeight, CarouselHeight, FooterHeight, NameRowHeight, ThemesHeight, ChallengesHeight,
                    PillPitch, KnobTravel);
                content.gameObject.SetActive(false);
                canvas.enabled = false;
                Directory.CreateDirectory(Folder);
                return PrefabUtility.SaveAsPrefabAsset(root, HubPath).GetComponent<ActivityHubScreen>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// One card (340 x 440): the outline (selected or featured), the cream card that takes the touch, the icon in a warm
        /// disc, the « Nouveau » or « Gratuit » tag on its top edge, the featured star, the name on two lines at most, the
        /// stars with the best score, ten round pips and « n/10 », and the unlock hint of a locked card.
        /// </summary>
        private static ActivityCardView BuildCard(RectTransform list, int index, KitSprites kit)
        {
            var tokens = kit.Tokens;
            var center = new Vector2(0.5f, 0.5f);
            var top = new Vector2(0.5f, 1f);
            var card = UiPipeline.Rect("Card" + index, list);
            UiPipeline.Place(card, new Vector2(0f, 0.5f), center, new Vector2(CardPad + CardWidth * 0.5f + index * CardPitch, -6f),
                new Vector2(CardWidth, CardHeight));
            var outline = UiPipeline.AddImage("Outline", card, kit.Panel, tokens.accentPrimary, Image.Type.Sliced);
            outline.pixelsPerUnitMultiplier = 1.4f;
            UiPipeline.Place(outline.rectTransform, center, center, Vector2.zero, new Vector2(CardWidth + 20f, CardHeight + 20f));
            outline.gameObject.SetActive(false);
            var back = UiPipeline.AddImage("Background", card, kit.Panel, Color.white, Image.Type.Sliced);
            back.pixelsPerUnitMultiplier = 1.4f;
            back.raycastTarget = true;
            UiPipeline.Stretch(back.rectTransform);
            var button = back.gameObject.AddComponent<Button>();
            UiPipeline.ConfigureButton(button, back);

            var disc = UiPipeline.AddImage("Disc", card, kit.Circle, Warm, Image.Type.Simple);
            UiPipeline.Place(disc.rectTransform, top, center, new Vector2(0f, -124f), new Vector2(180f, 180f));
            // The activity icons are drawn at 128 px with their own colors: shown at 128 units, never upscaled.
            var icon = UiPipeline.AddImage("Icon", disc.rectTransform, kit.Play, Color.white, Image.Type.Simple);
            UiPipeline.Place(icon.rectTransform, center, center, new Vector2(0f, 2f), new Vector2(128f, 128f));
            var featured = UiPipeline.AddImage("Featured", card, kit.Star, Gold, Image.Type.Simple);
            UiPipeline.Place(featured.rectTransform, top, center, new Vector2(84f, -52f), new Vector2(56f, 56f));
            featured.gameObject.SetActive(false);
            // A locked card: the closed padlock (ICO_Lock, 44 units) on the lower right of the disc.
            GameObject lockMark = null;
            if (kit.Lock != null)
            {
                var lockImage = UiPipeline.AddImage("Lock", card, kit.Lock, Color.white, Image.Type.Simple);
                UiPipeline.Place(lockImage.rectTransform, top, center, new Vector2(60f, -184f), new Vector2(44f, 44f));
                lockMark = lockImage.gameObject;
                lockMark.SetActive(false);
            }

            var newTag = Tag(card, "New", tokens.accentPrimary, tokens.textPrimary, kit, out var newLabel);
            var freeTag = Tag(card, "Free", tokens.success, tokens.backgroundLight, kit, out var freeLabel);

            var name = UiPipeline.AddText("Name", card, TextRole.Label, 42f, tokens.textPrimary, "Grooming", 36f);
            name.alignment = TextAlignmentOptions.Center;
            name.textWrappingMode = TextWrappingModes.Normal;
            name.lineSpacing = -14f;
            UiPipeline.Place(name.rectTransform, top, center, new Vector2(0f, -268f), new Vector2(312f, 100f));

            var progress = UiPipeline.Rect("Progress", card);
            UiPipeline.Stretch(progress);
            var starsRow = UiPipeline.Rect("Stars", progress);
            UiPipeline.Place(starsRow, top, center, new Vector2(0f, -344f), new Vector2(312f, 48f));
            var stars = new Image[3];
            for (int i = 0; i < stars.Length; i++)
            {
                stars[i] = UiPipeline.AddImage("Star" + i, starsRow, kit.Star, MarkOff, Image.Type.Simple);
                UiPipeline.Place(stars[i].rectTransform, center, center, new Vector2(-112f + i * 44f, 0f), new Vector2(40f, 40f));
            }
            var best = UiPipeline.AddText("Best", starsRow, TextRole.NumberSmall, 40f, tokens.textPrimary, "520");
            UiPipeline.Place(best.rectTransform, center, new Vector2(0f, 0.5f), new Vector2(-2f, 2f), new Vector2(160f, 48f));
            var pipsRow = UiPipeline.Rect("Pips", progress);
            UiPipeline.Place(pipsRow, top, center, new Vector2(0f, -400f), new Vector2(312f, 44f));
            var pips = new Image[ActivityCardView.Pips];
            for (int i = 0; i < pips.Length; i++)
            {
                pips[i] = UiPipeline.AddImage("Pip" + i, pipsRow, kit.Dot, MarkOff, Image.Type.Simple);
                UiPipeline.Place(pips[i].rectTransform, center, center, new Vector2(-139f + i * 16f, 0f), new Vector2(12f, 12f));
            }
            var pipCount = UiPipeline.AddText("Count", pipsRow, TextRole.NumberSmall, 36f, tokens.textSecondary, "0/10");
            UiPipeline.Place(pipCount.rectTransform, center, new Vector2(0f, 0.5f), new Vector2(21f, 2f), new Vector2(124f, 44f));

            var hint = UiPipeline.AddText("Hint", card, TextRole.Caption, 36f, tokens.textSecondary, "Refuge level 4");
            hint.alignment = TextAlignmentOptions.Center;
            hint.textWrappingMode = TextWrappingModes.Normal;
            hint.lineSpacing = -10f;
            UiPipeline.Place(hint.rectTransform, top, center, new Vector2(0f, -376f), new Vector2(312f, 120f));
            hint.gameObject.SetActive(false);

            var view = card.gameObject.AddComponent<ActivityCardView>();
            view.SetReferences(card, button, back, outline, disc, icon, name, progress.gameObject, starsRow, 68f, stars, best, pips, pipCount, hint, newTag,
                newLabel, freeTag, freeLabel, featured.gameObject, lockMark);
            card.gameObject.SetActive(false);
            return view;
        }

        /// <summary>A soft tag sitting on the card's top edge (« Nouveau », « Gratuit »).</summary>
        private static GameObject Tag(RectTransform card, string name, Color color, Color text, KitSprites kit, out TMP_Text label)
        {
            var tag = UiPipeline.AddImage(name + "Tag", card, kit.Pill, color, Image.Type.Sliced);
            tag.pixelsPerUnitMultiplier = 2.2f;
            UiPipeline.Place(tag.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260f, 60f));
            label = UiPipeline.AddText("Label", tag.rectTransform, TextRole.Label, 36f, text, name);
            label.alignment = TextAlignmentOptions.Center;
            UiPipeline.Place(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 3f), new Vector2(244f, 50f));
            tag.gameObject.SetActive(false);
            return tag.gameObject;
        }

        /// <summary>One challenge (172 x 136): its outline when picked, the card, the number, three small stars.</summary>
        private static ActivityChallengeCellView BuildCell(RectTransform parent, int index, Vector2 position, KitSprites kit)
        {
            var tokens = kit.Tokens;
            var center = new Vector2(0.5f, 0.5f);
            var cell = UiPipeline.Rect("Challenge" + (index + 1), parent);
            UiPipeline.Place(cell, new Vector2(0f, 1f), center, position, new Vector2(172f, 136f));
            var outline = UiPipeline.AddImage("Outline", cell, kit.Panel, tokens.accentPrimary, Image.Type.Sliced);
            outline.pixelsPerUnitMultiplier = 1.6f;
            UiPipeline.Place(outline.rectTransform, center, center, Vector2.zero, new Vector2(186f, 150f));
            outline.gameObject.SetActive(false);
            var back = UiPipeline.AddImage("Background", cell, kit.Panel, Color.white, Image.Type.Sliced);
            back.pixelsPerUnitMultiplier = 1.6f;
            back.raycastTarget = true;
            UiPipeline.Stretch(back.rectTransform);
            var button = back.gameObject.AddComponent<Button>();
            UiPipeline.ConfigureButton(button, back);
            var number = UiPipeline.AddText("Number", cell, TextRole.NumberSmall, 48f, tokens.textPrimary, (index + 1).ToString());
            number.alignment = TextAlignmentOptions.Center;
            UiPipeline.Place(number.rectTransform, center, center, new Vector2(0f, 22f), new Vector2(120f, 60f));
            var stars = new Image[3];
            for (int i = 0; i < stars.Length; i++)
            {
                stars[i] = UiPipeline.AddImage("Star" + i, cell, kit.Star, MarkOff, Image.Type.Simple);
                UiPipeline.Place(stars[i].rectTransform, center, center, new Vector2(-38f + i * 38f, -34f), new Vector2(32f, 32f));
            }
            GameObject lockMark = null;
            if (kit.Lock != null)
            {
                var lockImage = UiPipeline.AddImage("Lock", cell, kit.Lock, new Color(1f, 1f, 1f, 0.75f), Image.Type.Simple);
                UiPipeline.Place(lockImage.rectTransform, center, center, new Vector2(0f, -34f), new Vector2(36f, 36f));
                lockMark = lockImage.gameObject;
                lockMark.SetActive(false);
            }
            var view = cell.gameObject.AddComponent<ActivityChallengeCellView>();
            view.SetReferences(cell, button, back, outline, number, stars, lockMark);
            return view;
        }

        /// <summary>One theme pill (336 x 92): the biome's dot and its name.</summary>
        private static ActivityThemePillView BuildPill(RectTransform list, int index, KitSprites kit)
        {
            var tokens = kit.Tokens;
            var back = UiPipeline.AddImage("Pill" + index, list, kit.Pill, Color.white, Image.Type.Sliced);
            back.pixelsPerUnitMultiplier = 1.6f;
            back.raycastTarget = true;
            UiPipeline.Place(back.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(CardPad + index * PillPitch, 0f),
                new Vector2(PillWidth, 92f));
            var button = back.gameObject.AddComponent<Button>();
            UiPipeline.ConfigureButton(button, back);
            var dot = UiPipeline.AddImage("Dot", back.rectTransform, kit.Dot, tokens.accentIdle, Image.Type.Simple);
            UiPipeline.Place(dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(38f, 0f), new Vector2(30f, 30f));
            var name = UiPipeline.AddText("Name", back.rectTransform, TextRole.Label, 40f, tokens.textSecondary, "Meadow", 36f);
            UiPipeline.Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(64f, 2f), new Vector2(270f, 56f));
            var view = back.gameObject.AddComponent<ActivityThemePillView>();
            view.SetReferences(back.rectTransform, button, back, dot, name);
            back.gameObject.SetActive(false);
            return view;
        }

        // ---------------- PF_UI_ActivityHud (screen 030) ----------------

        public static ActivityHudScreen BuildHudPrefab(UiColorTokens tokens) => BuildHudPrefab(new KitSprites(tokens));

        private static ActivityHudScreen BuildHudPrefab(KitSprites kit)
        {
            var tokens = kit.Tokens;
            var center = new Vector2(0.5f, 0.5f);
            var root = new GameObject("PF_UI_ActivityHud", typeof(RectTransform));
            try
            {
                var canvas = Overlay(root, HudSortingOrder);
                var screen = root.AddComponent<ActivityHudScreen>();
                var content = Content(root, out var group);
                var safe = UiPipeline.Rect("SafeArea", content);
                UiPipeline.Stretch(safe);

                // The score: top center, own canvas (it changes often), passive.
                var scoreRoot = UiPipeline.Rect("Score", safe);
                UiPipeline.Place(scoreRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(460f, 180f));
                scoreRoot.gameObject.AddComponent<Canvas>();
                var scoreBack = UiPipeline.AddImage("Background", scoreRoot, kit.Pill, tokens.panelPrimary, Image.Type.Sliced);
                scoreBack.pixelsPerUnitMultiplier = 1.2f;
                UiPipeline.Stretch(scoreBack.rectTransform);
                var score = UiPipeline.AddText("Value", scoreRoot, TextRole.NumberLarge, 88f, tokens.textPrimary, "0", 56f);
                score.alignment = TextAlignmentOptions.Center;
                UiPipeline.Place(score.rectTransform, new Vector2(0.5f, 1f), center, new Vector2(0f, -66f), new Vector2(420f, 100f));
                var bar = UiPipeline.AddImage("Bar", scoreRoot, kit.Pill, tokens.panelSecondary, Image.Type.Sliced);
                bar.pixelsPerUnitMultiplier = 5f;
                UiPipeline.Place(bar.rectTransform, new Vector2(0.5f, 1f), center, new Vector2(0f, -142f), new Vector2(360f, 26f));
                var fill = UiPipeline.AddImage("Fill", bar.rectTransform, kit.Pill, tokens.accentPrimary, Image.Type.Sliced);
                fill.pixelsPerUnitMultiplier = 5f;
                UiPipeline.LeftFill(fill.rectTransform, 26f);
                var bestDot = UiPipeline.AddImage("Best", bar.rectTransform, kit.Dot, new Color(tokens.textSecondary.r, tokens.textSecondary.g,
                    tokens.textSecondary.b, 0.7f), Image.Type.Simple);
                UiPipeline.Place(bestDot.rectTransform, new Vector2(0f, 0.5f), center, Vector2.zero, new Vector2(16f, 16f));
                bestDot.gameObject.SetActive(false);
                var marks = new RectTransform[3];
                var markImages = new Image[3];
                for (int i = 0; i < marks.Length; i++)
                {
                    markImages[i] = UiPipeline.AddImage("Mark" + i, bar.rectTransform, kit.Star, MarkOff, Image.Type.Simple);
                    marks[i] = markImages[i].rectTransform;
                    UiPipeline.Place(marks[i], new Vector2(0f, 0.5f), center, new Vector2(120f * (i + 1), 0f), new Vector2(46f, 46f));
                }

                // The time left: a soft ring that empties calmly (never red), the seconds in a quiet color; own canvas.
                var timeRoot = UiPipeline.Rect("Time", safe);
                UiPipeline.Place(timeRoot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -28f), new Vector2(168f, 168f));
                timeRoot.gameObject.AddComponent<Canvas>();
                var timeDisc = UiPipeline.AddImage("Disc", timeRoot, kit.Circle, tokens.panelPrimary, Image.Type.Simple);
                UiPipeline.Stretch(timeDisc.rectTransform);
                var ringBack = UiPipeline.AddImage("RingBack", timeRoot, kit.Ring, tokens.panelSecondary, Image.Type.Simple);
                UiPipeline.Place(ringBack.rectTransform, center, center, Vector2.zero, new Vector2(150f, 150f));
                var ring = UiPipeline.AddImage("Ring", timeRoot, kit.Ring, RingSoft, Image.Type.Filled);
                ring.fillMethod = Image.FillMethod.Radial360;
                ring.fillOrigin = (int)Image.Origin360.Top;
                ring.fillClockwise = true;
                ring.fillAmount = 1f;
                ring.preserveAspect = true;
                UiPipeline.Place(ring.rectTransform, center, center, Vector2.zero, new Vector2(150f, 150f));
                var seconds = UiPipeline.AddText("Seconds", timeRoot, TextRole.NumberSmall, 44f, tokens.textSecondary, "45");
                seconds.alignment = TextAlignmentOptions.Center;
                UiPipeline.Place(seconds.rectTransform, center, center, new Vector2(0f, 2f), new Vector2(100f, 60f));

                // Pause: top right, a cream circle with two soft bars.
                var pauseImage = UiPipeline.AddImage("Pause", safe, kit.Circle, tokens.panelPrimary, Image.Type.Simple);
                pauseImage.raycastTarget = true;
                UiPipeline.Place(pauseImage.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -28f), new Vector2(136f, 136f));
                var pause = pauseImage.gameObject.AddComponent<Button>();
                UiPipeline.ConfigureButton(pause, pauseImage);
                if (kit.Pause != null)
                {
                    // ICO_Pause: two soft round-capped bars, a white glyph tinted like ICO_Close_X.
                    var pauseIcon = UiPipeline.AddImage("Icon", pauseImage.rectTransform, kit.Pause, tokens.textPrimary, Image.Type.Simple);
                    UiPipeline.Place(pauseIcon.rectTransform, center, center, Vector2.zero, new Vector2(64f, 64f));
                }
                else
                {
                    for (int i = 0; i < 2; i++)
                    {
                        var stroke = UiPipeline.AddImage("Bar" + i, pauseImage.rectTransform, kit.Pill, tokens.textPrimary, Image.Type.Sliced);
                        stroke.pixelsPerUnitMultiplier = 8f;
                        UiPipeline.Place(stroke.rectTransform, center, center, new Vector2(i == 0 ? -15f : 15f, 0f), new Vector2(18f, 56f));
                    }
                }

                // The streak (from ×2) at the top left under the ring, « Entraînement » at the top right under the pause.
                var streakBack = UiPipeline.AddImage("Streak", safe, kit.Pill, tokens.panelPrimary, Image.Type.Sliced);
                streakBack.pixelsPerUnitMultiplier = 1.6f;
                UiPipeline.Place(streakBack.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -226f), new Vector2(430f, 84f));
                var streakIcon = UiPipeline.AddImage("Icon", streakBack.rectTransform, kit.Paw, Color.white, Image.Type.Simple);
                UiPipeline.Place(streakIcon.rectTransform, new Vector2(0f, 0.5f), center, new Vector2(46f, 0f), new Vector2(52f, 52f));
                var streak = UiPipeline.AddText("Label", streakBack.rectTransform, TextRole.Label, 40f, tokens.textPrimary, "Streak ×3", 36f);
                UiPipeline.Place(streak.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(86f, 2f), new Vector2(330f, 60f));
                streakBack.gameObject.SetActive(false);
                var practiceTag = UiPipeline.AddImage("Practice", safe, kit.Pill, PracticeOn, Image.Type.Sliced);
                practiceTag.pixelsPerUnitMultiplier = 1.8f;
                UiPipeline.Place(practiceTag.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -230f), new Vector2(460f, 76f));
                var practiceIcon = UiPipeline.AddImage("Icon", practiceTag.rectTransform, kit.Practice != null ? kit.Practice : kit.Paw, Color.white,
                    Image.Type.Simple);
                UiPipeline.Place(practiceIcon.rectTransform, new Vector2(0f, 0.5f), center, new Vector2(42f, 0f), new Vector2(56f, 56f));
                var practiceLabel = UiPipeline.AddText("Label", practiceTag.rectTransform, TextRole.Label, 36f, tokens.textPrimary, "Practice");
                practiceLabel.alignment = TextAlignmentOptions.Center;
                UiPipeline.Place(practiceLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(76f, 2f), new Vector2(378f, 56f));
                practiceTag.gameObject.SetActive(false);

                // T38: the tip bubble, the boost chip and the round choices in the safe area; the tutorial paw on its own
                // full screen layer above them; the pause card stays above everything.
                BuildTip(screen, safe, kit);
                BuildBoost(screen, safe, kit);
                BuildPicker(screen, safe, kit);
                BuildGesture(screen, content, kit);
                // T39: the item row in the safe area's bottom right corner; the night drawn first, under everything else.
                BuildItems(screen, safe, kit);
                BuildSpotlight(screen, content);
                LendIcons(screen);

                // The pause card: a dim that keeps touches from the game, « Reprendre » and « Quitter ».
                var pauseLayer = UiPipeline.Rect("PauseLayer", content);
                UiPipeline.Stretch(pauseLayer);
                var pauseGroup = pauseLayer.gameObject.AddComponent<CanvasGroup>();
                var pauseScrim = UiPipeline.Rect("Scrim", pauseLayer).gameObject.AddComponent<Image>();
                UiPipeline.Stretch(pauseScrim.rectTransform);
                pauseScrim.color = tokens.darkOverlay;
                pauseScrim.raycastTarget = true;
                pauseScrim.maskable = false;
                var pauseCard = UiPipeline.Rect("Card", pauseLayer);
                UiPipeline.Place(pauseCard, center, center, new Vector2(0f, 40f), new Vector2(700f, 580f));
                var pauseCardBack = UiPipeline.AddImage("Background", pauseCard, kit.Panel, tokens.backgroundLight, Image.Type.Sliced);
                UiPipeline.Stretch(pauseCardBack.rectTransform);
                pauseCardBack.raycastTarget = true;
                var pauseTitle = UiPipeline.AddText("Title", pauseCard, TextRole.Heading, 76f, tokens.textPrimary, "Paused", 60f);
                pauseTitle.alignment = TextAlignmentOptions.Center;
                UiPipeline.Place(pauseTitle.rectTransform, new Vector2(0.5f, 1f), center, new Vector2(0f, -104f), new Vector2(600f, 96f));
                var resume = ProgressionUiPipeline.PrimaryButton(pauseCard, tokens, "Resume", new Vector2(0f, 196f), new Vector2(540f, 144f), out _,
                    out var resumeLabel);
                ButtonText(resumeLabel, 52f, 480f);
                var quitBack = UiPipeline.AddImage("Quit", pauseCard, kit.Pill, tokens.panelSecondary, Image.Type.Sliced);
                quitBack.pixelsPerUnitMultiplier = 1.6f;
                quitBack.raycastTarget = true;
                UiPipeline.Place(quitBack.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 44f), new Vector2(540f, 128f));
                var quit = quitBack.gameObject.AddComponent<Button>();
                UiPipeline.ConfigureButton(quit, quitBack);
                var quitLabel = UiPipeline.AddText("Label", quitBack.rectTransform, TextRole.Button, 48f, tokens.textPrimary, "Quit", 44f);
                quitLabel.alignment = TextAlignmentOptions.Center;
                UiPipeline.Place(quitLabel.rectTransform, center, center, new Vector2(0f, 4f), new Vector2(480f, 84f));
                pauseLayer.gameObject.SetActive(false);

                screen.SetReferences(tokens, canvas, content.gameObject, group, safe);
                screen.SetScoreReferences(scoreRoot, score, bar.rectTransform, fill.rectTransform, marks, markImages, bestDot.rectTransform, timeRoot, ring,
                    seconds, streakBack.rectTransform, streak, practiceTag.gameObject, practiceLabel);
                screen.SetPauseReferences(pause, pauseLayer.gameObject, pauseGroup, pauseCard, pauseTitle, resume, resumeLabel, quit, quitLabel, Gold,
                    MarkOff);
                content.gameObject.SetActive(false);
                canvas.enabled = false;
                Directory.CreateDirectory(Folder);
                return PrefabUtility.SaveAsPrefabAsset(root, HudPath).GetComponent<ActivityHudScreen>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// T38 tip (doc 01 A15): a soft cream bubble with a warm shadow under the score card and the streak row, on its own
        /// passive canvas. The bubble's width is set per tip around its text; the label keeps the widest box (920 units), so
        /// the longest tip plus 30 % fits at the Body minimum (38).
        /// </summary>
        private static void BuildTip(ActivityHudScreen screen, RectTransform safe, KitSprites kit)
        {
            var tokens = kit.Tokens;
            var center = new Vector2(0.5f, 0.5f);
            var top = new Vector2(0.5f, 1f);
            const float height = ActivityHudScreen.TipHeight;
            var root = UiPipeline.Rect("Tip", safe);
            UiPipeline.Place(root, top, top, new Vector2(0f, ActivityHudScreen.TipTop), new Vector2(ActivityHudScreen.TipMaxWidth, height + 16f));
            root.gameObject.AddComponent<Canvas>();
            var group = PassiveGroup(root.gameObject);
            var bubble = UiPipeline.Rect("Bubble", root);
            UiPipeline.Place(bubble, top, top, Vector2.zero, new Vector2(560f, height));
            var shadow = UiPipeline.AddImage("Shadow", bubble, kit.Pill, SoftShadow, Image.Type.Sliced);
            shadow.pixelsPerUnitMultiplier = 1.3f;
            UiPipeline.Stretch(shadow.rectTransform);
            shadow.rectTransform.offsetMin = new Vector2(6f, -10f);
            shadow.rectTransform.offsetMax = new Vector2(-6f, -6f);
            var back = UiPipeline.AddImage("Background", bubble, kit.Pill, tokens.backgroundLight, Image.Type.Sliced);
            back.pixelsPerUnitMultiplier = 1.3f;
            UiPipeline.Stretch(back.rectTransform);
            var label = UiPipeline.AddText("Label", root, TextRole.Body, 46f, tokens.textPrimary, "Brush the fur", 38f);
            label.alignment = TextAlignmentOptions.Center;
            UiPipeline.Place(label.rectTransform, top, center, new Vector2(0f, -height * 0.5f + 2f), new Vector2(ActivityHudScreen.TipLabelWidth, 72f));
            screen.SetTipReferences(root, group, bubble, label);
            root.gameObject.SetActive(false);
        }

        /// <summary>
        /// T38 boost chip (the golden brush, doc 01 S-ACT-001): a cream disc in a soft gold glow, the brush icon and a soft
        /// gold ring that empties like the time ring (never red), on its own passive canvas under the score card.
        /// </summary>
        private static void BuildBoost(ActivityHudScreen screen, RectTransform safe, KitSprites kit)
        {
            var tokens = kit.Tokens;
            var center = new Vector2(0.5f, 0.5f);
            const float size = ActivityHudScreen.BoostSize;
            var root = UiPipeline.Rect("Boost", safe);
            UiPipeline.Place(root, new Vector2(0.5f, 1f), center, new Vector2(0f, ActivityHudScreen.BoostY), new Vector2(size, size));
            root.gameObject.AddComponent<Canvas>();
            var group = PassiveGroup(root.gameObject);
            var glow = UiPipeline.AddImage("Glow", root, kit.Glow, GoldGlow, Image.Type.Simple);
            UiPipeline.Place(glow.rectTransform, center, center, Vector2.zero, new Vector2(size * 1.6f, size * 1.6f));
            var disc = UiPipeline.AddImage("Disc", root, kit.Circle, tokens.panelPrimary, Image.Type.Simple);
            UiPipeline.Stretch(disc.rectTransform);
            var track = UiPipeline.AddImage("RingBack", root, kit.Ring, tokens.panelSecondary, Image.Type.Simple);
            UiPipeline.Place(track.rectTransform, center, center, Vector2.zero, new Vector2(size * 0.92f, size * 0.92f));
            var ring = UiPipeline.AddImage("Ring", root, kit.Ring, Gold, Image.Type.Filled);
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            ring.fillClockwise = true;
            ring.fillAmount = 1f;
            ring.preserveAspect = true;
            UiPipeline.Place(ring.rectTransform, center, center, Vector2.zero, new Vector2(size * 0.92f, size * 0.92f));
            var fallback = kit.GoldenBrush != null ? kit.GoldenBrush : kit.Paw;
            var icon = UiPipeline.AddImage("Icon", root, fallback, Color.white, Image.Type.Simple);
            UiPipeline.Place(icon.rectTransform, center, center, new Vector2(0f, 2f), new Vector2(size * 0.6f, size * 0.6f));
            screen.SetBoostReferences(root, group, icon, ring, fallback);
            root.gameObject.SetActive(false);
        }

        /// <summary>The icons the stages borrow (<see cref="LentIcons"/>, their bundles never copy the UI atlas); a missing one is left out.</summary>
        private static void LendIcons(ActivityHudScreen screen)
        {
            var lent = new List<Sprite>();
            foreach (var name in LentIcons)
            {
                var sprite = Optional(Icon(name));
                if (sprite != null)
                    lent.Add(sprite);
            }
            screen.SetIcons(lent.ToArray());
        }

        /// <summary>
        /// T39 items (doc 01 S-ACT-002 « Objets »: the magnifier and the whistle, one each per game): 3 round buttons (150
        /// units, 48 dp and more) in a row at the safe area's bottom right, under the picker's place and away from the middle
        /// of the play area. Each is a cream disc (the touch, the kit's pressed tint) with a soft warm rim, a soft shadow under
        /// it, the item icon, and the small round check of a used item (ICO_Check) on its lower right. Own canvas with its
        /// raycaster; the screen places the row for the count shown.
        /// </summary>
        private static void BuildItems(ActivityHudScreen screen, RectTransform safe, KitSprites kit)
        {
            var tokens = kit.Tokens;
            var center = new Vector2(0.5f, 0.5f);
            var corner = new Vector2(1f, 0f);
            const int count = ActivityHudScreen.MaxItems;
            const float size = ActivityHudScreen.ItemSize;
            var root = UiPipeline.Rect("Items", safe);
            UiPipeline.Place(root, corner, corner, Vector2.zero,
                new Vector2(ActivityHudScreen.ItemsRight + count * ActivityHudScreen.ItemPitch, ActivityHudScreen.ItemsY * 2f + 20f));
            root.gameObject.AddComponent<Canvas>();
            root.gameObject.AddComponent<GraphicRaycaster>();
            var items = new RectTransform[count];
            var groups = new CanvasGroup[count];
            var buttons = new Button[count];
            var discs = new Image[count];
            var rims = new Image[count];
            var icons = new Image[count];
            var checks = new Image[count];
            for (int i = 0; i < count; i++)
            {
                var item = UiPipeline.Rect("Item" + i, root);
                UiPipeline.Place(item, corner, center, new Vector2(ActivityHudScreen.ItemX(i, count), ActivityHudScreen.ItemsY), new Vector2(size, size));
                groups[i] = item.gameObject.AddComponent<CanvasGroup>();
                var shadow = UiPipeline.AddImage("Shadow", item, kit.Glow, PickerShadow, Image.Type.Simple);
                UiPipeline.Place(shadow.rectTransform, center, center, new Vector2(0f, -14f), new Vector2(size * 1.3f, size * 1.3f));
                discs[i] = UiPipeline.AddImage("Button", item, kit.Circle, tokens.backgroundLight, Image.Type.Simple);
                discs[i].raycastTarget = true;
                UiPipeline.Stretch(discs[i].rectTransform);
                buttons[i] = discs[i].gameObject.AddComponent<Button>();
                UiPipeline.ConfigureButton(buttons[i], discs[i]);
                // A used item is greyed by the screen itself: the disabled tint changes nothing.
                var colors = buttons[i].colors;
                colors.disabledColor = Color.white;
                buttons[i].colors = colors;
                rims[i] = UiPipeline.AddImage("Rim", discs[i].rectTransform, kit.Ring, tokens.accentBusy, Image.Type.Simple);
                UiPipeline.Stretch(rims[i].rectTransform);
                // The item icons (agent HI, 128 px drawn) read best at 62 % of the button.
                icons[i] = UiPipeline.AddImage("Icon", discs[i].rectTransform, kit.Paw, Color.white, Image.Type.Simple);
                UiPipeline.Place(icons[i].rectTransform, center, center, new Vector2(0f, 2f), new Vector2(size * 0.62f, size * 0.62f));
                checks[i] = UiPipeline.AddImage("Check", item, kit.Check != null ? kit.Check : kit.Dot, kit.Check != null ? Color.white : tokens.success,
                    Image.Type.Simple);
                UiPipeline.Place(checks[i].rectTransform, center, center, new Vector2(size * 0.34f, -size * 0.34f),
                    new Vector2(ActivityHudScreen.ItemCheckSize, ActivityHudScreen.ItemCheckSize));
                // Drawn through its alpha (clear until the item is used, culled while clear): nothing toggles during a game.
                var checkColor = checks[i].color;
                checkColor.a = 0f;
                checks[i].color = checkColor;
                checks[i].canvasRenderer.cullTransparentMesh = true;
                items[i] = item;
                item.gameObject.SetActive(false);
            }
            screen.SetItemReferences(root, items, groups, buttons, discs, rims, icons, checks, kit.Paw);
            root.gameObject.SetActive(false);
        }

        /// <summary>
        /// MAT_UI_Spotlight (PocketWilds/UISpotlight) with the night's look (<see cref="Night"/>, <see cref="NightNear"/>…).
        /// Null while the shader is not imported: the HUD is then built without its night.
        /// </summary>
        public static Material EnsureSpotlightMaterial()
        {
            if (File.Exists(SpotlightShaderPath))
                AssetDatabase.ImportAsset(SpotlightShaderPath);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(SpotlightShaderPath);
            if (shader == null)
                return null;
            var material = AssetDatabase.LoadAssetAtPath<Material>(SpotlightMaterialPath);
            if (material == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SpotlightMaterialPath));
                material = new Material(shader) { name = Path.GetFileNameWithoutExtension(SpotlightMaterialPath) };
                AssetDatabase.CreateAsset(material, SpotlightMaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }
            material.SetColor("_Color", Night);
            material.SetVector("_Spot", new Vector4(0.5f, 0.5f, 0.25f, UiPipeline.ReferenceResolution.y / UiPipeline.ReferenceResolution.x));
            material.SetFloat("_Feather", NightFeather);
            material.SetFloat("_Near", NightNear);
            material.SetFloat("_Far", NightFar);
            material.SetFloat("_Reach", NightReach);
            material.SetFloat("_Fade", 1f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// T39 night (doc 01 S-ACT-002 challenge 7, an interface mask, no real-time light): a full screen layer drawn first in
        /// the HUD (the score, the tips and the items stay readable over it), on its own passive canvas: one sprite-less Image
        /// (UVs 0 to 1 over the screen) with MAT_UI_Spotlight, taking no touch, one draw call.
        /// </summary>
        private static void BuildSpotlight(ActivityHudScreen screen, RectTransform content)
        {
            var material = EnsureSpotlightMaterial();
            if (material == null)
            {
                Debug.LogWarning("[ActivityUiPipeline] " + SpotlightShaderPath + " is not imported yet: the HUD is built without its night.");
                return;
            }
            var root = UiPipeline.Rect("Night", content);
            UiPipeline.Stretch(root);
            root.SetAsFirstSibling();
            root.gameObject.AddComponent<Canvas>();
            PassiveGroup(root.gameObject);
            var image = UiPipeline.AddImage("Shade", root, null, Color.white, Image.Type.Simple);
            UiPipeline.Stretch(image.rectTransform);
            image.preserveAspect = false;
            image.material = material;
            screen.SetSpotlightReferences(root, image, material);
            root.gameObject.SetActive(false);
        }

        /// <summary>
        /// T38 picker (the three grooming bows): 4 big round choices (176 units, 48 dp and more) at the bottom of the play
        /// area, each a cream disc with a soft warm rim, a soft shadow under it, its icon, and the kit's pressed tint. Own
        /// canvas with its raycaster (the only touchable T38 part).
        /// </summary>
        private static void BuildPicker(ActivityHudScreen screen, RectTransform safe, KitSprites kit)
        {
            var tokens = kit.Tokens;
            var center = new Vector2(0.5f, 0.5f);
            int count = ActivityHudScreen.MaxPickerItems;
            var root = UiPipeline.Rect("Picker", safe);
            UiPipeline.Place(root, new Vector2(0.5f, 0f), center, new Vector2(0f, PickerY), new Vector2(count * PickerPitch, PickerSize + 80f));
            root.gameObject.AddComponent<Canvas>();
            root.gameObject.AddComponent<GraphicRaycaster>();
            var items = new RectTransform[count];
            var groups = new CanvasGroup[count];
            var buttons = new Button[count];
            var icons = new Image[count];
            for (int i = 0; i < count; i++)
            {
                var item = UiPipeline.Rect("Choice" + i, root);
                UiPipeline.Place(item, center, center, new Vector2((i - (count - 1) * 0.5f) * PickerPitch, 0f), new Vector2(PickerSize, PickerSize));
                groups[i] = item.gameObject.AddComponent<CanvasGroup>();
                var shadow = UiPipeline.AddImage("Shadow", item, kit.Glow, PickerShadow, Image.Type.Simple);
                UiPipeline.Place(shadow.rectTransform, center, center, new Vector2(0f, -18f), new Vector2(PickerSize * 1.3f, PickerSize * 1.3f));
                var disc = UiPipeline.AddImage("Button", item, kit.Circle, tokens.backgroundLight, Image.Type.Simple);
                disc.raycastTarget = true;
                UiPipeline.Stretch(disc.rectTransform);
                buttons[i] = disc.gameObject.AddComponent<Button>();
                UiPipeline.ConfigureButton(buttons[i], disc);
                var rim = UiPipeline.AddImage("Rim", disc.rectTransform, kit.Ring, tokens.accentBusy, Image.Type.Simple);
                UiPipeline.Stretch(rim.rectTransform);
                // The bow icons (agent GI) read best at 64 % of the button.
                icons[i] = UiPipeline.AddImage("Icon", disc.rectTransform, kit.Dot, Color.white, Image.Type.Simple);
                UiPipeline.Place(icons[i].rectTransform, center, center, new Vector2(0f, 2f), new Vector2(PickerSize * 0.64f, PickerSize * 0.64f));
                items[i] = item;
                item.gameObject.SetActive(false);
            }
            screen.SetPickerReferences(root, items, groups, buttons, icons, PickerPitch, kit.Dot);
            root.gameObject.SetActive(false);
        }

        /// <summary>
        /// T38 tutorial paw (doc 01 A15, doc Mini-jeux section 1): a full screen layer on its own passive canvas above the
        /// play area (under the pause card), with the paw (UI_Gesture_Swipe, else UI_Gesture_Tap), two soft rings for the
        /// taps and six warm round dots for the swipe's trail, all pooled here.
        /// </summary>
        private static void BuildGesture(ActivityHudScreen screen, RectTransform content, KitSprites kit)
        {
            var center = new Vector2(0.5f, 0.5f);
            var root = UiPipeline.Rect("Gesture", content);
            UiPipeline.Stretch(root);
            root.pivot = center;
            root.gameObject.AddComponent<Canvas>();
            var group = PassiveGroup(root.gameObject);
            // Dots, rings and the paw fade through their alpha while a gesture loops: a clear one draws nothing.
            var trail = new Image[ActivityHudScreen.TrailDots];
            for (int i = 0; i < trail.Length; i++)
            {
                trail[i] = UiPipeline.AddImage("Trail" + i, root, kit.Dot, TrailTint, Image.Type.Simple);
                UiPipeline.Place(trail[i].rectTransform, center, center, Vector2.zero, new Vector2(30f, 30f));
                trail[i].canvasRenderer.cullTransparentMesh = true;
                trail[i].gameObject.SetActive(false);
            }
            var rings = new Image[2];
            for (int i = 0; i < rings.Length; i++)
            {
                rings[i] = UiPipeline.AddImage("Ring" + i, root, kit.Ring, new Color(1f, 1f, 1f, 0f), Image.Type.Simple);
                UiPipeline.Place(rings[i].rectTransform, center, center, Vector2.zero, new Vector2(200f, 200f));
                rings[i].canvasRenderer.cullTransparentMesh = true;
                rings[i].gameObject.SetActive(false);
            }
            // UI_Gesture_Swipe (192 px, drawn sliding right) turns around its pad, the point that follows the path; the tap
            // paw around its center. 170 units, like the A12 paw.
            var paw = UiPipeline.AddImage("Paw", root, kit.GestureSwipe != null ? kit.GestureSwipe : kit.GestureTap, Color.white, Image.Type.Simple);
            UiPipeline.Place(paw.rectTransform, center, kit.GestureSwipe != null ? ActivityHudScreen.SwipePadPivot : center, Vector2.zero,
                new Vector2(170f, 170f));
            paw.canvasRenderer.cullTransparentMesh = true;
            screen.SetGestureReferences(root, group, paw.rectTransform, paw, rings, trail, kit.GestureSwipe, kit.GestureTap,
                ActivityHudScreen.SwipePadPivot);
            root.gameObject.SetActive(false);
        }

        // ---------------- PF_UI_ActivityResult (doc 05 sheet 20) ----------------

        public static ActivityResultScreen BuildResultPrefab(UiColorTokens tokens) => BuildResultPrefab(new KitSprites(tokens));

        private static ActivityResultScreen BuildResultPrefab(KitSprites kit)
        {
            var tokens = kit.Tokens;
            var center = new Vector2(0.5f, 0.5f);
            var top = new Vector2(0.5f, 1f);
            var root = new GameObject("PF_UI_ActivityResult", typeof(RectTransform));
            try
            {
                var canvas = Overlay(root, ResultSortingOrder);
                var screen = root.AddComponent<ActivityResultScreen>();
                var content = Content(root, out var group);

                // A dim that never closes the card (« Rejouer » and « Retour » do); from the second game it reveals all.
                var scrimImage = UiPipeline.Rect("Scrim", content).gameObject.AddComponent<Image>();
                UiPipeline.Stretch(scrimImage.rectTransform);
                scrimImage.color = tokens.darkOverlay;
                scrimImage.raycastTarget = true;
                scrimImage.maskable = false;
                var scrim = PlainButton(scrimImage);

                var card = UiPipeline.Rect("Card", content);
                UiPipeline.Place(card, center, center, new Vector2(0f, -40f), new Vector2(880f, ResultCardHeight));
                var back = UiPipeline.AddImage("Background", card, kit.Panel, tokens.backgroundLight, Image.Type.Sliced);
                UiPipeline.Stretch(back.rectTransform);
                back.raycastTarget = true;
                var cardTap = PlainButton(back);

                // The activity's icon floats on the card's top edge.
                var iconDisc = UiPipeline.AddImage("IconDisc", card, kit.Circle, Warm, Image.Type.Simple);
                UiPipeline.Place(iconDisc.rectTransform, top, center, new Vector2(0f, 6f), new Vector2(184f, 184f));
                var icon = UiPipeline.AddImage("Icon", iconDisc.rectTransform, kit.Play, Color.white, Image.Type.Simple);
                UiPipeline.Place(icon.rectTransform, center, center, new Vector2(0f, 2f), new Vector2(128f, 128f));
                var title = UiPipeline.AddText("Title", card, TextRole.Heading, 72f, tokens.textPrimary, "Well played", 60f);
                title.alignment = TextAlignmentOptions.Center;
                UiPipeline.Place(title.rectTransform, top, center, new Vector2(0f, -150f), new Vector2(800f, 96f));
                var subtitle = UiPipeline.AddText("Subtitle", card, TextRole.Caption, 40f, tokens.textSecondary, "Grooming", 36f);
                subtitle.alignment = TextAlignmentOptions.Center;
                UiPipeline.Place(subtitle.rectTransform, top, center, new Vector2(0f, -212f), new Vector2(800f, 54f));

                // Three stars: grey and soft until earned; the gold one pops over it.
                var slots = new RectTransform[3];
                var starsOn = new RectTransform[3];
                var starGroups = new CanvasGroup[3];
                for (int i = 0; i < 3; i++)
                {
                    // UI_Star_Result_Empty (the socket) and UI_Star_Result_Full (warm gold) in the same frame, untinted, pivoted on the
                    // star's own center so the pop stays on it; the round star tinted when they are missing.
                    bool drawn = kit.StarEmpty != null && kit.StarFull != null;
                    var slot = UiPipeline.AddImage("Star" + i, card, drawn ? kit.StarEmpty : kit.Star, drawn ? Color.white : MarkOff, Image.Type.Simple);
                    var pivot = drawn ? ResultStarPivot : center;
                    UiPipeline.Place(slot.rectTransform, top, pivot, new Vector2((i - 1) * 200f, i == 1 ? -320f : -348f), new Vector2(168f, 168f));
                    var on = UiPipeline.AddImage("On", slot.rectTransform, drawn ? kit.StarFull : kit.Star, drawn ? Color.white : Gold, Image.Type.Simple);
                    UiPipeline.Stretch(on.rectTransform);
                    on.rectTransform.pivot = pivot;
                    starGroups[i] = PassiveGroup(on.gameObject);
                    on.gameObject.SetActive(false);
                    slots[i] = slot.rectTransform;
                    starsOn[i] = on.rectTransform;
                }
                var score = UiPipeline.AddText("Score", card, TextRole.NumberLarge, 128f, tokens.textPrimary, "0", 72f);
                score.alignment = TextAlignmentOptions.Center;
                UiPipeline.Place(score.rectTransform, top, center, new Vector2(0f, -512f), new Vector2(760f, 150f));

                // « Nouveau record » (a gold ribbon with a round star) or « Record : n ».
                var ribbonBack = UiPipeline.AddImage("Record", card, kit.Pill, Gold, Image.Type.Sliced);
                ribbonBack.pixelsPerUnitMultiplier = 1.8f;
                UiPipeline.Place(ribbonBack.rectTransform, top, center, new Vector2(0f, -616f), new Vector2(560f, 88f));
                var ribbonGroup = PassiveGroup(ribbonBack.gameObject);
                // ICO_Record (a rosette holding a round gold star), else a cream round star.
                var ribbonStar = UiPipeline.AddImage("Icon", ribbonBack.rectTransform, kit.Record != null ? kit.Record : kit.Star,
                    kit.Record != null ? Color.white : tokens.backgroundLight, Image.Type.Simple);
                UiPipeline.Place(ribbonStar.rectTransform, new Vector2(0f, 0.5f), center, new Vector2(52f, 2f), new Vector2(68f, 68f));
                var ribbonLabel = UiPipeline.AddText("Label", ribbonBack.rectTransform, TextRole.Label, 44f, tokens.textPrimary, "New record", 36f);
                ribbonLabel.alignment = TextAlignmentOptions.Center;
                UiPipeline.Place(ribbonLabel.rectTransform, center, center, new Vector2(30f, 3f), new Vector2(440f, 64f));
                ribbonBack.gameObject.SetActive(false);
                var bestLine = UiPipeline.AddText("Best", card, TextRole.Caption, 40f, tokens.textSecondary, "Best: 520", 36f);
                bestLine.alignment = TextAlignmentOptions.Center;
                UiPipeline.Place(bestLine.rectTransform, top, center, new Vector2(0f, -616f), new Vector2(700f, 56f));
                bestLine.gameObject.SetActive(false);

                // The challenge played: « Défi n » and its stars; « Nouvelle étoile » when a star was won.
                var challengeRow = UiPipeline.Rect("Challenge", card);
                UiPipeline.Place(challengeRow, top, center, new Vector2(0f, -700f), new Vector2(840f, 64f));
                var challengeLabel = UiPipeline.AddText("Label", challengeRow, TextRole.Label, 42f, tokens.textPrimary, "Challenge 3", 36f);
                challengeLabel.alignment = TextAlignmentOptions.MidlineRight;
                UiPipeline.Place(challengeLabel.rectTransform, center, new Vector2(1f, 0.5f), new Vector2(-50f, 2f), new Vector2(360f, 60f));
                var challengeStars = new Image[3];
                for (int i = 0; i < 3; i++)
                {
                    challengeStars[i] = UiPipeline.AddImage("Star" + i, challengeRow, kit.Star, MarkOff, Image.Type.Simple);
                    UiPipeline.Place(challengeStars[i].rectTransform, center, center, new Vector2(i * 46f, 0f), new Vector2(40f, 40f));
                }
                var newStar = UiPipeline.AddImage("NewStar", card, kit.Pill, tokens.success, Image.Type.Sliced);
                newStar.pixelsPerUnitMultiplier = 2.2f;
                UiPipeline.Place(newStar.rectTransform, top, center, new Vector2(0f, -764f), new Vector2(440f, 56f));
                var newStarLabel = UiPipeline.AddText("Label", newStar.rectTransform, TextRole.Label, 36f, tokens.backgroundLight, "New star");
                newStarLabel.alignment = TextAlignmentOptions.Center;
                UiPipeline.Place(newStarLabel.rectTransform, center, center, new Vector2(0f, 3f), new Vector2(420f, 50f));
                newStar.gameObject.SetActive(false);
                challengeRow.gameObject.SetActive(false);

                // The reward cards: face down (a warm card with a paw), they turn over one by one.
                var rewardsRow = UiPipeline.Rect("Rewards", card);
                UiPipeline.Place(rewardsRow, top, center, new Vector2(0f, -880f), new Vector2(840f, 160f));
                var flips = new RectTransform[ActivityResultScreen.MaxRewards];
                var flipGroups = new CanvasGroup[flips.Length];
                var backs = new GameObject[flips.Length];
                var cells = new RewardCellView[flips.Length];
                for (int i = 0; i < flips.Length; i++)
                {
                    var flip = UiPipeline.Rect("Card" + i, rewardsRow);
                    UiPipeline.Place(flip, center, center, new Vector2((i - 2.5f) * RewardPitch, 0f), new Vector2(124f, 152f));
                    flipGroups[i] = PassiveGroup(flip.gameObject);
                    var cardBack = UiPipeline.AddImage("Back", flip, kit.Panel, Warm, Image.Type.Sliced);
                    cardBack.pixelsPerUnitMultiplier = 2f;
                    UiPipeline.Stretch(cardBack.rectTransform);
                    var paw = UiPipeline.AddImage("Paw", cardBack.rectTransform, kit.Paw, new Color(1f, 1f, 1f, 0.85f), Image.Type.Simple);
                    UiPipeline.Place(paw.rectTransform, center, center, new Vector2(0f, 4f), new Vector2(64f, 64f));
                    cells[i] = Cell(flip, "Cell", kit);
                    cells[i].gameObject.SetActive(false);
                    backs[i] = cardBack.gameObject;
                    flips[i] = flip;
                    flip.gameObject.SetActive(false);
                }
                var stickerNote = UiPipeline.AddText("StickerNote", card, TextRole.Caption, 38f, tokens.textSecondary, "Next sticker on the page", 36f);
                stickerNote.alignment = TextAlignmentOptions.Center;
                UiPipeline.Place(stickerNote.rectTransform, top, center, new Vector2(0f, -988f), new Vector2(800f, 50f));
                stickerNote.gameObject.SetActive(false);
                var practiceNote = UiPipeline.AddText("PracticeNote", card, TextRole.Body, 42f, tokens.textSecondary, "Practice game", 38f);
                practiceNote.alignment = TextAlignmentOptions.Center;
                UiPipeline.Place(practiceNote.rectTransform, top, center, new Vector2(0f, -880f), new Vector2(800f, 60f));
                practiceNote.gameObject.SetActive(false);

                // « Rejouer » and « Retour » at the thumb.
                var replay = ProgressionUiPipeline.PrimaryButton(card, tokens, "Replay", new Vector2(-205f, 44f), new Vector2(390f, 140f), out _,
                    out var replayLabel);
                ButtonText(replayLabel, 52f, 350f);
                var backButtonImage = UiPipeline.AddImage("Back", card, kit.Pill, tokens.panelSecondary, Image.Type.Sliced);
                backButtonImage.pixelsPerUnitMultiplier = 1.6f;
                backButtonImage.raycastTarget = true;
                UiPipeline.Place(backButtonImage.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(205f, 44f), new Vector2(390f, 140f));
                var backButton = backButtonImage.gameObject.AddComponent<Button>();
                UiPipeline.ConfigureButton(backButton, backButtonImage);
                var backLabel = UiPipeline.AddText("Label", backButtonImage.rectTransform, TextRole.Button, 52f, tokens.textPrimary, "Back", 44f);
                backLabel.alignment = TextAlignmentOptions.Center;
                UiPipeline.Place(backLabel.rectTransform, center, center, new Vector2(0f, 6f), new Vector2(350f, 90f));

                var burst = BuildBurst(content, kit);

                screen.SetReferences(tokens, canvas, content.gameObject, group, card, scrim, cardTap, icon, title, subtitle);
                screen.SetScoreReferences(slots, starsOn, starGroups, score, ribbonBack.rectTransform, ribbonGroup, ribbonLabel, bestLine, challengeRow,
                    challengeLabel, challengeStars, newStar.rectTransform, newStarLabel);
                screen.SetRewardReferences(rewardsRow, flips, flipGroups, backs, cells, stickerNote, practiceNote, RewardPitch, ResultChallengeSpace, replay,
                    replayLabel, backButton, backLabel);
                screen.SetArtReferences(burst, kit.Icons(), kit.ActivityIcons, kit.Play, Gold, MarkOff);
                content.gameObject.SetActive(false);
                canvas.enabled = false;
                Directory.CreateDirectory(Folder);
                return PrefabUtility.SaveAsPrefabAsset(root, ResultPath).GetComponent<ActivityResultScreen>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ---------------- PF_UI_TicketsEmpty ----------------

        public static TicketsEmptyCard BuildTicketsEmptyPrefab(UiColorTokens tokens) => BuildTicketsEmptyPrefab(new KitSprites(tokens));

        private static TicketsEmptyCard BuildTicketsEmptyPrefab(KitSprites kit)
        {
            var tokens = kit.Tokens;
            var center = new Vector2(0.5f, 0.5f);
            var top = new Vector2(0.5f, 1f);
            var root = new GameObject("PF_UI_TicketsEmpty", typeof(RectTransform));
            try
            {
                var canvas = Overlay(root, TicketsEmptySortingOrder);
                var screen = root.AddComponent<TicketsEmptyCard>();
                var content = Content(root, out var group);
                // A touch beside the card closes it: never blocking.
                var scrimImage = UiPipeline.Rect("Scrim", content).gameObject.AddComponent<Image>();
                UiPipeline.Stretch(scrimImage.rectTransform);
                scrimImage.color = tokens.darkOverlay;
                scrimImage.raycastTarget = true;
                scrimImage.maskable = false;
                var outside = PlainButton(scrimImage);

                var card = UiPipeline.Rect("Card", content);
                UiPipeline.Place(card, center, center, new Vector2(0f, -20f), new Vector2(880f, 980f));
                var back = UiPipeline.AddImage("Background", card, kit.Panel, tokens.backgroundLight, Image.Type.Sliced);
                UiPipeline.Stretch(back.rectTransform);
                back.raycastTarget = true;
                var close = RoundClose(card, kit, new Vector2(-76f, -76f));

                var glow = UiPipeline.AddImage("Glow", card, kit.Glow, GoldGlow, Image.Type.Simple);
                UiPipeline.Place(glow.rectTransform, top, center, new Vector2(0f, -150f), new Vector2(300f, 300f));
                var ticket = UiPipeline.AddImage("Ticket", card, kit.Ticket, Color.white, Image.Type.Simple);
                UiPipeline.Place(ticket.rectTransform, top, center, new Vector2(0f, -150f), new Vector2(156f, 156f));
                var title = UiPipeline.AddText("Title", card, TextRole.Subheading, 50f, tokens.textPrimary, "Tickets on the way", 44f);
                title.alignment = TextAlignmentOptions.Center;
                title.textWrappingMode = TextWrappingModes.Normal;
                title.lineSpacing = -10f;
                UiPipeline.Place(title.rectTransform, top, center, new Vector2(0f, -310f), new Vector2(800f, 120f));
                // The time comes first: the real minutes to the next free ticket.
                var time = UiPipeline.AddText("Time", card, TextRole.Label, 46f, tokens.textPrimary, "Next ticket in 1 h 12 min", 36f);
                time.alignment = TextAlignmentOptions.Center;
                UiPipeline.Place(time.rectTransform, top, center, new Vector2(0f, -404f), new Vector2(800f, 64f));

                var practice = ProgressionUiPipeline.PrimaryButton(card, tokens, "Practice", new Vector2(0f, 980f - 590f - 75f), new Vector2(800f, 150f),
                    out _, out var practiceLabel);
                ButtonText(practiceLabel, 52f, 770f);
                var practiceLine = UiPipeline.AddText("PracticeLine", card, TextRole.Caption, 38f, tokens.textSecondary, "Free, no rewards", 36f);
                practiceLine.alignment = TextAlignmentOptions.Center;
                UiPipeline.Place(practiceLine.rectTransform, top, center, new Vector2(0f, -690f), new Vector2(800f, 50f));

                // The pack: « 3 billets » beside its price chip; dimmed softly with a calm word when it cannot be taken.
                var packBack = UiPipeline.AddImage("Pack", card, kit.Pill, tokens.panelSecondary, Image.Type.Sliced);
                packBack.pixelsPerUnitMultiplier = 1.6f;
                packBack.raycastTarget = true;
                UiPipeline.Place(packBack.rectTransform, top, center, new Vector2(0f, -800f), new Vector2(800f, 140f));
                var pack = packBack.gameObject.AddComponent<Button>();
                UiPipeline.ConfigureButton(pack, packBack);
                var packGroup = packBack.gameObject.AddComponent<CanvasGroup>();
                var packIcon = UiPipeline.AddImage("Icon", packBack.rectTransform, kit.Ticket, Color.white, Image.Type.Simple);
                UiPipeline.Place(packIcon.rectTransform, new Vector2(0f, 0.5f), center, new Vector2(70f, 2f), new Vector2(80f, 80f));
                var packLabel = UiPipeline.AddText("Label", packBack.rectTransform, TextRole.Button, 48f, tokens.textPrimary, "3 tickets", 44f);
                UiPipeline.Place(packLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(124f, 4f), new Vector2(380f, 84f));
                var priceChip = UiPipeline.AddImage("Price", packBack.rectTransform, kit.Pill, tokens.backgroundLight, Image.Type.Sliced);
                priceChip.pixelsPerUnitMultiplier = 2f;
                UiPipeline.Place(priceChip.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(210f, 96f));
                var gem = UiPipeline.AddImage("Gem", priceChip.rectTransform, kit.Gems, Color.white, Image.Type.Simple);
                UiPipeline.Place(gem.rectTransform, new Vector2(0f, 0.5f), center, new Vector2(48f, 2f), new Vector2(60f, 60f));
                var price = UiPipeline.AddText("Value", priceChip.rectTransform, TextRole.NumberSmall, 46f, tokens.textPrimary, "30");
                UiPipeline.Place(price.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(86f, 4f), new Vector2(116f, 60f));
                var packNote = UiPipeline.AddText("Note", card, TextRole.Caption, 38f, tokens.textSecondary, "More packs tomorrow", 36f);
                packNote.alignment = TextAlignmentOptions.Center;
                UiPipeline.Place(packNote.rectTransform, top, center, new Vector2(0f, -900f), new Vector2(800f, 50f));
                packNote.gameObject.SetActive(false);

                screen.SetReferences(tokens, canvas, content.gameObject, group, card, outside, close, title, time, practice, practiceLabel, practiceLine, pack,
                    packGroup, packLabel, price, packNote);
                content.gameObject.SetActive(false);
                canvas.enabled = false;
                Directory.CreateDirectory(Folder);
                return PrefabUtility.SaveAsPrefabAsset(root, TicketsEmptyPath).GetComponent<TicketsEmptyCard>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ---------------- Shared parts ----------------

        private static Canvas Overlay(GameObject root, int order)
        {
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.None;
            UiPipeline.AddScaler(root);
            root.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static RectTransform Content(GameObject root, out CanvasGroup group)
        {
            var content = UiPipeline.Rect("Content", root.transform);
            UiPipeline.Stretch(content);
            group = content.gameObject.AddComponent<CanvasGroup>();
            return content;
        }

        private static CanvasGroup PassiveGroup(GameObject go)
        {
            var group = go.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            return group;
        }

        /// <summary>A touch catcher without a visual transition (a dim, a card that reveals on a tap).</summary>
        private static Button PlainButton(Image target)
        {
            var button = target.gameObject.AddComponent<Button>();
            button.targetGraphic = target;
            button.transition = Selectable.Transition.None;
            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            return button;
        }

        /// <summary>The round close (124 units, a soft X), anchored at the top right of <paramref name="parent"/>.</summary>
        private static Button RoundClose(RectTransform parent, KitSprites kit, Vector2 position)
        {
            var image = UiPipeline.AddImage("Close", parent, kit.Circle, Color.white, Image.Type.Simple);
            image.raycastTarget = true;
            UiPipeline.Place(image.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), position, new Vector2(124f, 124f));
            var button = image.gameObject.AddComponent<Button>();
            UiPipeline.ConfigureButton(button, image);
            var x = UiPipeline.AddImage("X", image.rectTransform, kit.Close, kit.Tokens.textSecondary, Image.Type.Simple);
            UiPipeline.Place(x.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(54f, 54f));
            return button;
        }

        private static void Horizontal(ScrollRect scroll, RectTransform viewport, RectTransform content)
        {
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.08f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 40f;
        }

        /// <summary>A reward cell (124 x 152), as the T36 screens build it: a soft disc with the icon, the amount under it.</summary>
        private static RewardCellView Cell(RectTransform parent, string name, KitSprites kit)
        {
            var tokens = kit.Tokens;
            var center = new Vector2(0.5f, 0.5f);
            var cell = UiPipeline.Rect(name, parent);
            UiPipeline.Place(cell, center, center, Vector2.zero, new Vector2(124f, 152f));
            var group = PassiveGroup(cell.gameObject);
            var disc = UiPipeline.AddImage("Disc", cell, kit.Circle, tokens.panelSecondary, Image.Type.Simple);
            UiPipeline.Place(disc.rectTransform, center, center, new Vector2(0f, 18f), new Vector2(108f, 108f));
            var icon = UiPipeline.AddImage("Icon", disc.rectTransform, kit.Gems, Color.white, Image.Type.Simple);
            UiPipeline.Place(icon.rectTransform, center, center, new Vector2(0f, 2f), new Vector2(78f, 78f));
            var spots = UiPipeline.AddImage("Spots", icon.rectTransform, kit.Spots, Color.white, Image.Type.Simple);
            UiPipeline.Stretch(spots.rectTransform);
            spots.gameObject.SetActive(false);
            var amount = UiPipeline.AddText("Amount", cell, TextRole.NumberSmall, 40f, tokens.textPrimary, "0", 36f);
            amount.alignment = TextAlignmentOptions.Center;
            UiPipeline.Place(amount.rectTransform, center, center, new Vector2(0f, -56f), new Vector2(150f, 44f));
            var view = cell.gameObject.AddComponent<RewardCellView>();
            view.SetReferences(cell, group, disc, icon, spots, amount);
            return view;
        }

        /// <summary>The UiBurst layer (H-VFX-073): full screen, its own passive canvas above the card, 24 pooled images.</summary>
        private static UiBurst BuildBurst(Transform parent, KitSprites kit)
        {
            var rect = UiPipeline.Rect("Burst", parent);
            UiPipeline.Stretch(rect);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.gameObject.AddComponent<Canvas>();
            PassiveGroup(rect.gameObject);
            var images = new Image[UiBurst.MaxImages];
            for (int i = 0; i < images.Length; i++)
            {
                images[i] = UiPipeline.AddImage("Mote" + i, rect, kit.Dot, Color.white, Image.Type.Simple);
                UiPipeline.Place(images[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24f, 24f));
                images[i].gameObject.SetActive(false);
            }
            var burst = rect.gameObject.AddComponent<UiBurst>();
            burst.SetReferences(rect, images, kit.Dot, kit.Ring, kit.Star, kit.Heart);
            rect.gameObject.SetActive(false);
            return burst;
        }

        /// <summary>A primary button's label at its own size and box (PrimaryButton auto-sizes from 56 down to 44).</summary>
        private static void ButtonText(TMP_Text label, float size, float width)
        {
            label.fontSizeMax = size;
            label.fontSize = size;
            label.fontSizeMin = 44f;
            label.rectTransform.sizeDelta = new Vector2(width, label.rectTransform.sizeDelta.y);
        }

        /// <summary>Everything in a scrolled strip is clipped by its viewport (the kit builds labels and images unmaskable).</summary>
        private static void Maskable(GameObject root)
        {
            foreach (var graphic in root.GetComponentsInChildren<MaskableGraphic>(true))
                graphic.maskable = true;
        }

        // ---------------- Scene ----------------

        /// <summary>The four screens in the vertical slice, and the scene entry's fields pointing at them.</summary>
        public static void WireScene()
        {
            var scene = EditorSceneManager.OpenScene(UiPipeline.ScenePath, OpenSceneMode.Single);
            var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
            if (entry == null)
                throw new InvalidOperationException("SceneEntry (VerticalSliceEntry) missing in " + UiPipeline.ScenePath);
            T Ensure<T>(string path, string name) where T : MonoBehaviour
            {
                var existing = Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
                if (existing != null)
                    return existing;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? throw new FileNotFoundException("Build the prefab first.", path);
                var created = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, scene)).GetComponent<T>();
                created.name = name;
                return created;
            }
            var hub = Ensure<ActivityHubScreen>(HubPath, "Screen_ActivityHub");
            var hud = Ensure<ActivityHudScreen>(HudPath, "Screen_ActivityHud");
            var result = Ensure<ActivityResultScreen>(ResultPath, "Screen_ActivityResult");
            var empty = Ensure<TicketsEmptyCard>(TicketsEmptyPath, "Screen_TicketsEmpty");
            var so = new SerializedObject(entry);
            so.FindProperty("_activityHub").objectReferenceValue = hub;
            so.FindProperty("_activityHud").objectReferenceValue = hud;
            so.FindProperty("_activityResult").objectReferenceValue = result;
            so.FindProperty("_ticketsEmpty").objectReferenceValue = empty;
            so.ApplyModifiedPropertiesWithoutUndo();
            // Some editor tool calls refuse SerializedObject references (seen in T34): the fields are checked and set directly.
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            foreach (var (field, value) in new (string, MonoBehaviour)[] { ("_activityHub", hub), ("_activityHud", hud), ("_activityResult", result),
                         ("_ticketsEmpty", empty) })
            {
                var info = typeof(VerticalSliceEntry).GetField(field, flags);
                if (info != null && !ReferenceEquals(info.GetValue(entry), value))
                    info.SetValue(entry, value);
            }
            EditorUtility.SetDirty(entry);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
