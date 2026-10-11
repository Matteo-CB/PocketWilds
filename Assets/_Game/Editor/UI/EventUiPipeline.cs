using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PocketWilds.Content;
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
    /// T48 screens: PF_UI_EventHub (screen 018: banner, currency, tabs, the track, the egg, the shop, the quests, the challenges,
    /// the personal goal, the Memory Shop, the announce page and the end summary card), the Event button of the HUD's right rail
    /// (built inside UiPipeline's HUD through <see cref="AddHudEvent"/>), and the hub's wiring in the vertical slice. Same rules
    /// as UiPipeline: 1080 x 2340, text styles by role, 30 pt floor, every label measured in en and fr, texts centered on the
    /// visible pill, scrolled content masked, content saved inactive. Run after UiPipeline.BuildAll and ShopUiPipeline.BuildAll.
    /// </summary>
    public static class EventUiPipeline
    {
        public const string HubPrefabPath = "Assets/_Game/Prefabs/UI/Screens/PF_UI_EventHub.prefab";
        public const int HubSortingOrder = 22;
        public const float TrackTop = 400f;
        public const float TierPitch = 210f;
        public const float ShopTop = 120f;
        public const float LinePitch = 190f;
        public const float QuestTop = 170f;
        public const float QuestPitch = 180f;
        public const float ChallengeTop = 300f;
        public const float ChallengePitch = 130f;
        public const float PageTop = 660f;

        private static readonly StringBuilder Problems = new StringBuilder();
        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        private static readonly Vector2 Top = new Vector2(0.5f, 1f);
        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        [MenuItem("Pocket Wilds/UI/Build Event Screens (T48)", priority = 216)]
        public static void BuildMenu() => Debug.Log("[EventUiPipeline] " + BuildAll());

        public static string BuildAll()
        {
            Problems.Clear();
            UiPipeline.EnsureLocalization();
            var tokens = UiPipeline.EnsureTokens();
            BuildHubPrefab(tokens);
            AssetDatabase.SaveAssets();
            WireScene();
            return "PF_UI_EventHub built and wired; " + (Problems.Length > 0 ? "text fit: " + Problems : "every label fits in en and fr.");
        }

        private static Sprite Icon(string name) => OutfitUiPipeline.FindSprite(name) ?? OutfitUiPipeline.FindSprite("ICO_Paw");

        private static Sprite Kit(string path) =>
            AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? throw new FileNotFoundException("Run SourceArt/UI/Tools/pw_ui_sprites.py first.", path);

        private static void Fit(TMP_Text label, params string[] keys) => Problems.Append(EventTexts.Fit(label, keys));

        // ---------------- HUD (called by UiPipeline.AddHudDaily) ----------------

        /// <summary>
        /// The Event button, third on the right rail (doc 01 section 1.3: Play, Today, Event), in Today's style: the cream circle,
        /// ICO_Event, the tier progress ring around it (UI_Bubble_Ring filled radially from the top, over a faint full ring), the
        /// dot of a reward to claim. Saved hidden; RefugeHud.SetEvent shows it during an event's announce and run.
        /// </summary>
        public static RectTransform AddHudEvent(RectTransform rail, RefugeHud hud, UiColorTokens tokens)
        {
            var circle = Kit(UiPipeline.CirclePath);
            var ringSprite = AssetDatabase.LoadAssetAtPath<Sprite>(UiPipeline.KitFolder + "/UI_Bubble_Ring.png") ?? circle;
            var root = UiPipeline.Rect("Event", rail);
            UiPipeline.Place(root, Top, Center, new Vector2(0f, -RefugeHud.RailPitch * 2.5f), new Vector2(UiPipeline.RailButtonSize, UiPipeline.RailButtonSize));
            var image = UiPipeline.AddImage("Button", root, circle, tokens.panelPrimary, Image.Type.Simple);
            image.raycastTarget = true;
            UiPipeline.Stretch(image.rectTransform);
            var button = image.gameObject.AddComponent<Button>();
            UiPipeline.ConfigureButton(button, image);
            var track = UiPipeline.AddImage("RingTrack", root, ringSprite, new Color(tokens.accentIdle.r, tokens.accentIdle.g, tokens.accentIdle.b, 0.9f), Image.Type.Simple);
            UiPipeline.Place(track.rectTransform, Center, Center, Vector2.zero, new Vector2(176f, 176f));
            var ring = UiPipeline.AddImage("Ring", root, ringSprite, tokens.accentPrimary, Image.Type.Filled);
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            ring.fillClockwise = true;
            ring.fillAmount = 0f;
            UiPipeline.Place(ring.rectTransform, Center, Center, Vector2.zero, new Vector2(176f, 176f));
            var icon = UiPipeline.AddImage("Icon", image.rectTransform, Icon("ICO_Event"), Color.white, Image.Type.Simple);
            UiPipeline.Place(icon.rectTransform, Center, Center, new Vector2(0f, 6f), new Vector2(112f, 112f));
            var dot = UiPipeline.AddImage("Dot", root, Kit(UiPipeline.DotPath), tokens.errorSoft, Image.Type.Simple);
            UiPipeline.Place(dot.rectTransform, new Vector2(1f, 1f), Center, new Vector2(-24f, -24f), new Vector2(46f, 46f));
            dot.gameObject.SetActive(false);
            root.gameObject.SetActive(false);
            hud.SetEventReferences(button, root, ring, dot.gameObject);
            return root;
        }

        // ---------------- Hub ----------------

        private sealed class Kitset
        {
            public UiColorTokens Tokens;
            public Sprite Panel, Pill, Circle, Check, Lock, Currency;
        }

        public static EventHubScreen BuildHubPrefab(UiColorTokens tokens)
        {
            var kit = new Kitset
            {
                Tokens = tokens, Panel = Kit(UiPipeline.PanelPath), Pill = Kit(UiPipeline.PillPath), Circle = Kit(UiPipeline.CirclePath),
                Check = Icon("ICO_Check"), Lock = Icon("ICO_Lock"), Currency = Icon("ICO_Event_Currency"),
            };
            var root = new GameObject("PF_UI_EventHub", typeof(RectTransform));
            try
            {
                var ui = new EventHubRefs { tokens = tokens };
                ui.canvas = root.AddComponent<Canvas>();
                ui.canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                ui.canvas.sortingOrder = HubSortingOrder;
                UiPipeline.AddScaler(root);
                root.AddComponent<GraphicRaycaster>();
                var screen = root.AddComponent<EventHubScreen>();
                var content = UiPipeline.Rect("Content", root.transform);
                UiPipeline.Stretch(content);
                ui.content = content.gameObject;
                ui.group = content.gameObject.AddComponent<CanvasGroup>();
                ui.safe = UiPipeline.Rect("SafeArea", content);
                UiPipeline.Stretch(ui.safe);

                BuildHub(ui, kit);
                BuildSummary(ui, kit);
                FillArt(ui);
                ui.trackTop = TrackTop;
                ui.tierPitch = TierPitch;
                ui.shopTop = ShopTop;
                ui.linePitch = LinePitch;
                ui.questTop = QuestTop;
                ui.questPitch = QuestPitch;
                ui.challengeTop = ChallengeTop;
                ui.challengePitch = ChallengePitch;
                screen.SetReferences(ui);
                content.gameObject.SetActive(false);
                ui.canvas.enabled = false;
                Directory.CreateDirectory(Path.GetDirectoryName(HubPrefabPath));
                UiPipeline.MaskScrollContent(root);
                return PrefabUtility.SaveAsPrefabAsset(root, HubPrefabPath).GetComponent<EventHubScreen>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildHub(EventHubRefs ui, Kitset kit)
        {
            var tokens = kit.Tokens;
            var hub = UiPipeline.Rect("Hub", ui.safe);
            UiPipeline.Stretch(hub);
            ui.hub = hub.gameObject;
            var back = UiPipeline.AddImage("Background", hub, null, tokens.backgroundLight, Image.Type.Simple);
            back.raycastTarget = true;
            UiPipeline.Stretch(back.rectTransform);

            // Banner: the event's name, the real date, the currency, the featured species, the close button.
            var banner = UiPipeline.AddImage("Banner", hub, kit.Panel, new Color(1f, 0.93f, 0.84f, 1f), Image.Type.Sliced);
            UiPipeline.Place(banner.rectTransform, Top, Top, new Vector2(0f, -24f), new Vector2(1032f, 380f));
            ui.featured = UiPipeline.AddImage("Featured", banner.rectTransform, Icon("ICO_Paw"), new Color(0.47f, 0.4f, 0.33f, 0.55f), Image.Type.Simple);
            ui.featured.preserveAspect = true;
            UiPipeline.Place(ui.featured.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-170f, -50f), new Vector2(280f, 280f));
            ui.name = Text("Name", banner.rectTransform, TextRole.Heading, 64f, 40f, tokens.textPrimary, new Vector2(44f, -40f), new Vector2(520f, 90f));
            Fit(ui.name, "event.test.name");
            ui.date = Text("Date", banner.rectTransform, TextRole.Body, 42f, 32f, tokens.textSecondary, new Vector2(44f, -136f), new Vector2(520f, 64f));
            Fit(ui.date, EventText.Ends, EventText.Starts);
            var chip = UiPipeline.AddImage("Currency", banner.rectTransform, kit.Pill, tokens.panelPrimary, Image.Type.Sliced);
            chip.pixelsPerUnitMultiplier = 1.6f;
            UiPipeline.Place(chip.rectTransform, TopLeft, TopLeft, new Vector2(44f, -230f), new Vector2(330f, 110f));
            var chipIcon = UiPipeline.AddImage("Icon", chip.rectTransform, kit.Currency, Color.white, Image.Type.Simple);
            UiPipeline.Place(chipIcon.rectTransform, new Vector2(0f, 0.5f), Center, new Vector2(60f, 0f), new Vector2(84f, 84f));
            ui.currency = UiPipeline.AddText("Value", chip.rectTransform, TextRole.NumberSmall, 52f, tokens.textPrimary, "13 000");
            UiPipeline.Place(ui.currency.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(112f, 2f), new Vector2(200f, 70f));
            var closeImage = UiPipeline.AddImage("Close", banner.rectTransform, kit.Circle, tokens.panelPrimary, Image.Type.Simple);
            closeImage.raycastTarget = true;
            UiPipeline.Place(closeImage.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(124f, 124f));
            ui.close = closeImage.gameObject.AddComponent<Button>();
            UiPipeline.ConfigureButton(ui.close, closeImage);
            var cross = UiPipeline.AddImage("Icon", closeImage.rectTransform, Kit(UiPipeline.CloseIconPath), tokens.textSecondary, Image.Type.Simple);
            UiPipeline.Place(cross.rectTransform, Center, Center, new Vector2(0f, 4f), new Vector2(58f, 58f));

            // Tabs: two rows of four (Parcours, Œuf, Boutique, Quêtes / Défis, Objectif, Souvenirs).
            string[] tabKeys = { EventText.TabTrack, EventText.TabEgg, EventText.TabShop, EventText.TabQuests, EventText.TabChallenges, EventText.TabGoal,
                EventText.TabMemory };
            ui.tabs = new Button[tabKeys.Length];
            ui.tabBacks = new Image[tabKeys.Length];
            ui.tabLabels = new TMP_Text[tabKeys.Length];
            for (int i = 0; i < tabKeys.Length; i++)
            {
                var tab = UiPipeline.AddImage("Tab" + i, hub, kit.Pill, tokens.panelSecondary, Image.Type.Sliced);
                tab.pixelsPerUnitMultiplier = 1.6f;
                tab.raycastTarget = true;
                UiPipeline.Place(tab.rectTransform, TopLeft, TopLeft, new Vector2(24f + (i % 4) * 262f, -424f - (i / 4) * 116f), new Vector2(246f, 100f));
                ui.tabs[i] = tab.gameObject.AddComponent<Button>();
                UiPipeline.ConfigureButton(ui.tabs[i], tab);
                ui.tabBacks[i] = tab;
                var label = UiPipeline.AddText("Label", tab.rectTransform, TextRole.Button, 40f, tokens.textPrimary, "Challenges", 30f);
                label.alignment = TextAlignmentOptions.Center;
                label.enableAutoSizing = true;
                label.fontSizeMax = 40f;
                label.fontSizeMin = 30f;
                UiPipeline.Place(label.rectTransform, Center, Center, new Vector2(0f, 2f), new Vector2(226f, 70f));
                Fit(label, tabKeys[i]);
                ui.tabLabels[i] = label;
            }

            // One scrolled area; each tab is a page the scroll swaps in.
            var viewport = UiPipeline.Rect("Viewport", hub);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = new Vector2(0f, -PageTop);
            viewport.gameObject.AddComponent<RectMask2D>();
            var drag = viewport.gameObject.AddComponent<Image>();
            drag.color = new Color(1f, 1f, 1f, 0f);
            drag.raycastTarget = true;
            ui.pages = new RectTransform[EventHubScreen.AnnouncePage + 1];
            string[] pageNames = { "Track", "Egg", "Shop", "Quests", "Challenges", "Goal", "Memory", "Announce" };
            for (int i = 0; i < ui.pages.Length; i++)
            {
                var page = UiPipeline.Rect(pageNames[i], viewport);
                page.anchorMin = new Vector2(0f, 1f);
                page.anchorMax = new Vector2(1f, 1f);
                page.pivot = new Vector2(0.5f, 1f);
                page.anchoredPosition = Vector2.zero;
                page.sizeDelta = new Vector2(0f, 1200f);
                ui.pages[i] = page;
            }
            ui.scroll = viewport.gameObject.AddComponent<ScrollRect>();
            UiPipeline.ConfigureScroll(ui.scroll, viewport, ui.pages[0]);

            BuildTrack(ui, kit, ui.pages[(int)EventHubTab.Track]);
            BuildEgg(ui, kit, ui.pages[(int)EventHubTab.Egg]);
            BuildShop(ui, kit, ui.pages[(int)EventHubTab.Shop]);
            BuildQuests(ui, kit, ui.pages[(int)EventHubTab.Quests]);
            BuildChallenges(ui, kit, ui.pages[(int)EventHubTab.Challenges]);
            BuildGoal(ui, kit, ui.pages[(int)EventHubTab.Goal]);
            BuildMemory(ui, kit, ui.pages[(int)EventHubTab.Memory]);
            BuildAnnounce(ui, kit, ui.pages[EventHubScreen.AnnouncePage]);
            foreach (var graphic in viewport.GetComponentsInChildren<MaskableGraphic>(true))
                graphic.maskable = true;
            for (int i = 1; i < ui.pages.Length; i++)
                ui.pages[i].gameObject.SetActive(false);
        }

        private static void BuildTrack(EventHubRefs ui, Kitset kit, RectTransform page)
        {
            var tokens = kit.Tokens;
            page.sizeDelta = new Vector2(0f, TrackTop + EventHubModel.MaxTierRows * TierPitch + 60f);
            ui.trackPoints = Text("Points", page, TextRole.Body, 44f, 32f, tokens.textPrimary, new Vector2(44f, -24f), new Vector2(992f, 64f));
            Fit(ui.trackPoints, EventText.TrackPoints);
            ui.trackFill = Bar(page, kit, new Vector2(44f, -100f), new Vector2(992f, 36f), tokens.accentPrimary);
            var row = UiPipeline.Rect("Buttons", page);
            UiPipeline.Place(row, TopLeft, TopLeft, new Vector2(0f, -150f), new Vector2(1080f, 160f));
            ui.claimAll = ProgressionUiPipeline.PrimaryButton(row, tokens, "ClaimAll", new Vector2(-260f, 0f), new Vector2(480f, 150f), out _, out var claimAllLabel);
            Floor(claimAllLabel, 40f);
            Fit(claimAllLabel, EventText.ClaimAll);
            ui.claimAllLabel = claimAllLabel;
            ui.goldGet = ProgressionUiPipeline.PrimaryButton(row, tokens, "GoldBook", new Vector2(260f, 0f), new Vector2(480f, 150f), out var goldBack, out var goldLabel);
            goldBack.color = new Color(1f, 0.86f, 0.52f, 1f);
            Floor(goldLabel, 34f);
            Fit(goldLabel, EventText.GoldGet);
            ui.goldGetLabel = goldLabel;
            ui.freeHeader = Text("FreeHeader", page, TextRole.Subheading, 40f, 30f, tokens.textSecondary, new Vector2(170f, -330f), new Vector2(410f, 60f));
            ui.freeHeader.alignment = TextAlignmentOptions.Center;
            Fit(ui.freeHeader, EventText.TrackFree);
            ui.goldHeader = Text("GoldHeader", page, TextRole.Subheading, 40f, 30f, tokens.textSecondary, new Vector2(600f, -330f), new Vector2(410f, 60f));
            ui.goldHeader.alignment = TextAlignmentOptions.Center;
            Fit(ui.goldHeader, EventText.GoldTitle);

            ui.tiers = new EventTierRefs[EventHubModel.MaxTierRows];
            for (int i = 0; i < ui.tiers.Length; i++)
            {
                var refs = new EventTierRefs();
                refs.root = UiPipeline.Rect("Tier" + (i + 1), page);
                UiPipeline.Place(refs.root, TopLeft, TopLeft, new Vector2(0f, -(TrackTop + i * TierPitch)), new Vector2(1080f, 200f));
                refs.badge = UiPipeline.AddImage("Badge", refs.root, kit.Circle, tokens.accentIdle, Image.Type.Simple);
                UiPipeline.Place(refs.badge.rectTransform, TopLeft, TopLeft, new Vector2(40f, -45f), new Vector2(110f, 110f));
                refs.number = UiPipeline.AddText("Number", refs.badge.rectTransform, TextRole.NumberSmall, 44f, tokens.textPrimary, "36");
                refs.number.alignment = TextAlignmentOptions.Center;
                UiPipeline.Place(refs.number.rectTransform, Center, Center, new Vector2(0f, 3f), new Vector2(92f, 60f));
                refs.free = Cell(refs.root, kit, "Free", new Vector2(170f, -5f), tokens.panelPrimary, i == 0);
                refs.gold = Cell(refs.root, kit, "Gold", new Vector2(600f, -5f), new Color(1f, 0.92f, 0.72f, 1f), false);
                ui.tiers[i] = refs;
            }
        }

        private static EventCellRefs Cell(RectTransform parent, Kitset kit, string name, Vector2 position, Color color, bool measure)
        {
            var tokens = kit.Tokens;
            var cell = new EventCellRefs();
            cell.root = UiPipeline.Rect(name, parent);
            UiPipeline.Place(cell.root, TopLeft, TopLeft, position, new Vector2(410f, 190f));
            cell.back = UiPipeline.AddImage("Back", cell.root, kit.Panel, color, Image.Type.Sliced);
            cell.back.pixelsPerUnitMultiplier = 1.6f;
            UiPipeline.Stretch(cell.back.rectTransform);
            cell.icon = UiPipeline.AddImage("Icon", cell.root, Icon("ICO_Currency_Gems"), Color.white, Image.Type.Simple);
            UiPipeline.Place(cell.icon.rectTransform, TopLeft, TopLeft, new Vector2(16f, -20f), new Vector2(100f, 100f));
            cell.extra = UiPipeline.AddImage("Extra", cell.root, Icon("ICO_Souvenir"), Color.white, Image.Type.Simple);
            UiPipeline.Place(cell.extra.rectTransform, TopLeft, TopLeft, new Vector2(78f, -100f), new Vector2(54f, 54f));
            cell.extra.gameObject.SetActive(false);
            cell.label = Text("Label", cell.root, TextRole.Body, 34f, 30f, tokens.textPrimary, new Vector2(128f, -14f), new Vector2(266f, 92f));
            cell.label.textWrappingMode = TextWrappingModes.Normal;
            cell.label.alignment = TextAlignmentOptions.TopLeft;
            if (measure)
                Fit(cell.label, EventText.RewardKeys);
            var claim = UiPipeline.AddImage("Claim", cell.root, kit.Pill, tokens.accentPrimary, Image.Type.Sliced);
            claim.pixelsPerUnitMultiplier = 1.6f;
            claim.raycastTarget = true;
            UiPipeline.Place(claim.rectTransform, TopLeft, TopLeft, new Vector2(128f, -110f), new Vector2(266f, 70f));
            cell.claim = claim.gameObject.AddComponent<Button>();
            UiPipeline.ConfigureButton(cell.claim, claim);
            cell.claimLabel = UiPipeline.AddText("Label", claim.rectTransform, TextRole.Button, 34f, tokens.textPrimary, "Claim", 30f);
            cell.claimLabel.alignment = TextAlignmentOptions.Center;
            cell.claimLabel.enableAutoSizing = true;
            cell.claimLabel.fontSizeMax = 34f;
            cell.claimLabel.fontSizeMin = 30f;
            UiPipeline.Place(cell.claimLabel.rectTransform, Center, Center, new Vector2(0f, 1f), new Vector2(254f, 58f));
            if (measure)
                Fit(cell.claimLabel, EventText.Claim);
            claim.gameObject.SetActive(false);
            var done = UiPipeline.AddImage("Done", cell.root, kit.Check, Color.white, Image.Type.Simple);
            UiPipeline.Place(done.rectTransform, TopLeft, TopLeft, new Vector2(330f, -118f), new Vector2(60f, 60f));
            done.gameObject.SetActive(false);
            cell.done = done.gameObject;
            var locked = UiPipeline.AddImage("Lock", cell.root, kit.Lock, tokens.textDisabled, Image.Type.Simple);
            UiPipeline.Place(locked.rectTransform, TopLeft, TopLeft, new Vector2(330f, -118f), new Vector2(60f, 60f));
            locked.gameObject.SetActive(false);
            cell.locked = locked.gameObject;
            return cell;
        }

        private static void BuildEgg(EventHubRefs ui, Kitset kit, RectTransform page)
        {
            var tokens = kit.Tokens;
            page.sizeDelta = new Vector2(0f, 1330f);
            ui.eggTitle = Text("Title", page, TextRole.Subheading, 48f, 34f, tokens.textPrimary, new Vector2(44f, -24f), new Vector2(760f, 70f));
            Fit(ui.eggTitle, EventText.EggTitle);
            var oddsImage = UiPipeline.AddImage("Odds", page, kit.Circle, tokens.panelPrimary, Image.Type.Simple);
            oddsImage.raycastTarget = true;
            UiPipeline.Place(oddsImage.rectTransform, TopLeft, TopLeft, new Vector2(920f, -14f), new Vector2(112f, 112f));
            ui.odds = oddsImage.gameObject.AddComponent<Button>();
            UiPipeline.ConfigureButton(ui.odds, oddsImage);
            var oddsIcon = UiPipeline.AddImage("Icon", oddsImage.rectTransform, Icon("ICO_EventEgg_Odds"), Color.white, Image.Type.Simple);
            UiPipeline.Place(oddsIcon.rectTransform, Center, Center, new Vector2(0f, 3f), new Vector2(80f, 80f));
            var egg = UiPipeline.AddImage("Egg", page, Kit(UiPipeline.EggIconPath), Color.white, Image.Type.Simple);
            UiPipeline.Place(egg.rectTransform, TopLeft, TopLeft, new Vector2(410f, -110f), new Vector2(260f, 260f));
            var costIcon = UiPipeline.AddImage("CostIcon", page, kit.Currency, Color.white, Image.Type.Simple);
            UiPipeline.Place(costIcon.rectTransform, TopLeft, TopLeft, new Vector2(430f, -386f), new Vector2(64f, 64f));
            ui.eggCost = UiPipeline.AddText("Cost", page, TextRole.NumberSmall, 52f, tokens.textPrimary, "300");
            UiPipeline.Place(ui.eggCost.rectTransform, TopLeft, TopLeft, new Vector2(504f, -380f), new Vector2(200f, 76f));
            var row = UiPipeline.Rect("OpenRow", page);
            UiPipeline.Place(row, TopLeft, TopLeft, new Vector2(0f, -470f), new Vector2(1080f, 160f));
            ui.eggOpen = ProgressionUiPipeline.PrimaryButton(row, tokens, "Open", new Vector2(0f, 0f), new Vector2(520f, 150f), out _, out var openLabel);
            Floor(openLabel, 40f);
            Fit(openLabel, EventText.EggOpen);
            ui.eggOpenLabel = openLabel;
            ui.eggNewIn = Text("NewIn", page, TextRole.Body, 40f, 30f, tokens.textPrimary, new Vector2(44f, -640f), new Vector2(992f, 64f));
            ui.eggNewIn.alignment = TextAlignmentOptions.Center;
            Fit(ui.eggNewIn, EventText.EggNewIn, EventText.EggNewIn + ".one", EventText.EggComplete);
            ui.eggResult = Text("Result", page, TextRole.Body, 40f, 30f, tokens.textSecondary, new Vector2(44f, -710f), new Vector2(992f, 64f));
            ui.eggResult.alignment = TextAlignmentOptions.Center;
            Fit(ui.eggResult, EventText.EggNew, EventText.EggDuplicate, EventText.NotEnough);
            ui.eggSlots = new Image[EventHubScreen.EggSlots];
            ui.eggSlotDone = new GameObject[EventHubScreen.EggSlots];
            for (int i = 0; i < ui.eggSlots.Length; i++)
            {
                var slot = UiPipeline.AddImage("Slot" + i, page, kit.Panel, tokens.panelSecondary, Image.Type.Sliced);
                slot.pixelsPerUnitMultiplier = 1.6f;
                UiPipeline.Place(slot.rectTransform, TopLeft, TopLeft, new Vector2(64f + (i % 4) * 244f, -800f - (i / 4) * 244f), new Vector2(220f, 220f));
                ui.eggSlots[i] = UiPipeline.AddImage("Icon", slot.rectTransform, Icon("ICO_Paw"), Color.white, Image.Type.Simple);
                UiPipeline.Place(ui.eggSlots[i].rectTransform, Center, Center, Vector2.zero, new Vector2(150f, 150f));
                var done = UiPipeline.AddImage("Done", slot.rectTransform, kit.Check, Color.white, Image.Type.Simple);
                UiPipeline.Place(done.rectTransform, TopLeft, TopLeft, new Vector2(156f, -156f), new Vector2(52f, 52f));
                done.gameObject.SetActive(false);
                ui.eggSlotDone[i] = done.gameObject;
            }
            // Opaque and tall enough to cover the egg, its button and the counter line: no text cut by its edge or seen through it.
            var panel = UiPipeline.AddImage("OddsPanel", page, kit.Panel, tokens.backgroundLight, Image.Type.Sliced);
            UiPipeline.Place(panel.rectTransform, TopLeft, TopLeft, new Vector2(64f, -104f), new Vector2(952f, 726f));
            panel.raycastTarget = true;
            ui.oddsPanel = panel.gameObject;
            ui.oddsTitle = Text("Title", panel.rectTransform, TextRole.Subheading, 46f, 34f, tokens.textPrimary, new Vector2(40f, -30f), new Vector2(870f, 70f));
            Fit(ui.oddsTitle, EventText.EggOddsTitle);
            ui.oddsItem = Text("Item", panel.rectTransform, TextRole.Body, 38f, 30f, tokens.textPrimary, new Vector2(40f, -120f), new Vector2(870f, 60f));
            Fit(ui.oddsItem, EventText.EggOddsItem);
            ui.oddsSecret = Text("Secret", panel.rectTransform, TextRole.Body, 38f, 30f, tokens.textPrimary, new Vector2(40f, -190f), new Vector2(870f, 60f));
            Fit(ui.oddsSecret, EventText.EggOddsSecret);
            ui.oddsRule = Text("Rule", panel.rectTransform, TextRole.Body, 36f, 30f, tokens.textSecondary, new Vector2(40f, -270f), new Vector2(870f, 200f));
            ui.oddsRule.textWrappingMode = TextWrappingModes.Normal;
            ui.oddsRule.alignment = TextAlignmentOptions.TopLeft;
            Fit(ui.oddsRule, EventText.EggOddsRule);
            panel.gameObject.SetActive(false);
        }

        private static EventLineRefs Line(RectTransform parent, Kitset kit, string name, float y, bool measure, string[] titleKeys, string buttonKey, Sprite icon,
            bool swap)
        {
            var tokens = kit.Tokens;
            var line = new EventLineRefs();
            line.root = UiPipeline.Rect(name, parent);
            UiPipeline.Place(line.root, TopLeft, TopLeft, new Vector2(24f, -y), new Vector2(1032f, 176f));
            var back = UiPipeline.AddImage("Back", line.root, kit.Panel, tokens.panelPrimary, Image.Type.Sliced);
            back.pixelsPerUnitMultiplier = 1.6f;
            UiPipeline.Stretch(back.rectTransform);
            line.icon = UiPipeline.AddImage("Icon", line.root, icon, Color.white, Image.Type.Simple);
            UiPipeline.Place(line.icon.rectTransform, TopLeft, TopLeft, new Vector2(20f, -28f), new Vector2(120f, 120f));
            line.title = Text("Title", line.root, TextRole.Body, 40f, 30f, tokens.textPrimary, new Vector2(160f, -22f), new Vector2(580f, 64f));
            if (measure)
                Fit(line.title, titleKeys);
            line.subIcon = UiPipeline.AddImage("SubIcon", line.root, kit.Currency, Color.white, Image.Type.Simple);
            UiPipeline.Place(line.subIcon.rectTransform, TopLeft, TopLeft, new Vector2(160f, -100f), new Vector2(52f, 52f));
            line.sub = Text("Sub", line.root, TextRole.Body, 38f, 30f, tokens.textSecondary, new Vector2(222f, -96f), new Vector2(520f, 60f));
            if (measure)
                Fit(line.sub, EventText.ShopLeft, EventText.Owned, EventText.ShopSoldOut);
            var button = UiPipeline.AddImage(swap ? "Swap" : "Buy", line.root, swap ? kit.Circle : kit.Pill, swap ? tokens.panelSecondary : tokens.accentPrimary,
                swap ? Image.Type.Simple : Image.Type.Sliced);
            button.raycastTarget = true;
            if (swap)
            {
                UiPipeline.Place(button.rectTransform, TopLeft, TopLeft, new Vector2(906f, -38f), new Vector2(100f, 100f));
                var swapIcon = UiPipeline.AddImage("Icon", button.rectTransform, Icon("ICO_Swap"), Color.white, Image.Type.Simple);
                UiPipeline.Place(swapIcon.rectTransform, Center, Center, new Vector2(0f, 2f), new Vector2(64f, 64f));
            }
            else
            {
                button.pixelsPerUnitMultiplier = 1.6f;
                UiPipeline.Place(button.rectTransform, TopLeft, TopLeft, new Vector2(760f, -48f), new Vector2(250f, 90f));
                line.buttonLabel = UiPipeline.AddText("Label", button.rectTransform, TextRole.Button, 38f, tokens.textPrimary, "Buy", 30f);
                line.buttonLabel.alignment = TextAlignmentOptions.Center;
                UiPipeline.Place(line.buttonLabel.rectTransform, Center, Center, new Vector2(0f, 1f), new Vector2(236f, 76f));
                if (measure && buttonKey != null)
                    Fit(line.buttonLabel, buttonKey);
            }
            line.button = button.gameObject.AddComponent<Button>();
            UiPipeline.ConfigureButton(line.button, button);
            var done = UiPipeline.AddImage("Done", line.root, kit.Check, Color.white, Image.Type.Simple);
            UiPipeline.Place(done.rectTransform, TopLeft, TopLeft, new Vector2(924f, -56f), new Vector2(64f, 64f));
            done.gameObject.SetActive(false);
            line.done = done.gameObject;
            return line;
        }

        private static string[] ShopKeys()
        {
            var keys = new List<string>(EventText.RewardKeys) { EventText.ShopEgg, EventText.ShopSeeds };
            return keys.ToArray();
        }

        private static void BuildShop(EventHubRefs ui, Kitset kit, RectTransform page)
        {
            var tokens = kit.Tokens;
            ui.shopTitle = Text("Title", page, TextRole.Subheading, 48f, 34f, tokens.textPrimary, new Vector2(44f, -24f), new Vector2(992f, 70f));
            Fit(ui.shopTitle, EventText.ShopTitle);
            ui.shop = new EventLineRefs[EventHubModel.MaxShopRows];
            var keys = ShopKeys();
            for (int i = 0; i < ui.shop.Length; i++)
                ui.shop[i] = Line(page, kit, "Line" + i, ShopTop + i * LinePitch, i == 0, keys, EventText.ShopBuy, Icon("ICO_Decoration"), false);
            var panel = UiPipeline.AddImage("Stickers", page, kit.Panel, tokens.panelSecondary, Image.Type.Sliced);
            ui.stickerPanel = panel.rectTransform;
            UiPipeline.Place(ui.stickerPanel, TopLeft, TopLeft, new Vector2(24f, -(ShopTop + ui.shop.Length * LinePitch + 30f)), new Vector2(1032f, 340f));
            ui.stickerTitle = Text("Title", ui.stickerPanel, TextRole.Subheading, 40f, 30f, tokens.textPrimary, new Vector2(30f, -24f), new Vector2(970f, 60f));
            Fit(ui.stickerTitle, EventText.ShopPickSticker);
            ui.stickerChips = new Button[EventHubScreen.StickerChips];
            ui.stickerLabels = new TMP_Text[EventHubScreen.StickerChips];
            for (int i = 0; i < ui.stickerChips.Length; i++)
            {
                var chip = UiPipeline.AddImage("Chip" + i, ui.stickerPanel, kit.Pill, tokens.panelPrimary, Image.Type.Sliced);
                chip.pixelsPerUnitMultiplier = 1.6f;
                chip.raycastTarget = true;
                UiPipeline.Place(chip.rectTransform, TopLeft, TopLeft, new Vector2(26f + (i % 6) * 166f, -100f - (i / 6) * 116f), new Vector2(150f, 100f));
                ui.stickerChips[i] = chip.gameObject.AddComponent<Button>();
                UiPipeline.ConfigureButton(ui.stickerChips[i], chip);
                var label = UiPipeline.AddText("Label", chip.rectTransform, TextRole.NumberSmall, 44f, tokens.textPrimary, "12");
                label.alignment = TextAlignmentOptions.Center;
                UiPipeline.Place(label.rectTransform, Center, Center, new Vector2(0f, 2f), new Vector2(130f, 70f));
                ui.stickerLabels[i] = label;
            }
            panel.gameObject.SetActive(false);
            page.sizeDelta = new Vector2(0f, ShopTop + ui.shop.Length * LinePitch + 430f);
        }

        private static void BuildMemory(EventHubRefs ui, Kitset kit, RectTransform page)
        {
            var tokens = kit.Tokens;
            ui.memoryTitle = Text("Title", page, TextRole.Subheading, 48f, 34f, tokens.textPrimary, new Vector2(44f, -24f), new Vector2(992f, 70f));
            Fit(ui.memoryTitle, EventText.MemoryTitle);
            ui.memory = new EventLineRefs[EventHubModel.MaxMemoryRows];
            for (int i = 0; i < ui.memory.Length; i++)
                ui.memory[i] = Line(page, kit, "Line" + i, ShopTop + i * LinePitch, i == 0, EventText.RewardKeys, EventText.ShopBuy, Icon("ICO_Decoration"), false);
            page.sizeDelta = new Vector2(0f, ShopTop + ui.memory.Length * LinePitch + 60f);
        }

        private static void BuildQuests(EventHubRefs ui, Kitset kit, RectTransform page)
        {
            var tokens = kit.Tokens;
            ui.dailyTitle = Text("DailyTitle", page, TextRole.Subheading, 48f, 34f, tokens.textPrimary, new Vector2(44f, -24f), new Vector2(992f, 70f));
            Fit(ui.dailyTitle, EventText.QuestsDaily);
            ui.pending = Text("Pending", page, TextRole.Body, 36f, 30f, tokens.textSecondary, new Vector2(44f, -96f), new Vector2(480f, 56f));
            Fit(ui.pending, EventText.QuestsPending);
            ui.swaps = Text("Swaps", page, TextRole.Body, 36f, 30f, tokens.textSecondary, new Vector2(540f, -96f), new Vector2(496f, 56f));
            ui.swaps.alignment = TextAlignmentOptions.MidlineRight;
            Fit(ui.swaps, EventText.QuestsSwapsLeft);
            var dailyKeys = new List<string>();
            foreach (var template in EventQuestCatalog.Daily)
                dailyKeys.Add(template.TextKey);
            var longKeys = new List<string>();
            foreach (var template in EventQuestCatalog.Long)
                longKeys.Add(template.TextKey);
            ui.daily = new EventLineRefs[EventHubModel.MaxDailyRows];
            for (int i = 0; i < ui.daily.Length; i++)
                ui.daily[i] = QuestLine(page, kit, "Daily" + i, QuestTop + i * QuestPitch, i == 0 ? dailyKeys.ToArray() : null, true);
            ui.longTitle = Text("LongTitle", page, TextRole.Subheading, 48f, 34f, tokens.textPrimary, new Vector2(44f, -(QuestTop + ui.daily.Length * QuestPitch + 40f)),
                new Vector2(992f, 70f));
            Fit(ui.longTitle, EventText.QuestsLong);
            ui.longQuests = new EventLineRefs[EventHubModel.MaxLongRows];
            float longTop = QuestTop + ui.daily.Length * QuestPitch + 140f;
            for (int i = 0; i < ui.longQuests.Length; i++)
                ui.longQuests[i] = QuestLine(page, kit, "Long" + i, longTop + i * QuestPitch, i == 0 ? longKeys.ToArray() : null, false);
            page.sizeDelta = new Vector2(0f, longTop + ui.longQuests.Length * QuestPitch + 60f);
        }

        private static EventLineRefs QuestLine(RectTransform parent, Kitset kit, string name, float y, string[] keys, bool swap)
        {
            var tokens = kit.Tokens;
            var line = new EventLineRefs();
            line.root = UiPipeline.Rect(name, parent);
            UiPipeline.Place(line.root, TopLeft, TopLeft, new Vector2(24f, -y), new Vector2(1032f, 168f));
            var back = UiPipeline.AddImage("Back", line.root, kit.Panel, tokens.panelPrimary, Image.Type.Sliced);
            back.pixelsPerUnitMultiplier = 1.6f;
            UiPipeline.Stretch(back.rectTransform);
            line.icon = UiPipeline.AddImage("Icon", line.root, Icon("ICO_Quest"), Color.white, Image.Type.Simple);
            UiPipeline.Place(line.icon.rectTransform, TopLeft, TopLeft, new Vector2(20f, -34f), new Vector2(100f, 100f));
            line.title = Text("Title", line.root, TextRole.Body, 36f, 30f, tokens.textPrimary, new Vector2(136f, -14f), new Vector2(740f, 96f));
            line.title.textWrappingMode = TextWrappingModes.Normal;
            line.title.alignment = TextAlignmentOptions.TopLeft;
            if (keys != null)
                Fit(line.title, keys);
            line.sub = Text("Progress", line.root, TextRole.Label, 36f, 30f, tokens.textSecondary, new Vector2(136f, -112f), new Vector2(560f, 48f));
            var button = UiPipeline.AddImage("Swap", line.root, kit.Circle, tokens.panelSecondary, Image.Type.Simple);
            button.raycastTarget = true;
            UiPipeline.Place(button.rectTransform, TopLeft, TopLeft, new Vector2(906f, -34f), new Vector2(100f, 100f));
            var swapIcon = UiPipeline.AddImage("Icon", button.rectTransform, Icon("ICO_Swap"), Color.white, Image.Type.Simple);
            UiPipeline.Place(swapIcon.rectTransform, Center, Center, new Vector2(0f, 2f), new Vector2(64f, 64f));
            line.button = button.gameObject.AddComponent<Button>();
            UiPipeline.ConfigureButton(line.button, button);
            button.gameObject.SetActive(swap);
            var done = UiPipeline.AddImage("Done", line.root, kit.Check, Color.white, Image.Type.Simple);
            UiPipeline.Place(done.rectTransform, TopLeft, TopLeft, new Vector2(924f, -52f), new Vector2(64f, 64f));
            done.gameObject.SetActive(false);
            line.done = done.gameObject;
            return line;
        }

        private static void BuildChallenges(EventHubRefs ui, Kitset kit, RectTransform page)
        {
            var tokens = kit.Tokens;
            ui.starsTitle = Text("Stars", page, TextRole.Subheading, 48f, 34f, tokens.textPrimary, new Vector2(44f, -24f), new Vector2(992f, 70f));
            Fit(ui.starsTitle, EventText.ChallengesStars);
            ui.starsFill = Bar(page, kit, new Vector2(44f, -110f), new Vector2(992f, 36f), tokens.currencyLight);
            ui.outfitLine = Text("Outfit", page, TextRole.Body, 38f, 30f, tokens.textPrimary, new Vector2(44f, -160f), new Vector2(992f, 60f));
            Fit(ui.outfitLine, EventText.ChallengesOutfit);
            ui.practice = Text("Practice", page, TextRole.Body, 34f, 30f, tokens.textSecondary, new Vector2(44f, -224f), new Vector2(992f, 56f));
            Fit(ui.practice, EventText.ChallengesPractice);
            var full = AssetDatabase.LoadAssetAtPath<Sprite>(UiPipeline.KitFolder + "/UI_Star_Result_Full.png") ?? Kit(UiPipeline.StarPath);
            ui.challenges = new EventLineRefs[EventDefinition.ChallengeCount];
            for (int i = 0; i < ui.challenges.Length; i++)
            {
                var line = new EventLineRefs();
                line.root = UiPipeline.Rect("Challenge" + i, page);
                UiPipeline.Place(line.root, TopLeft, TopLeft, new Vector2(24f, -(ChallengeTop + i * ChallengePitch)), new Vector2(1032f, 118f));
                var back = UiPipeline.AddImage("Back", line.root, kit.Panel, tokens.panelPrimary, Image.Type.Sliced);
                back.pixelsPerUnitMultiplier = 1.6f;
                UiPipeline.Stretch(back.rectTransform);
                line.icon = UiPipeline.AddImage("Icon", line.root, Icon("ICO_Challenge"), Color.white, Image.Type.Simple);
                UiPipeline.Place(line.icon.rectTransform, TopLeft, TopLeft, new Vector2(20f, -14f), new Vector2(90f, 90f));
                line.title = Text("Title", line.root, TextRole.Body, 40f, 30f, tokens.textPrimary, new Vector2(130f, -27f), new Vector2(460f, 64f));
                if (i == 0)
                    Fit(line.title, EventText.ChallengeName);
                line.stars = new Image[3];
                for (int s = 0; s < 3; s++)
                {
                    line.stars[s] = UiPipeline.AddImage("Star" + s, line.root, full, Color.white, Image.Type.Simple);
                    UiPipeline.Place(line.stars[s].rectTransform, TopLeft, TopLeft, new Vector2(640f + s * 120f, -9f), new Vector2(100f, 100f));
                }
                ui.challenges[i] = line;
            }
            page.sizeDelta = new Vector2(0f, ChallengeTop + ui.challenges.Length * ChallengePitch + 60f);
        }

        private static void BuildGoal(EventHubRefs ui, Kitset kit, RectTransform page)
        {
            var tokens = kit.Tokens;
            page.sizeDelta = new Vector2(0f, 520f);
            ui.goalTitle = Text("Title", page, TextRole.Heading, 56f, 40f, tokens.textPrimary, new Vector2(44f, -40f), new Vector2(992f, 80f));
            ui.goalTitle.alignment = TextAlignmentOptions.Center;
            Fit(ui.goalTitle, EventText.GoalTitle);
            ui.goalFill = Bar(page, kit, new Vector2(44f, -170f), new Vector2(992f, 56f), tokens.accentPrimary);
            ui.goalProgress = Text("Progress", page, TextRole.Body, 44f, 32f, tokens.textPrimary, new Vector2(44f, -250f), new Vector2(992f, 70f));
            ui.goalProgress.alignment = TextAlignmentOptions.Center;
            Fit(ui.goalProgress, EventText.GoalProgress);
            ui.goalNote = Text("Note", page, TextRole.Body, 38f, 30f, tokens.textSecondary, new Vector2(44f, -340f), new Vector2(992f, 120f));
            ui.goalNote.textWrappingMode = TextWrappingModes.Normal;
            ui.goalNote.alignment = TextAlignmentOptions.Top;
            Fit(ui.goalNote, EventText.GoalNote, EventText.GoalReached);
        }

        private static void BuildAnnounce(EventHubRefs ui, Kitset kit, RectTransform page)
        {
            var tokens = kit.Tokens;
            page.sizeDelta = new Vector2(0f, 720f);
            ui.announceTitle = Text("Title", page, TextRole.Subheading, 48f, 34f, tokens.textPrimary, new Vector2(44f, -30f), new Vector2(992f, 70f));
            Fit(ui.announceTitle, EventText.Headline);
            ui.headline = new EventCellRefs[3];
            for (int i = 0; i < ui.headline.Length; i++)
            {
                var cell = new EventCellRefs();
                cell.root = UiPipeline.Rect("Headline" + i, page);
                UiPipeline.Place(cell.root, TopLeft, TopLeft, new Vector2(24f + i * 352f, -120f), new Vector2(328f, 380f));
                cell.back = UiPipeline.AddImage("Back", cell.root, kit.Panel, tokens.panelPrimary, Image.Type.Sliced);
                UiPipeline.Stretch(cell.back.rectTransform);
                cell.icon = UiPipeline.AddImage("Icon", cell.root, Icon("ICO_Event_Outfit"), Color.white, Image.Type.Simple);
                UiPipeline.Place(cell.icon.rectTransform, Top, Top, new Vector2(0f, -30f), new Vector2(180f, 180f));
                cell.label = UiPipeline.AddText("Label", cell.root, TextRole.Body, 38f, tokens.textPrimary, "Outfit", 30f);
                cell.label.textWrappingMode = TextWrappingModes.Normal;
                cell.label.alignment = TextAlignmentOptions.Top;
                UiPipeline.Place(cell.label.rectTransform, Top, Top, new Vector2(0f, -232f), new Vector2(300f, 120f));
                if (i == 0)
                    Fit(cell.label, EventText.RewardKeys);
                ui.headline[i] = cell;
            }
            ui.announceNote = Text("Note", page, TextRole.Body, 38f, 30f, tokens.textSecondary, new Vector2(44f, -540f), new Vector2(992f, 120f));
            ui.announceNote.textWrappingMode = TextWrappingModes.Normal;
            ui.announceNote.alignment = TextAlignmentOptions.Top;
            Fit(ui.announceNote, EventText.AnnounceNote);
        }

        private static void BuildSummary(EventHubRefs ui, Kitset kit)
        {
            var tokens = kit.Tokens;
            var root = UiPipeline.Rect("Summary", ui.safe);
            UiPipeline.Stretch(root);
            ui.summary = root.gameObject;
            var scrim = UiPipeline.AddImage("Scrim", root, null, tokens.darkOverlay, Image.Type.Simple);
            scrim.raycastTarget = true;
            UiPipeline.Stretch(scrim.rectTransform);
            var card = UiPipeline.AddImage("Card", root, kit.Panel, tokens.backgroundLight, Image.Type.Sliced);
            UiPipeline.Place(card.rectTransform, Center, Center, Vector2.zero, new Vector2(920f, 1000f));
            var rect = card.rectTransform;
            var icon = UiPipeline.AddImage("Icon", rect, Icon("ICO_Souvenir"), Color.white, Image.Type.Simple);
            UiPipeline.Place(icon.rectTransform, Top, Top, new Vector2(0f, -40f), new Vector2(170f, 170f));
            ui.summaryTitle = UiPipeline.AddText("Title", rect, TextRole.Heading, 56f, tokens.textPrimary, "The event is over", 40f);
            ui.summaryTitle.alignment = TextAlignmentOptions.Center;
            UiPipeline.Place(ui.summaryTitle.rectTransform, Top, Top, new Vector2(0f, -224f), new Vector2(840f, 80f));
            Fit(ui.summaryTitle, EventText.SummaryTitle);
            ui.summaryName = UiPipeline.AddText("Name", rect, TextRole.Body, 42f, tokens.textSecondary, "Trial event", 32f);
            ui.summaryName.alignment = TextAlignmentOptions.Center;
            UiPipeline.Place(ui.summaryName.rectTransform, Top, Top, new Vector2(0f, -310f), new Vector2(840f, 64f));
            Fit(ui.summaryName, "event.test.name");
            ui.summaryCurrency = SummaryLine(rect, kit, "Currency", -420f, kit.Currency, EventText.SummaryCurrency);
            ui.summaryLight = SummaryLine(rect, kit, "Light", -510f, Kit(UiPipeline.LightIconPath), EventText.SummaryLight);
            ui.summaryTier = SummaryLine(rect, kit, "Tier", -600f, Icon("ICO_Event"), EventText.SummaryTier);
            ui.summaryOk = ProgressionUiPipeline.PrimaryButton(rect, tokens, "Ok", new Vector2(0f, 60f), new Vector2(520f, 150f), out _, out var okLabel);
            Floor(okLabel, 40f);
            Fit(okLabel, EventText.SummaryOk);
            ui.summaryOkLabel = okLabel;
            root.gameObject.SetActive(false);
        }

        private static TMP_Text SummaryLine(RectTransform card, Kitset kit, string name, float y, Sprite sprite, string key)
        {
            var icon = UiPipeline.AddImage(name + "Icon", card, sprite, Color.white, Image.Type.Simple);
            UiPipeline.Place(icon.rectTransform, TopLeft, TopLeft, new Vector2(80f, y), new Vector2(72f, 72f));
            var label = Text(name, card, TextRole.Body, 42f, 32f, kit.Tokens.textPrimary, new Vector2(172f, y + 2f), new Vector2(680f, 76f));
            Fit(label, key);
            return label;
        }

        private static void FillArt(EventHubRefs ui)
        {
            var paw = Icon("ICO_Paw");
            var icons = new Sprite[EventIconSlots.Count];
            string[] names =
            {
                "ICO_Paw", "ICO_Currency_Gems", "ICO_Ticket", "ICO_Treat", "ICO_Hatch_Egg", "ICO_Currency_Light", "ICO_Sticker", "ICO_Decoration",
                "ICO_Event_Outfit", "ICO_Paw", "ICO_Achievement", "ICO_AlbumStar", "ICO_Decoration", "ICO_Souvenir", "ICO_Favorite_Heart",
                "ICO_Hatch_Egg", "ICO_Seed",
            };
            for (int i = 0; i < icons.Length; i++)
                icons[i] = Icon(names[i]) ?? paw;
            ui.icons = icons;
            ui.paw = paw;
            ui.secret = Kit(UiPipeline.StarPath);
            ui.starFull = AssetDatabase.LoadAssetAtPath<Sprite>(UiPipeline.KitFolder + "/UI_Star_Result_Full.png") ?? ui.secret;
            ui.starEmpty = AssetDatabase.LoadAssetAtPath<Sprite>(UiPipeline.KitFolder + "/UI_Star_Result_Empty.png") ?? ui.secret;
            // The featured species: the Codex's silhouettes (already loaded by the Codex, so they cost nothing more here).
            var keys = new List<string>();
            var sprites = new List<Sprite>();
            foreach (var guid in AssetDatabase.FindAssets("TH_species_ t:Sprite", new[] { "Assets/_Game" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                string file = Path.GetFileNameWithoutExtension(path);
                const string prefix = "TH_species_", suffix = "_Silhouette";
                if (!file.StartsWith(prefix, StringComparison.Ordinal) || !file.EndsWith(suffix, StringComparison.Ordinal))
                    continue;
                string species = "species." + file.Substring(prefix.Length, file.Length - prefix.Length - suffix.Length);
                if (keys.Contains(species))
                    continue;
                keys.Add(species);
                sprites.Add(AssetDatabase.LoadAssetAtPath<Sprite>(path));
            }
            ui.featuredKeys = keys.ToArray();
            ui.featuredSprites = sprites.ToArray();
        }

        // ---------------- Helpers ----------------

        private static TextMeshProUGUI Text(string name, RectTransform parent, TextRole role, float size, float min, Color color, Vector2 position, Vector2 box)
        {
            var label = UiPipeline.AddText(name, parent, role, size, color, string.Empty, min);
            // Shrinks from its size to its minimum to fit any language (the fit check measures it at the minimum).
            label.enableAutoSizing = true;
            label.fontSizeMax = size;
            label.fontSizeMin = min;
            UiPipeline.Place(label.rectTransform, TopLeft, TopLeft, position, box);
            return label;
        }

        private static RectTransform Bar(RectTransform parent, Kitset kit, Vector2 position, Vector2 size, Color color)
        {
            var bar = UiPipeline.AddImage("Bar", parent, kit.Pill, kit.Tokens.panelSecondary, Image.Type.Sliced);
            bar.pixelsPerUnitMultiplier = 4f;
            UiPipeline.Place(bar.rectTransform, TopLeft, TopLeft, position, size);
            var fill = UiPipeline.AddImage("Fill", bar.rectTransform, kit.Pill, color, Image.Type.Sliced);
            fill.pixelsPerUnitMultiplier = 4f;
            UiPipeline.LeftFill(fill.rectTransform, size.y);
            return fill.rectTransform;
        }

        private static void Floor(TMP_Text label, float min)
        {
            label.enableAutoSizing = true;
            label.fontSizeMax = label.fontSize;
            label.fontSizeMin = Mathf.Max(30f, min);
        }

        // ---------------- Wiring ----------------

        /// <summary>The hub in the vertical slice, handed to VerticalSliceEntry (scene saved).</summary>
        public static void WireScene()
        {
            var scene = EditorSceneManager.OpenScene(UiPipeline.ScenePath, OpenSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<EventHubScreen>(HubPrefabPath);
            VerticalSliceEntry entry = null;
            EventHubScreen hub = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                entry = entry != null ? entry : root.GetComponent<VerticalSliceEntry>();
                hub = hub != null ? hub : root.GetComponent<EventHubScreen>();
            }
            if (entry == null)
                throw new InvalidOperationException("SceneEntry (VerticalSliceEntry) missing in " + UiPipeline.ScenePath);
            if (hub == null)
            {
                hub = ((GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, scene)).GetComponent<EventHubScreen>();
                hub.name = "Screen_EventHub";
            }
            typeof(VerticalSliceEntry).GetField("_eventHub", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(entry, hub);
            EditorUtility.SetDirty(entry);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
