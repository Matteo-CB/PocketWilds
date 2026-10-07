using System;
using System.IO;
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
    /// T35B HUD parts of the egg choice (doc 02 Œufs, sections 2.2, 3 and 5), added while UiPipeline builds the HUD:
    /// the egg pills under the hatch button (shell and spots tinted per egg, the ring of the chosen one, the stock of a
    /// Discovery egg), the chosen egg's name above the button, the × 1 / × 3 / × 10 pill right of the price, the Focus
    /// egg's three targets left of the button, the price word ("Free") in the price pill, the tinted egg on the button
    /// and the swipe on the button. Also PF_UI_RewardSummary (screen 028), the card at the end of a multi hatch, and its
    /// wiring in the vertical slice. Sprites come from SourceArt/UI/Tools/pw_ui_sprites_t35b.py.
    /// </summary>
    public static class EggUiPipeline
    {
        public const string ShellPath = UiPipeline.KitFolder + "/UI_Egg_Shell.png";
        public const string SpotsPath = UiPipeline.KitFolder + "/UI_Egg_Spots.png";
        public const string CatalogPath = "Assets/_Game/Content/Visuals/DEF_CodexVisuals.asset";
        public const string SummaryPath = ProgressionUiPipeline.Folder + "PF_UI_RewardSummary.prefab";

        /// <summary>Above the windows (34), under the level reward card (36) that may wait for it.</summary>
        public const int SummarySortingOrder = 35;

        /// <summary>Summary grid (reference px): five cells of 184 across, two rows.</summary>
        private const float CellWidth = 184f;
        private const float CellHeight = 312f;
        private const float CellPitchX = 196f;
        private const float CellPitchY = 330f;
        private const float GridY = 30f;
        private const float StarPitch = 30f;

        /// <summary>
        /// Pill size and pitch (reference px, 1080 x 2340 portrait): a 96 px egg in a 124 px touch area (48 dp), four of
        /// them fit between Codex and Shop.
        /// </summary>
        private const float PillSize = 96f;
        private const float TouchSize = 124f;
        private const float PillPitch = 124f;
        private const float TargetSize = 84f;
        private const float TargetPitch = 96f;

        private static Sprite Load(string path) =>
            AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? throw new FileNotFoundException("Run SourceArt/UI/Tools/pw_ui_sprites_t35b.py first.", path);

        public static void ImportSprites()
        {
            foreach (var path in new[] { ShellPath, SpotsPath })
            {
                if (!File.Exists(path))
                    throw new FileNotFoundException("Run SourceArt/UI/Tools/pw_ui_sprites_t35b.py first.", path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
        }

        [MenuItem("Pocket Wilds/UI/Build Egg Choice (T35B)", priority = 204)]
        public static void BuildMenu() => Debug.Log("[EggUiPipeline] " + BuildAll());

        /// <summary>Rebuilds the HUD (with its T35B parts) and the scene wiring.</summary>
        public static string BuildAll()
        {
            ImportSprites();
            var tokens = UiPipeline.EnsureTokens();
            UiPipeline.EnsureAtlas();
            UiPipeline.EnsureLocalization();
            UiPipeline.BuildHudPrefab(tokens);
            BuildRewardSummaryPrefab(tokens);
            AssetDatabase.SaveAssets();
            UiPipeline.SetupScene();
            WireScene();
            return "HUD egg choice and PF_UI_RewardSummary built and wired";
        }

        /// <summary>Called by UiPipeline.BuildHudPrefab once the hatch action, its price pill and its egg exist.</summary>
        public static void AddHudEggChoice(RectTransform action, RectTransform costRow, Image costIcon, Image button, Image eggIcon,
            RefugeHud hud, UiColorTokens tokens)
        {
            var pill = Load(UiPipeline.PillPath);
            var circle = Load(UiPipeline.CirclePath);
            var shellSprite = Load(ShellPath);
            var spotsSprite = Load(SpotsPath);
            var ringSprite = Load(UiPipeline.KitFolder + "/UI_Ring_Soft.png");
            var dotSprite = Load(UiPipeline.DotPath);
            var catalog = AssetDatabase.LoadAssetAtPath<CodexVisualCatalog>(CatalogPath)
                ?? throw new FileNotFoundException("DEF_CodexVisuals missing (run Render Animal Thumbnails).", CatalogPath);

            // Pills under the price, centered by EggChoiceBar for two, three or four eggs (no layout group on the HUD).
            var pillsRoot = UiPipeline.Rect("EggPills", action);
            UiPipeline.Place(pillsRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, -58f),
                new Vector2(4f * PillPitch, TouchSize));
            int slots = EggChoice.MaxSlots;
            var pills = new Button[slots];
            var shells = new Image[slots];
            var spots = new Image[slots];
            var rings = new Image[slots];
            var counts = new TMP_Text[slots];
            for (int i = 0; i < slots; i++)
            {
                // The touch area: an invisible circle (atlased sprite, so the batch holds) around the visible pill.
                var hit = UiPipeline.AddImage("Pill" + i, pillsRoot, circle, new Color(1f, 1f, 1f, 0f), Image.Type.Simple);
                hit.raycastTarget = true;
                UiPipeline.Place(hit.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2((i - (slots - 1) * 0.5f) * PillPitch, 0f), new Vector2(TouchSize, TouchSize));
                var back = UiPipeline.AddImage("Back", hit.rectTransform, circle, tokens.panelPrimary, Image.Type.Simple);
                UiPipeline.Place(back.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PillSize, PillSize));
                pills[i] = hit.gameObject.AddComponent<Button>();
                UiPipeline.ConfigureButton(pills[i], back);
                rings[i] = UiPipeline.AddImage("Ring", back.rectTransform, ringSprite, tokens.accentPrimary, Image.Type.Simple);
                UiPipeline.Place(rings[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(PillSize + 18f, PillSize + 18f));
                rings[i].enabled = false;
                shells[i] = UiPipeline.AddImage("Shell", back.rectTransform, shellSprite, Color.white, Image.Type.Simple);
                UiPipeline.Place(shells[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 3f), new Vector2(58f, 68f));
                spots[i] = UiPipeline.AddImage("Spots", back.rectTransform, spotsSprite, Color.white, Image.Type.Simple);
                UiPipeline.Place(spots[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 3f), new Vector2(58f, 68f));
                // The stock of a Discovery egg: a small warm dot with the number, top right.
                var badge = UiPipeline.AddImage("Count", back.rectTransform, dotSprite, tokens.currencyLight, Image.Type.Simple);
                UiPipeline.Place(badge.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-12f, -12f), new Vector2(48f, 48f));
                counts[i] = UiPipeline.AddText("Value", badge.rectTransform, TextRole.NumberSmall, 30f, tokens.textPrimary, "1", 22f);
                counts[i].alignment = TextAlignmentOptions.Center;
                UiPipeline.Place(counts[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(46f, 40f));
                badge.gameObject.SetActive(false);
            }
            pillsRoot.gameObject.SetActive(false);

            // The chosen egg's name above the button for a moment after a change.
            var nameRoot = UiPipeline.Rect("EggName", action);
            UiPipeline.Place(nameRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 48f), new Vector2(480f, 84f));
            var nameGroup = nameRoot.gameObject.AddComponent<CanvasGroup>();
            nameGroup.interactable = false;
            nameGroup.blocksRaycasts = false;
            var nameBack = UiPipeline.AddImage("Background", nameRoot, pill, tokens.panelPrimary, Image.Type.Sliced);
            UiPipeline.Stretch(nameBack.rectTransform);
            var nameLabel = UiPipeline.AddText("Label", nameRoot, TextRole.Label, 40f, tokens.textPrimary, "", 28f);
            nameLabel.alignment = TextAlignmentOptions.Center;
            UiPipeline.Stretch(nameLabel.rectTransform);
            nameLabel.rectTransform.offsetMin = new Vector2(28f, 6f);
            nameLabel.rectTransform.offsetMax = new Vector2(-28f, -6f);
            nameRoot.gameObject.SetActive(false);

            // × 1 / × 3 / × 10 right of the price pill, same height, in a 124 px tall touch area that rises beside the
            // button (48 dp); hidden until × 3 unlocks.
            var quantityRoot = UiPipeline.Rect("Quantity", action);
            UiPipeline.Place(quantityRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(206f, 4f), new Vector2(132f, TouchSize));
            var quantityHit = UiPipeline.AddImage("Button", quantityRoot, pill, new Color(1f, 1f, 1f, 0f), Image.Type.Sliced);
            quantityHit.raycastTarget = true;
            UiPipeline.Stretch(quantityHit.rectTransform);
            var quantityBack = UiPipeline.AddImage("Background", quantityHit.rectTransform, pill, tokens.panelPrimary, Image.Type.Sliced);
            UiPipeline.Place(quantityBack.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(132f, 84f));
            var quantityButton = quantityHit.gameObject.AddComponent<Button>();
            UiPipeline.ConfigureButton(quantityButton, quantityBack);
            var quantityLabel = UiPipeline.AddText("Label", quantityBack.rectTransform, TextRole.NumberSmall, 44f, tokens.textPrimary, "× 1", 30f);
            quantityLabel.alignment = TextAlignmentOptions.Center;
            UiPipeline.Stretch(quantityLabel.rectTransform);
            quantityLabel.rectTransform.offsetMin = new Vector2(10f, 4f);
            quantityLabel.rectTransform.offsetMax = new Vector2(-10f, 0f);
            quantityRoot.gameObject.SetActive(false);

            // The Focus egg's targets: a column of three portraits left of the button (silhouettes until found).
            var targetsRoot = UiPipeline.Rect("FocusTargets", action);
            UiPipeline.Place(targetsRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-226f, 160f),
                new Vector2(TargetSize + 8f, 3f * TargetPitch));
            var portraits = new Image[3];
            for (int i = 0; i < portraits.Length; i++)
            {
                var frame = UiPipeline.AddImage("Target" + i, targetsRoot, circle, tokens.panelPrimary, Image.Type.Simple);
                UiPipeline.Place(frame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, (1 - i) * TargetPitch),
                    new Vector2(TargetSize, TargetSize));
                portraits[i] = UiPipeline.AddImage("Portrait", frame.rectTransform, null, Color.white, Image.Type.Simple);
                portraits[i].preserveAspect = true;
                UiPipeline.Place(portraits[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 2f),
                    new Vector2(TargetSize - 14f, TargetSize - 14f));
            }
            targetsRoot.gameObject.SetActive(false);

            // The price word replaces the Light icon and amount ("Free", a Discovery egg's stock).
            var priceWord = UiPipeline.AddText("Word", costRow, TextRole.Label, 40f, tokens.textPrimary, "", 26f);
            priceWord.alignment = TextAlignmentOptions.Center;
            UiPipeline.Stretch(priceWord.rectTransform);
            priceWord.rectTransform.offsetMin = new Vector2(18f, 4f);
            priceWord.rectTransform.offsetMax = new Vector2(-18f, 0f);
            priceWord.gameObject.SetActive(false);

            // The button's egg in the chosen egg's colors, on the icon's frame (the icon hides while they show).
            var buttonShell = UiPipeline.AddImage("Shell", eggIcon.rectTransform, shellSprite, Color.white, Image.Type.Simple);
            UiPipeline.Place(buttonShell.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -2f), new Vector2(124f, 145f));
            buttonShell.gameObject.SetActive(false);
            var buttonSpots = UiPipeline.AddImage("Spots", eggIcon.rectTransform, spotsSprite, Color.white, Image.Type.Simple);
            UiPipeline.Place(buttonSpots.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -2f), new Vector2(124f, 145f));
            buttonSpots.gameObject.SetActive(false);

            var bar = action.gameObject.AddComponent<EggChoiceBar>();
            bar.SetReferences(pillsRoot, PillPitch, pills, shells, spots, rings, counts, nameRoot, nameGroup, nameLabel, quantityRoot, quantityButton,
                quantityLabel, targetsRoot, portraits, catalog, eggIcon, buttonShell, buttonSpots,
                new Color(tokens.textSecondary.r, tokens.textSecondary.g, tokens.textSecondary.b, 0.5f));
            button.gameObject.AddComponent<EggSwipe>().SetBar(bar);
            hud.SetEggChoiceReferences(bar, costIcon, priceWord);
        }

        // ---------------- Summary card (screen 028) ----------------

        public static RewardSummaryScreen BuildRewardSummaryPrefab(UiColorTokens tokens)
        {
            var circle = Load(UiPipeline.CirclePath);
            var pill = Load(UiPipeline.PillPath);
            var star = Load(UiPipeline.StarPath);
            var rarity = AssetDatabase.LoadAssetAtPath<UiRaritySet>(FeedbackUiPipeline.RaritySetPath)
                ?? throw new FileNotFoundException("DEF_UiRarity missing (run the T34A UI build).", FeedbackUiPipeline.RaritySetPath);
            var catalog = AssetDatabase.LoadAssetAtPath<CodexVisualCatalog>(CatalogPath)
                ?? throw new FileNotFoundException("DEF_CodexVisuals missing (run Render Animal Thumbnails).", CatalogPath);
            var root = new GameObject("PF_UI_RewardSummary", typeof(RectTransform));
            try
            {
                var screen = root.AddComponent<RewardSummaryScreen>();
                // The scrim does not close it: the newborns are looked at, then the button closes the card.
                var card = ProgressionUiPipeline.Window(root, SummarySortingOrder, tokens, new Vector2(1032f, 1180f), -20f, false, out var canvas,
                    out var content, out var group, out _);
                var title = UiPipeline.AddText("Title", card, TextRole.Heading, 64f, tokens.textPrimary, "Welcome to the refuge", 44f);
                title.alignment = TextAlignmentOptions.Center;
                UiPipeline.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -96f), new Vector2(900f, 110f));

                int cells = RewardSummaryScreen.MaxCells;
                var cellRects = new RectTransform[cells];
                var cellGroups = new CanvasGroup[cells];
                var frames = new Image[cells];
                var portraits = new Image[cells];
                var names = new TMP_Text[cells];
                var words = new TMP_Text[cells];
                var badges = new Image[cells];
                var stars = new Image[cells * RewardSummaryScreen.MaxStars];
                var newBadges = new GameObject[cells];
                var newLabels = new TMP_Text[cells];
                for (int i = 0; i < cells; i++)
                {
                    var cell = UiPipeline.Rect("Cell" + i, card);
                    UiPipeline.Place(cell, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CellWidth, CellHeight));
                    cellRects[i] = cell;
                    cellGroups[i] = cell.gameObject.AddComponent<CanvasGroup>();
                    cellGroups[i].interactable = false;
                    cellGroups[i].blocksRaycasts = false;
                    // The portrait in its rarity's frame (each rarity its own motif), the rarity badge at its corner.
                    var portraitBack = UiPipeline.AddImage("PortraitBack", cell, circle, tokens.panelSecondary, Image.Type.Simple);
                    UiPipeline.Place(portraitBack.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 70f),
                        new Vector2(150f, 150f));
                    portraits[i] = UiPipeline.AddImage("Portrait", cell, null, Color.white, Image.Type.Simple);
                    portraits[i].preserveAspect = true;
                    UiPipeline.Place(portraits[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 72f),
                        new Vector2(144f, 144f));
                    frames[i] = UiPipeline.AddImage("Frame", cell, rarity.Frame(RarityTier.Common), Color.white, Image.Type.Simple);
                    UiPipeline.Place(frames[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 70f),
                        new Vector2(176f, 176f));
                    badges[i] = UiPipeline.AddImage("Badge", cell, rarity.Badge(RarityTier.Common), Color.white, Image.Type.Simple);
                    UiPipeline.Place(badges[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(62f, 4f), new Vector2(54f, 54f));
                    names[i] = UiPipeline.AddText("Name", cell, TextRole.Label, 32f, tokens.textPrimary, "Rabbit", 22f);
                    names[i].alignment = TextAlignmentOptions.Center;
                    UiPipeline.Place(names[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -46f),
                        new Vector2(CellWidth, 46f));
                    for (int s = 0; s < RewardSummaryScreen.MaxStars; s++)
                    {
                        var image = UiPipeline.AddImage("Star" + s, cell, star, Color.white, Image.Type.Simple);
                        UiPipeline.Place(image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((s - 2.5f) * StarPitch, -88f),
                            new Vector2(28f, 28f));
                        stars[i * RewardSummaryScreen.MaxStars + s] = image;
                    }
                    words[i] = UiPipeline.AddText("Rarity", cell, TextRole.Caption, 28f, tokens.textSecondary, "Common", 20f);
                    words[i].alignment = TextAlignmentOptions.Center;
                    UiPipeline.Place(words[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -126f),
                        new Vector2(CellWidth, 40f));
                    // « Nouveau »: a warm pill across the top of the frame.
                    var newRoot = UiPipeline.Rect("New", cell);
                    UiPipeline.Place(newRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(150f, 50f));
                    var newBack = UiPipeline.AddImage("Background", newRoot, pill, tokens.accentPrimary, Image.Type.Sliced);
                    UiPipeline.Stretch(newBack.rectTransform);
                    newLabels[i] = UiPipeline.AddText("Label", newRoot, TextRole.Label, 28f, tokens.textPrimary, "New", 20f);
                    newLabels[i].alignment = TextAlignmentOptions.Center;
                    UiPipeline.Stretch(newLabels[i].rectTransform);
                    newLabels[i].rectTransform.offsetMin = new Vector2(10f, 2f);
                    newLabels[i].rectTransform.offsetMax = new Vector2(-10f, -2f);
                    newBadges[i] = newRoot.gameObject;
                }
                var button = ProgressionUiPipeline.PrimaryButton(card, tokens, "Continue", new Vector2(0f, 52f), new Vector2(520f, 150f), out _,
                    out var label);
                screen.SetReferences(canvas, content, group, card, title, cellRects, cellGroups, frames, portraits, names, words, badges, stars, newBadges,
                    newLabels, button, label, CellPitchX, CellPitchY, GridY, StarPitch, rarity, tokens, catalog);
                content.SetActive(false);
                canvas.enabled = false;
                Directory.CreateDirectory(Path.GetDirectoryName(SummaryPath));
                return PrefabUtility.SaveAsPrefabAsset(root, SummaryPath).GetComponent<RewardSummaryScreen>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>Places the summary card in the vertical slice and gives it to the scene entry.</summary>
        public static void WireScene()
        {
            var scene = EditorSceneManager.OpenScene(UiPipeline.ScenePath, OpenSceneMode.Single);
            var entry = Object.FindAnyObjectByType<VerticalSliceEntry>()
                ?? throw new InvalidOperationException("SceneEntry (VerticalSliceEntry) missing in " + UiPipeline.ScenePath);
            var summary = Object.FindAnyObjectByType<RewardSummaryScreen>(FindObjectsInactive.Include);
            if (summary == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SummaryPath);
                summary = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, scene)).GetComponent<RewardSummaryScreen>();
                summary.name = "Screen_RewardSummary";
            }
            var so = new SerializedObject(entry);
            so.FindProperty("_rewardSummary").objectReferenceValue = summary;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
