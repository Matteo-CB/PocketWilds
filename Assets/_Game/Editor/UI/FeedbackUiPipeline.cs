using System;
using System.IO;
using PocketWilds.Bootstrap;
using PocketWilds.Content;
using PocketWilds.Editor.Import;
using PocketWilds.Presentation;
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
    /// T34A screens of the slice: the HUD's feedback parts (reveal banner, toasts, tutorial paw, collect motes, egg
    /// odds button), PF_UI_AnimalQuickSheet, PF_UI_Modal, PF_UI_Splash, DEF_UiRarity and Atlas_Codex, and their
    /// wiring in the vertical slice and the bootstrap scene. Sprites: SourceArt/UI/Tools/pw_ui_sprites.py (and
    /// pw_ui_sprites_t34a.py). Same rules as UiPipeline: 1080 x 2340 reference, text styles by role, own canvases
    /// for animated parts, no layout groups, content saved inactive.
    /// </summary>
    public static class FeedbackUiPipeline
    {
        public const string CodexArtFolder = TextureImportRules.UiRoot + "Codex";
        public const string CodexAtlasPath = TextureImportRules.UiRoot + "Atlases/Atlas_Codex.spriteatlasv2";
        public const string RaritySetPath = TextureImportRules.UiRoot + "DEF_UiRarity.asset";
        public const string SheetPrefabPath = "Assets/_Game/Prefabs/UI/Screens/PF_UI_AnimalQuickSheet.prefab";
        public const string ModalPrefabPath = "Assets/_Game/Prefabs/UI/Screens/PF_UI_Modal.prefab";
        public const string SplashPrefabPath = "Assets/_Game/Prefabs/UI/Screens/PF_UI_Splash.prefab";
        public const string CodexVisualsPath = "Assets/_Game/Content/Visuals/DEF_CodexVisuals.asset";
        public const string BootstrapScenePath = "Assets/_Game/Scenes/SC_Bootstrap.unity";

        public const int SheetSortingOrder = 26;
        public const int ModalSortingOrder = 40;
        public const int SplashSortingOrder = 100;

        public static readonly string[] Rarities = { "Common", "Uncommon", "Rare", "Epic", "Legendary", "Fabulous", "Secret" };
        public static readonly string[] Traits = { "Tiny", "Giant", "Shiny", "Golden", "Albino", "Melanistic" };

        private static string Kit(string name) => UiPipeline.KitFolder + "/" + name + ".png";
        private static string Icon(string name) => UiPipeline.IconFolder + "/" + name + ".png";
        private static string Codex(string name) => CodexArtFolder + "/" + name + ".png";

        private static Sprite Load(string path) =>
            AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? throw new FileNotFoundException("Run SourceArt/UI/Tools/pw_ui_sprites.py first.", path);

        [MenuItem("Pocket Wilds/UI/Build Feedback Screens (T34A)", priority = 201)]
        public static void BuildMenu() => Debug.Log("[FeedbackUiPipeline] " + BuildAll());

        /// <summary>Rebuilds the HUD (with its T34A parts), the three new screens and the wiring.</summary>
        public static string BuildAll()
        {
            ImportSprites();
            var tokens = UiPipeline.EnsureTokens();
            UiPipeline.EnsureAtlas();
            EnsureCodexAtlas();
            var rarity = EnsureRaritySet();
            UiPipeline.EnsureLocalization();
            UiPipeline.BuildHudPrefab(tokens);
            BuildSheetPrefab(tokens, rarity);
            BuildModalPrefab(tokens);
            BuildSplashPrefab(tokens);
            AssetDatabase.SaveAssets();
            UiPipeline.SetupScene();
            WireScene();
            WireBootstrap();
            return "HUD feedback, sheet, modal, splash built and wired";
        }

        public static void ImportSprites()
        {
            foreach (var r in Rarities)
            {
                AssetDatabase.ImportAsset(Codex("UI_Frame_" + r), ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(Codex("ICO_Rarity_" + r), ImportAssetOptions.ForceUpdate);
            }
            foreach (var t in Traits)
                AssetDatabase.ImportAsset(Codex("ICO_Trait_" + t), ImportAssetOptions.ForceUpdate);
            foreach (var k in new[] { "UI_Ring_Soft", "UI_Gesture_Tap", "UI_Splash_Gradient" })
                AssetDatabase.ImportAsset(Kit(k), ImportAssetOptions.ForceUpdate);
            foreach (var i in new[] { "ICO_Favorite_Heart", "ICO_Favorite_HeartOutline", "ICO_Info", "ICO_Paw", "ICO_Unlock", "ICO_Warning" })
                AssetDatabase.ImportAsset(Icon(i), ImportAssetOptions.ForceUpdate);
        }

        /// <summary>Atlas_Codex (UI doc ATLAS PLAN): rarity frames and badges, trait badges. Loaded with the screens using them.</summary>
        public static void EnsureCodexAtlas()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CodexAtlasPath));
            var atlas = new SpriteAtlasAsset();
            atlas.Add(new[] { AssetDatabase.LoadAssetAtPath<Object>(CodexArtFolder) });
            SpriteAtlasAsset.Save(atlas, CodexAtlasPath);
            AssetDatabase.ImportAsset(CodexAtlasPath, ImportAssetOptions.ForceUpdate);
            var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(CodexAtlasPath);
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

        public static UiRaritySet EnsureRaritySet()
        {
            var set = AssetDatabase.LoadAssetAtPath<UiRaritySet>(RaritySetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<UiRaritySet>();
                AssetDatabase.CreateAsset(set, RaritySetPath);
            }
            set.frames = Array.ConvertAll(Rarities, r => Load(Codex("UI_Frame_" + r)));
            set.badges = Array.ConvertAll(Rarities, r => Load(Codex("ICO_Rarity_" + r)));
            set.traits = Array.ConvertAll(Traits, t => Load(Codex("ICO_Trait_" + t)));
            set.favoriteOn = Load(Icon("ICO_Favorite_Heart"));
            set.favoriteOff = Load(Icon("ICO_Favorite_HeartOutline"));
            EditorUtility.SetDirty(set);
            return set;
        }

        // ---------------- HUD parts (called by UiPipeline.BuildHudPrefab) ----------------

        /// <summary>Adds the T34A parts to the HUD being built. Each animated part has its own canvas; none takes touches except the info button.</summary>
        public static void AddHudFeedback(GameObject root, RectTransform safe, RectTransform action, RectTransform chip, RectTransform chipIcon,
            RefugeHud hud, UiColorTokens tokens)
        {
            var rarity = AssetDatabase.LoadAssetAtPath<UiRaritySet>(RaritySetPath) ?? EnsureRaritySet();
            var pill = Load(UiPipeline.PillPath);
            var panel = Load(UiPipeline.PanelPath);
            var circle = Load(UiPipeline.CirclePath);
            var glow = Load(UiPipeline.GlowPath);
            var star = Load(UiPipeline.StarPath);

            // Reveal banner: upper middle of the screen, above the egg the camera frames.
            var bannerRoot = UiPipeline.Rect("RevealBanner", safe);
            bannerRoot.gameObject.AddComponent<Canvas>();
            var bannerGroup = Passive(bannerRoot.gameObject);
            UiPipeline.Place(bannerRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -560f), new Vector2(980f, 330f));
            var bannerGlow = UiPipeline.AddImage("Glow", bannerRoot, glow, new Color(1f, 1f, 1f, 0.55f), Image.Type.Simple);
            bannerGlow.preserveAspect = false;
            UiPipeline.Place(bannerGlow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(1120f, 560f));
            var bannerBack = UiPipeline.AddImage("Background", bannerRoot, panel, tokens.panelPrimary, Image.Type.Sliced);
            UiPipeline.Stretch(bannerBack.rectTransform);
            var bannerBadge = UiPipeline.AddImage("Badge", bannerRoot, rarity.Badge(RarityTier.Rare), Color.white, Image.Type.Simple);
            UiPipeline.Place(bannerBadge.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(124f, 124f));
            // Heading (Pocket Plush 60-90): Display cannot shrink under 96, and a 30 % longer title must still fit.
            var bannerTitle = UiPipeline.AddText("Title", bannerRoot, TextRole.Heading, 90f, tokens.textPrimary, "New species!", 60f);
            bannerTitle.alignment = TextAlignmentOptions.Center;
            UiPipeline.Place(bannerTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -122f), new Vector2(920f, 124f));
            var bannerName = UiPipeline.AddText("Name", bannerRoot, TextRole.Subheading, 54f, tokens.textSecondary, "Rabbit", 44f);
            bannerName.alignment = TextAlignmentOptions.Center;
            UiPipeline.Place(bannerName.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -214f), new Vector2(920f, 72f));
            var bannerStars = new Image[6];
            for (int i = 0; i < bannerStars.Length; i++)
            {
                bannerStars[i] = UiPipeline.AddImage("Star" + i, bannerRoot, star, Color.white, Image.Type.Simple);
                UiPipeline.Place(bannerStars[i].rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -280f), new Vector2(44f, 44f));
            }
            var reveal = bannerRoot.gameObject.AddComponent<RevealBanner>();
            reveal.SetReferences(bannerRoot, bannerGroup, bannerGlow, bannerBadge, bannerTitle, bannerName, bannerStars, rarity, tokens);
            bannerRoot.gameObject.SetActive(false);

            // Toasts: the slot under the Light chip (shared with the heat notice, which goes first).
            var toastRoot = UiPipeline.Rect("Toast", safe);
            toastRoot.gameObject.AddComponent<Canvas>();
            var toastGroup = Passive(toastRoot.gameObject);
            UiPipeline.Place(toastRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -198f), new Vector2(960f, 112f));
            var toastBack = UiPipeline.AddImage("Background", toastRoot, pill, tokens.panelPrimary, Image.Type.Sliced);
            UiPipeline.Stretch(toastBack.rectTransform);
            var toastIcon = UiPipeline.AddImage("Icon", toastRoot, Load(UiPipeline.LightIconPath), Color.white, Image.Type.Simple);
            UiPipeline.Place(toastIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(70f, 2f), new Vector2(80f, 80f));
            var toastLabel = UiPipeline.AddText("Label", toastRoot, TextRole.Label, 42f, tokens.textPrimary, "", 36f);
            toastLabel.alignment = TextAlignmentOptions.MidlineLeft;
            UiPipeline.Place(toastLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(128f, 2f), new Vector2(800f, 80f));
            var toasts = toastRoot.gameObject.AddComponent<ToastQueue>();
            var paw = Load(Icon("ICO_Paw"));
            toasts.SetReferences(toastRoot, toastGroup, toastIcon, toastLabel,
                new[] { Load(UiPipeline.LightIconPath), star, Load(Icon("ICO_Unlock")), paw, paw, Load(Icon("ICO_Warning")) },
                new[] { Color.white, tokens.rarity[4], Color.white, tokens.textSecondary, tokens.textSecondary, Color.white });
            toastRoot.gameObject.SetActive(false);

            // Tutorial paw: full screen space (it follows the egg wherever the camera puts it).
            var hintRoot = UiPipeline.Rect("TapHint", root.transform);
            hintRoot.gameObject.AddComponent<Canvas>();
            var hintGroup = Passive(hintRoot.gameObject);
            hintGroup.alpha = 0f;
            UiPipeline.Place(hintRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320f, 320f));
            var ring = UiPipeline.AddImage("Ring", hintRoot, Load(Kit("UI_Ring_Soft")), new Color(1f, 1f, 1f, 0f), Image.Type.Simple);
            UiPipeline.Place(ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220f, 220f));
            var hand = UiPipeline.AddImage("Paw", hintRoot, Load(Kit("UI_Gesture_Tap")), Color.white, Image.Type.Simple);
            // The paw floats just above the egg (never hiding it) and taps down toward it; the ring spreads on the egg.
            UiPipeline.Place(hand.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(170f, 170f));
            var tapHint = hintRoot.gameObject.AddComponent<TapHint>();
            tapHint.SetReferences(hintRoot, hintGroup, hand.rectTransform, ring);
            hintRoot.gameObject.SetActive(false);

            // Collect motes: full screen, twelve Light icons that fly into the chip; a ring on the chip as they land.
            var motesRoot = UiPipeline.Rect("CollectMotes", root.transform);
            motesRoot.gameObject.AddComponent<Canvas>();
            Passive(motesRoot.gameObject);
            UiPipeline.Stretch(motesRoot);
            var lightIcon = Load(UiPipeline.LightIconPath);
            var motes = new RectTransform[CollectMotes.MaxMotes];
            for (int i = 0; i < motes.Length; i++)
            {
                var mote = UiPipeline.AddImage("Mote" + i, motesRoot, lightIcon, Color.white, Image.Type.Simple);
                UiPipeline.Place(mote.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(72f, 72f));
                mote.gameObject.SetActive(false);
                motes[i] = mote.rectTransform;
            }
            var landRing = UiPipeline.AddImage("LandRing", chipIcon, Load(Kit("UI_Ring_Soft")), new Color(tokens.currencyLight.r, tokens.currencyLight.g, tokens.currencyLight.b, 0f), Image.Type.Simple);
            UiPipeline.Place(landRing.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(170f, 170f));
            var collect = motesRoot.gameObject.AddComponent<CollectMotes>();
            collect.SetReferences(motesRoot, motes, chipIcon, chip, landRing);
            motesRoot.gameObject.SetActive(false);

            // Egg odds: a small round "i" at the top right of the hatch button (doc 05: the info button shows the odds).
            var infoRoot = UiPipeline.Rect("Info", action);
            UiPipeline.Place(infoRoot, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(26f, -46f), new Vector2(128f, 128f));
            var infoImage = UiPipeline.AddImage("Button", infoRoot, circle, tokens.panelPrimary, Image.Type.Simple);
            infoImage.raycastTarget = true;
            UiPipeline.Stretch(infoImage.rectTransform);
            var infoButton = infoImage.gameObject.AddComponent<Button>();
            UiPipeline.ConfigureButton(infoButton, infoImage);
            var infoIcon = UiPipeline.AddImage("Icon", infoImage.rectTransform, Load(Icon("ICO_Info")), tokens.textSecondary, Image.Type.Simple);
            UiPipeline.Place(infoIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(68f, 68f));

            hud.SetFeedbackReferences(reveal, toasts, tapHint, collect, infoButton, infoRoot);
        }

        private static CanvasGroup Passive(GameObject go)
        {
            var group = go.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            return group;
        }

        // ---------------- Animal quick sheet ----------------

        public static AnimalSheetScreen BuildSheetPrefab(UiColorTokens tokens, UiRaritySet rarity)
        {
            var panel = Load(UiPipeline.PanelPath);
            var pill = Load(UiPipeline.PillPath);
            var circle = Load(UiPipeline.CirclePath);
            var buttonSprite = Load(UiPipeline.ButtonPrimaryPath);
            var star = Load(UiPipeline.StarPath);
            var root = new GameObject("PF_UI_AnimalQuickSheet", typeof(RectTransform));
            try
            {
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = SheetSortingOrder;
                UiPipeline.AddScaler(root);
                root.AddComponent<GraphicRaycaster>();
                var screen = root.AddComponent<AnimalSheetScreen>();

                var content = UiPipeline.Rect("Content", root.transform);
                UiPipeline.Stretch(content);
                var group = content.gameObject.AddComponent<CanvasGroup>();
                var scrimImage = UiPipeline.Rect("Scrim", content).gameObject.AddComponent<Image>();
                UiPipeline.Stretch(scrimImage.rectTransform);
                // Lighter than a full modal: the refuge stays the hero above the card.
                scrimImage.color = new Color(tokens.darkOverlay.r, tokens.darkOverlay.g, tokens.darkOverlay.b, tokens.darkOverlay.a * 0.6f);
                scrimImage.raycastTarget = true;
                scrimImage.maskable = false;
                var scrim = scrimImage.gameObject.AddComponent<Button>();
                scrim.transition = Selectable.Transition.None;
                var nav = scrim.navigation;
                nav.mode = Navigation.Mode.None;
                scrim.navigation = nav;

                var safe = UiPipeline.Rect("SafeArea", content);
                UiPipeline.Stretch(safe);
                var card = UiPipeline.Rect("Card", safe);
                // T36: 100 units taller for the « Friandise » row under the bond bar.
                UiPipeline.Place(card, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(1032f, SheetCardHeight));
                var back = UiPipeline.AddImage("Background", card, panel, tokens.backgroundLight, Image.Type.Sliced);
                UiPipeline.Stretch(back.rectTransform);
                // Touches on the card stay on the card (they never close it through the scrim).
                back.raycastTarget = true;

                // Portrait in its rarity frame.
                var portraitBack = UiPipeline.AddImage("PortraitBack", card, panel, tokens.panelSecondary, Image.Type.Sliced);
                portraitBack.pixelsPerUnitMultiplier = 1.6f;
                UiPipeline.Place(portraitBack.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(66f, -66f), new Vector2(308f, 308f));
                var portrait = UiPipeline.AddImage("Portrait", card, null, Color.white, Image.Type.Simple);
                UiPipeline.Place(portrait.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(72f, -72f), new Vector2(296f, 296f));
                var frame = UiPipeline.AddImage("Frame", card, rarity.Frame(RarityTier.Common), tokens.rarity[0], Image.Type.Simple);
                UiPipeline.Place(frame.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(360f, 360f));

                const float column = 422f;
                var name = UiPipeline.AddText("Name", card, TextRole.Heading, 84f, tokens.textPrimary, "Rabbit", 60f);
                UiPipeline.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(column, -52f), new Vector2(540f, 104f));
                var species = UiPipeline.AddText("Species", card, TextRole.Body, 46f, tokens.textSecondary, "Brown", 38f);
                UiPipeline.Place(species.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(column, -156f), new Vector2(560f, 62f));
                species.rectTransform.sizeDelta = new Vector2(1032f - column - 28f, 62f);
                var badge = UiPipeline.AddImage("RarityBadge", card, rarity.Badge(RarityTier.Common), tokens.rarity[0], Image.Type.Simple);
                UiPipeline.Place(badge.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(column - 4f, -226f), new Vector2(76f, 76f));
                var rarityLabel = UiPipeline.AddText("Rarity", card, TextRole.Label, 44f, tokens.textPrimary, "Common", 36f);
                UiPipeline.Place(rarityLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(column + 84f, -234f), new Vector2(300f, 60f));
                var stars = new Image[6];
                for (int i = 0; i < stars.Length; i++)
                {
                    stars[i] = UiPipeline.AddImage("Star" + i, card, star, Color.white, Image.Type.Simple);
                    UiPipeline.Place(stars[i].rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(column + i * 46f, -312f), new Vector2(42f, 42f));
                }
                var traits = new Image[AnimalSheetScreen.MaxTraits];
                for (int i = 0; i < traits.Length; i++)
                {
                    traits[i] = UiPipeline.AddImage("Trait" + i, card, rarity.traits.Length > 0 ? rarity.traits[0] : null, Color.white, Image.Type.Simple);
                    UiPipeline.Place(traits[i].rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(column + i * 96f, -368f), new Vector2(88f, 88f));
                    traits[i].gameObject.SetActive(false);
                }

                // Bond: label, bar, level.
                var bondLabel = UiPipeline.AddText("BondLabel", card, TextRole.Label, 42f, tokens.textSecondary, "Bond", 36f);
                UiPipeline.Place(bondLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(66f, -500f), new Vector2(200f, 60f));
                var bar = UiPipeline.AddImage("BondBar", card, pill, tokens.panelSecondary, Image.Type.Sliced);
                bar.pixelsPerUnitMultiplier = 4f;
                UiPipeline.Place(bar.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(270f, -500f), new Vector2(560f, 36f));
                var fill = UiPipeline.AddImage("Fill", bar.rectTransform, pill, new Color(0.94f, 0.56f, 0.72f, 1f), Image.Type.Sliced);
                fill.pixelsPerUnitMultiplier = 4f;
                UiPipeline.LeftFill(fill.rectTransform, 36f);
                var bondLevel = UiPipeline.AddText("BondLevel", card, TextRole.NumberSmall, 48f, tokens.textPrimary, "0");
                bondLevel.alignment = TextAlignmentOptions.MidlineLeft;
                UiPipeline.Place(bondLevel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(856f, -500f), new Vector2(120f, 64f));

                // Bottom row: favorite heart (round), Details (primary).
                var favoriteRoot = UiPipeline.Rect("Favorite", card);
                UiPipeline.Place(favoriteRoot, Vector2.zero, Vector2.zero, new Vector2(56f, 52f), new Vector2(156f, 156f));
                var favoriteImage = UiPipeline.AddImage("Button", favoriteRoot, circle, tokens.panelPrimary, Image.Type.Simple);
                favoriteImage.raycastTarget = true;
                UiPipeline.Stretch(favoriteImage.rectTransform);
                var favorite = favoriteImage.gameObject.AddComponent<Button>();
                UiPipeline.ConfigureButton(favorite, favoriteImage);
                var heart = UiPipeline.AddImage("Heart", favoriteImage.rectTransform, rarity.favoriteOff, tokens.textSecondary, Image.Type.Simple);
                UiPipeline.Place(heart.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(92f, 92f));

                var detailsImage = UiPipeline.AddImage("Details", card, buttonSprite, tokens.accentPrimary, Image.Type.Sliced);
                detailsImage.raycastTarget = true;
                UiPipeline.Place(detailsImage.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-56f, 52f), new Vector2(560f, 156f));
                var details = detailsImage.gameObject.AddComponent<Button>();
                UiPipeline.ConfigureButton(details, detailsImage);
                var detailsLabel = UiPipeline.AddText("Label", detailsImage.rectTransform, TextRole.Button, 58f, tokens.textPrimary, "Details", 44f);
                detailsLabel.alignment = TextAlignmentOptions.Center;
                UiPipeline.Place(detailsLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(500f, 100f));

                var closeRoot = UiPipeline.Rect("Close", card);
                // Floats on the card's corner: the name keeps the whole top row.
                UiPipeline.Place(closeRoot, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-56f, 6f), new Vector2(128f, 128f));
                var closeImage = UiPipeline.AddImage("Button", closeRoot, circle, tokens.panelPrimary, Image.Type.Simple);
                closeImage.raycastTarget = true;
                UiPipeline.Stretch(closeImage.rectTransform);
                var close = closeImage.gameObject.AddComponent<Button>();
                UiPipeline.ConfigureButton(close, closeImage);
                var closeIcon = UiPipeline.AddImage("Icon", closeImage.rectTransform, Load(UiPipeline.CloseIconPath), tokens.textSecondary, Image.Type.Simple);
                UiPipeline.Place(closeIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(60f, 60f));

                var visuals = AssetDatabase.LoadAssetAtPath<CodexVisualCatalog>(CodexVisualsPath);
                screen.SetReferences(canvas, content.gameObject, group, safe, card, scrim, close, portrait, frame, name, species, badge, rarityLabel,
                    stars, traits, bondLabel, bar.rectTransform, fill.rectTransform, bondLevel, favorite, heart, details, detailsLabel, rarity, tokens, visuals);
                AddSheetTreat(screen, card, tokens, buttonSprite);
                content.gameObject.SetActive(false);
                canvas.enabled = false;
                Directory.CreateDirectory(Path.GetDirectoryName(SheetPrefabPath));
                UiPipeline.MaskScrollContent(root);
                return PrefabUtility.SaveAsPrefabAsset(root, SheetPrefabPath).GetComponent<AnimalSheetScreen>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>The quick sheet's card height (T34A 860, + 100 in T36 for the treat row).</summary>
        public const float SheetCardHeight = 960f;

        /// <summary>The biome treats with their own icon on the sheet (the others show ICO_Treat).</summary>
        private static readonly string[,] SheetTreatIcons = { { "treat.clover_crunch", "ICO_Treat_CloverCrunch" } };

        /// <summary>
        /// The row under the bond bar (T36 treat, T38 « Brosser »): two buttons of the same look side by side across the card
        /// (48 to 984), or a lone one centered under the bar (270 to 830). Card space, top-left anchor, button centers.
        /// </summary>
        public const float SheetRowY = -620f;
        public const float SheetRowButtonWidth = 458f;
        public const float SheetRowButtonHeight = 132f;
        public static readonly Vector2 SheetRowSolo = new Vector2(550f, SheetRowY);
        public static readonly Vector2 SheetRowLeft = new Vector2(48f + SheetRowButtonWidth * 0.5f, SheetRowY);
        public static readonly Vector2 SheetRowRight = new Vector2(984f - SheetRowButtonWidth * 0.5f, SheetRowY);

        /// <summary>
        /// T36 (doc 01 section 10.1): the « Friandise » button under the bond bar (the treat's icon, its name with « ×n »
        /// owned under it, a small heart when the family prefers it), and four hearts that fly from it to the bar. Hidden
        /// until the sheet gets its treat data. T38: 458 units wide with its word and count stacked, so the « Brosser »
        /// button (<see cref="AddSheetGroom"/>) fits beside it; « Friandise » 30 % longer still fits at the Button minimum.
        /// </summary>
        private static void AddSheetTreat(AnimalSheetScreen screen, RectTransform card, UiColorTokens tokens, Sprite buttonSprite)
        {
            var center = new Vector2(0.5f, 0.5f);
            var left = new Vector2(0f, 0.5f);
            var treatSprite = Load(Icon("ICO_Treat"));
            var heartSprite = Load(Icon("ICO_Favorite_Heart"));

            var root = UiPipeline.Rect("Treat", card);
            // Centered under the bond bar (270 to 830) when alone; the sheet moves it left when « Brosser » shows.
            UiPipeline.Place(root, new Vector2(0f, 1f), center, SheetRowSolo, new Vector2(SheetRowButtonWidth, SheetRowButtonHeight));
            var back = UiPipeline.AddImage("Button", root, buttonSprite, tokens.panelSecondary, Image.Type.Sliced);
            back.pixelsPerUnitMultiplier = 1.3f;
            back.raycastTarget = true;
            UiPipeline.Stretch(back.rectTransform);
            var button = back.gameObject.AddComponent<Button>();
            UiPipeline.ConfigureButton(button, back);
            // No treat: the sheet sets its own soft look (idle color, faded icon); the button adds no grey veil on top.
            var colors = button.colors;
            colors.disabledColor = Color.white;
            button.colors = colors;
            var icon = UiPipeline.AddImage("Icon", back.rectTransform, treatSprite, Color.white, Image.Type.Simple);
            UiPipeline.Place(icon.rectTransform, left, center, new Vector2(60f, 4f), new Vector2(88f, 88f));
            var preferred = UiPipeline.AddImage("Preferred", back.rectTransform, heartSprite, Color.white, Image.Type.Simple);
            UiPipeline.Place(preferred.rectTransform, left, center, new Vector2(98f, 40f), new Vector2(42f, 42f));
            preferred.gameObject.SetActive(false);
            // Wide enough for « Friandise » 30 % longer at the Button minimum (Gate 12: 320 needed, box 330).
            var label = UiPipeline.AddText("Label", back.rectTransform, TextRole.Button, 50f, tokens.textPrimary, "Treat", 44f);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            UiPipeline.Place(label.rectTransform, left, left, new Vector2(114f, 24f), new Vector2(330f, 62f));
            var count = UiPipeline.AddText("Count", back.rectTransform, TextRole.NumberSmall, 40f, tokens.textPrimary, "×0", 36f);
            count.alignment = TextAlignmentOptions.MidlineLeft;
            UiPipeline.Place(count.rectTransform, left, left, new Vector2(116f, -30f), new Vector2(200f, 46f));
            root.gameObject.SetActive(false);

            // Hearts that rise from the treat to the bar's end (same top-left anchor as the bar: one space for both).
            var hearts = new RectTransform[4];
            var heartImages = new Image[hearts.Length];
            for (int i = 0; i < hearts.Length; i++)
            {
                heartImages[i] = UiPipeline.AddImage("TreatHeart" + i, card, heartSprite, Color.white, Image.Type.Simple);
                hearts[i] = heartImages[i].rectTransform;
                UiPipeline.Place(hearts[i], new Vector2(0f, 1f), center, new Vector2(550f, -560f), new Vector2(52f, 52f));
                hearts[i].gameObject.SetActive(false);
            }

            var ids = new uint[SheetTreatIcons.GetLength(0)];
            var icons = new Sprite[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                ids[i] = PocketWilds.Core.ContentId.FromSource(SheetTreatIcons[i, 0]).Value;
                icons[i] = Load(Icon(SheetTreatIcons[i, 1]));
            }
            screen.SetTreatReferences(root, button, back, icon, label, count, preferred.gameObject, treatSprite, ids, icons, hearts, heartImages);
            AddSheetGroom(screen, card, tokens, buttonSprite);
        }

        /// <summary>
        /// T38 (doc 01 S-ACT-001: « Brosser » / « Groom » in the sheet): the treat's twin on the right of the row, with the
        /// Grooming icon (ICO_Act_Grooming), its word, and under it « Gratuit » in a soft green tag (the free first game, doc
        /// 01 A15) or a small ticket and « 1 ». Hidden until the sheet hears Grooming can start.
        /// </summary>
        private static void AddSheetGroom(AnimalSheetScreen screen, RectTransform card, UiColorTokens tokens, Sprite buttonSprite)
        {
            var center = new Vector2(0.5f, 0.5f);
            var left = new Vector2(0f, 0.5f);
            var groomSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Icon(ActivityText.IconName(PocketWilds.Simulation.ActivityId.Grooming)))
                              ?? AssetDatabase.LoadAssetAtPath<Sprite>(Icon("ICO_Play")) ?? Load(Icon("ICO_Paw"));
            var ticketSprite = Load(Icon("ICO_Ticket"));

            var root = UiPipeline.Rect("Groom", card);
            UiPipeline.Place(root, new Vector2(0f, 1f), center, SheetRowRight, new Vector2(SheetRowButtonWidth, SheetRowButtonHeight));
            var back = UiPipeline.AddImage("Button", root, buttonSprite, tokens.panelSecondary, Image.Type.Sliced);
            back.pixelsPerUnitMultiplier = 1.3f;
            back.raycastTarget = true;
            UiPipeline.Stretch(back.rectTransform);
            var button = back.gameObject.AddComponent<Button>();
            UiPipeline.ConfigureButton(button, back);
            var icon = UiPipeline.AddImage("Icon", back.rectTransform, groomSprite, Color.white, Image.Type.Simple);
            UiPipeline.Place(icon.rectTransform, left, center, new Vector2(60f, 4f), new Vector2(88f, 88f));
            // « Brosser » 30 % longer fits at the Button minimum (290 needed, box 330).
            var label = UiPipeline.AddText("Label", back.rectTransform, TextRole.Button, 50f, tokens.textPrimary, "Groom", 44f);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            UiPipeline.Place(label.rectTransform, left, left, new Vector2(114f, 24f), new Vector2(330f, 62f));

            // « Gratuit »: a soft green tag like the hub card's (218 needed with 30 % more at the Label minimum, box 226).
            var free = UiPipeline.AddImage("Free", back.rectTransform, Load(UiPipeline.PillPath), tokens.success, Image.Type.Sliced);
            free.pixelsPerUnitMultiplier = 2.4f;
            UiPipeline.Place(free.rectTransform, left, left, new Vector2(110f, -30f), new Vector2(246f, 50f));
            var freeLabel = UiPipeline.AddText("Label", free.rectTransform, TextRole.Label, 36f, tokens.backgroundLight, "Free");
            freeLabel.alignment = TextAlignmentOptions.Center;
            UiPipeline.Place(freeLabel.rectTransform, center, center, new Vector2(0f, 2f), new Vector2(226f, 46f));
            free.gameObject.SetActive(false);

            // A game's cost: a small ticket and « 1 ».
            var ticket = UiPipeline.Rect("Ticket", back.rectTransform);
            UiPipeline.Place(ticket, left, left, new Vector2(112f, -30f), new Vector2(160f, 48f));
            var ticketIcon = UiPipeline.AddImage("Icon", ticket, ticketSprite, Color.white, Image.Type.Simple);
            UiPipeline.Place(ticketIcon.rectTransform, left, left, Vector2.zero, new Vector2(46f, 46f));
            var cost = UiPipeline.AddText("Count", ticket, TextRole.NumberSmall, 40f, tokens.textPrimary, "1", 36f);
            cost.alignment = TextAlignmentOptions.MidlineLeft;
            UiPipeline.Place(cost.rectTransform, left, left, new Vector2(54f, 2f), new Vector2(100f, 46f));
            ticket.gameObject.SetActive(false);
            root.gameObject.SetActive(false);

            screen.SetGroomReferences(root, button, label, free.gameObject, freeLabel, ticket.gameObject, SheetRowSolo, SheetRowLeft, SheetRowRight);
        }

        // ---------------- Modal ----------------

        public static ModalScreen BuildModalPrefab(UiColorTokens tokens)
        {
            var panel = Load(UiPipeline.PanelPath);
            var circle = Load(UiPipeline.CirclePath);
            var buttonSprite = Load(UiPipeline.ButtonPrimaryPath);
            var root = new GameObject("PF_UI_Modal", typeof(RectTransform));
            try
            {
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = ModalSortingOrder;
                UiPipeline.AddScaler(root);
                root.AddComponent<GraphicRaycaster>();
                var screen = root.AddComponent<ModalScreen>();

                var content = UiPipeline.Rect("Content", root.transform);
                UiPipeline.Stretch(content);
                var group = content.gameObject.AddComponent<CanvasGroup>();
                var scrimImage = UiPipeline.Rect("Scrim", content).gameObject.AddComponent<Image>();
                UiPipeline.Stretch(scrimImage.rectTransform);
                scrimImage.color = tokens.darkOverlay;
                scrimImage.raycastTarget = true;
                scrimImage.maskable = false;
                var scrim = scrimImage.gameObject.AddComponent<Button>();
                scrim.transition = Selectable.Transition.None;
                var nav = scrim.navigation;
                nav.mode = Navigation.Mode.None;
                scrim.navigation = nav;

                var card = UiPipeline.Rect("Card", content);
                UiPipeline.Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(860f, 640f));
                var back = UiPipeline.AddImage("Background", card, panel, tokens.backgroundLight, Image.Type.Sliced);
                UiPipeline.Stretch(back.rectTransform);
                back.raycastTarget = true;

                var iconBack = UiPipeline.AddImage("IconBack", card, circle, tokens.accentPrimary, Image.Type.Simple);
                UiPipeline.Place(iconBack.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(150f, 150f));
                var icon = UiPipeline.AddImage("Icon", iconBack.rectTransform, Load(Icon("ICO_Info")), Color.white, Image.Type.Simple);
                UiPipeline.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(84f, 84f));

                var title = UiPipeline.AddText("Title", card, TextRole.Heading, 70f, tokens.textPrimary, "Title", 52f);
                title.alignment = TextAlignmentOptions.Center;
                UiPipeline.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -124f), new Vector2(810f, 96f));
                var body = UiPipeline.AddText("Body", card, TextRole.Body, 42f, tokens.textPrimary, "");
                body.alignment = TextAlignmentOptions.Top;
                body.textWrappingMode = TextWrappingModes.Normal;
                body.lineSpacing = 8f;
                UiPipeline.Place(body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(740f, 200f));

                var confirmImage = UiPipeline.AddImage("Confirm", card, buttonSprite, tokens.accentPrimary, Image.Type.Sliced);
                confirmImage.raycastTarget = true;
                UiPipeline.Place(confirmImage.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(188f, 44f), new Vector2(350f, 148f));
                var confirm = confirmImage.gameObject.AddComponent<Button>();
                UiPipeline.ConfigureButton(confirm, confirmImage);
                var confirmLabel = UiPipeline.AddText("Label", confirmImage.rectTransform, TextRole.Button, 56f, tokens.textPrimary, "OK", 44f);
                confirmLabel.alignment = TextAlignmentOptions.Center;
                UiPipeline.Place(confirmLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(300f, 96f));

                var cancelImage = UiPipeline.AddImage("Cancel", card, buttonSprite, tokens.panelSecondary, Image.Type.Sliced);
                cancelImage.raycastTarget = true;
                UiPipeline.Place(cancelImage.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-188f, 44f), new Vector2(350f, 148f));
                var cancel = cancelImage.gameObject.AddComponent<Button>();
                UiPipeline.ConfigureButton(cancel, cancelImage);
                var cancelLabel = UiPipeline.AddText("Label", cancelImage.rectTransform, TextRole.Button, 56f, tokens.textSecondary, "Cancel", 44f);
                cancelLabel.alignment = TextAlignmentOptions.Center;
                UiPipeline.Place(cancelLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(300f, 96f));

                screen.SetReferences(canvas, content.gameObject, group, card, scrim, iconBack, icon, title, body, confirm, confirmLabel, cancel, cancelLabel,
                    new[] { Load(Icon("ICO_Info")), null, Load(Icon("ICO_Warning")) },
                    new[] { tokens.accentPrimary, tokens.accentPrimary, tokens.backgroundLight });
                content.gameObject.SetActive(false);
                canvas.enabled = false;
                Directory.CreateDirectory(Path.GetDirectoryName(ModalPrefabPath));
                UiPipeline.MaskScrollContent(root);
                return PrefabUtility.SaveAsPrefabAsset(root, ModalPrefabPath).GetComponent<ModalScreen>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ---------------- Splash ----------------

        public static SplashScreen BuildSplashPrefab(UiColorTokens tokens)
        {
            var root = new GameObject("PF_UI_Splash", typeof(RectTransform));
            try
            {
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = SplashSortingOrder;
                UiPipeline.AddScaler(root);
                var group = root.AddComponent<CanvasGroup>();
                group.interactable = false;
                group.blocksRaycasts = false;
                var screen = root.AddComponent<SplashScreen>();

                var background = UiPipeline.AddImage("Background", root.transform, Load(Kit("UI_Splash_Gradient")), Color.white, Image.Type.Simple);
                background.preserveAspect = false;
                UiPipeline.Stretch(background.rectTransform);
                var glow = UiPipeline.AddImage("Glow", root.transform, Load(UiPipeline.GlowPath), new Color(1f, 1f, 1f, 0.7f), Image.Type.Simple);
                UiPipeline.Place(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 180f), new Vector2(1200f, 1200f));
                // Placeholder lockup (the game's name in Pocket Plush, live text) until the final name and logo.
                var logo = UiPipeline.AddText("Logo", root.transform, TextRole.Display, 150f, new Color(0.42f, 0.3f, 0.2f, 1f), "Pocket Wilds", 110f);
                logo.alignment = TextAlignmentOptions.Center;
                UiPipeline.Place(logo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 180f), new Vector2(980f, 240f));
                var paw = Load(Icon("ICO_Paw"));
                var paws = new RectTransform[3];
                for (int i = 0; i < paws.Length; i++)
                {
                    var p = UiPipeline.AddImage("Paw" + i, root.transform, paw, tokens.accentPrimary, Image.Type.Simple);
                    UiPipeline.Place(p.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), new Vector2((i - 1) * 120f, -240f), new Vector2(76f, 76f));
                    paws[i] = p.rectTransform;
                }
                screen.SetReferences(group, paws, logo.rectTransform);
                Directory.CreateDirectory(Path.GetDirectoryName(SplashPrefabPath));
                return PrefabUtility.SaveAsPrefabAsset(root, SplashPrefabPath).GetComponent<SplashScreen>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ---------------- Wiring ----------------

        /// <summary>Sheet and modal in the vertical slice, assigned to VerticalSliceEntry (scene saved).</summary>
        public static void WireScene()
        {
            var scene = EditorSceneManager.OpenScene(UiPipeline.ScenePath, OpenSceneMode.Single);
            // Prefabs loaded after the scene opens: assets loaded before a single-mode open can be unloaded with it.
            var sheetPrefab = AssetDatabase.LoadAssetAtPath<AnimalSheetScreen>(SheetPrefabPath);
            var modalPrefab = AssetDatabase.LoadAssetAtPath<ModalScreen>(ModalPrefabPath);
            VerticalSliceEntry entry = null;
            AnimalSheetScreen sheet = null;
            ModalScreen modal = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                entry = entry ?? root.GetComponent<VerticalSliceEntry>();
                sheet = sheet ?? root.GetComponent<AnimalSheetScreen>();
                modal = modal ?? root.GetComponent<ModalScreen>();
            }
            if (entry == null)
                throw new InvalidOperationException("SceneEntry (VerticalSliceEntry) missing in " + UiPipeline.ScenePath);
            if (sheet == null)
            {
                sheet = ((GameObject)PrefabUtility.InstantiatePrefab(sheetPrefab.gameObject, scene)).GetComponent<AnimalSheetScreen>();
                sheet.name = "Screen_AnimalSheet";
            }
            if (modal == null)
            {
                modal = ((GameObject)PrefabUtility.InstantiatePrefab(modalPrefab.gameObject, scene)).GetComponent<ModalScreen>();
                modal.name = "Screen_Modal";
            }
            // SerializedObject refuses some references from editor tool calls: the fields are set directly (as T34).
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            typeof(VerticalSliceEntry).GetField("_animalSheet", flags).SetValue(entry, sheet);
            typeof(VerticalSliceEntry).GetField("_modal", flags).SetValue(entry, modal);
            EditorUtility.SetDirty(entry);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>The splash prefab on the bootstrap (scene saved).</summary>
        public static void WireBootstrap()
        {
            var scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
            var splashPrefab = AssetDatabase.LoadAssetAtPath<SplashScreen>(SplashPrefabPath);
            var bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            if (bootstrap == null)
                throw new InvalidOperationException("SC_Bootstrap has no GameBootstrap.");
            typeof(GameBootstrap).GetField("_splashPrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(bootstrap, splashPrefab);
            EditorUtility.SetDirty(bootstrap);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
