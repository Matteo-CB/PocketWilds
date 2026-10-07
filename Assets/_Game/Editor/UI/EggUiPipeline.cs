using System.IO;
using PocketWilds.Presentation;
using PocketWilds.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.Editor.UI
{
    /// <summary>
    /// T35B HUD parts of the egg choice (doc 02 Œufs, sections 2.2, 3 and 5), added while UiPipeline builds the HUD:
    /// the egg pills under the hatch button (shell and spots tinted per egg, the ring of the chosen one, the stock of a
    /// Discovery egg), the chosen egg's name above the button, the × 1 / × 3 / × 10 pill right of the price, the Focus
    /// egg's three targets left of the button, the price word ("Free") in the price pill, the tinted egg on the button
    /// and the swipe on the button. Sprites come from SourceArt/UI/Tools/pw_ui_sprites_t35b.py.
    /// </summary>
    public static class EggUiPipeline
    {
        public const string ShellPath = UiPipeline.KitFolder + "/UI_Egg_Shell.png";
        public const string SpotsPath = UiPipeline.KitFolder + "/UI_Egg_Spots.png";
        public const string CatalogPath = "Assets/_Game/Content/Visuals/DEF_CodexVisuals.asset";

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
            AssetDatabase.SaveAssets();
            UiPipeline.SetupScene();
            return "HUD egg choice built";
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
            UiPipeline.Place(quantityRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(196f, 4f), new Vector2(132f, TouchSize));
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
                quantityLabel, targetsRoot, portraits, catalog, eggIcon, buttonShell, buttonSpots);
            button.gameObject.AddComponent<EggSwipe>().SetBar(bar);
            hud.SetEggChoiceReferences(bar, costIcon, priceWord);
        }
    }
}
