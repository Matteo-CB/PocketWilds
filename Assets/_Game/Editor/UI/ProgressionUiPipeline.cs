using System;
using System.IO;
using PocketWilds.Presentation;
using PocketWilds.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.UI
{
    /// <summary>
    /// T35A UI build (doc 02 Améliorations, section 11): the HUD's Upgrades button with its dot and the refuge level
    /// badge (added while UiPipeline builds the HUD), PF_UI_Upgrades with its three PF_UI_UpgradeCard, PF_UI_ZoneOpen,
    /// PF_UI_RefugeLevel, PF_UI_LevelReward, PF_UI_BiomeGate, and their wiring in the vertical slice. Icons come from
    /// SourceArt/UI/Tools/pw_ui_sprites_t35a.py. Layout targets the 1080 x 2340 portrait reference like every screen.
    /// </summary>
    public static class ProgressionUiPipeline
    {
        public const string Folder = "Assets/_Game/Prefabs/UI/Screens/";
        public const string UpgradesPath = Folder + "PF_UI_Upgrades.prefab";
        public const string ZoneOpenPath = Folder + "PF_UI_ZoneOpen.prefab";
        public const string RefugeLevelPath = Folder + "PF_UI_RefugeLevel.prefab";
        public const string LevelRewardPath = Folder + "PF_UI_LevelReward.prefab";
        public const string BiomeGatePath = Folder + "PF_UI_BiomeGate.prefab";

        public const int UpgradesSortingOrder = 21;
        public const int WindowSortingOrder = 34;
        public const int RewardSortingOrder = 36;

        public static readonly string[] Icons =
        {
            "ICO_Upgrade", "ICO_Upgrade_Production", "ICO_Upgrade_Nests", "ICO_Upgrade_Curiosity", "ICO_ZoneSign", "ICO_RefugeLevel",
            "ICO_BiomeMastery", "ICO_Check",
        };

        /// <summary>Card icons by upgrade kind, and the soft tint of the circle behind each.</summary>
        private static readonly string[] CardIcons = { "ICO_Upgrade_Production", "ICO_Upgrade_Nests", "ICO_Upgrade_Curiosity" };
        private static readonly Color[] CardTints = { UiColorTokens.Hex(0xDDEFC8), UiColorTokens.Hex(0xF6E2C4), UiColorTokens.Hex(0xDCEAF6) };

        private static readonly Color GainColor = new Color(0.79f, 0.47f, 0.16f, 1f);
        private static readonly Color PawBrown = UiColorTokens.Hex(0x8A5A2E);

        private static string Icon(string name) => UiPipeline.IconFolder + "/" + name + ".png";
        private static string Kit(string name) => UiPipeline.KitFolder + "/" + name + ".png";

        private static Sprite Load(string path) =>
            AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? throw new FileNotFoundException("Run SourceArt/UI/Tools/pw_ui_sprites_t35a.py first.", path);

        [MenuItem("Pocket Wilds/UI/Build Progression Screens (T35A)", priority = 203)]
        public static void BuildMenu() => Debug.Log("[ProgressionUiPipeline] " + BuildAll());

        /// <summary>Rebuilds the HUD (with its T35A parts), the five screens and the wiring.</summary>
        public static string BuildAll()
        {
            ImportSprites();
            var tokens = UiPipeline.EnsureTokens();
            UiPipeline.EnsureAtlas();
            UiPipeline.EnsureLocalization();
            UiPipeline.BuildHudPrefab(tokens);
            BuildUpgradesPrefab(tokens);
            BuildZoneOpenPrefab(tokens);
            BuildRefugeLevelPrefab(tokens);
            BuildLevelRewardPrefab(tokens);
            BuildBiomeGatePrefab(tokens);
            AssetDatabase.SaveAssets();
            UiPipeline.SetupScene();
            WireScene();
            return "HUD progression parts, Upgrades, ZoneOpen, RefugeLevel, LevelReward, BiomeGate built and wired";
        }

        public static void ImportSprites()
        {
            foreach (var name in Icons)
            {
                string path = Icon(name);
                if (!File.Exists(path))
                    throw new FileNotFoundException("Run SourceArt/UI/Tools/pw_ui_sprites_t35a.py first.", path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
        }

        // ---------------- HUD parts (called by UiPipeline.BuildHudPrefab) ----------------

        /// <summary>
        /// The Upgrades button above the Codex (same size, the thumb's reach), its dot, the refuge level badge top left,
        /// and the Light chip made touchable (it opens Upgrades too). The button starts hidden: it appears after the
        /// third hatch.
        /// </summary>
        public static void AddHudProgression(RectTransform safe, RectTransform shortcuts, RectTransform chip, RefugeHud hud, UiColorTokens tokens)
        {
            var circle = Load(UiPipeline.CirclePath);
            var upgradeRoot = UiPipeline.Rect("Upgrade", shortcuts);
            UiPipeline.Place(upgradeRoot, Vector2.zero, Vector2.zero, new Vector2(52f, 344f), new Vector2(176f, 176f));
            var upgradeImage = UiPipeline.AddImage("Button", upgradeRoot, circle, tokens.panelPrimary, Image.Type.Simple);
            upgradeImage.raycastTarget = true;
            UiPipeline.Stretch(upgradeImage.rectTransform);
            var upgradeButton = upgradeImage.gameObject.AddComponent<Button>();
            UiPipeline.ConfigureButton(upgradeButton, upgradeImage);
            var upgradeIcon = UiPipeline.AddImage("Icon", upgradeImage.rectTransform, Load(Icon("ICO_Upgrade")), Color.white, Image.Type.Simple);
            UiPipeline.Place(upgradeIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(116f, 116f));
            var dot = UiPipeline.AddImage("Dot", upgradeRoot, Load(UiPipeline.DotPath), tokens.errorSoft, Image.Type.Simple);
            UiPipeline.Place(dot.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-26f, -26f), new Vector2(46f, 46f));
            dot.gameObject.SetActive(false);
            upgradeRoot.gameObject.SetActive(false);

            // Refuge level badge, top left: the medallion with the number; a ring and six paws for the level up.
            var levelRoot = UiPipeline.Rect("RefugeLevel", shortcuts);
            UiPipeline.Place(levelRoot, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(104f, -110f), new Vector2(150f, 150f));
            var ring = UiPipeline.AddImage("Ring", levelRoot, Load(Kit("UI_Ring_Soft")), tokens.currencyLight, Image.Type.Simple);
            UiPipeline.Place(ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(170f, 170f));
            ring.gameObject.SetActive(false);
            var paws = new RectTransform[6];
            var pawImages = new Image[6];
            for (int i = 0; i < paws.Length; i++)
            {
                pawImages[i] = UiPipeline.AddImage("Paw" + i, levelRoot, Load(Icon("ICO_Paw")), UiColorTokens.Hex(0xF2A93B), Image.Type.Simple);
                paws[i] = pawImages[i].rectTransform;
                UiPipeline.Place(paws[i], new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 40f));
                paws[i].gameObject.SetActive(false);
            }
            var medal = UiPipeline.AddImage("Medal", levelRoot, Load(Icon("ICO_RefugeLevel")), Color.white, Image.Type.Simple);
            medal.raycastTarget = true;
            UiPipeline.Stretch(medal.rectTransform);
            var levelButton = medal.gameObject.AddComponent<Button>();
            UiPipeline.ConfigureButton(levelButton, medal);
            var levelLabel = UiPipeline.AddText("Level", medal.rectTransform, TextRole.NumberLarge, 56f, tokens.textPrimary, "1", 36f);
            levelLabel.alignment = TextAlignmentOptions.Center;
            UiPipeline.Place(levelLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -12f), new Vector2(110f, 70f));
            // A reward card waits for a calm moment: a dot on the badge until then (touching the badge shows it).
            var levelDot = UiPipeline.AddImage("Dot", levelRoot, Load(UiPipeline.DotPath), tokens.errorSoft, Image.Type.Simple);
            UiPipeline.Place(levelDot.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-18f, -18f), new Vector2(42f, 42f));
            levelDot.gameObject.SetActive(false);

            // The Light chip opens Upgrades too: its background takes touches (own raycaster on its canvas).
            chip.gameObject.AddComponent<GraphicRaycaster>();
            var chipBack = chip.Find("Background").GetComponent<Image>();
            chipBack.raycastTarget = true;
            var lightButton = chipBack.gameObject.AddComponent<Button>();
            UiPipeline.ConfigureButton(lightButton, chipBack);

            hud.SetProgressionReferences(upgradeButton, upgradeRoot, dot.gameObject, levelButton, levelRoot, medal.rectTransform, levelLabel,
                ring.rectTransform, ring, paws, pawImages, lightButton, levelDot.gameObject);
        }

        // ---------------- Upgrades ----------------

        public static UpgradesScreen BuildUpgradesPrefab(UiColorTokens tokens)
        {
            var panelSprite = Load(UiPipeline.PanelPath);
            var pill = Load(UiPipeline.PillPath);
            var circle = Load(UiPipeline.CirclePath);
            var buttonSprite = Load(UiPipeline.ButtonPrimaryPath);
            var root = new GameObject("PF_UI_Upgrades", typeof(RectTransform));
            try
            {
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = UpgradesSortingOrder;
                UiPipeline.AddScaler(root);
                root.AddComponent<GraphicRaycaster>();
                var screen = root.AddComponent<UpgradesScreen>();

                var content = UiPipeline.Rect("Content", root.transform);
                UiPipeline.Stretch(content);
                var group = content.gameObject.AddComponent<CanvasGroup>();
                // Above the panel the refuge stays visible, barely dimmed; a touch there closes the panel.
                var outsideImage = UiPipeline.Rect("Outside", content).gameObject.AddComponent<Image>();
                UiPipeline.Stretch(outsideImage.rectTransform);
                var dim = tokens.darkOverlay;
                dim.a = 0.16f;
                outsideImage.color = dim;
                outsideImage.raycastTarget = true;
                outsideImage.maskable = false;
                var outside = outsideImage.gameObject.AddComponent<Button>();
                outside.transition = Selectable.Transition.None;
                var nav = outside.navigation;
                nav.mode = Navigation.Mode.None;
                outside.navigation = nav;

                var safe = UiPipeline.Rect("SafeArea", content);
                UiPipeline.Stretch(safe);
                var panel = UiPipeline.Rect("Panel", safe);
                panel.anchorMin = new Vector2(0f, 0f);
                panel.anchorMax = new Vector2(1f, 0f);
                panel.pivot = new Vector2(0.5f, 0f);
                panel.anchoredPosition = Vector2.zero;
                panel.sizeDelta = new Vector2(0f, 1560f);
                var back = UiPipeline.AddImage("Background", panel, panelSprite, tokens.backgroundLight, Image.Type.Sliced);
                UiPipeline.Stretch(back.rectTransform);
                // Runs on below the safe area to the screen's bottom edge (gesture bar), corners hidden there.
                back.rectTransform.offsetMin = new Vector2(0f, -260f);
                back.raycastTarget = true;
                var handle = UiPipeline.AddImage("Handle", panel, pill, tokens.panelSecondary, Image.Type.Sliced);
                handle.pixelsPerUnitMultiplier = 6f;
                UiPipeline.Place(handle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -28f), new Vector2(140f, 18f));

                var title = UiPipeline.AddText("Title", panel, TextRole.Heading, 68f, tokens.textPrimary, "Upgrades", 50f);
                UiPipeline.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(64f, -98f), new Vector2(560f, 90f));
                var close = CloseButton(panel, tokens, circle, new Vector2(-96f, -98f));
                var rateIcon = UiPipeline.AddImage("RateIcon", panel, Load(UiPipeline.LightIconPath), Color.white, Image.Type.Simple);
                UiPipeline.Place(rateIcon.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(98f, -186f), new Vector2(66f, 66f));
                var rate = UiPipeline.AddText("Rate", panel, TextRole.NumberSmall, 50f, tokens.textPrimary, "0/s");
                UiPipeline.Place(rate.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(140f, -184f), new Vector2(330f, 64f));

                var qty = new Button[3];
                var qtyBack = new Image[3];
                var qtyLabel = new TMP_Text[3];
                string[] qtyNames = { "× 1", "× 10", "Max" };
                for (int i = 0; i < 3; i++)
                {
                    qtyBack[i] = UiPipeline.AddImage("Qty" + i, panel, pill, tokens.panelSecondary, Image.Type.Sliced);
                    qtyBack[i].raycastTarget = true;
                    qtyBack[i].pixelsPerUnitMultiplier = 1.6f;
                    UiPipeline.Place(qtyBack[i].rectTransform, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-64f - (2 - i) * 164f, -186f),
                        new Vector2(150f, 92f));
                    qty[i] = qtyBack[i].gameObject.AddComponent<Button>();
                    UiPipeline.ConfigureButton(qty[i], qtyBack[i]);
                    qtyLabel[i] = UiPipeline.AddText("Label", qtyBack[i].rectTransform, TextRole.Button, 40f, tokens.textSecondary, qtyNames[i], 30f);
                    qtyLabel[i].alignment = TextAlignmentOptions.Center;
                    UiPipeline.Place(qtyLabel[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(136f, 70f));
                }

                var cards = new UpgradeCardView[3];
                for (int i = 0; i < 3; i++)
                    cards[i] = BuildCard(panel, i, tokens, panelSprite, pill, circle, buttonSprite);

                // Biome mastery.
                var mastery = UiPipeline.AddImage("Mastery", panel, panelSprite, tokens.panelSecondary, Image.Type.Sliced);
                mastery.pixelsPerUnitMultiplier = 1.6f;
                UiPipeline.Place(mastery.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -1100f), new Vector2(952f, 172f));
                var masteryIcon = UiPipeline.AddImage("Icon", mastery.rectTransform, Load(Icon("ICO_BiomeMastery")), Color.white, Image.Type.Simple);
                UiPipeline.Place(masteryIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(84f, 2f), new Vector2(112f, 112f));
                var masteryTitle = UiPipeline.AddText("Title", mastery.rectTransform, TextRole.Label, 42f, tokens.textPrimary, "Meadow mastery", 32f);
                UiPipeline.Place(masteryTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(164f, -40f), new Vector2(600f, 56f));
                var masteryLevel = UiPipeline.AddText("Level", mastery.rectTransform, TextRole.NumberSmall, 48f, tokens.textPrimary, "1");
                masteryLevel.alignment = TextAlignmentOptions.MidlineRight;
                UiPipeline.Place(masteryLevel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-40f, -40f), new Vector2(140f, 60f));
                var masteryBar = UiPipeline.AddImage("Bar", mastery.rectTransform, pill, tokens.backgroundLight, Image.Type.Sliced);
                masteryBar.pixelsPerUnitMultiplier = 4.4f;
                UiPipeline.Place(masteryBar.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(164f, -92f), new Vector2(748f, 28f));
                var masteryFill = UiPipeline.AddImage("Fill", masteryBar.rectTransform, pill, tokens.success, Image.Type.Sliced);
                masteryFill.pixelsPerUnitMultiplier = 4.4f;
                UiPipeline.LeftFill(masteryFill.rectTransform, 28f);
                var masteryNext = UiPipeline.AddText("Next", mastery.rectTransform, TextRole.Body, 34f, tokens.textSecondary, "Level 2: Cozy Nests", 28f);
                UiPipeline.Place(masteryNext.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(164f, -138f), new Vector2(748f, 46f));

                // Next zone.
                var zone = UiPipeline.AddImage("Zone", panel, panelSprite, tokens.panelSecondary, Image.Type.Sliced);
                zone.pixelsPerUnitMultiplier = 1.6f;
                UiPipeline.Place(zone.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -1296f), new Vector2(952f, 190f));
                var zoneIcon = UiPipeline.AddImage("Icon", zone.rectTransform, Load(Icon("ICO_ZoneSign")), Color.white, Image.Type.Simple);
                UiPipeline.Place(zoneIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(84f, 4f), new Vector2(120f, 120f));
                var zoneLabel = UiPipeline.AddText("Label", zone.rectTransform, TextRole.Label, 32f, tokens.textSecondary, "Next zone", 26f);
                UiPipeline.Place(zoneLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(164f, -38f), new Vector2(510f, 44f));
                var zoneTitle = UiPipeline.AddText("Title", zone.rectTransform, TextRole.Label, 42f, tokens.textPrimary, "The Flower Meadow", 30f);
                UiPipeline.Place(zoneTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(164f, -86f), new Vector2(510f, 56f));
                var priceRow = UiPipeline.Rect("Price", zone.rectTransform);
                UiPipeline.Place(priceRow, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(164f, -142f), new Vector2(400f, 60f));
                var priceIcon = UiPipeline.AddImage("Icon", priceRow, Load(UiPipeline.LightIconPath), Color.white, Image.Type.Simple);
                UiPipeline.Place(priceIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(54f, 54f));
                var zonePrice = UiPipeline.AddText("Value", priceRow, TextRole.NumberSmall, 44f, tokens.textPrimary, "0");
                UiPipeline.Place(zonePrice.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(66f, 2f), new Vector2(320f, 60f));
                var zoneCondition = UiPipeline.AddText("Condition", zone.rectTransform, TextRole.Body, 34f, tokens.textSecondary, "", 28f);
                UiPipeline.Place(zoneCondition.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(164f, -142f), new Vector2(510f, 50f));
                var seeImage = UiPipeline.AddImage("See", zone.rectTransform, buttonSprite, Color.white, Image.Type.Sliced);
                seeImage.raycastTarget = true;
                UiPipeline.Place(seeImage.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(230f, 128f));
                var see = seeImage.gameObject.AddComponent<Button>();
                UiPipeline.ConfigureButton(see, seeImage);
                var seeLabel = UiPipeline.AddText("Label", seeImage.rectTransform, TextRole.Button, 46f, tokens.textPrimary, "See", 34f);
                seeLabel.alignment = TextAlignmentOptions.Center;
                UiPipeline.Place(seeLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(200f, 80f));

                screen.SetReferences(tokens, canvas, content.gameObject, group, safe, panel, outside, close, title, rate, qty, qtyBack, qtyLabel, cards);
                screen.SetFooterReferences(masteryTitle, masteryLevel, masteryBar.rectTransform, masteryFill.rectTransform, masteryNext, zone.gameObject,
                    zoneLabel, zoneTitle, priceRow.gameObject, zonePrice, zoneCondition, see, seeLabel);
                content.gameObject.SetActive(false);
                canvas.enabled = false;
                Directory.CreateDirectory(Folder);
                UiPipeline.MaskScrollContent(root);
                return PrefabUtility.SaveAsPrefabAsset(root, UpgradesPath).GetComponent<UpgradesScreen>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>PF_UI_UpgradeCard: 952 x 250, icon in its tinted circle with the level under it, texts, buy button.</summary>
        private static UpgradeCardView BuildCard(RectTransform parent, int index, UiColorTokens tokens, Sprite panelSprite, Sprite pill, Sprite circle,
            Sprite buttonSprite)
        {
            var card = UiPipeline.Rect("Card" + index, parent);
            // Centered pivot: the purchase bounce scales around the card's middle.
            UiPipeline.Place(card, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -262f - index * 274f - 125f), new Vector2(952f, 250f));
            var back = UiPipeline.AddImage("Background", card, panelSprite, Color.white, Image.Type.Sliced);
            back.pixelsPerUnitMultiplier = 1.4f;
            UiPipeline.Stretch(back.rectTransform);

            var ring = UiPipeline.AddImage("Ring", card, Load(Kit("UI_Ring_Soft")), tokens.currencyLight, Image.Type.Simple);
            UiPipeline.Place(ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 420f));
            ring.gameObject.SetActive(false);

            var iconBack = UiPipeline.AddImage("IconBack", card, circle, CardTints[index], Image.Type.Simple);
            UiPipeline.Place(iconBack.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(104f, 14f), new Vector2(168f, 168f));
            var icon = UiPipeline.AddImage("Icon", iconBack.rectTransform, Load(Icon(CardIcons[index])), Color.white, Image.Type.Simple);
            UiPipeline.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(112f, 112f));
            var levelBack = UiPipeline.AddImage("LevelBack", card, pill, tokens.panelSecondary, Image.Type.Sliced);
            levelBack.pixelsPerUnitMultiplier = 2.4f;
            UiPipeline.Place(levelBack.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(104f, -84f), new Vector2(138f, 52f));
            var level = UiPipeline.AddText("Level", levelBack.rectTransform, TextRole.NumberSmall, 32f, tokens.textPrimary, "Lv. 0", 26f);
            level.alignment = TextAlignmentOptions.Center;
            UiPipeline.Place(level.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 3f), new Vector2(128f, 44f));

            var name = UiPipeline.AddText("Name", card, TextRole.Label, 44f, tokens.textPrimary, "Meadow Glow", 32f);
            UiPipeline.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(206f, -58f), new Vector2(420f, 60f));
            var gain = UiPipeline.AddText("Gain", card, TextRole.NumberSmall, 40f, GainColor, "+10% Light", 30f);
            UiPipeline.Place(gain.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(206f, -122f), new Vector2(420f, 56f));
            var detail = UiPipeline.AddText("Detail", card, TextRole.Body, 34f, tokens.textSecondary, "", 26f);
            UiPipeline.Place(detail.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(206f, -180f), new Vector2(420f, 50f));

            var buyImage = UiPipeline.AddImage("Buy", card, buttonSprite, tokens.accentPrimary, Image.Type.Sliced);
            buyImage.raycastTarget = true;
            UiPipeline.Place(buyImage.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(290f, 180f));
            var buy = buyImage.gameObject.AddComponent<Button>();
            UiPipeline.ConfigureButton(buy, buyImage);
            var priceRow = UiPipeline.Rect("Price", buyImage.rectTransform);
            UiPipeline.Place(priceRow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 26f), new Vector2(250f, 64f));
            var priceIcon = UiPipeline.AddImage("Icon", priceRow, Load(UiPipeline.LightIconPath), Color.white, Image.Type.Simple);
            UiPipeline.Place(priceIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(56f, 56f));
            var price = UiPipeline.AddText("Value", priceRow, TextRole.NumberSmall, 46f, tokens.textPrimary, "0", 30f);
            UiPipeline.Place(price.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(64f, 2f), new Vector2(186f, 60f));
            var levels = UiPipeline.AddText("Levels", buyImage.rectTransform, TextRole.Label, 32f, tokens.textPrimary, "+1 lv.", 26f);
            levels.alignment = TextAlignmentOptions.Center;
            UiPipeline.Place(levels.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -36f), new Vector2(250f, 44f));

            var state = UiPipeline.AddText("State", card, TextRole.Label, 36f, tokens.success, "Maxed", 28f);
            state.alignment = TextAlignmentOptions.Center;
            state.textWrappingMode = TextWrappingModes.Normal;
            UiPipeline.Place(state.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, -30f), new Vector2(290f, 100f));
            var stateIcon = UiPipeline.AddImage("Icon", state.rectTransform, Load(Icon("ICO_BiomeMastery")), Color.white, Image.Type.Simple);
            UiPipeline.Place(stateIcon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(68f, 68f));
            state.gameObject.SetActive(false);

            var best = UiPipeline.AddImage("Best", card, pill, tokens.success, Image.Type.Sliced);
            best.pixelsPerUnitMultiplier = 2.2f;
            UiPipeline.Place(best.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(22f, 4f), new Vector2(236f, 58f));
            var bestLabel = UiPipeline.AddText("Label", best.rectTransform, TextRole.Label, 32f, tokens.backgroundLight, "Best pick", 24f);
            bestLabel.alignment = TextAlignmentOptions.Center;
            UiPipeline.Place(bestLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 3f), new Vector2(216f, 48f));
            best.gameObject.SetActive(false);

            // Round motes rising from the button (round five-point stars for a milestone), animated by the card.
            var motesRoot = UiPipeline.Rect("Motes", card);
            UiPipeline.Place(motesRoot, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-169f, 40f), new Vector2(10f, 10f));
            var motes = new RectTransform[6];
            var moteImages = new Image[6];
            for (int i = 0; i < motes.Length; i++)
            {
                moteImages[i] = UiPipeline.AddImage("Mote" + i, motesRoot, Load(UiPipeline.DotPath), tokens.currencyLight, Image.Type.Simple);
                motes[i] = moteImages[i].rectTransform;
                UiPipeline.Place(motes[i], new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30f, 30f));
                motes[i].gameObject.SetActive(false);
            }

            var view = card.gameObject.AddComponent<UpgradeCardView>();
            view.SetReferences(card, name, level, gain, detail, buy, buyImage, priceRow.gameObject, price, levels, state, stateIcon, best.gameObject,
                bestLabel, motes, moteImages, Load(UiPipeline.DotPath), Load(UiPipeline.StarPath), ring.rectTransform, ring);
            return view;
        }

        // ---------------- Windows ----------------

        /// <summary>Overlay canvas, dim scrim (a button unless <paramref name="scrimCloses"/> is false), cream card.</summary>
        internal static RectTransform Window(GameObject root, int order, UiColorTokens tokens, Vector2 cardSize, float cardY, bool scrimCloses,
            out Canvas canvas, out GameObject content, out CanvasGroup group, out Button scrim)
        {
            canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            UiPipeline.AddScaler(root);
            root.AddComponent<GraphicRaycaster>();
            var contentRect = UiPipeline.Rect("Content", root.transform);
            UiPipeline.Stretch(contentRect);
            content = contentRect.gameObject;
            group = content.AddComponent<CanvasGroup>();
            var scrimImage = UiPipeline.Rect("Scrim", contentRect).gameObject.AddComponent<Image>();
            UiPipeline.Stretch(scrimImage.rectTransform);
            scrimImage.color = tokens.darkOverlay;
            scrimImage.raycastTarget = true;
            scrimImage.maskable = false;
            scrim = null;
            if (scrimCloses)
            {
                scrim = scrimImage.gameObject.AddComponent<Button>();
                scrim.transition = Selectable.Transition.None;
                var nav = scrim.navigation;
                nav.mode = Navigation.Mode.None;
                scrim.navigation = nav;
            }
            var card = UiPipeline.Rect("Card", contentRect);
            UiPipeline.Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, cardY), cardSize);
            var back = UiPipeline.AddImage("Background", card, Load(UiPipeline.PanelPath), tokens.backgroundLight, Image.Type.Sliced);
            UiPipeline.Stretch(back.rectTransform);
            back.raycastTarget = true;
            return card;
        }

        private static Button CloseButton(RectTransform parent, UiColorTokens tokens, Sprite circle, Vector2 position)
        {
            var image = UiPipeline.AddImage("Close", parent, circle, Color.white, Image.Type.Simple);
            image.raycastTarget = true;
            UiPipeline.Place(image.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), position, new Vector2(120f, 120f));
            var button = image.gameObject.AddComponent<Button>();
            UiPipeline.ConfigureButton(button, image);
            var x = UiPipeline.AddImage("X", image.rectTransform, Load(UiPipeline.CloseIconPath), tokens.textSecondary, Image.Type.Simple);
            UiPipeline.Place(x.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(54f, 54f));
            return button;
        }

        internal static Button PrimaryButton(RectTransform parent, UiColorTokens tokens, string name, Vector2 position, Vector2 size, out Image back,
            out TMP_Text label)
        {
            back = UiPipeline.AddImage(name, parent, Load(UiPipeline.ButtonPrimaryPath), tokens.accentPrimary, Image.Type.Sliced);
            back.raycastTarget = true;
            UiPipeline.Place(back.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), position, size);
            var button = back.gameObject.AddComponent<Button>();
            UiPipeline.ConfigureButton(button, back);
            label = UiPipeline.AddText("Label", back.rectTransform, TextRole.Button, 56f, tokens.textPrimary, "OK", 40f);
            label.alignment = TextAlignmentOptions.Center;
            UiPipeline.Place(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, PillCenter(size.y)),
                new Vector2(size.x - 50f, 96f));
            return button;
        }

        /// <summary>
        /// The visible pill of UI_Button_Primary_9Slice sits above its rect's center: the sprite (168 px of borders) has 6 px
        /// clear at the top and 35 px of soft shadow at the bottom, borders shrink with a rect under 168. Texts center on the
        /// pill, never on the rect (owner report 2026-10-11: « Jouer » touched the top edge).
        /// </summary>
        internal static float PillCenter(float height) => 14.5f * Mathf.Min(1f, height / 168f);

        /// <summary>Half the visible pill's height for a button of that height (see <see cref="PillCenter"/>).</summary>
        internal static float PillHalfHeight(float height) => (height - 41f * Mathf.Min(1f, height / 168f)) * 0.5f;

        /// <summary>A Light price: icon and value, centered on <paramref name="position"/> for a typical short value.</summary>
        private static TMP_Text PriceRow(RectTransform parent, UiColorTokens tokens, Vector2 anchor, Vector2 position, float size, out RectTransform iconRect)
        {
            var rect = UiPipeline.Rect("Price", parent);
            UiPipeline.Place(rect, anchor, new Vector2(0.5f, 0.5f), position, new Vector2(520f, size + 20f));
            var icon = UiPipeline.AddImage("Icon", rect, Load(UiPipeline.LightIconPath), Color.white, Image.Type.Simple);
            UiPipeline.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-70f, 0f), new Vector2(size + 8f, size + 8f));
            var value = UiPipeline.AddText("Value", rect, TextRole.NumberLarge, size, tokens.textPrimary, "0");
            UiPipeline.Place(value.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-58f, 4f), new Vector2(330f, size + 10f));
            iconRect = icon.rectTransform;
            return value;
        }

        public static ZoneOpenScreen BuildZoneOpenPrefab(UiColorTokens tokens)
        {
            var circle = Load(UiPipeline.CirclePath);
            var pill = Load(UiPipeline.PillPath);
            var root = new GameObject("PF_UI_ZoneOpen", typeof(RectTransform));
            try
            {
                var screen = root.AddComponent<ZoneOpenScreen>();
                var card = Window(root, WindowSortingOrder, tokens, new Vector2(860f, 1330f), 20f, true, out var canvas, out var content, out var group,
                    out var scrim);
                var close = CloseButton(card, tokens, circle, new Vector2(-74f, -74f));
                // Long zone names take two lines ("Open the Flower Meadow"): the title never runs under the close button.
                var title = Title(card, tokens, "Open the Orchard");

                var frame = UiPipeline.AddImage("Frame", card, Load(UiPipeline.PanelPath), tokens.panelSecondary, Image.Type.Sliced);
                frame.pixelsPerUnitMultiplier = 1.4f;
                UiPipeline.Place(frame.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -240f), new Vector2(760f, 380f));
                var capture = UiPipeline.AddImage("Capture", frame.rectTransform, null, Color.white, Image.Type.Simple);
                capture.preserveAspect = false;
                UiPipeline.Stretch(capture.rectTransform);
                capture.rectTransform.offsetMin = new Vector2(10f, 14f);
                capture.rectTransform.offsetMax = new Vector2(-10f, -10f);
                capture.gameObject.SetActive(false);
                var standIn = UiPipeline.AddImage("StandIn", frame.rectTransform, Load(Icon("ICO_ZoneSign")), Color.white, Image.Type.Simple);
                UiPipeline.Place(standIn.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(230f, 230f));

                var gainProduction = GainRow(card, tokens, "Production", Load(UiPipeline.LightIconPath), Color.white, -680f);
                var gainAnimals = GainRow(card, tokens, "Animals", Load(Icon("ICO_Paw")), PawBrown, -760f);
                var gainDecor = GainRow(card, tokens, "Decor", Load(Icon("ICO_ZoneSign")), Color.white, -840f);
                var price = PriceRow(card, tokens, new Vector2(0.5f, 1f), new Vector2(0f, -930f), 72f, out var priceIcon);

                var conditionRow = UiPipeline.Rect("Condition", card);
                UiPipeline.Place(conditionRow, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -988f), new Vector2(720f, 110f));
                var condition = UiPipeline.AddText("Text", conditionRow, TextRole.Body, 36f, tokens.textSecondary, "", 28f);
                condition.alignment = TextAlignmentOptions.Center;
                UiPipeline.Place(condition.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(720f, 54f));
                var bar = UiPipeline.AddImage("Bar", conditionRow, pill, tokens.panelSecondary, Image.Type.Sliced);
                bar.pixelsPerUnitMultiplier = 4.4f;
                UiPipeline.Place(bar.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(560f, 28f));
                var fill = UiPipeline.AddImage("Fill", bar.rectTransform, pill, tokens.success, Image.Type.Sliced);
                fill.pixelsPerUnitMultiplier = 4.4f;
                UiPipeline.LeftFill(fill.rectTransform, 28f);
                conditionRow.gameObject.SetActive(false);

                var open = PrimaryButton(card, tokens, "Open", new Vector2(0f, 52f), new Vector2(460f, 150f), out var openBack, out var openLabel);
                screen.SetReferences(tokens, canvas, content, group, card, scrim, close, title, capture, standIn.gameObject, gainProduction.text,
                    gainAnimals.text, gainDecor.row, gainDecor.text, priceIcon, price, conditionRow.gameObject, condition, bar.rectTransform, fill.rectTransform,
                    open, openBack, openLabel);
                content.SetActive(false);
                canvas.enabled = false;
                UiPipeline.MaskScrollContent(root);
                return PrefabUtility.SaveAsPrefabAsset(root, ZoneOpenPath).GetComponent<ZoneOpenScreen>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>A window's title: up to two centered lines between the card's edge and the close button.</summary>
        private static TMP_Text Title(RectTransform card, UiColorTokens tokens, string sample)
        {
            var title = UiPipeline.AddText("Title", card, TextRole.Heading, 62f, tokens.textPrimary, sample, 60f);
            title.alignment = TextAlignmentOptions.Center;
            title.textWrappingMode = TextWrappingModes.Normal;
            title.lineSpacing = -12f;
            UiPipeline.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -126f), new Vector2(600f, 160f));
            return title;
        }

        private static (GameObject row, TMP_Text text) GainRow(RectTransform card, UiColorTokens tokens, string name, Sprite icon, Color tint, float y)
        {
            var row = UiPipeline.Rect("Gain" + name, card);
            UiPipeline.Place(row, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(640f, 70f));
            var image = UiPipeline.AddImage("Icon", row, icon, tint, Image.Type.Simple);
            UiPipeline.Place(image.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(36f, 0f), new Vector2(64f, 64f));
            var text = UiPipeline.AddText("Text", row, TextRole.Body, 42f, tokens.textPrimary, "", 32f);
            UiPipeline.Place(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(90f, 2f), new Vector2(550f, 60f));
            return (row.gameObject, text);
        }

        public static RefugeLevelScreen BuildRefugeLevelPrefab(UiColorTokens tokens)
        {
            var circle = Load(UiPipeline.CirclePath);
            var pill = Load(UiPipeline.PillPath);
            var root = new GameObject("PF_UI_RefugeLevel", typeof(RectTransform));
            try
            {
                var screen = root.AddComponent<RefugeLevelScreen>();
                var card = Window(root, WindowSortingOrder, tokens, new Vector2(900f, 1600f), 0f, true, out var canvas, out var content, out var group,
                    out var scrim);
                var close = CloseButton(card, tokens, circle, new Vector2(-74f, -74f));
                var medal = UiPipeline.AddImage("Medal", card, Load(Icon("ICO_RefugeLevel")), Color.white, Image.Type.Simple);
                UiPipeline.Place(medal.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -160f), new Vector2(240f, 240f));
                var level = UiPipeline.AddText("Level", medal.rectTransform, TextRole.NumberLarge, 92f, tokens.textPrimary, "1", 56f);
                level.alignment = TextAlignmentOptions.Center;
                UiPipeline.Place(level.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -18f), new Vector2(170f, 110f));
                var title = UiPipeline.AddText("Title", card, TextRole.Heading, 58f, tokens.textPrimary, "Refuge level", 44f);
                title.alignment = TextAlignmentOptions.Center;
                UiPipeline.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -330f), new Vector2(720f, 80f));
                var bar = UiPipeline.AddImage("Bar", card, pill, tokens.panelSecondary, Image.Type.Sliced);
                bar.pixelsPerUnitMultiplier = 3.6f;
                UiPipeline.Place(bar.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -412f), new Vector2(640f, 34f));
                var fill = UiPipeline.AddImage("Fill", bar.rectTransform, pill, tokens.currencyLight, Image.Type.Sliced);
                fill.pixelsPerUnitMultiplier = 3.6f;
                UiPipeline.LeftFill(fill.rectTransform, 34f);
                var score = UiPipeline.AddText("Score", card, TextRole.NumberSmall, 40f, tokens.textSecondary, "0 / 20");
                score.alignment = TextAlignmentOptions.Center;
                UiPipeline.Place(score.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -474f), new Vector2(600f, 56f));

                var sourcesHeader = UiPipeline.AddText("ScoreHeader", card, TextRole.Subheading, 44f, tokens.textSecondary, "Score");
                var sourceRows = new RectTransform[RefugeLevelData.MaxSources];
                var sourceNames = new TMP_Text[sourceRows.Length];
                var sourceValues = new TMP_Text[sourceRows.Length];
                for (int i = 0; i < sourceRows.Length; i++)
                {
                    sourceRows[i] = UiPipeline.Rect("Source" + i, card);
                    sourceNames[i] = UiPipeline.AddText("Name", sourceRows[i], TextRole.Body, 40f, tokens.textPrimary, "Species", 30f);
                    UiPipeline.Place(sourceNames[i].rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(560f, 56f));
                    sourceValues[i] = UiPipeline.AddText("Value", sourceRows[i], TextRole.NumberSmall, 40f, tokens.textPrimary, "0");
                    sourceValues[i].alignment = TextAlignmentOptions.MidlineRight;
                    UiPipeline.Place(sourceValues[i].rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(200f, 56f));
                }
                var trackHeader = UiPipeline.AddText("RewardsHeader", card, TextRole.Subheading, 44f, tokens.textSecondary, "Rewards");
                var trackRows = new RectTransform[RefugeLevelData.MaxTrack];
                var checks = new Image[trackRows.Length];
                var dots = new Image[trackRows.Length];
                var levels = new TMP_Text[trackRows.Length];
                var rewards = new TMP_Text[trackRows.Length];
                for (int i = 0; i < trackRows.Length; i++)
                {
                    trackRows[i] = UiPipeline.Rect("Track" + i, card);
                    checks[i] = UiPipeline.AddImage("Check", trackRows[i], Load(Icon("ICO_Check")), Color.white, Image.Type.Simple);
                    UiPipeline.Place(checks[i].rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(34f, 0f), new Vector2(60f, 60f));
                    dots[i] = UiPipeline.AddImage("Dot", trackRows[i], Load(UiPipeline.DotPath), tokens.accentIdle, Image.Type.Simple);
                    UiPipeline.Place(dots[i].rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(34f, 0f), new Vector2(40f, 40f));
                    levels[i] = UiPipeline.AddText("Level", trackRows[i], TextRole.Label, 40f, tokens.textPrimary, "Lv. 2", 30f);
                    UiPipeline.Place(levels[i].rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(86f, 2f), new Vector2(170f, 56f));
                    rewards[i] = UiPipeline.AddText("Reward", trackRows[i], TextRole.Body, 38f, tokens.textPrimary, "", 28f);
                    UiPipeline.Place(rewards[i].rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(262f, 2f), new Vector2(500f, 56f));
                }

                screen.SetReferences(tokens, canvas, content, group, card, scrim, close, title, level, bar.rectTransform, fill.rectTransform, score);
                screen.SetRowReferences(sourcesHeader, sourceRows, sourceNames, sourceValues, trackHeader, trackRows, checks, dots, levels, rewards);
                content.SetActive(false);
                canvas.enabled = false;
                UiPipeline.MaskScrollContent(root);
                return PrefabUtility.SaveAsPrefabAsset(root, RefugeLevelPath).GetComponent<RefugeLevelScreen>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        public static LevelRewardScreen BuildLevelRewardPrefab(UiColorTokens tokens)
        {
            var root = new GameObject("PF_UI_LevelReward", typeof(RectTransform));
            try
            {
                var screen = root.AddComponent<LevelRewardScreen>();
                // The scrim never closes it: the rewards are collected with a touch on the button.
                var card = Window(root, RewardSortingOrder, tokens, new Vector2(760f, 700f), -40f, false, out var canvas, out var content, out var group,
                    out _);
                var glow = UiPipeline.AddImage("Glow", card, Load(UiPipeline.GlowPath), new Color(1f, 0.83f, 0.42f, 0.85f), Image.Type.Simple);
                UiPipeline.Place(glow.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(440f, 440f));
                var medal = UiPipeline.AddImage("Medal", card, Load(Icon("ICO_RefugeLevel")), Color.white, Image.Type.Simple);
                UiPipeline.Place(medal.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(250f, 250f));
                var level = UiPipeline.AddText("Level", medal.rectTransform, TextRole.NumberLarge, 96f, tokens.textPrimary, "2", 56f);
                level.alignment = TextAlignmentOptions.Center;
                UiPipeline.Place(level.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -18f), new Vector2(180f, 120f));
                var title = UiPipeline.AddText("Title", card, TextRole.Heading, 66f, tokens.textPrimary, "Level 2", 48f);
                title.alignment = TextAlignmentOptions.Center;
                UiPipeline.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -196f), new Vector2(660f, 90f));
                var gemsRow = UiPipeline.Rect("Gems", card);
                UiPipeline.Place(gemsRow, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -300f), new Vector2(560f, 90f));
                var gemIcon = UiPipeline.AddImage("Icon", gemsRow, Load(UiPipeline.GemsIconPath), Color.white, Image.Type.Simple);
                UiPipeline.Place(gemIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-96f, 0f), new Vector2(86f, 86f));
                var gems = UiPipeline.AddText("Value", gemsRow, TextRole.NumberLarge, 58f, tokens.textPrimary, "+5 Gems", 40f);
                UiPipeline.Place(gems.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-84f, 4f), new Vector2(360f, 80f));
                var unlocks = UiPipeline.AddText("Unlocks", card, TextRole.Body, 42f, tokens.textSecondary, "", 32f);
                unlocks.alignment = TextAlignmentOptions.Top;
                unlocks.textWrappingMode = TextWrappingModes.Normal;
                UiPipeline.Place(unlocks.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -370f), new Vector2(640f, 140f));
                var claim = PrimaryButton(card, tokens, "Claim", new Vector2(0f, 52f), new Vector2(460f, 150f), out _, out var claimLabel);
                screen.SetReferences(canvas, content, group, card, medal.rectTransform, level, title, gemsRow.gameObject, gemIcon.rectTransform, gems, unlocks,
                    claim, claimLabel);
                content.SetActive(false);
                canvas.enabled = false;
                UiPipeline.MaskScrollContent(root);
                return PrefabUtility.SaveAsPrefabAsset(root, LevelRewardPath).GetComponent<LevelRewardScreen>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        public static BiomeGateScreen BuildBiomeGatePrefab(UiColorTokens tokens)
        {
            var circle = Load(UiPipeline.CirclePath);
            var root = new GameObject("PF_UI_BiomeGate", typeof(RectTransform));
            try
            {
                var screen = root.AddComponent<BiomeGateScreen>();
                var card = Window(root, WindowSortingOrder, tokens, new Vector2(860f, 1140f), 30f, true, out var canvas, out var content, out var group,
                    out var scrim);
                var close = CloseButton(card, tokens, circle, new Vector2(-74f, -74f));
                var title = Title(card, tokens, "Open the Forest");
                // Six round silhouettes of the biome's species (a soft paw until their art is in the build).
                for (int i = 0; i < 6; i++)
                {
                    var spot = UiPipeline.AddImage("Silhouette" + i, card, Load(UiPipeline.DotPath), tokens.panelSecondary, Image.Type.Simple);
                    UiPipeline.Place(spot.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-300f + i * 120f, -276f),
                        new Vector2(104f, 104f));
                    var paw = UiPipeline.AddImage("Paw", spot.rectTransform, Load(Icon("ICO_Paw")), tokens.textDisabled, Image.Type.Simple);
                    UiPipeline.Place(paw.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(50f, 50f));
                }
                var rows = new GameObject[BiomeGateData.MaxConditions];
                var checks = new Image[rows.Length];
                var dots = new Image[rows.Length];
                var texts = new TMP_Text[rows.Length];
                for (int i = 0; i < rows.Length; i++)
                {
                    var row = UiPipeline.Rect("Condition" + i, card);
                    UiPipeline.Place(row, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -BiomeGateScreen.ConditionsTop - i * BiomeGateScreen.ConditionPitch),
                        new Vector2(720f, 76f));
                    checks[i] = UiPipeline.AddImage("Check", row, Load(Icon("ICO_Check")), Color.white, Image.Type.Simple);
                    UiPipeline.Place(checks[i].rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(32f, 0f), new Vector2(60f, 60f));
                    dots[i] = UiPipeline.AddImage("Dot", row, Load(UiPipeline.DotPath), tokens.accentIdle, Image.Type.Simple);
                    UiPipeline.Place(dots[i].rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(32f, 0f), new Vector2(40f, 40f));
                    texts[i] = UiPipeline.AddText("Text", row, TextRole.Body, 40f, tokens.textPrimary, "", 30f);
                    UiPipeline.Place(texts[i].rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(84f, 2f), new Vector2(630f, 60f));
                    rows[i] = row.gameObject;
                }
                var soon = UiPipeline.AddText("Soon", card, TextRole.Heading, 60f, UiColorTokens.Hex(0xC9782A), "Coming soon", 44f);
                soon.alignment = TextAlignmentOptions.Center;
                UiPipeline.Place(soon.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 100f), new Vector2(640f, 90f));
                var buyRow = UiPipeline.Rect("Buy", card);
                UiPipeline.Stretch(buyRow);
                var price = PriceRow(buyRow, tokens, new Vector2(0.5f, 0f), new Vector2(0f, 250f), 66f, out var priceIcon);
                var open = PrimaryButton(buyRow, tokens, "Open", new Vector2(0f, 52f), new Vector2(460f, 150f), out var openBack, out var openLabel);
                screen.SetReferences(tokens, canvas, content, group, card, scrim, close, title, rows, checks, dots, texts, soon, buyRow.gameObject, priceIcon,
                    price, open, openBack, openLabel);
                content.SetActive(false);
                canvas.enabled = false;
                UiPipeline.MaskScrollContent(root);
                return PrefabUtility.SaveAsPrefabAsset(root, BiomeGatePath).GetComponent<BiomeGateScreen>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ---------------- Scene ----------------

        /// <summary>The five screens in the vertical slice, and the scene entry's fields pointing at them.</summary>
        public static void WireScene()
        {
            var scene = EditorSceneManager.OpenScene(UiPipeline.ScenePath, OpenSceneMode.Single);
            var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
            if (entry == null)
                throw new InvalidOperationException("SceneEntry (VerticalSliceEntry) missing in " + UiPipeline.ScenePath);
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            void Wire<T>(string field, string path, string name) where T : MonoBehaviour
            {
                var existing = Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
                if (existing == null)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    existing = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, scene)).GetComponent<T>();
                    existing.name = name;
                }
                typeof(VerticalSliceEntry).GetField(field, flags).SetValue(entry, existing);
            }
            Wire<UpgradesScreen>("_upgrades", UpgradesPath, "Screen_Upgrades");
            Wire<ZoneOpenScreen>("_zoneOpen", ZoneOpenPath, "Screen_ZoneOpen");
            Wire<RefugeLevelScreen>("_refugeLevel", RefugeLevelPath, "Screen_RefugeLevel");
            Wire<LevelRewardScreen>("_levelReward", LevelRewardPath, "Screen_LevelReward");
            Wire<BiomeGateScreen>("_biomeGate", BiomeGatePath, "Screen_BiomeGate");
            EditorUtility.SetDirty(entry);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
