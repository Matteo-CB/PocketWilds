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
    /// T36 screens build (doc 06 screens 031, 032 and 004; doc 01 Contenu sections 1.4 to 6; doc 05 Sensations sheets 18
    /// and 19): PF_UI_Today (welcome gift, Cadeau du jour box, wish rows, 28-day strip, journal row), PF_UI_WeeklyJournal
    /// (goals, completion chest, days left), the return gift block added to PF_UI_OfflineReturn, Atlas_Today (the screen's
    /// own art: gift box layers, calendar tile, paw stamp, chest; Atlas_CommonHUD is full, sprites review note), and the
    /// wiring in the vertical slice. Every sprite is made in house (SourceArt/UI/Tools/pw_ui_sprites_t36.py); every label
    /// uses a DEF_TextStyles role; texts come from the localization tables (DailyVisitText keys). Layout targets the
    /// 1080 x 2340 portrait reference; touch targets are 124 units (48 dp) or more. Rerunnable: prefabs are rebuilt, the
    /// gift block replaces its previous version.
    /// </summary>
    public static class DailyVisitUiPipeline
    {
        public const string Folder = ProgressionUiPipeline.Folder;
        public const string TodayPath = Folder + "PF_UI_Today.prefab";
        public const string JournalPath = Folder + "PF_UI_WeeklyJournal.prefab";
        public const string TodayAtlasPath = TextureImportRules.UiRoot + "Atlases/Atlas_Today.spriteatlasv2";
        public const string DiscoveryEggPath = "Assets/_Game/Content/Eggs/DEF_Egg_Discovery.asset";
        public const string CatalogPath = "Assets/_Game/Content/Visuals/DEF_CodexVisuals.asset";
        public const string GiftBlockName = "GiftBlock";

        /// <summary>Between Upgrades (21) and the windows (34).</summary>
        public const int TodaySortingOrder = 22;

        public const int JournalSortingOrder = 23;

        /// <summary>Art used only by Today and the journal: packed in Atlas_Today, kept out of Atlas_CommonHUD.</summary>
        public static readonly string[] TodayAtlasSprites =
        {
            "UI_GiftBox_Base", "UI_GiftBox_Band", "UI_GiftBox_Lid", "UI_Calendar_Box", "UI_PawStamp", "UI_Journal_Chest",
        };

        private static readonly string[] KitFiles =
        {
            "UI_GiftBox_Base", "UI_GiftBox_Band", "UI_GiftBox_Lid", "UI_Calendar_Box", "UI_PawStamp", "UI_Journal_Chest", "UI_Ring_Soft",
            "UI_Gesture_Tap", "UI_Egg_Shell", "UI_Egg_Spots",
        };

        private static readonly string[] IconFiles =
        {
            "ICO_Today", "ICO_Gift", "ICO_Wish", "ICO_Wish_Done", "ICO_Calendar_Days", "ICO_WeeklyJournal", "ICO_Ticket", "ICO_Seed", "ICO_Treat",
            "ICO_Treat_CloverCrunch", "ICO_Swap", "ICO_Decoration", "ICO_Info", "ICO_Check", "ICO_Paw", "ICO_Favorite_Heart",
        };

        // Warm tints of the screens (doc 05 palette; tokens for text and actions).
        private static readonly Color WarmCard = UiColorTokens.Hex(0xFFF1DA);
        private static readonly Color GoldGlow = new Color(1f, 0.83f, 0.42f, 0.6f);
        private static readonly Color StarGold = UiColorTokens.Hex(0xF2B544);
        private static readonly Color InkRose = new Color(0.898f, 0.537f, 0.612f, 1f);

        private static string Icon(string name) => UiPipeline.IconFolder + "/" + name + ".png";
        private static string Kit(string name) => UiPipeline.KitFolder + "/" + name + ".png";

        private static Sprite Load(string path) =>
            AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? throw new FileNotFoundException("Run SourceArt/UI/Tools/pw_ui_sprites_t36.py first.", path);

        [MenuItem("Pocket Wilds/UI/Build Daily Visit (T36)", priority = 205)]
        public static void BuildMenu() => Debug.Log("[DailyVisitUiPipeline] " + BuildAll());

        /// <summary>Standalone: tokens, atlases, texts, the two screens, the return gift block and the wiring.</summary>
        public static string BuildAll()
        {
            ImportSprites();
            var tokens = UiPipeline.EnsureTokens();
            UiPipeline.EnsureAtlas();
            UiPipeline.EnsureLocalization();
            return Extend(tokens);
        }

        /// <summary>
        /// The T36 part of a full UI build (UiPipeline.BuildAll calls it at its end, after the HUD and the offline return
        /// prefab are rebuilt): atlases, PF_UI_Today, PF_UI_WeeklyJournal, the gift block, the scene wiring.
        /// </summary>
        public static string Extend(UiColorTokens tokens)
        {
            ImportSprites();
            EnsureAtlases();
            var kit = new KitSprites(tokens);
            BuildTodayPrefab(kit);
            BuildJournalPrefab(kit);
            AddReturnGiftBlock(kit);
            AssetDatabase.SaveAssets();
            WireScene();
            return "PF_UI_Today, PF_UI_WeeklyJournal, the return gift block and Atlas_Today built and wired";
        }

        public static void ImportSprites()
        {
            foreach (var name in KitFiles)
                ImportOne(Kit(name));
            foreach (var name in IconFiles)
                ImportOne(Icon(name));
        }

        private static void ImportOne(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("Run SourceArt/UI/Tools/pw_ui_sprites_t36.py first.", path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        // ---------------- Atlases ----------------

        /// <summary>
        /// Atlas_Today packs the screen's own art (about a quarter of a 1024 page); Atlas_CommonHUD keeps every other Kit
        /// and Icons sprite, listed one by one so the Today art is never packed twice.
        /// </summary>
        public static void EnsureAtlases()
        {
            if (EditorSettings.spritePackerMode == SpritePackerMode.Disabled)
                EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;
            var today = new List<Object>();
            foreach (var name in TodayAtlasSprites)
                today.Add(AssetDatabase.LoadAssetAtPath<Texture2D>(Kit(name)) ?? throw new FileNotFoundException("Missing T36 sprite.", Kit(name)));
            WriteAtlas(TodayAtlasPath, today.ToArray());
            WriteAtlas(UiPipeline.AtlasPath, CommonAtlasPackables());
        }

        /// <summary>
        /// The Kit and Icons textures for Atlas_CommonHUD, the Today-only art left out (UiPipeline.EnsureAtlas can pack
        /// these instead of the two folders).
        /// </summary>
        public static Object[] CommonAtlasPackables()
        {
            var list = new List<Object>();
            foreach (var folder in new[] { UiPipeline.KitFolder, UiPipeline.IconFolder })
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (Path.GetDirectoryName(path)?.Replace('\\', '/') != folder)
                        continue;
                    if (Array.IndexOf(TodayAtlasSprites, Path.GetFileNameWithoutExtension(path)) >= 0)
                        continue;
                    // T37: the activity screens' art has its own atlas (Atlas_Activities).
                    if (ActivityUiPipeline.IsAtlasSprite(Path.GetFileNameWithoutExtension(path)))
                        continue;
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (texture != null)
                        list.Add(texture);
                }
            }
            return list.ToArray();
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
            public readonly Sprite Chevron = Load(UiPipeline.BackIconPath);
            public readonly Sprite Ring = Load(Kit("UI_Ring_Soft"));
            public readonly Sprite Gesture = Load(Kit("UI_Gesture_Tap"));
            public readonly Sprite BoxBase = Load(Kit("UI_GiftBox_Base"));
            public readonly Sprite BoxBand = Load(Kit("UI_GiftBox_Band"));
            public readonly Sprite BoxLid = Load(Kit("UI_GiftBox_Lid"));
            public readonly Sprite CalendarBox = Load(Kit("UI_Calendar_Box"));
            public readonly Sprite PawStamp = Load(Kit("UI_PawStamp"));
            public readonly Sprite Chest = Load(Kit("UI_Journal_Chest"));
            public readonly Sprite Heart = Load(Icon("ICO_Favorite_Heart"));
            public readonly Sprite Check = Load(Icon("ICO_Check"));
            public readonly Sprite Info = Load(Icon("ICO_Info"));
            public readonly Sprite Paw = Load(Icon("ICO_Paw"));
            public readonly Sprite Swap = Load(Icon("ICO_Swap"));
            public readonly Sprite Gems = Load(UiPipeline.GemsIconPath);

            public KitSprites(UiColorTokens tokens) => Tokens = tokens;

            /// <summary>The reward sprites (doc 06 icon inventory). ICO_Sticker and a flower sugar icon do not exist yet.</summary>
            public RewardIconSet Icons()
            {
                var egg = AssetDatabase.LoadAssetAtPath<EggDefinitionAsset>(DiscoveryEggPath);
                var set = new RewardIconSet
                {
                    gems = Gems,
                    light = Load(UiPipeline.LightIconPath),
                    ticket = Load(Icon("ICO_Ticket")),
                    treat = Load(Icon("ICO_Treat")),
                    seed = Load(Icon("ICO_Seed")),
                    eggShell = Load(Kit("UI_Egg_Shell")),
                    eggSpots = Load(Kit("UI_Egg_Spots")),
                    decoration = Load(Icon("ICO_Decoration")),
                    sticker = Load(Icon("ICO_Gift")),
                    flowerSugar = Load(Icon("ICO_Treat")),
                    bond = Heart,
                    items = new[]
                    {
                        new RewardIconSet.ItemIcon { sourceId = "treat.clover_crunch", sprite = Load(Icon("ICO_Treat_CloverCrunch")) },
                    },
                    discTints = new[]
                    {
                        Tokens.panelSecondary, UiColorTokens.Hex(0xDCEAF6), UiColorTokens.Hex(0xFFF0C8), UiColorTokens.Hex(0xF6E2C4),
                        UiColorTokens.Hex(0xF9E0E4), UiColorTokens.Hex(0xDDEFC8), UiColorTokens.Hex(0xFFF4DE), UiColorTokens.Hex(0xF6E2C4),
                        UiColorTokens.Hex(0xF3EBDD), UiColorTokens.Hex(0xF9E0E4), UiColorTokens.Hex(0xF9E0E4),
                    },
                };
                if (egg != null)
                {
                    set.eggShellColor = egg.shell;
                    set.eggSpotsColor = egg.spots;
                }
                return set;
            }
        }

        // ---------------- PF_UI_Today ----------------

        public static TodayScreen BuildTodayPrefab(UiColorTokens tokens) => BuildTodayPrefab(new KitSprites(tokens));

        private static TodayScreen BuildTodayPrefab(KitSprites kit)
        {
            var tokens = kit.Tokens;
            var root = new GameObject("PF_UI_Today", typeof(RectTransform));
            try
            {
                var sheet = Sheet(root, TodaySortingOrder, kit, 140f, 176f, out var canvas, out var content, out var group, out var safe, out var panel,
                    out var outside, out var close, out var closeLabel, out var scroll, out var viewport, out var list);
                var screen = root.AddComponent<TodayScreen>();

                var headerIcon = UiPipeline.AddImage("Icon", panel, Load(Icon("ICO_Today")), Color.white, Image.Type.Simple);
                UiPipeline.Place(headerIcon.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(112f, -80f), new Vector2(96f, 96f));
                var title = UiPipeline.AddText("Title", panel, TextRole.Heading, 66f, tokens.textPrimary, "Today", 56f);
                UiPipeline.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(176f, -80f), new Vector2(700f, 84f));

                // Welcome gift (days 1 to 3).
                var welcome = Section("Welcome", list, 260f, kit.Panel, WarmCard);
                var welcomeIcon = UiPipeline.AddImage("Icon", welcome, Load(Icon("ICO_Gift")), Color.white, Image.Type.Simple);
                UiPipeline.Place(welcomeIcon.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(92f, -138f), new Vector2(120f, 120f));
                var welcomeTitle = UiPipeline.AddText("Title", welcome, TextRole.Subheading, 46f, tokens.textPrimary, "Welcome gift", 38f);
                UiPipeline.Place(welcomeTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(176f, -50f), new Vector2(740f, 62f));
                // The day under the title: a heading never shrinks under 44 and « Cadeau de bienvenue » needs the whole line.
                var welcomeDay = UiPipeline.AddText("Day", welcome, TextRole.Caption, 38f, tokens.textSecondary, "Day 1", 36f);
                welcomeDay.alignment = TextAlignmentOptions.MidlineLeft;
                UiPipeline.Place(welcomeDay.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(178f, -98f), new Vector2(300f, 48f));
                var welcomeCells = new RewardCellView[TodayScreen.MaxWelcomeCells];
                for (int i = 0; i < welcomeCells.Length; i++)
                    welcomeCells[i] = Cell(welcome, "Cell" + i, new Vector2(0f, 1f), new Vector2(420f + (i - 1) * 132f, -182f), kit);
                var welcomeClaim = ProgressionUiPipeline.PrimaryButton(welcome, tokens, "Claim", Vector2.zero, new Vector2(330f, 124f), out _,
                    out var welcomeClaimLabel);
                UiPipeline.Place((RectTransform)welcomeClaim.transform, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-28f, -176f),
                    new Vector2(330f, 124f));
                ButtonText(welcomeClaimLabel, 44f);
                var welcomeDone = UiPipeline.Rect("Done", welcome);
                UiPipeline.Place(welcomeDone, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-28f, -176f), new Vector2(330f, 124f));
                var doneCheck = UiPipeline.AddImage("Check", welcomeDone, kit.Check, Color.white, Image.Type.Simple);
                UiPipeline.Place(doneCheck.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 22f), new Vector2(64f, 64f));
                var welcomeTomorrow = UiPipeline.AddText("Tomorrow", welcomeDone, TextRole.Caption, 38f, tokens.textSecondary, "See you tomorrow", 36f);
                welcomeTomorrow.alignment = TextAlignmentOptions.Center;
                UiPipeline.Place(welcomeTomorrow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -32f), new Vector2(250f, 46f));
                welcomeDone.gameObject.SetActive(false);

                var gift = BuildGiftCard(list, kit);
                var wishHeader = Section("Wishes", list, 112f, null, Color.clear);
                var wishTitle = UiPipeline.AddText("Title", wishHeader, TextRole.Subheading, 50f, tokens.textPrimary, "Wishes", 40f);
                UiPipeline.Place(wishTitle.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(540f, 70f));
                var claimAll = ProgressionUiPipeline.PrimaryButton(wishHeader, tokens, "ClaimAll", Vector2.zero, new Vector2(450f, 124f), out _,
                    out var claimAllLabel);
                UiPipeline.Place((RectTransform)claimAll.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(450f, 124f));
                ButtonText(claimAllLabel, 44f);
                claimAll.gameObject.SetActive(false);
                var swapUsed = Caption(list, "SwapUsed", tokens, "Free swap used. Next one tomorrow.");
                var waiting = Caption(list, "Waiting", tokens, "Your wishes are waiting");
                waiting.color = tokens.textPrimary;

                var rows = new WishRowView[TodayScreen.MaxWishRows];
                for (int i = 0; i < rows.Length; i++)
                    rows[i] = BuildWishRow(list, i, kit);

                var calendarCard = Section("Calendar", list, 400f, kit.Panel, Color.white);
                var calendar = BuildCalendar(calendarCard, kit, scroll);

                var journalRow = Section("Journal", list, 132f, kit.Panel, Color.white);
                var journalBack = journalRow.GetChild(0).GetComponent<Image>();
                journalBack.raycastTarget = true;
                var journalButton = journalBack.gameObject.AddComponent<Button>();
                UiPipeline.ConfigureButton(journalButton, journalBack);
                var journalIcon = UiPipeline.AddImage("Icon", journalRow, Load(Icon("ICO_WeeklyJournal")), Color.white, Image.Type.Simple);
                UiPipeline.Place(journalIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(82f, 0f), new Vector2(104f, 104f));
                var journalDot = UiPipeline.AddImage("Dot", journalRow, kit.Dot, tokens.errorSoft, Image.Type.Simple);
                UiPipeline.Place(journalDot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(124f, 38f), new Vector2(40f, 40f));
                journalDot.gameObject.SetActive(false);
                var journalLabel = UiPipeline.AddText("Label", journalRow, TextRole.Label, 46f, tokens.textPrimary, "Journal 2/7", 36f);
                UiPipeline.Place(journalLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(160f, 2f), new Vector2(640f, 64f));
                var chevron = UiPipeline.AddImage("Chevron", journalRow, kit.Chevron, tokens.textSecondary, Image.Type.Simple);
                UiPipeline.Place(chevron.rectTransform, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-60f, 0f), new Vector2(56f, 56f));
                chevron.rectTransform.localEulerAngles = new Vector3(0f, 0f, 180f);

                var burst = BuildBurst(content.transform, kit);
                var hint = BuildHint(content.transform, kit);

                var wishIcons = new Sprite[WishSystems.Count];
                var fallback = Load(Icon(WishIcons.Default));
                for (int i = 0; i < wishIcons.Length; i++)
                {
                    string path = Icon(WishIcons.SpriteName((WishSystem)i));
                    wishIcons[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? fallback;
                }

                Maskable(list.gameObject);
                screen.SetReferences(tokens, canvas, content, group, safe, panel, outside, close, closeLabel, title, scroll, viewport, list);
                screen.SetWelcomeReferences(welcome, welcomeTitle, welcomeDay, welcomeCells, welcomeClaim, welcomeClaimLabel, welcomeDone.gameObject,
                    welcomeTomorrow, 132f, 420f);
                screen.SetSectionReferences(gift, wishHeader, wishTitle, claimAll, claimAllLabel, swapUsed, waiting, rows, calendarCard, calendar, journalRow,
                    journalButton, journalLabel, journalDot.gameObject);
                screen.SetArtReferences(burst, hint, kit.Icons(), wishIcons, Load(Icon(WishIcons.Done)), kit.Paw,
                    AssetDatabase.LoadAssetAtPath<CodexVisualCatalog>(CatalogPath));
                screen.SetLayout(140f, 176f, 260f, 336f, 112f, 60f, 150f, 10f, 400f, 132f, 20f);
                content.SetActive(false);
                canvas.enabled = false;
                Directory.CreateDirectory(Folder);
                return PrefabUtility.SaveAsPrefabAsset(root, TodayPath).GetComponent<TodayScreen>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// The Cadeau du jour card: the box (base, band and lid on one frame; the lid under a parent pivoted at its own
        /// center, 0.73 of the frame), the info button, the title, the size word, the lot's cells, « Ouvrir », « À demain ».
        /// </summary>
        private static GiftBoxView BuildGiftCard(RectTransform list, KitSprites kit)
        {
            var tokens = kit.Tokens;
            var card = Section("Gift", list, 336f, kit.Panel, Color.white);
            const float frame = 240f;
            var box = UiPipeline.Rect("Box", card);
            // Pivot at the box's foot: the swings rock it on its base.
            UiPipeline.Place(box, new Vector2(0f, 1f), new Vector2(0.5f, 0.12f), new Vector2(148f, -168f - (0.5f - 0.12f) * frame), new Vector2(frame, frame));
            var baseImage = UiPipeline.AddImage("Base", box, kit.BoxBase, Color.white, Image.Type.Simple);
            UiPipeline.Stretch(baseImage.rectTransform);
            baseImage.raycastTarget = true;
            var boxButton = baseImage.gameObject.AddComponent<Button>();
            UiPipeline.ConfigureButton(boxButton, baseImage);
            var band = UiPipeline.AddImage("Band", box, kit.BoxBand, new Color(0.949f, 0.663f, 0.663f, 1f), Image.Type.Simple);
            UiPipeline.Stretch(band.rectTransform);
            var lidPivot = UiPipeline.Rect("LidPivot", box);
            UiPipeline.Place(lidPivot, new Vector2(0.5f, 0.73f), new Vector2(0.5f, 0.73f), Vector2.zero, new Vector2(frame, frame));
            var lid = UiPipeline.AddImage("Lid", lidPivot, kit.BoxLid, Color.white, Image.Type.Simple);
            UiPipeline.Stretch(lid.rectTransform);

            var infoImage = UiPipeline.AddImage("Info", card, kit.Circle, tokens.panelSecondary, Image.Type.Simple);
            infoImage.raycastTarget = true;
            UiPipeline.Place(infoImage.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-76f, -68f), new Vector2(124f, 124f));
            var info = infoImage.gameObject.AddComponent<Button>();
            UiPipeline.ConfigureButton(info, infoImage);
            var infoIcon = UiPipeline.AddImage("Icon", infoImage.rectTransform, kit.Info, Color.white, Image.Type.Simple);
            UiPipeline.Place(infoIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64f, 64f));

            var title = UiPipeline.AddText("Title", card, TextRole.Subheading, 48f, tokens.textPrimary, "Daily gift", 40f);
            UiPipeline.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(310f, -52f), new Vector2(480f, 64f));
            var sizeWord = UiPipeline.AddText("Size", card, TextRole.Label, 42f, tokens.textSecondary, "Nice gift", 36f);
            UiPipeline.Place(sizeWord.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(310f, -100f), new Vector2(480f, 56f));
            sizeWord.gameObject.SetActive(false);

            var cellsRoot = UiPipeline.Rect("Cells", card);
            UiPipeline.Place(cellsRoot, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(632f, -204f), new Vector2(600f, 160f));
            var cells = new RewardCellView[GiftBoxView.MaxCells];
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i] = Cell(cellsRoot, "Cell" + i, new Vector2(0.5f, 0.5f), new Vector2((i - 1.5f) * 140f, 0f), kit);
                cells[i].gameObject.SetActive(false);
            }
            var open = ProgressionUiPipeline.PrimaryButton(card, tokens, "Open", Vector2.zero, new Vector2(380f, 136f), out _, out var openLabel);
            UiPipeline.Place((RectTransform)open.transform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(632f, -196f), new Vector2(380f, 136f));
            var tomorrow = UiPipeline.AddText("Tomorrow", card, TextRole.Caption, 38f, tokens.textSecondary, "See you tomorrow", 36f);
            tomorrow.alignment = TextAlignmentOptions.Center;
            UiPipeline.Place(tomorrow.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(632f, -306f), new Vector2(560f, 44f));
            tomorrow.gameObject.SetActive(false);

            var view = card.gameObject.AddComponent<GiftBoxView>();
            view.SetReferences(card, boxButton, box, band, lidPivot, lid, title, sizeWord, open, openLabel, tomorrow, info, cells, 140f,
                new Vector2(88f, -112f));
            return view;
        }

        private static WishRowView BuildWishRow(RectTransform list, int index, KitSprites kit)
        {
            var tokens = kit.Tokens;
            var row = UiPipeline.Rect("Wish" + index, list);
            // Centered pivot: the claim pop scales around the row's middle.
            UiPipeline.Place(row, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -75f), new Vector2(952f, 150f));
            var back = UiPipeline.AddImage("Background", row, kit.Panel, Color.white, Image.Type.Sliced);
            back.pixelsPerUnitMultiplier = 1.6f;
            UiPipeline.Stretch(back.rectTransform);

            var disc = UiPipeline.AddImage("PortraitBack", row, kit.Circle, tokens.panelSecondary, Image.Type.Simple);
            UiPipeline.Place(disc.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(80f, 0f), new Vector2(120f, 120f));
            var portrait = UiPipeline.AddImage("Portrait", disc.rectTransform, kit.Paw, Color.white, Image.Type.Simple);
            UiPipeline.Place(portrait.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(104f, 104f));
            var badge = UiPipeline.AddImage("Badge", row, kit.Circle, Color.white, Image.Type.Simple);
            UiPipeline.Place(badge.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(126f, -40f), new Vector2(60f, 60f));
            var badgeIcon = UiPipeline.AddImage("Icon", badge.rectTransform, Load(Icon(WishIcons.Default)), Color.white, Image.Type.Simple);
            UiPipeline.Place(badgeIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(48f, 48f));

            var text = UiPipeline.AddText("Text", row, TextRole.Body, 38f, tokens.textPrimary, "Hatch 3 eggs", 36f);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.lineSpacing = -8f;
            UiPipeline.Place(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(170f, 24f), new Vector2(420f, 84f));
            var bar = UiPipeline.AddImage("Bar", row, kit.Pill, tokens.panelSecondary, Image.Type.Sliced);
            bar.pixelsPerUnitMultiplier = 4.4f;
            UiPipeline.Place(bar.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(170f, -42f), new Vector2(214f, 24f));
            var fill = UiPipeline.AddImage("Fill", bar.rectTransform, kit.Pill, tokens.accentPrimary, Image.Type.Sliced);
            fill.pixelsPerUnitMultiplier = 4.4f;
            UiPipeline.LeftFill(fill.rectTransform, 24f);
            var progress = UiPipeline.AddText("Progress", row, TextRole.Caption, 36f, tokens.textSecondary, "0/3");
            UiPipeline.Place(progress.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(396f, -42f), new Vector2(96f, 44f));
            var gemsRow = UiPipeline.Rect("Gems", row);
            UiPipeline.Place(gemsRow, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(498f, -42f), new Vector2(96f, 44f));
            var gemIcon = UiPipeline.AddImage("Icon", gemsRow, kit.Gems, Color.white, Image.Type.Simple);
            UiPipeline.Place(gemIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(42f, 42f));
            var gems = UiPipeline.AddText("Value", gemsRow, TextRole.NumberSmall, 36f, tokens.textPrimary, "3");
            UiPipeline.Place(gems.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(46f, 2f), new Vector2(54f, 44f));

            var claim = ProgressionUiPipeline.PrimaryButton(row, tokens, "Claim", Vector2.zero, new Vector2(330f, 124f), out _, out var claimLabel);
            UiPipeline.Place((RectTransform)claim.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(330f, 124f));
            ButtonText(claimLabel, 44f);
            claim.gameObject.SetActive(false);
            var swapBack = UiPipeline.AddImage("Swap", row, kit.Pill, tokens.panelSecondary, Image.Type.Sliced);
            swapBack.raycastTarget = true;
            swapBack.pixelsPerUnitMultiplier = 1.6f;
            UiPipeline.Place(swapBack.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(330f, 124f));
            var swap = swapBack.gameObject.AddComponent<Button>();
            UiPipeline.ConfigureButton(swap, swapBack);
            var swapIcon = UiPipeline.AddImage("Icon", swapBack.rectTransform, kit.Swap, Color.white, Image.Type.Simple);
            UiPipeline.Place(swapIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(50f, 0f), new Vector2(54f, 54f));
            var swapLabel = UiPipeline.AddText("Label", swapBack.rectTransform, TextRole.Button, 42f, tokens.textPrimary, "Swap", 36f);
            UiPipeline.Place(swapLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(92f, 4f), new Vector2(222f, 70f));
            swap.gameObject.SetActive(false);

            var view = row.gameObject.AddComponent<WishRowView>();
            view.SetReferences(row, portrait, badgeIcon, text, bar.rectTransform, fill.rectTransform, fill, progress, gemsRow, gems, claim, claimLabel, swap,
                swapLabel);
            row.gameObject.SetActive(false);
            return view;
        }

        /// <summary>The calendar card: title, « Tamponner » (or today's check), the 28-tile strip, « Demain : … », the days to the big box.</summary>
        private static CalendarStripView BuildCalendar(RectTransform card, KitSprites kit, ScrollRect outer)
        {
            var tokens = kit.Tokens;
            var title = UiPipeline.AddText("Title", card, TextRole.Subheading, 46f, tokens.textPrimary, "Calendar", 38f);
            UiPipeline.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(32f, -60f), new Vector2(420f, 64f));
            var stamp = ProgressionUiPipeline.PrimaryButton(card, tokens, "Stamp", Vector2.zero, new Vector2(340f, 124f), out _, out var stampLabel);
            UiPipeline.Place((RectTransform)stamp.transform, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-24f, -68f), new Vector2(340f, 124f));
            ButtonText(stampLabel, 44f);
            var doneRow = UiPipeline.Rect("Done", card);
            UiPipeline.Place(doneRow, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-24f, -62f), new Vector2(560f, 60f));
            var doneCheck = UiPipeline.AddImage("Check", doneRow, kit.Check, Color.white, Image.Type.Simple);
            UiPipeline.Place(doneCheck.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(54f, 54f));
            var done = UiPipeline.AddText("Label", doneRow, TextRole.Caption, 38f, tokens.textSecondary, "Stamped", 36f);
            done.alignment = TextAlignmentOptions.MidlineRight;
            UiPipeline.Place(done.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-64f, 2f), new Vector2(490f, 50f));
            doneRow.gameObject.SetActive(false);

            // The strip: its own scroll view and canvas (scrolling and today's breathing rebuild only the strip).
            const float pitch = 170f;
            var strip = UiPipeline.Rect("Strip", card);
            UiPipeline.Place(strip, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -136f), new Vector2(904f, 164f));
            strip.gameObject.AddComponent<RectMask2D>();
            var dragArea = strip.gameObject.AddComponent<Image>();
            dragArea.color = new Color(1f, 1f, 1f, 0f);
            dragArea.raycastTarget = true;
            var stripScroll = strip.gameObject.AddComponent<ScrollRect>();
            var stripContent = UiPipeline.Rect("Boxes", strip);
            stripContent.anchorMin = new Vector2(0f, 0f);
            stripContent.anchorMax = new Vector2(0f, 1f);
            stripContent.pivot = new Vector2(0f, 0.5f);
            stripContent.anchoredPosition = Vector2.zero;
            stripContent.sizeDelta = new Vector2(CalendarStripView.Boxes * pitch + 12f, 0f);
            stripContent.gameObject.AddComponent<Canvas>();
            stripContent.gameObject.AddComponent<GraphicRaycaster>();
            stripScroll.viewport = strip;
            stripScroll.content = stripContent;
            stripScroll.horizontal = true;
            stripScroll.vertical = false;
            stripScroll.movementType = ScrollRect.MovementType.Elastic;
            stripScroll.elasticity = 0.08f;
            stripScroll.inertia = true;
            stripScroll.decelerationRate = 0.12f;
            stripScroll.scrollSensitivity = 40f;

            var glow = UiPipeline.AddImage("Glow", stripContent, kit.Glow, GoldGlow, Image.Type.Simple);
            UiPipeline.Place(glow.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(6f + pitch * 0.5f, 0f), new Vector2(230f, 230f));
            glow.gameObject.SetActive(false);
            var boxes = new RectTransform[CalendarStripView.Boxes];
            var backs = new Image[boxes.Length];
            var days = new TMP_Text[boxes.Length];
            var icons = new Image[boxes.Length];
            var spots = new Image[boxes.Length];
            var amounts = new TMP_Text[boxes.Length];
            var stamps = new Image[boxes.Length];
            var bigMarks = new Image[boxes.Length];
            var buttons = new Button[boxes.Length];
            var eggShell = Load(Kit("UI_Egg_Shell"));
            var eggSpots = Load(Kit("UI_Egg_Spots"));
            for (int i = 0; i < boxes.Length; i++)
            {
                var box = UiPipeline.Rect("Box" + (i + 1), stripContent);
                UiPipeline.Place(box, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(6f + pitch * 0.5f + i * pitch, 0f), new Vector2(158f, 150f));
                backs[i] = UiPipeline.AddImage("Tile", box, kit.CalendarBox, Color.white, Image.Type.Simple);
                backs[i].preserveAspect = false;
                backs[i].raycastTarget = true;
                UiPipeline.Stretch(backs[i].rectTransform);
                buttons[i] = backs[i].gameObject.AddComponent<Button>();
                UiPipeline.ConfigureButton(buttons[i], backs[i]);
                days[i] = UiPipeline.AddText("Day", box, TextRole.Caption, 36f, tokens.textSecondary, (i + 1).ToString());
                days[i].alignment = TextAlignmentOptions.Center;
                UiPipeline.Place(days[i].rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -25f), new Vector2(120f, 34f));
                icons[i] = UiPipeline.AddImage("Icon", box, eggShell, Color.white, Image.Type.Simple);
                UiPipeline.Place(icons[i].rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), new Vector2(54f, 54f));
                spots[i] = UiPipeline.AddImage("Spots", icons[i].rectTransform, eggSpots, Color.white, Image.Type.Simple);
                UiPipeline.Stretch(spots[i].rectTransform);
                spots[i].gameObject.SetActive(false);
                amounts[i] = UiPipeline.AddText("Amount", box, TextRole.NumberSmall, 36f, tokens.textPrimary, "30 min");
                amounts[i].alignment = TextAlignmentOptions.Center;
                UiPipeline.Place(amounts[i].rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), new Vector2(152f, 36f));
                stamps[i] = UiPipeline.AddImage("Stamp", box, kit.PawStamp, Color.white, Image.Type.Simple);
                UiPipeline.Place(stamps[i].rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -82f), new Vector2(104f, 104f));
                stamps[i].gameObject.SetActive(false);
                bigMarks[i] = UiPipeline.AddImage("Big", box, kit.Star, StarGold, Image.Type.Simple);
                UiPipeline.Place(bigMarks[i].rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-14f, -12f), new Vector2(44f, 44f));
                bigMarks[i].gameObject.SetActive(false);
                boxes[i] = box;
            }
            var ink = UiPipeline.AddImage("Ink", stripContent, kit.Ring, InkRose, Image.Type.Simple);
            UiPipeline.Place(ink.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(6f + pitch * 0.5f, 0f), new Vector2(170f, 170f));
            ink.gameObject.SetActive(false);

            var tomorrowIcon = UiPipeline.AddImage("TomorrowIcon", card, eggShell, Color.white, Image.Type.Simple);
            UiPipeline.Place(tomorrowIcon.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(58f, -328f), new Vector2(50f, 50f));
            var tomorrowSpots = UiPipeline.AddImage("Spots", tomorrowIcon.rectTransform, eggSpots, Color.white, Image.Type.Simple);
            UiPipeline.Stretch(tomorrowSpots.rectTransform);
            tomorrowSpots.gameObject.SetActive(false);
            var tomorrow = UiPipeline.AddText("Tomorrow", card, TextRole.Body, 38f, tokens.textPrimary, "Tomorrow: 30 min", 36f);
            UiPipeline.Place(tomorrow.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(96f, -326f), new Vector2(820f, 52f));
            var bigStar = UiPipeline.AddImage("BigStar", card, kit.Star, StarGold, Image.Type.Simple);
            UiPipeline.Place(bigStar.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(58f, -372f), new Vector2(42f, 42f));
            var bigIn = UiPipeline.AddText("BigIn", card, TextRole.Caption, 38f, tokens.textSecondary, "6 more days", 36f);
            UiPipeline.Place(bigIn.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(96f, -370f), new Vector2(820f, 50f));

            var view = strip.gameObject.AddComponent<CalendarStripView>();
            view.SetReferences(stripScroll, stripContent, boxes, backs, days, icons, spots, amounts, stamps, bigMarks, buttons, glow, ink, pitch);
            view.SetCardReferences(outer, title, stamp, stampLabel, doneRow.gameObject, done, tomorrowIcon, tomorrowSpots, tomorrow, bigIn);
            return view;
        }

        // ---------------- PF_UI_WeeklyJournal ----------------

        public static WeeklyJournalScreen BuildJournalPrefab(UiColorTokens tokens) => BuildJournalPrefab(new KitSprites(tokens));

        private static WeeklyJournalScreen BuildJournalPrefab(KitSprites kit)
        {
            var tokens = kit.Tokens;
            var root = new GameObject("PF_UI_WeeklyJournal", typeof(RectTransform));
            try
            {
                var sheet = Sheet(root, JournalSortingOrder, kit, 256f, 176f, out var canvas, out var content, out var group, out var safe, out var panel,
                    out var outside, out var close, out var closeLabel, out var scroll, out _, out var list);
                var screen = root.AddComponent<WeeklyJournalScreen>();

                var icon = UiPipeline.AddImage("Icon", panel, Load(Icon("ICO_WeeklyJournal")), Color.white, Image.Type.Simple);
                UiPipeline.Place(icon.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(124f, -106f), new Vector2(120f, 120f));
                var title = UiPipeline.AddText("Title", panel, TextRole.Heading, 62f, tokens.textPrimary, "Your weekly journal", 50f);
                UiPipeline.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(204f, -84f), new Vector2(812f, 80f));
                var days = UiPipeline.AddText("Days", panel, TextRole.Body, 40f, tokens.textSecondary, "4 more days", 38f);
                UiPipeline.Place(days.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(204f, -148f), new Vector2(812f, 56f));
                // What each goal pays, said once (doc 01 section 5.1): « Chaque objectif » and its two cells, under the days.
                var each = UiPipeline.AddText("Each", panel, TextRole.Caption, 38f, tokens.textSecondary, "Each goal", 36f);
                each.alignment = TextAlignmentOptions.MidlineLeft;
                UiPipeline.Place(each.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(204f, -202f), new Vector2(300f, 52f));
                var each0 = Chip(panel, "Each0", new Vector2(512f, 0f), kit);
                var each1 = Chip(panel, "Each1", new Vector2(620f, 0f), kit);
                foreach (var chip in new[] { each0, each1 })
                {
                    var rect = (RectTransform)chip.transform;
                    rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                    rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -202f);
                }
                ((RectTransform)each1.transform).sizeDelta = new Vector2(190f, ((RectTransform)each1.transform).sizeDelta.y);

                // Last week's reached goals (paid at the next opening).
                var carry = Section("Carry", list, 236f, kit.Panel, WarmCard);
                var carryText = UiPipeline.AddText("Text", carry, TextRole.Body, 40f, tokens.textPrimary, "From last week", 36f);
                carryText.textWrappingMode = TextWrappingModes.Normal;
                UiPipeline.Place(carryText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(32f, -52f), new Vector2(620f, 70f));
                var carryCells = new RewardCellView[WeeklyJournalScreen.MaxCarryCells];
                for (int i = 0; i < carryCells.Length; i++)
                    carryCells[i] = Cell(carry, "Cell" + i, new Vector2(0f, 1f), new Vector2(330f + (i - 1) * 132f, -148f), kit);
                var carryClaim = ProgressionUiPipeline.PrimaryButton(carry, tokens, "Claim", Vector2.zero, new Vector2(330f, 124f), out _,
                    out var carryClaimLabel);
                UiPipeline.Place((RectTransform)carryClaim.transform, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-28f, -148f),
                    new Vector2(330f, 124f));
                ButtonText(carryClaimLabel, 44f);
                carry.gameObject.SetActive(false);

                var rows = new JournalGoalRowView[WeeklyJournalScreen.MaxGoals];
                for (int i = 0; i < rows.Length; i++)
                    rows[i] = BuildGoalRow(list, i, kit);

                // The completion chest: 15 Gems and a Discovery egg (doc 01 section 5.1).
                var chestCard = Section("Chest", list, 270f, kit.Panel, WarmCard);
                var chestGlow = UiPipeline.AddImage("Glow", chestCard, kit.Glow, GoldGlow, Image.Type.Simple);
                UiPipeline.Place(chestGlow.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(140f, -135f), new Vector2(300f, 300f));
                chestGlow.gameObject.SetActive(false);
                var chest = UiPipeline.AddImage("Chest", chestCard, kit.Chest, Color.white, Image.Type.Simple);
                const float chestSize = 200f;
                UiPipeline.Place(chest.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.12f), new Vector2(140f, -135f - (0.5f - 0.12f) * chestSize),
                    new Vector2(chestSize, chestSize));
                var chestTitle = UiPipeline.AddText("Title", chestCard, TextRole.Subheading, 46f, tokens.textPrimary, "Journal complete", 38f);
                UiPipeline.Place(chestTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(270f, -56f), new Vector2(640f, 64f));
                var chestCells = new RewardCellView[WeeklyJournalScreen.MaxChestCells];
                for (int i = 0; i < chestCells.Length; i++)
                    chestCells[i] = Cell(chestCard, "Cell" + i, new Vector2(0f, 1f), new Vector2(470f + (i - 0.5f) * 132f, -172f), kit);
                var chestClaim = ProgressionUiPipeline.PrimaryButton(chestCard, tokens, "Claim", Vector2.zero, new Vector2(330f, 124f), out _,
                    out var chestClaimLabel);
                UiPipeline.Place((RectTransform)chestClaim.transform, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-28f, -172f),
                    new Vector2(330f, 124f));
                ButtonText(chestClaimLabel, 44f);
                chestClaim.gameObject.SetActive(false);
                var chestProgress = UiPipeline.AddText("Progress", chestCard, TextRole.NumberSmall, 46f, tokens.textSecondary, "0/7");
                chestProgress.alignment = TextAlignmentOptions.MidlineRight;
                UiPipeline.Place(chestProgress.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-40f, -172f), new Vector2(200f, 60f));
                var chestCheck = UiPipeline.AddImage("Check", chestCard, kit.Check, Color.white, Image.Type.Simple);
                UiPipeline.Place(chestCheck.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-110f, -172f), new Vector2(92f, 92f));
                chestCheck.gameObject.SetActive(false);

                var burst = BuildBurst(content.transform, kit);
                var slotIcons = new[]
                {
                    Load(UiPipeline.CodexIconPath), Load(Icon("ICO_Ticket")), Load(Icon("ICO_Today")), Load(Icon("ICO_Seed")), kit.Heart,
                    Load(Icon("ICO_ZoneSign")), kit.Paw,
                };

                Maskable(list.gameObject);
                screen.SetReferences(tokens, canvas, content, group, safe, panel, outside, close, closeLabel, title, days, scroll, list);
                screen.SetSectionReferences(carry, carryText, carryCells, carryClaim, carryClaimLabel, rows, chestCard, chest.rectTransform, chestGlow,
                    chestTitle, chestProgress, chestCells, chestClaim, chestClaimLabel, chestCheck);
                screen.SetArtReferences(burst, kit.Icons(), slotIcons);
                screen.SetEachGoalReferences(each, each0, each1);
                screen.SetLayout(256f, 176f, 236f, 144f, 10f, 270f, 22f, 132f, 330f, 470f);
                content.SetActive(false);
                canvas.enabled = false;
                Directory.CreateDirectory(Folder);
                return PrefabUtility.SaveAsPrefabAsset(root, JournalPath).GetComponent<WeeklyJournalScreen>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static JournalGoalRowView BuildGoalRow(RectTransform list, int index, KitSprites kit)
        {
            var tokens = kit.Tokens;
            var row = UiPipeline.Rect("Goal" + index, list);
            UiPipeline.Place(row, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -72f), new Vector2(952f, 144f));
            var back = UiPipeline.AddImage("Background", row, kit.Panel, Color.white, Image.Type.Sliced);
            back.pixelsPerUnitMultiplier = 1.6f;
            UiPipeline.Stretch(back.rectTransform);
            var disc = UiPipeline.AddImage("SlotBack", row, kit.Circle, tokens.panelSecondary, Image.Type.Simple);
            UiPipeline.Place(disc.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(72f, 0f), new Vector2(106f, 106f));
            var slotIcon = UiPipeline.AddImage("Icon", disc.rectTransform, kit.Paw, Color.white, Image.Type.Simple);
            UiPipeline.Place(slotIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(72f, 72f));
            var text = UiPipeline.AddText("Text", row, TextRole.Body, 38f, tokens.textPrimary, "Hatch 30 eggs", 36f);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.lineSpacing = -8f;
            UiPipeline.Place(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(142f, 24f), new Vector2(480f, 82f));
            var bar = UiPipeline.AddImage("Bar", row, kit.Pill, tokens.panelSecondary, Image.Type.Sliced);
            bar.pixelsPerUnitMultiplier = 4.4f;
            UiPipeline.Place(bar.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(142f, -42f), new Vector2(330f, 24f));
            var fill = UiPipeline.AddImage("Fill", bar.rectTransform, kit.Pill, tokens.accentPrimary, Image.Type.Sliced);
            fill.pixelsPerUnitMultiplier = 4.4f;
            UiPipeline.LeftFill(fill.rectTransform, 24f);
            var progress = UiPipeline.AddText("Progress", row, TextRole.Caption, 36f, tokens.textSecondary, "0/30");
            UiPipeline.Place(progress.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(484f, -42f), new Vector2(120f, 44f));
            var reward0 = Chip(row, "Reward0", new Vector2(462f, -42f), kit);
            var reward1 = Chip(row, "Reward1", new Vector2(566f, -42f), kit);
            reward0.gameObject.SetActive(false);
            reward1.gameObject.SetActive(false);

            var claim = ProgressionUiPipeline.PrimaryButton(row, tokens, "Claim", Vector2.zero, new Vector2(310f, 124f), out _, out var claimLabel);
            UiPipeline.Place((RectTransform)claim.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(310f, 124f));
            ButtonText(claimLabel, 44f);
            claim.gameObject.SetActive(false);
            var check = UiPipeline.AddImage("Check", row, kit.Check, Color.white, Image.Type.Simple);
            UiPipeline.Place(check.rectTransform, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-128f, 0f), new Vector2(84f, 84f));
            check.gameObject.SetActive(false);

            // « Tu as déjà commencé »: a soft green tag in the right column until the goal is reached (first journal).
            var headStart = UiPipeline.AddImage("HeadStart", row, kit.Pill, tokens.success, Image.Type.Sliced);
            headStart.pixelsPerUnitMultiplier = 1.6f;
            UiPipeline.Place(headStart.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(300f, 100f));
            var headStartLabel = UiPipeline.AddText("Label", headStart.rectTransform, TextRole.Label, 36f, tokens.backgroundLight, "Off to a good start");
            headStartLabel.alignment = TextAlignmentOptions.Center;
            headStartLabel.textWrappingMode = TextWrappingModes.Normal;
            headStartLabel.lineSpacing = -10f;
            UiPipeline.Place(headStartLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(276f, 90f));
            headStart.gameObject.SetActive(false);

            var view = row.gameObject.AddComponent<JournalGoalRowView>();
            view.SetReferences(row, slotIcon, text, bar.rectTransform, fill.rectTransform, fill, progress, reward0, reward1, claim, claimLabel, check,
                headStart.gameObject, headStartLabel);
            row.gameObject.SetActive(false);
            return view;
        }

        // ---------------- Return gift (screen 004) ----------------

        /// <summary>
        /// Adds (or rebuilds) the gift block under the Light of PF_UI_OfflineReturn: the gift icon with « Un cadeau pour toi »,
        /// five reward cells, four « Pendant ton absence » lines. Hidden in the prefab; the screen shows it for a long absence.
        /// </summary>
        public static void AddReturnGiftBlock(UiColorTokens tokens) => AddReturnGiftBlock(new KitSprites(tokens));

        private static void AddReturnGiftBlock(KitSprites kit)
        {
            var tokens = kit.Tokens;
            var root = PrefabUtility.LoadPrefabContents(UiPipeline.OfflineReturnPrefabPath);
            try
            {
                var screen = root.GetComponent<OfflineReturnScreen>() ?? throw new InvalidOperationException("PF_UI_OfflineReturn has no OfflineReturnScreen.");
                var card = root.transform.Find("Content/Card") as RectTransform ?? throw new InvalidOperationException("PF_UI_OfflineReturn: Content/Card missing.");
                var previous = card.Find(GiftBlockName);
                if (previous != null)
                    Object.DestroyImmediate(previous.gameObject);

                var block = UiPipeline.Rect(GiftBlockName, card);
                UiPipeline.Place(block, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -350f), new Vector2(720f, 270f));
                var icon = UiPipeline.AddImage("Icon", block, Load(Icon("ICO_Gift")), Color.white, Image.Type.Simple);
                UiPipeline.Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-250f, -40f), new Vector2(76f, 76f));
                // Label style (36 to 46): « Un grand cadeau pour toi » and its icon fit the 744-wide card.
                var subtitle = UiPipeline.AddText("Subtitle", block, TextRole.Label, 46f, tokens.textPrimary, "A gift for you", 36f);
                UiPipeline.Place(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(40f, -40f), new Vector2(560f, 64f));
                var cells = new RewardCellView[OfflineReturnScreen.MaxGiftCells];
                for (int i = 0; i < cells.Length; i++)
                    cells[i] = Cell(block, "Cell" + i, new Vector2(0.5f, 1f), new Vector2((i - 2f) * 132f, -150f), kit);
                var lines = new TMP_Text[OfflineReturnScreen.MaxGiftLines];
                for (int i = 0; i < lines.Length; i++)
                {
                    lines[i] = UiPipeline.AddText("Line" + i, block, TextRole.Caption, 38f, tokens.textSecondary, "3 wishes are waiting", 36f);
                    lines[i].alignment = TextAlignmentOptions.Center;
                    UiPipeline.Place(lines[i].rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -258f - i * 52f),
                        new Vector2(700f, 48f));
                    lines[i].gameObject.SetActive(false);
                }
                block.gameObject.SetActive(false);
                screen.SetGiftReferences(block.gameObject, icon.rectTransform, subtitle, cells, lines, kit.Icons(), 132f, 290f, 52f);
                EditorUtility.SetDirty(screen);
                PrefabUtility.SaveAsPrefabAsset(root, UiPipeline.OfflineReturnPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ---------------- Shared parts ----------------

        /// <summary>
        /// A bottom sheet (Upgrades' look): overlay canvas, a light dim above that closes it, the cream panel running under
        /// the gesture bar, its handle, a scrolled list with its own canvas between the header and the footer, one Close at
        /// the thumb.
        /// </summary>
        private static RectTransform Sheet(GameObject root, int order, KitSprites kit, float header, float footer, out Canvas canvas, out GameObject content,
            out CanvasGroup group, out RectTransform safe, out RectTransform panel, out Button outside, out Button close, out TMP_Text closeLabel,
            out ScrollRect scroll, out RectTransform viewport, out RectTransform list)
        {
            var tokens = kit.Tokens;
            canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            UiPipeline.AddScaler(root);
            root.AddComponent<GraphicRaycaster>();

            var contentRect = UiPipeline.Rect("Content", root.transform);
            UiPipeline.Stretch(contentRect);
            content = contentRect.gameObject;
            group = content.AddComponent<CanvasGroup>();
            var outsideImage = UiPipeline.Rect("Outside", contentRect).gameObject.AddComponent<Image>();
            UiPipeline.Stretch(outsideImage.rectTransform);
            var dim = tokens.darkOverlay;
            dim.a = 0.16f;
            outsideImage.color = dim;
            outsideImage.raycastTarget = true;
            outsideImage.maskable = false;
            outside = outsideImage.gameObject.AddComponent<Button>();
            outside.transition = Selectable.Transition.None;
            var nav = outside.navigation;
            nav.mode = Navigation.Mode.None;
            outside.navigation = nav;

            safe = UiPipeline.Rect("SafeArea", contentRect);
            UiPipeline.Stretch(safe);
            panel = UiPipeline.Rect("Panel", safe);
            panel.anchorMin = new Vector2(0f, 0f);
            panel.anchorMax = new Vector2(1f, 0f);
            panel.pivot = new Vector2(0.5f, 0f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(0f, 1700f);
            var back = UiPipeline.AddImage("Background", panel, kit.Panel, tokens.backgroundLight, Image.Type.Sliced);
            UiPipeline.Stretch(back.rectTransform);
            back.rectTransform.offsetMin = new Vector2(0f, -260f);
            back.raycastTarget = true;
            var handle = UiPipeline.AddImage("Handle", panel, kit.Pill, tokens.panelSecondary, Image.Type.Sliced);
            handle.pixelsPerUnitMultiplier = 6f;
            UiPipeline.Place(handle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -24f), new Vector2(140f, 18f));

            viewport = UiPipeline.Rect("Viewport", panel);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(0f, footer);
            viewport.offsetMax = new Vector2(0f, -header);
            viewport.gameObject.AddComponent<RectMask2D>();
            var dragArea = viewport.gameObject.AddComponent<Image>();
            dragArea.color = new Color(1f, 1f, 1f, 0f);
            dragArea.raycastTarget = true;
            list = UiPipeline.Rect("List", viewport);
            list.anchorMin = new Vector2(0.5f, 1f);
            list.anchorMax = new Vector2(0.5f, 1f);
            list.pivot = new Vector2(0.5f, 1f);
            list.anchoredPosition = Vector2.zero;
            list.sizeDelta = new Vector2(952f, 1200f);
            // Own canvas (as the Codex list): scrolling moves it without rebuilding the sheet; its raycaster keeps it tappable.
            list.gameObject.AddComponent<Canvas>();
            list.gameObject.AddComponent<GraphicRaycaster>();
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            UiPipeline.ConfigureScroll(scroll, viewport, list);

            var closeBack = UiPipeline.AddImage("Close", panel, kit.Pill, tokens.panelSecondary, Image.Type.Sliced);
            closeBack.raycastTarget = true;
            closeBack.pixelsPerUnitMultiplier = 1.6f;
            UiPipeline.Place(closeBack.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(440f, 124f));
            close = closeBack.gameObject.AddComponent<Button>();
            UiPipeline.ConfigureButton(close, closeBack);
            closeLabel = UiPipeline.AddText("Label", closeBack.rectTransform, TextRole.Button, 50f, tokens.textPrimary, "Close", 40f);
            closeLabel.alignment = TextAlignmentOptions.Center;
            UiPipeline.Place(closeLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(400f, 84f));
            return panel;
        }

        /// <summary>A card of the list: anchored at the list's top, pivot at its own top, 952 wide; a background when given.</summary>
        private static RectTransform Section(string name, RectTransform list, float height, Sprite background, Color tint)
        {
            var section = UiPipeline.Rect(name, list);
            UiPipeline.Place(section, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(952f, height));
            if (background != null)
            {
                var back = UiPipeline.AddImage("Background", section, background, tint, Image.Type.Sliced);
                back.pixelsPerUnitMultiplier = 1.4f;
                UiPipeline.Stretch(back.rectTransform);
            }
            return section;
        }

        private static TMP_Text Caption(RectTransform list, string name, UiColorTokens tokens, string sample)
        {
            var caption = UiPipeline.AddText(name, list, TextRole.Body, 40f, tokens.textSecondary, sample, 36f);
            caption.alignment = TextAlignmentOptions.Center;
            UiPipeline.Place(caption.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(900f, 60f));
            caption.gameObject.SetActive(false);
            return caption;
        }

        /// <summary>A reward cell (120 x 150): a soft disc with the icon (and the egg's spots), the amount under it.</summary>
        private static RewardCellView Cell(RectTransform parent, string name, Vector2 anchor, Vector2 position, KitSprites kit)
        {
            var tokens = kit.Tokens;
            var cell = UiPipeline.Rect(name, parent);
            UiPipeline.Place(cell, anchor, new Vector2(0.5f, 0.5f), position, new Vector2(124f, 152f));
            var group = cell.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            var disc = UiPipeline.AddImage("Disc", cell, kit.Circle, tokens.panelSecondary, Image.Type.Simple);
            UiPipeline.Place(disc.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 18f), new Vector2(108f, 108f));
            var icon = UiPipeline.AddImage("Icon", disc.rectTransform, kit.Gems, Color.white, Image.Type.Simple);
            UiPipeline.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(78f, 78f));
            var spots = UiPipeline.AddImage("Spots", icon.rectTransform, Load(Kit("UI_Egg_Spots")), Color.white, Image.Type.Simple);
            UiPipeline.Stretch(spots.rectTransform);
            spots.gameObject.SetActive(false);
            var amount = UiPipeline.AddText("Amount", cell, TextRole.NumberSmall, 40f, tokens.textPrimary, "0", 36f);
            amount.alignment = TextAlignmentOptions.Center;
            UiPipeline.Place(amount.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -56f), new Vector2(150f, 44f));
            var view = cell.gameObject.AddComponent<RewardCellView>();
            view.SetReferences(cell, group, disc, icon, spots, amount);
            return view;
        }

        /// <summary>A compact reward (journal rows): the icon and the amount side by side.</summary>
        private static RewardCellView Chip(RectTransform parent, string name, Vector2 position, KitSprites kit)
        {
            var tokens = kit.Tokens;
            var chip = UiPipeline.Rect(name, parent);
            UiPipeline.Place(chip, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), position, new Vector2(100f, 52f));
            var group = chip.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            var icon = UiPipeline.AddImage("Icon", chip, kit.Gems, Color.white, Image.Type.Simple);
            UiPipeline.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(46f, 46f));
            var spots = UiPipeline.AddImage("Spots", icon.rectTransform, Load(Kit("UI_Egg_Spots")), Color.white, Image.Type.Simple);
            UiPipeline.Stretch(spots.rectTransform);
            spots.gameObject.SetActive(false);
            var amount = UiPipeline.AddText("Amount", chip, TextRole.NumberSmall, 36f, tokens.textPrimary, "5");
            UiPipeline.Place(amount.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(50f, 2f), new Vector2(54f, 44f));
            var view = chip.gameObject.AddComponent<RewardCellView>();
            view.SetReferences(chip, group, null, icon, spots, amount);
            return view;
        }

        /// <summary>The UiBurst layer: full screen, its own passive canvas above the sheet, 24 pooled images.</summary>
        private static UiBurst BuildBurst(Transform parent, KitSprites kit)
        {
            var rect = UiPipeline.Rect("Burst", parent);
            UiPipeline.Stretch(rect);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.gameObject.AddComponent<Canvas>();
            var group = rect.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
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

        /// <summary>The tutorial paw of A12 (the HUD's TapHint, UI_Gesture_Tap): its own passive canvas above the sheet.</summary>
        private static TapHint BuildHint(Transform parent, KitSprites kit)
        {
            var hintRoot = UiPipeline.Rect("GiftHint", parent);
            hintRoot.gameObject.AddComponent<Canvas>();
            var group = hintRoot.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            group.alpha = 0f;
            UiPipeline.Place(hintRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320f, 320f));
            var ring = UiPipeline.AddImage("Ring", hintRoot, kit.Ring, new Color(1f, 1f, 1f, 0f), Image.Type.Simple);
            UiPipeline.Place(ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220f, 220f));
            var paw = UiPipeline.AddImage("Paw", hintRoot, kit.Gesture, Color.white, Image.Type.Simple);
            // The paw floats above the box (never hiding it) and taps down toward it; the ring spreads on the box.
            UiPipeline.Place(paw.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(170f, 170f));
            var hint = hintRoot.gameObject.AddComponent<TapHint>();
            hint.SetReferences(hintRoot, group, paw.rectTransform, ring);
            hintRoot.gameObject.SetActive(false);
            return hint;
        }

        /// <summary>A primary button's label at a smaller size (PrimaryButton auto-sizes from 56 down to 40).</summary>
        private static void ButtonText(TMP_Text label, float size)
        {
            label.fontSizeMax = size;
            label.fontSize = size;
            label.fontSizeMin = 36f;
        }

        /// <summary>Everything in a scrolled list is clipped by its viewport (the kit builds labels and images unmaskable).</summary>
        private static void Maskable(GameObject root)
        {
            foreach (var graphic in root.GetComponentsInChildren<MaskableGraphic>(true))
                graphic.maskable = true;
        }

        // ---------------- Scene ----------------

        /// <summary>Today and the journal in the vertical slice, and the scene entry's fields pointing at them.</summary>
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
            var today = Ensure<TodayScreen>(TodayPath, "Screen_Today");
            var journal = Ensure<WeeklyJournalScreen>(JournalPath, "Screen_WeeklyJournal");
            var so = new SerializedObject(entry);
            so.FindProperty("_today").objectReferenceValue = today;
            so.FindProperty("_weeklyJournal").objectReferenceValue = journal;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(entry);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
