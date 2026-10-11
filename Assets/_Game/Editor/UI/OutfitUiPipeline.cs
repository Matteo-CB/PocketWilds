using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PocketWilds.Presentation;
using PocketWilds.Simulation;
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
    /// T47 screens: PF_UI_OutfitPicker (a bottom panel over the refuge), the sheet's « Tenue » button, the Codex's Species /
    /// Companions tabs and companion cards, and the picker's wiring in the vertical slice. Same rules as UiPipeline: 1080 x 2340,
    /// text styles by role, 30 pt floor, every label measured in en and fr, texts centered on the visible pill, content saved
    /// inactive. Run after ShopUiPipeline.BuildAll (it rebuilds the sheet and the Codex, then wires the picker).
    /// </summary>
    public static class OutfitUiPipeline
    {
        public const string PickerPrefabPath = "Assets/_Game/Prefabs/UI/Screens/PF_UI_OutfitPicker.prefab";
        public const string ThumbnailFolder = "Assets/_Game/Art/UI/Thumbnails/Animals";
        public const int PickerSortingOrder = 23;
        public const float PickerCardHeight = 1080f;

        /// <summary>The Codex header: title, progress and, since T47, the Species / Companions tabs.</summary>
        public const float CodexHeaderHeight = 420f;
        public const float CompanionOwnedHeight = 1560f;
        public const float CompanionLockedHeight = 720f;

        private static readonly StringBuilder Problems = new StringBuilder();
        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        private static readonly Vector2 Top = new Vector2(0.5f, 1f);
        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 Left = new Vector2(0f, 0.5f);

        [MenuItem("Pocket Wilds/UI/Build Outfit and Companion Screens (T47)", priority = 215)]
        public static void BuildMenu() => Debug.Log("[OutfitUiPipeline] " + BuildAll());

        public static string BuildAll()
        {
            Problems.Clear();
            UiPipeline.EnsureLocalization();
            var tokens = UiPipeline.EnsureTokens();
            FeedbackUiPipeline.BuildSheetPrefab(tokens, FeedbackUiPipeline.EnsureRaritySet());
            UiPipeline.BuildCodexPrefab(tokens);
            BuildPickerPrefab(tokens);
            AssetDatabase.SaveAssets();
            WireScene();
            return "sheet « Tenue », Codex Companions tab, PF_UI_OutfitPicker built and wired; " +
                   (Problems.Length > 0 ? "text fit: " + Problems : "every label fits in en and fr.");
        }

        /// <summary>A sprite by exact name: the icon folder first, then anywhere under Assets/_Game. Null when missing.</summary>
        public static Sprite FindSprite(string name)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(UiPipeline.IconFolder + "/" + name + ".png");
            if (sprite != null)
                return sprite;
            foreach (var guid in AssetDatabase.FindAssets(name + " t:Sprite", new[] { "Assets/_Game" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == name)
                    return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            return null;
        }

        private static Sprite Kit(string path) =>
            AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? throw new FileNotFoundException("Run SourceArt/UI/Tools/pw_ui_sprites.py first.", path);

        private static Sprite Crown() => FindSprite("ICO_Accessory_FlowerCrown") ?? FindSprite("ICO_Cosmetic_FlowerCrown") ?? FindSprite("ICO_Paw");

        private static void Fit(TMP_Text label, params string[] keys) => Problems.Append(OutfitTexts.Fit(label, keys));

        private static void Floor(TMP_Text label, float min)
        {
            label.enableAutoSizing = true;
            label.fontSizeMax = label.fontSize;
            label.fontSizeMin = Mathf.Max(30f, min);
        }

        // ---------------- Sheet « Tenue » ----------------

        /// <summary>
        /// « Tenue » between the favorite heart (56 to 212) and Details (narrowed to <paramref name="detailsWidth"/>), same height
        /// and margins: the crown icon and the word, centered on the visible pill. Hidden until the refuge says something fits.
        /// </summary>
        public static void AddSheetOutfit(AnimalSheetScreen screen, RectTransform card, UiColorTokens tokens, Sprite buttonSprite, float detailsWidth)
        {
            const float height = 156f;
            float left = 236f;
            float width = 1032f - 56f - detailsWidth - 24f - left;
            var root = UiPipeline.Rect("Outfit", card);
            UiPipeline.Place(root, Vector2.zero, Vector2.zero, new Vector2(left, 52f), new Vector2(width, height));
            var back = UiPipeline.AddImage("Button", root, buttonSprite, tokens.panelSecondary, Image.Type.Sliced);
            back.raycastTarget = true;
            UiPipeline.Stretch(back.rectTransform);
            var button = back.gameObject.AddComponent<Button>();
            UiPipeline.ConfigureButton(button, back);
            float pill = ProgressionUiPipeline.PillCenter(height);
            var icon = UiPipeline.AddImage("Icon", back.rectTransform, Crown(), Color.white, Image.Type.Simple);
            icon.preserveAspect = true;
            UiPipeline.Place(icon.rectTransform, Left, Center, new Vector2(70f, pill), new Vector2(76f, 76f));
            var label = UiPipeline.AddText("Label", back.rectTransform, TextRole.Button, 50f, tokens.textPrimary, "Outfit", 40f);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            UiPipeline.Place(label.rectTransform, Left, Left, new Vector2(122f, pill), new Vector2(width - 122f - 20f, 70f));
            Fit(label, OutfitText.SheetButton);
            root.gameObject.SetActive(false);
            screen.SetOutfitReferences(root, button, label);
        }

        // ---------------- Outfit picker ----------------

        public static OutfitPickerScreen BuildPickerPrefab(UiColorTokens tokens)
        {
            var panel = Kit(UiPipeline.PanelPath);
            var pillSprite = Kit(UiPipeline.PillPath);
            var circle = Kit(UiPipeline.CirclePath);
            var lockSprite = FindSprite("ICO_Lock") ?? FindSprite("ICO_Paw");
            var paw = FindSprite("ICO_Paw");
            var root = new GameObject("PF_UI_OutfitPicker", typeof(RectTransform));
            try
            {
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = PickerSortingOrder;
                UiPipeline.AddScaler(root);
                root.AddComponent<GraphicRaycaster>();
                var screen = root.AddComponent<OutfitPickerScreen>();
                var content = UiPipeline.Rect("Content", root.transform);
                UiPipeline.Stretch(content);
                var group = content.gameObject.AddComponent<CanvasGroup>();
                var safe = UiPipeline.Rect("SafeArea", content);
                UiPipeline.Stretch(safe);
                // No scrim: the refuge stays in view and the animal wears each change above the panel.
                var card = UiPipeline.Rect("Card", safe);
                UiPipeline.Place(card, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(1032f, PickerCardHeight));
                var back = UiPipeline.AddImage("Background", card, panel, tokens.backgroundLight, Image.Type.Sliced);
                UiPipeline.Stretch(back.rectTransform);
                back.raycastTarget = true;

                var title = UiPipeline.AddText("Title", card, TextRole.Heading, 64f, tokens.textPrimary, "Outfit", 48f);
                UiPipeline.Place(title.rectTransform, TopLeft, TopLeft, new Vector2(60f, -44f), new Vector2(620f, 84f));
                Fit(title, OutfitText.Title);
                var name = UiPipeline.AddText("Name", card, TextRole.Body, 44f, tokens.textSecondary, "Field Hamster", 34f);
                UiPipeline.Place(name.rectTransform, TopLeft, TopLeft, new Vector2(60f, -128f), new Vector2(780f, 60f));

                var closeRoot = UiPipeline.Rect("Close", card);
                UiPipeline.Place(closeRoot, new Vector2(1f, 1f), Center, new Vector2(-56f, 6f), new Vector2(128f, 128f));
                var closeImage = UiPipeline.AddImage("Button", closeRoot, circle, tokens.panelPrimary, Image.Type.Simple);
                closeImage.raycastTarget = true;
                UiPipeline.Stretch(closeImage.rectTransform);
                var close = closeImage.gameObject.AddComponent<Button>();
                UiPipeline.ConfigureButton(close, closeImage);
                var cross = UiPipeline.AddImage("Icon", closeImage.rectTransform, Kit(UiPipeline.CloseIconPath), tokens.textSecondary, Image.Type.Simple);
                UiPipeline.Place(cross.rectTransform, Center, Center, new Vector2(0f, 4f), new Vector2(60f, 60f));

                // Slots: Head, Neck, Back, Tail (a lock when hidden or nothing owned fits).
                int slots = OutfitPickerScreen.Slots;
                var slotButtons = new Button[slots];
                var slotBacks = new Image[slots];
                var slotLabels = new TMP_Text[slots];
                var slotLocks = new GameObject[slots];
                for (int i = 0; i < slots; i++)
                {
                    var slotBack = UiPipeline.AddImage("Slot" + i, card, pillSprite, tokens.panelSecondary, Image.Type.Sliced);
                    slotBack.pixelsPerUnitMultiplier = 1.6f;
                    slotBack.raycastTarget = true;
                    UiPipeline.Place(slotBack.rectTransform, TopLeft, TopLeft, new Vector2(42f + i * 242f, -210f), new Vector2(222f, 140f));
                    slotButtons[i] = slotBack.gameObject.AddComponent<Button>();
                    UiPipeline.ConfigureButton(slotButtons[i], slotBack);
                    slotBacks[i] = slotBack;
                    var slotLabel = UiPipeline.AddText("Label", slotBack.rectTransform, TextRole.Button, 42f, tokens.textPrimary, "Head", 32f);
                    slotLabel.alignment = TextAlignmentOptions.Center;
                    // The whole pill for the word (« Queue » at 30 pt); the lock sits on the pill's top right corner, clear of it.
                    slotLabel.fontSizeMin = 30f;
                    UiPipeline.Place(slotLabel.rectTransform, Center, Center, new Vector2(0f, -4f), new Vector2(190f, 70f));
                    Fit(slotLabel, OutfitText.SlotKey(OutfitPicker.Sockets[i]));
                    slotLabels[i] = slotLabel;
                    var lockIcon = UiPipeline.AddImage("Lock", slotBack.rectTransform, lockSprite, tokens.textDisabled, Image.Type.Simple);
                    UiPipeline.Place(lockIcon.rectTransform, new Vector2(1f, 1f), Center, new Vector2(-26f, -24f), new Vector2(32f, 32f));
                    lockIcon.gameObject.SetActive(false);
                    slotLocks[i] = lockIcon.gameObject;
                }

                var line = UiPipeline.AddText("Line", card, TextRole.Body, 40f, tokens.textSecondary, "Nothing for this spot yet", 32f);
                line.alignment = TextAlignmentOptions.Center;
                UiPipeline.Place(line.rectTransform, TopLeft, Center, new Vector2(516f, -400f), new Vector2(920f, 64f));
                Fit(line, OutfitText.SlotLocked, OutfitText.SlotHidden, OutfitText.Empty);

                // The owned accessories that fit the chosen slot.
                int choices = OutfitPickerScreen.Choices;
                var choiceButtons = new Button[choices];
                var choiceBacks = new Image[choices];
                var choiceIcons = new Image[choices];
                var choiceLabels = new TMP_Text[choices];
                for (int i = 0; i < choices; i++)
                {
                    var choiceBack = UiPipeline.AddImage("Choice" + i, card, panel, tokens.panelSecondary, Image.Type.Sliced);
                    choiceBack.pixelsPerUnitMultiplier = 1.6f;
                    choiceBack.raycastTarget = true;
                    UiPipeline.Place(choiceBack.rectTransform, TopLeft, Top, new Vector2(516f + (i - 1.5f) * 240f, -440f), new Vector2(220f, 250f));
                    choiceButtons[i] = choiceBack.gameObject.AddComponent<Button>();
                    UiPipeline.ConfigureButton(choiceButtons[i], choiceBack);
                    choiceBacks[i] = choiceBack;
                    choiceIcons[i] = UiPipeline.AddImage("Icon", choiceBack.rectTransform, Crown(), Color.white, Image.Type.Simple);
                    choiceIcons[i].preserveAspect = true;
                    UiPipeline.Place(choiceIcons[i].rectTransform, Top, Top, new Vector2(0f, -16f), new Vector2(110f, 110f));
                    var choiceLabel = UiPipeline.AddText("Label", choiceBack.rectTransform, TextRole.Label, 34f, tokens.textPrimary, "Flower Crown", 30f);
                    choiceLabel.textWrappingMode = TextWrappingModes.Normal;
                    choiceLabel.alignment = TextAlignmentOptions.Top;
                    // Two lines at most (« Couronne de / fleurs »), 24 units clear of the card's bottom.
                    UiPipeline.Place(choiceLabel.rectTransform, Top, Top, new Vector2(0f, -134f), new Vector2(212f, 92f));
                    Fit(choiceLabel, "entitlement.cosmetic.flower_crown.name", "accessory.fieldhamster.seedbasket.name");
                    choiceLabels[i] = choiceLabel;
                    choiceBack.gameObject.SetActive(false);
                }

                // Palettes of the worn accessory: a colored dot and its name.
                int palettes = OutfitPickerScreen.Palettes;
                var paletteButtons = new Button[palettes];
                var paletteBacks = new Image[palettes];
                var paletteDots = new Image[palettes];
                var paletteLabels = new TMP_Text[palettes];
                string[] paletteNames = { "meadow", "blossom", "frost" };
                for (int i = 0; i < palettes; i++)
                {
                    var chip = UiPipeline.AddImage("Palette" + i, card, pillSprite, tokens.panelSecondary, Image.Type.Sliced);
                    chip.pixelsPerUnitMultiplier = 2f;
                    chip.raycastTarget = true;
                    UiPipeline.Place(chip.rectTransform, TopLeft, Center, new Vector2(516f + (i - 1) * 320f, -770f), new Vector2(300f, 96f));
                    paletteButtons[i] = chip.gameObject.AddComponent<Button>();
                    UiPipeline.ConfigureButton(paletteButtons[i], chip);
                    paletteBacks[i] = chip;
                    paletteDots[i] = UiPipeline.AddImage("Dot", chip.rectTransform, circle, Color.white, Image.Type.Simple);
                    UiPipeline.Place(paletteDots[i].rectTransform, Left, Center, new Vector2(52f, 2f), new Vector2(48f, 48f));
                    var paletteLabel = UiPipeline.AddText("Label", chip.rectTransform, TextRole.Button, 38f, tokens.textPrimary, "Meadow", 30f);
                    paletteLabel.alignment = TextAlignmentOptions.MidlineLeft;
                    paletteLabel.enableAutoSizing = true;
                    paletteLabel.fontSizeMax = 38f;
                    paletteLabel.fontSizeMin = 30f;
                    UiPipeline.Place(paletteLabel.rectTransform, Left, Left, new Vector2(92f, 2f), new Vector2(188f, 60f));
                    Fit(paletteLabel, OutfitText.PaletteKey(paletteNames[i]));
                    paletteLabels[i] = paletteLabel;
                    chip.gameObject.SetActive(false);
                }

                var remove = ProgressionUiPipeline.PrimaryButton(card, tokens, "Remove", new Vector2(0f, 44f), new Vector2(420f, 150f), out var removeBack,
                    out var removeLabel);
                removeBack.color = tokens.panelSecondary;
                Floor(removeLabel, 40f);
                Fit(removeLabel, OutfitText.Remove);

                var toShop = ProgressionUiPipeline.PrimaryButton(card, tokens, "ToShop", new Vector2(0f, PickerCardHeight - 580f - 75f), new Vector2(560f, 150f),
                    out _, out var toShopLabel);
                Floor(toShopLabel, 40f);
                Fit(toShopLabel, OutfitText.ToShop);
                toShop.gameObject.SetActive(false);

                var keys = new List<string>();
                var sprites = new List<Sprite>();
                var crown = Crown();
                foreach (var pair in new[] { ("ICO_Accessory_FlowerCrown", crown), ("ICO_Cosmetic_FlowerCrown", crown),
                             ("ICO_Accessory_SeedBasket", FindSprite("ICO_Accessory_SeedBasket") ?? paw) })
                {
                    keys.Add(pair.Item1);
                    sprites.Add(pair.Item2);
                }
                foreach (var guid in AssetDatabase.FindAssets("ICO_Accessory_ t:Sprite", new[] { "Assets/_Game" }))
                {
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guid));
                    if (sprite != null && !keys.Contains(sprite.name))
                    {
                        keys.Add(sprite.name);
                        sprites.Add(sprite);
                    }
                }

                screen.SetReferences(canvas, content.gameObject, group, safe, card, close, title, name, slotButtons, slotBacks, slotLabels, slotLocks, line,
                    choiceButtons, choiceBacks, choiceIcons, choiceLabels, paletteButtons, paletteBacks, paletteDots, paletteLabels, remove, removeBack,
                    removeLabel, toShop, toShopLabel, tokens, paw, keys.ToArray(), sprites.ToArray());
                content.gameObject.SetActive(false);
                canvas.enabled = false;
                Directory.CreateDirectory(Path.GetDirectoryName(PickerPrefabPath));
                UiPipeline.MaskScrollContent(root);
                return PrefabUtility.SaveAsPrefabAsset(root, PickerPrefabPath).GetComponent<OutfitPickerScreen>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ---------------- Codex Companions tab ----------------

        /// <summary>
        /// « Espèces » / « Compagnons » tabs under the progress bar, and the Companions panel: one card per companion of
        /// <see cref="CompanionCatalog"/>, in its own scroll, behind the species sheet.
        /// </summary>
        public static void AddCodexCompanions(CodexScreen screen, RectTransform safe, RectTransform header, RectTransform sheetRect, UiColorTokens tokens)
        {
            var panelSprite = Kit(UiPipeline.PanelPath);
            var pillSprite = Kit(UiPipeline.PillPath);
            var circle = Kit(UiPipeline.CirclePath);
            var lockSprite = FindSprite("ICO_Lock") ?? FindSprite("ICO_Paw");
            var heart = FindSprite("ICO_Favorite_Heart") ?? FindSprite("ICO_Paw");

            Button Tab(string name, float x, string key, out Image back, out TMP_Text label)
            {
                back = UiPipeline.AddImage(name, header, pillSprite, tokens.panelSecondary, Image.Type.Sliced);
                back.pixelsPerUnitMultiplier = 1.6f;
                back.raycastTarget = true;
                UiPipeline.Place(back.rectTransform, Top, Center, new Vector2(x, -CodexHeaderHeight + 62f), new Vector2(440f, 100f));
                var button = back.gameObject.AddComponent<Button>();
                UiPipeline.ConfigureButton(button, back);
                label = UiPipeline.AddText("Label", back.rectTransform, TextRole.Button, 44f, tokens.textPrimary, "Companions", 32f);
                label.alignment = TextAlignmentOptions.Center;
                UiPipeline.Place(label.rectTransform, Center, Center, new Vector2(0f, 2f), new Vector2(392f, 70f));
                Fit(label, key);
                return button;
            }

            var speciesTab = Tab("TabSpecies", -230f, OutfitText.CodexSpeciesTab, out var speciesBack, out var speciesLabel);
            speciesBack.color = tokens.accentPrimary;
            var companionsTab = Tab("TabCompanions", 230f, OutfitText.CodexCompanionsTab, out var companionsBack, out var companionsLabel);

            var panel = UiPipeline.Rect("Companions", safe);
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = new Vector2(0f, -CodexHeaderHeight);
            panel.SetSiblingIndex(sheetRect.GetSiblingIndex());
            var viewport = UiPipeline.Rect("Viewport", panel);
            UiPipeline.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            var drag = viewport.gameObject.AddComponent<Image>();
            drag.color = new Color(1f, 1f, 1f, 0f);
            drag.raycastTarget = true;
            var list = UiPipeline.Rect("List", viewport);
            list.anchorMin = new Vector2(0f, 1f);
            list.anchorMax = new Vector2(1f, 1f);
            list.pivot = new Vector2(0.5f, 1f);
            list.anchoredPosition = Vector2.zero;
            list.sizeDelta = new Vector2(0f, CompanionOwnedHeight + 80f);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            UiPipeline.ConfigureScroll(scroll, viewport, list);

            var rows = CompanionCatalog.Rows;
            var cards = new CodexCompanionCardView[rows.Length];
            for (int i = 0; i < rows.Length; i++)
                cards[i] = BuildCompanionCard(list, "Companion" + i, rows[i], tokens, panelSprite, pillSprite, circle, lockSprite, heart);
            foreach (var graphic in panel.GetComponentsInChildren<MaskableGraphic>(true))
                graphic.maskable = true;
            panel.gameObject.SetActive(false);
            screen.SetCompanionReferences(speciesTab, speciesBack, speciesLabel, companionsTab, companionsBack, companionsLabel, panel.gameObject, scroll,
                list, cards);
        }

        private static CodexCompanionCardView BuildCompanionCard(RectTransform list, string name, CompanionRow row, UiColorTokens tokens, Sprite panelSprite,
            Sprite pillSprite, Sprite circle, Sprite lockSprite, Sprite heart)
        {
            // Sprite names follow the frozen ids: TH_variant_fieldhamster_<look>, TH_species_fieldhamster_Silhouette.
            string companion = "fieldhamster";
            string[] looks = { "fieldgold", "wheatcream", "fieldlavender" };
            var lookSprites = new Sprite[looks.Length];
            for (int i = 0; i < looks.Length; i++)
                lookSprites[i] = FindSprite("TH_variant_" + companion + "_" + looks[i]);
            var silhouetteSprite = FindSprite("TH_species_" + companion + "_Silhouette");

            var rect = UiPipeline.Rect(name, list);
            UiPipeline.Place(rect, Top, Top, new Vector2(0f, -40f), new Vector2(960f, CompanionOwnedHeight));
            var view = rect.gameObject.AddComponent<CodexCompanionCardView>();
            var back = UiPipeline.AddImage("Background", rect, panelSprite, tokens.panelPrimary, Image.Type.Sliced);
            UiPipeline.Stretch(back.rectTransform);

            // Owned: picture, name, bond, bonus, ladder, looks, « Tenue ».
            var owned = UiPipeline.Rect("Owned", rect);
            UiPipeline.Stretch(owned);
            var thumbBack = UiPipeline.AddImage("ThumbBack", owned, panelSprite, tokens.panelSecondary, Image.Type.Sliced);
            UiPipeline.Place(thumbBack.rectTransform, TopLeft, TopLeft, new Vector2(40f, -40f), new Vector2(300f, 300f));
            var thumb = UiPipeline.AddImage("Thumb", owned, lookSprites[0], Color.white, Image.Type.Simple);
            thumb.preserveAspect = true;
            UiPipeline.Place(thumb.rectTransform, TopLeft, TopLeft, new Vector2(50f, -50f), new Vector2(280f, 280f));
            var title = UiPipeline.AddText("Name", owned, TextRole.Heading, 64f, tokens.textPrimary, "Field Hamster", 40f);
            title.enableAutoSizing = true;
            title.fontSizeMax = 64f;
            title.fontSizeMin = 40f;
            UiPipeline.Place(title.rectTransform, TopLeft, TopLeft, new Vector2(380f, -56f), new Vector2(540f, 84f));
            Fit(title, "species.fieldhamster.name");
            var bondLabel = UiPipeline.AddText("BondLabel", owned, TextRole.Label, 40f, tokens.textSecondary, "Bond", 32f);
            UiPipeline.Place(bondLabel.rectTransform, TopLeft, TopLeft, new Vector2(380f, -156f), new Vector2(300f, 56f));
            Fit(bondLabel, FeedbackText.SheetBond);
            var bar = UiPipeline.AddImage("BondBar", owned, pillSprite, tokens.panelSecondary, Image.Type.Sliced);
            bar.pixelsPerUnitMultiplier = 4f;
            UiPipeline.Place(bar.rectTransform, TopLeft, Left, new Vector2(380f, -240f), new Vector2(420f, 36f));
            var fill = UiPipeline.AddImage("Fill", bar.rectTransform, pillSprite, new Color(0.94f, 0.56f, 0.72f, 1f), Image.Type.Sliced);
            fill.pixelsPerUnitMultiplier = 4f;
            UiPipeline.LeftFill(fill.rectTransform, 36f);
            var level = UiPipeline.AddText("BondLevel", owned, TextRole.NumberSmall, 48f, tokens.textPrimary, "0");
            level.alignment = TextAlignmentOptions.MidlineLeft;
            UiPipeline.Place(level.rectTransform, TopLeft, Left, new Vector2(824f, -240f), new Vector2(110f, 64f));
            var bonus = UiPipeline.AddText("Bonus", owned, TextRole.Body, 36f, tokens.textSecondary, "Light +5% for Meadow animals", 30f);
            bonus.textWrappingMode = TextWrappingModes.Normal;
            bonus.alignment = TextAlignmentOptions.TopLeft;
            UiPipeline.Place(bonus.rectTransform, TopLeft, TopLeft, new Vector2(380f, -284f), new Vector2(540f, 96f));
            Fit(bonus, OutfitText.BonusKey(companion));

            int rungs = CompanionLadder.Rungs;
            var groups = new CanvasGroup[rungs];
            var rungLevels = new TMP_Text[rungs];
            var kinds = new TMP_Text[rungs];
            var names = new TMP_Text[rungs];
            var locks = new GameObject[rungs];
            var done = new GameObject[rungs];
            string[] nameKeys = { "variant.fieldhamster.wheatcream.name", "variant.fieldhamster.fieldlavender.name", "accessory.fieldhamster.seedbasket.name",
                "pose.fill_cheeks.name", "title.full_cheeks.name" };
            for (int i = 0; i < rungs; i++)
            {
                var rung = UiPipeline.Rect("Rung" + i, owned);
                UiPipeline.Place(rung, TopLeft, TopLeft, new Vector2(40f, -400f - i * 120f), new Vector2(880f, 110f));
                groups[i] = rung.gameObject.AddComponent<CanvasGroup>();
                var badge = UiPipeline.AddImage("Level", rung, circle, tokens.accentPrimary, Image.Type.Simple);
                UiPipeline.Place(badge.rectTransform, TopLeft, TopLeft, new Vector2(0f, -7f), new Vector2(96f, 96f));
                rungLevels[i] = UiPipeline.AddText("Value", badge.rectTransform, TextRole.NumberSmall, 40f, tokens.textPrimary, "25");
                rungLevels[i].alignment = TextAlignmentOptions.Center;
                UiPipeline.Place(rungLevels[i].rectTransform, Center, Center, new Vector2(0f, 4f), new Vector2(80f, 56f));
                kinds[i] = UiPipeline.AddText("Kind", rung, TextRole.Label, 34f, tokens.textSecondary, "Photo pose", 30f);
                UiPipeline.Place(kinds[i].rectTransform, TopLeft, TopLeft, new Vector2(124f, -4f), new Vector2(640f, 48f));
                Fit(kinds[i], OutfitText.CodexRewardLook, OutfitText.CodexRewardAccessory, OutfitText.CodexRewardPose, OutfitText.CodexRewardTitle);
                names[i] = UiPipeline.AddText("Reward", rung, TextRole.Body, 42f, tokens.textPrimary, "Field Lavender", 32f);
                UiPipeline.Place(names[i].rectTransform, TopLeft, TopLeft, new Vector2(124f, -50f), new Vector2(640f, 56f));
                Fit(names[i], nameKeys[i]);
                var lockIcon = UiPipeline.AddImage("Lock", rung, lockSprite, tokens.textDisabled, Image.Type.Simple);
                UiPipeline.Place(lockIcon.rectTransform, TopLeft, Center, new Vector2(836f, -55f), new Vector2(60f, 60f));
                locks[i] = lockIcon.gameObject;
                var doneIcon = UiPipeline.AddImage("Done", rung, heart, Color.white, Image.Type.Simple);
                UiPipeline.Place(doneIcon.rectTransform, TopLeft, Center, new Vector2(836f, -55f), new Vector2(60f, 60f));
                doneIcon.gameObject.SetActive(false);
                done[i] = doneIcon.gameObject;
            }

            var looksTitle = UiPipeline.AddText("LooksTitle", owned, TextRole.Subheading, 44f, tokens.textSecondary, "Looks", 34f);
            UiPipeline.Place(looksTitle.rectTransform, TopLeft, TopLeft, new Vector2(40f, -1010f), new Vector2(600f, 60f));
            Fit(looksTitle, OutfitText.CodexLooks);
            var lookButtons = new Button[CodexCompanionCardView.Looks];
            var lookBacks = new Image[lookButtons.Length];
            var lookThumbs = new Image[lookButtons.Length];
            var lookLocks = new GameObject[lookButtons.Length];
            for (int i = 0; i < lookButtons.Length; i++)
            {
                var lookBack = UiPipeline.AddImage("Look" + i, owned, panelSprite, tokens.panelSecondary, Image.Type.Sliced);
                lookBack.pixelsPerUnitMultiplier = 1.6f;
                lookBack.raycastTarget = true;
                UiPipeline.Place(lookBack.rectTransform, TopLeft, TopLeft, new Vector2(40f + i * 230f, -1090f), new Vector2(200f, 200f));
                lookButtons[i] = lookBack.gameObject.AddComponent<Button>();
                UiPipeline.ConfigureButton(lookButtons[i], lookBack);
                lookBacks[i] = lookBack;
                lookThumbs[i] = UiPipeline.AddImage("Thumb", lookBack.rectTransform, lookSprites[i], Color.white, Image.Type.Simple);
                lookThumbs[i].preserveAspect = true;
                UiPipeline.Place(lookThumbs[i].rectTransform, Center, Center, Vector2.zero, new Vector2(176f, 176f));
                var lookLock = UiPipeline.AddImage("Lock", lookBack.rectTransform, lockSprite, tokens.textSecondary, Image.Type.Simple);
                UiPipeline.Place(lookLock.rectTransform, new Vector2(1f, 0f), Center, new Vector2(-36f, 36f), new Vector2(52f, 52f));
                lookLocks[i] = lookLock.gameObject;
            }
            var outfit = ProgressionUiPipeline.PrimaryButton(owned, tokens, "Outfit", new Vector2(0f, 50f), new Vector2(520f, 150f), out _, out var outfitLabel);
            Floor(outfitLabel, 40f);
            Fit(outfitLabel, OutfitText.SheetButton);

            // Not owned: the silhouette and « Pack de la Prairie » under it.
            var locked = UiPipeline.Rect("Locked", rect);
            UiPipeline.Place(locked, Top, Top, Vector2.zero, new Vector2(960f, CompanionLockedHeight));
            // The Codex's soft tint for what is still to find (a white shape would vanish on the cream card).
            var silhouette = UiPipeline.AddImage("Silhouette", locked, silhouetteSprite, new Color(0.47f, 0.4f, 0.33f, 0.5f), Image.Type.Simple);
            silhouette.preserveAspect = true;
            UiPipeline.Place(silhouette.rectTransform, Top, Top, new Vector2(0f, -40f), new Vector2(420f, 420f));
            var pack = ProgressionUiPipeline.PrimaryButton(locked, tokens, "Pack", new Vector2(0f, 60f), new Vector2(640f, 150f), out _, out var packLabel);
            Floor(packLabel, 40f);
            Fit(packLabel, "shop.pack.grassland.title");
            locked.gameObject.SetActive(false);

            view.SetReferences(rect, owned.gameObject, locked.gameObject, thumb, title, bondLabel, bar.rectTransform, fill.rectTransform, level, bonus, groups,
                rungLevels, kinds, names, locks, done, looksTitle, lookButtons, lookBacks, lookThumbs, lookLocks, outfit, outfitLabel, silhouette, pack,
                packLabel, companion, "shop.pack.grassland.title", lookSprites, silhouetteSprite, CompanionOwnedHeight, CompanionLockedHeight, tokens);
            return view;
        }

        // ---------------- Wiring ----------------

        /// <summary>The picker in the vertical slice, handed to VerticalSliceEntry (scene saved).</summary>
        public static void WireScene()
        {
            var scene = EditorSceneManager.OpenScene(UiPipeline.ScenePath, OpenSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<OutfitPickerScreen>(PickerPrefabPath);
            VerticalSliceEntry entry = null;
            OutfitPickerScreen picker = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                entry = entry != null ? entry : root.GetComponent<VerticalSliceEntry>();
                picker = picker != null ? picker : root.GetComponent<OutfitPickerScreen>();
            }
            if (entry == null)
                throw new InvalidOperationException("SceneEntry (VerticalSliceEntry) missing in " + UiPipeline.ScenePath);
            if (picker == null)
            {
                picker = ((GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, scene)).GetComponent<OutfitPickerScreen>();
                picker.name = "Screen_OutfitPicker";
            }
            typeof(VerticalSliceEntry).GetField("_outfitPicker", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(entry, picker);
            EditorUtility.SetDirty(entry);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
