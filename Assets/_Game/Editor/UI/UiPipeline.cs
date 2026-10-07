using System;
using System.IO;
using PocketWilds.Content;
using PocketWilds.Editor.Import;
using PocketWilds.Presentation;
using PocketWilds.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.UI
{
    /// <summary>
    /// T17 HUD build: color tokens, Atlas_CommonHUD, PF_UI_HUD_Main and its wiring in the vertical slice.
    /// Sprites come from SourceArt/UI/Tools/pw_ui_sprites.py (run it first when shapes change).
    /// Layout targets a 1080x2340 portrait reference; split canvases (Light chip / hatch action).
    /// </summary>
    public static class UiPipeline
    {
        public const string KitFolder = TextureImportRules.UiRoot + "Kit";
        public const string IconFolder = TextureImportRules.UiRoot + "Icons";
        public const string AtlasPath = TextureImportRules.UiRoot + "Atlases/Atlas_CommonHUD.spriteatlasv2";
        public const string TokensPath = TextureImportRules.UiRoot + "DEF_UiColorTokens.asset";
        public const string PillPath = KitFolder + "/UI_Pill_9Slice.png";
        public const string CirclePath = KitFolder + "/UI_Button_IconCircle.png";
        public const string LightIconPath = IconFolder + "/ICO_Currency_Light.png";
        public const string EggIconPath = IconFolder + "/ICO_Hatch_Egg.png";
        public const string PanelPath = KitFolder + "/UI_Panel_Primary_9Slice.png";
        public const string ButtonPrimaryPath = KitFolder + "/UI_Button_Primary_9Slice.png";
        public const string GlowPath = KitFolder + "/UI_Glow_Soft.png";
        public const string HudPrefabPath = "Assets/_Game/Prefabs/UI/Screens/PF_UI_HUD_Main.prefab";
        public const string OfflineReturnPrefabPath = "Assets/_Game/Prefabs/UI/Screens/PF_UI_OfflineReturn.prefab";
        public const string CodexPrefabPath = "Assets/_Game/Prefabs/UI/Screens/PF_UI_Codex.prefab";
        public const string DotPath = KitFolder + "/UI_Dot.png";
        public const string StarPath = IconFolder + "/ICO_Rarity_Star.png";
        public const string BackIconPath = IconFolder + "/ICO_Back_Chevron.png";
        public const string CodexIconPath = IconFolder + "/ICO_Codex_Book.png";
        public const string MasteryIconPath = IconFolder + "/ICO_Mastery_StarPaw.png";
        public const string GemsIconPath = IconFolder + "/ICO_Currency_Gems.png";
        public const string ShopIconPath = IconFolder + "/ICO_Shop_Bag.png";
        public const string CloseIconPath = IconFolder + "/ICO_Close_X.png";
        public const string LocalizationFolder = "Assets/_Game/Content/Localization";
        public const string ContentDatabasePath = "Assets/_Game/Content/DEF_ContentDatabase.asset";
        public const string HatchConfigPath = "Assets/_Game/Content/Visuals/DEF_HatchPresentation.asset";
        public const string ScenePath = "Assets/_Game/Scenes/SC_VerticalSlice_Grassland.unity";

        public static readonly Vector2 ReferenceResolution = new Vector2(1080f, 2340f);

        /// <summary>
        /// Shipped player-facing text (Writing Guide: short, no em dash). English first: it is the fallback.
        /// Columns: key, en, fr.
        /// </summary>
        public static readonly string[,] Texts =
        {
            { UiTextKeys.OfflineWelcomeBack, "Welcome back", "Bon retour" },
            { UiTextKeys.CollectButton, "Collect", "Récolter" },
            { UiTextKeys.CodexTitle, "Codex", "Codex" },
            { UiTextKeys.CodexVariants, "Variants", "Variantes" },
            { UiTextKeys.CodexNew, "New", "Nouveau" },
            { UiTextKeys.CodexUnknown, "???", "???" },
            // Shop (T28). Writing Guide: what you get, the price, one-time or not; no pressure words.
            { UiTextKeys.ShopTitle, "Shop", "Boutique" },
            { UiTextKeys.ShopFeatured, "Featured", "À la une" },
            { UiTextKeys.ShopGems, "Gems", "Gemmes" },
            { UiTextKeys.ShopBestValue, "Best value", "Meilleure offre" },
            { UiTextKeys.ShopOwned, "Owned", "Acheté" },
            { UiTextKeys.ShopOneTime, "One-time offer", "Offre unique" },
            { UiTextKeys.ShopContentsGems, "{0} Gems", "{0} Gemmes" },
            { UiTextKeys.ShopRestore, "Restore purchases", "Restaurer les achats" },
            { UiTextKeys.ShopThanks, "Thank you. +{0} Gems", "Merci. +{0} Gemmes" },
            { UiTextKeys.ShopThanksItem, "Thank you", "Merci" },
            { UiTextKeys.ShopFailed, "Purchase failed. Try again.", "Achat impossible. Réessaie." },
            { UiTextKeys.ShopPending, "Purchase pending. It will arrive soon.", "Achat en attente. Il arrivera bientôt." },
            { UiTextKeys.ShopUnavailable, "Shop unavailable. Try again later.", "Boutique indisponible. Réessaie plus tard." },
            { UiTextKeys.ShopNotEnoughGems, "Not enough Gems.", "Pas assez de Gemmes." },
            { UiTextKeys.ShopRestored, "Purchases restored", "Achats restaurés" },
            { UiTextKeys.ShopNothingToRestore, "Nothing to restore", "Rien à restaurer" },
            // T34: heat protection lowered the picture. Calm, no alarm word.
            { UiTextKeys.QualityThermalNotice, "Phone is warm: lighter visuals to stay smooth", "Téléphone chaud : image allégée pour rester fluide" },
            // T34A: reveal banner, rarity names (shape and stars too, never color alone), egg odds, toasts, animal sheet.
            { FeedbackText.RevealNewSpecies, "New species!", "Nouvelle espèce !" },
            { FeedbackText.RevealNewVariant, "New variant!", "Nouvelle variante !" },
            { FeedbackText.RarityCommon, "Common", "Commun" },
            { FeedbackText.RarityUncommon, "Uncommon", "Peu commun" },
            { FeedbackText.RarityRare, "Rare", "Rare" },
            { FeedbackText.RarityEpic, "Epic", "Épique" },
            { FeedbackText.RarityLegendary, "Legendary", "Légendaire" },
            { FeedbackText.RarityFabulous, "Fabulous", "Fabuleux" },
            { FeedbackText.EggOddsTitle, "Egg odds", "Chances de l'œuf" },
            { FeedbackText.EggOddsLine, "{0}: {1}%", "{0} : {1} %" },
            { FeedbackText.EggOddsGuarantee, "After {0} eggs without a new species, the next one brings one.",
                "Après {0} œufs sans nouvelle espèce, le suivant en apporte une." },
            { FeedbackText.ModalOkButton, "OK", "OK" },
            { FeedbackText.ModalCancelButton, "Cancel", "Annuler" },
            { FeedbackText.ToastShopOpen, "The shop is open", "La boutique est ouverte" },
            { FeedbackText.ToastNotEnoughLight, "Not enough Light yet", "Pas encore assez de Lumière" },
            { FeedbackText.SheetDetailsButton, "Details", "Détails" },
            { FeedbackText.SheetBond, "Bond", "Lien" },
            { "shop.starter_pack.title", "Starter Pack", "Pack de départ" },
            { "entitlement.cosmetic.flower_crown.name", "Flower Crown", "Couronne de fleurs" },
            // Content names (display keys of biome, egg, species and variant definitions).
            { "biome.grassland.name", "Meadow", "Prairie" },
            { "egg.grassland.basic.name", "Meadow Egg", "Œuf de prairie" },
            { "egg.grassland.focus.name", "Precious Meadow Egg", "Œuf de prairie précieux" },
            { "species.rabbit.name", "Rabbit", "Lapin" },
            { "variant.rabbit.brown.name", "Brown", "Brun" },
            { "variant.rabbit.white.name", "White", "Blanc" },
            { "variant.rabbit.grey.name", "Grey", "Gris" },
            { "variant.rabbit.black.name", "Black", "Noir" },
            { "variant.rabbit.spotted.name", "Spotted", "Tacheté" },
            { "variant.rabbit.sakura.name", "Sakura", "Sakura" },
            { "species.squirrel.name", "Squirrel", "Écureuil" },
            { "variant.squirrel.red.name", "Red", "Roux" },
            { "variant.squirrel.brown.name", "Brown", "Brun" },
            { "variant.squirrel.grey.name", "Grey", "Gris" },
            { "variant.squirrel.cream.name", "Cream", "Crème" },
            { "variant.squirrel.black.name", "Black", "Noir" },
            { "variant.squirrel.autumn.name", "Autumn", "Automne" },
            // T35A zones (doc 02 Améliorations, section 11.7). Zone names carry their article: "Open the Orchard".
            { ProgressionText.ZoneOpenTitle, "Open {zone}", "Ouvrir {zone}" },
            { ProgressionText.ZoneOpenButton, "Open", "Ouvrir" },
            { ProgressionText.ZoneOpenPrice, "Price: {price} Light", "Prix : {price} Lumière" },
            { ProgressionText.ZoneGainProduction, "× 1.25 Light", "× 1,25 Lumière" },
            { ProgressionText.ZoneGainAnimals, "+3 animals on screen", "+3 animaux visibles" },
            { ProgressionText.ZoneGainDecor, "6 decoration spots", "6 places de décoration" },
            { ProgressionText.ZoneLockedMastery, "{mastery} {level} first", "D'abord : {mastery} {level}" },
            { ProgressionText.ZoneNeedLight, "A little more Light and it opens.", "Encore un peu de Lumière et elle s'ouvre." },
            { ProgressionText.ZoneLater, "The zone before it opens first.", "La zone d'avant s'ouvre d'abord." },
            { ProgressionText.BiomeMastery("biome.grassland"), "Meadow mastery", "Maîtrise de la Prairie" },
            { "zone.grassland.start.name", "the Start", "le Départ" },
            { "zone.grassland.meadow.name", "the Flower Meadow", "la Prairie fleurie" },
            { "zone.grassland.streambend.name", "the Stream Bend", "le Coude du ruisseau" },
            { "zone.grassland.orchard.name", "the Orchard", "le Verger" },
            { "zone.grassland.hill.name", "the Hill", "la Colline" },
            { "zone.grassland.windmillrise.name", "Windmill Rise", "la Butte du moulin" },
            { "zone.grassland.pondedge.name", "the Pond Edge", "le Bord de la mare" },
            { "zone.grassland.forestedge.name", "the Forest Edge", "la Lisière" },
            { "species.hedgehog.name", "Hedgehog", "Hérisson" },
            { "variant.hedgehog.brown.name", "Brown", "Brun" },
            { "variant.hedgehog.cream.name", "Cream", "Crème" },
            { "variant.hedgehog.grey.name", "Grey", "Gris" },
            { "variant.hedgehog.black.name", "Black", "Noir" },
            { "variant.hedgehog.white.name", "White", "Blanc" },
            { "variant.hedgehog.mushroom.name", "Mushroom", "Champignon" },
        };

        public static readonly string[] Languages = { "en", "fr" };

        [MenuItem("Pocket Wilds/UI/Build HUD and Screens", priority = 200)]
        public static void BuildAll()
        {
            ReimportSprites();
            var tokens = EnsureTokens();
            EnsureAtlas();
            PocketWilds.Editor.Content.BiomePipeline.EnsureBiomes();
            EnsureLocalization();
            BuildHudPrefab(tokens);
            BuildOfflineReturnPrefab(tokens);
            BuildCodexPrefab(tokens);
            AssetDatabase.SaveAssets();
            SetupScene();
            Debug.Log("[UiPipeline] HUD and screens built and wired.");
        }

        /// <summary>LOC_en / LOC_fr from <see cref="Texts"/>, registered in the content database.</summary>
        public static void EnsureLocalization()
        {
            Directory.CreateDirectory(LocalizationFolder);
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(ContentDatabasePath)
                           ?? throw new InvalidOperationException("Missing " + ContentDatabasePath);
            database.localization.RemoveAll(t => t == null);
            for (int language = 0; language < Languages.Length; language++)
            {
                string path = $"{LocalizationFolder}/LOC_{Languages[language]}.asset";
                var table = AssetDatabase.LoadAssetAtPath<LocalizationTableAsset>(path);
                if (table == null)
                {
                    table = ScriptableObject.CreateInstance<LocalizationTableAsset>();
                    AssetDatabase.CreateAsset(table, path);
                }
                table.languageCode = Languages[language];
                for (int i = 0; i < Texts.GetLength(0); i++)
                    table.Set(Texts[i, 0], Texts[i, language + 1]);
                EditorUtility.SetDirty(table);
                if (!database.localization.Contains(table))
                    database.localization.Insert(Mathf.Min(language, database.localization.Count), table);
            }
            EditorUtility.SetDirty(database);
        }

        public static void ReimportSprites()
        {
            foreach (var path in new[] { PillPath, CirclePath, PanelPath, ButtonPrimaryPath, GlowPath, DotPath, LightIconPath, EggIconPath,
                         StarPath, BackIconPath, CodexIconPath, MasteryIconPath })
            {
                if (!File.Exists(path))
                    throw new FileNotFoundException("Run SourceArt/UI/Tools/pw_ui_sprites.py first.", path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
        }

        public static UiColorTokens EnsureTokens()
        {
            var tokens = AssetDatabase.LoadAssetAtPath<UiColorTokens>(TokensPath);
            if (tokens != null)
                return tokens;
            tokens = ScriptableObject.CreateInstance<UiColorTokens>();
            AssetDatabase.CreateAsset(tokens, TokensPath);
            return tokens;
        }

        /// <summary>Atlas_CommonHUD (UI doc ATLAS PLAN): kit + HUD icons, rect packing (9-slices), no mipmaps.</summary>
        public static void EnsureAtlas()
        {
            if (EditorSettings.spritePackerMode == SpritePackerMode.Disabled)
                EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;
            Directory.CreateDirectory(Path.GetDirectoryName(AtlasPath));
            var atlas = new SpriteAtlasAsset();
            atlas.Add(new[] { AssetDatabase.LoadAssetAtPath<Object>(KitFolder), AssetDatabase.LoadAssetAtPath<Object>(IconFolder) });
            SpriteAtlasAsset.Save(atlas, AtlasPath);
            AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceUpdate);

            var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(AtlasPath);
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

        // ---------------- Prefab ----------------

        public static RefugeHud BuildHudPrefab(UiColorTokens tokens)
        {
            var pill = AssetDatabase.LoadAssetAtPath<Sprite>(PillPath);
            var circle = AssetDatabase.LoadAssetAtPath<Sprite>(CirclePath);
            var lightIcon = AssetDatabase.LoadAssetAtPath<Sprite>(LightIconPath);
            var eggIcon = AssetDatabase.LoadAssetAtPath<Sprite>(EggIconPath);
            if (pill == null || circle == null || lightIcon == null || eggIcon == null)
                throw new InvalidOperationException("HUD sprites missing.");

            var root = new GameObject("PF_UI_HUD_Main", typeof(RectTransform));
            try
            {
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 10;
                canvas.pixelPerfect = false;
                canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.None;
                var scaler = root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = ReferenceResolution;
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
                scaler.referencePixelsPerUnit = 100f;
                var hud = root.AddComponent<RefugeHud>();

                var safe = Rect("SafeArea", root.transform);
                Stretch(safe);

                // Light chip: top center, own canvas, nothing interactive (no raycaster).
                var chip = Rect("LightChip", safe);
                chip.gameObject.AddComponent<Canvas>();
                Place(chip, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(400f, 136f));
                var chipBack = AddImage("Background", chip, pill, tokens.panelPrimary, Image.Type.Sliced);
                Stretch(chipBack.rectTransform);
                var chipIcon = AddImage("Icon", chip, lightIcon, Color.white, Image.Type.Simple);
                Place(chipIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(70f, 2f), new Vector2(104f, 104f));
                var value = AddText("Value", chip, TextRole.NumberLarge, 64f, tokens.textPrimary, "0");
                Place(value.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(132f, 14f), new Vector2(250f, 70f));
                var rate = AddText("Rate", chip, TextRole.NumberSmall, 36f, tokens.textSecondary, "+0/s");
                Place(rate.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(134f, -36f), new Vector2(250f, 44f));

                // Hatch action: bottom center in the thumb zone, own canvas + raycaster.
                var action = Rect("HatchAction", safe);
                action.gameObject.AddComponent<Canvas>();
                action.gameObject.AddComponent<GraphicRaycaster>();
                Place(action, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(300f, 330f));
                var hatchRoot = Rect("HatchRoot", action);
                Stretch(hatchRoot);
                hatchRoot.pivot = new Vector2(0.5f, 0.45f);

                var buttonImage = AddImage("Button", hatchRoot, circle, tokens.accentPrimary, Image.Type.Simple);
                buttonImage.raycastTarget = true;
                Place(buttonImage.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(260f, 260f));
                var button = buttonImage.gameObject.AddComponent<Button>();
                button.targetGraphic = buttonImage;
                button.transition = Selectable.Transition.ColorTint;
                var colors = ColorBlock.defaultColorBlock;
                colors.normalColor = Color.white;
                colors.highlightedColor = Color.white;
                colors.selectedColor = Color.white;
                colors.pressedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
                colors.fadeDuration = 0.08f;
                button.colors = colors;
                var navigation = button.navigation;
                navigation.mode = Navigation.Mode.None;
                button.navigation = navigation;

                var egg = AddImage("Egg", buttonImage.rectTransform, eggIcon, Color.white, Image.Type.Simple);
                Place(egg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(156f, 156f));

                var costRow = Rect("Cost", hatchRoot);
                Place(costRow, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(228f, 84f));
                var costBack = AddImage("Background", costRow, pill, tokens.panelPrimary, Image.Type.Sliced);
                Stretch(costBack.rectTransform);
                var costIcon = AddImage("Icon", costRow, lightIcon, Color.white, Image.Type.Simple);
                Place(costIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(46f, 3f), new Vector2(62f, 62f));
                var cost = AddText("Value", costRow, TextRole.NumberSmall, 46f, tokens.textPrimary, "10");
                Place(cost.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(84f, 3f), new Vector2(132f, 60f));

                hud.SetReferences(tokens, safe, chip, value, rate, button, hatchRoot, buttonImage, egg, costRow.gameObject, cost);

                // Shortcuts: the Codex, bottom left in the thumb zone, own canvas + raycaster.
                var shortcuts = Rect("Shortcuts", safe);
                Stretch(shortcuts);
                shortcuts.gameObject.AddComponent<Canvas>();
                shortcuts.gameObject.AddComponent<GraphicRaycaster>();
                var codexRoot = Rect("Codex", shortcuts);
                Place(codexRoot, Vector2.zero, Vector2.zero, new Vector2(52f, 132f), new Vector2(176f, 176f));
                var codexImage = AddImage("Button", codexRoot, circle, tokens.panelPrimary, Image.Type.Simple);
                codexImage.raycastTarget = true;
                Stretch(codexImage.rectTransform);
                var codexButton = codexImage.gameObject.AddComponent<Button>();
                ConfigureButton(codexButton, codexImage);
                var codexIcon = AddImage("Icon", codexImage.rectTransform, AssetDatabase.LoadAssetAtPath<Sprite>(CodexIconPath), Color.white, Image.Type.Simple);
                Place(codexIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(112f, 112f));
                var badge = AddImage("Badge", codexRoot, AssetDatabase.LoadAssetAtPath<Sprite>(DotPath), tokens.errorSoft, Image.Type.Simple);
                Place(badge.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-26f, -26f), new Vector2(46f, 46f));
                badge.gameObject.SetActive(false);
                hud.SetShortcutReferences(codexButton, codexRoot, badge.gameObject);

                // Shop (T28): bottom right, mirror of the Codex; Gems chip top right. Hidden until the shop unlocks.
                var shopRoot = Rect("Shop", shortcuts);
                Place(shopRoot, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-52f, 132f), new Vector2(176f, 176f));
                var shopImage = AddImage("Button", shopRoot, circle, tokens.panelPrimary, Image.Type.Simple);
                shopImage.raycastTarget = true;
                Stretch(shopImage.rectTransform);
                var shopButton = shopImage.gameObject.AddComponent<Button>();
                ConfigureButton(shopButton, shopImage);
                var bagIcon = AddImage("Icon", shopImage.rectTransform, AssetDatabase.LoadAssetAtPath<Sprite>(ShopIconPath), Color.white, Image.Type.Simple);
                Place(bagIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(112f, 112f));
                shopRoot.gameObject.SetActive(false);
                var gemsChip = Rect("GemsChip", safe);
                gemsChip.gameObject.AddComponent<Canvas>();
                Place(gemsChip, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-36f, -60f), new Vector2(236f, 100f));
                var gemsBack = AddImage("Background", gemsChip, pill, tokens.panelPrimary, Image.Type.Sliced);
                Stretch(gemsBack.rectTransform);
                var gemsIcon = AddImage("Icon", gemsChip, AssetDatabase.LoadAssetAtPath<Sprite>(GemsIconPath), Color.white, Image.Type.Simple);
                Place(gemsIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(52f, 2f), new Vector2(76f, 76f));
                var gemsValue = AddText("Value", gemsChip, TextRole.NumberSmall, 48f, tokens.textPrimary, "0");
                Place(gemsValue.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(98f, 4f), new Vector2(130f, 64f));
                gemsChip.gameObject.SetActive(false);
                hud.SetShopReferences(shopButton, shopRoot, gemsChip, gemsValue);

                // Mastery toast above the hatch action: own canvas, animated only while shown.
                var masteryRoot = Rect("Mastery", safe);
                masteryRoot.gameObject.AddComponent<Canvas>();
                var masteryGroup = masteryRoot.gameObject.AddComponent<CanvasGroup>();
                masteryGroup.interactable = false;
                masteryGroup.blocksRaycasts = false;
                Place(masteryRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 452f), new Vector2(500f, 110f));
                var toastBack = AddImage("Background", masteryRoot, pill, tokens.panelPrimary, Image.Type.Sliced);
                Stretch(toastBack.rectTransform);
                var toastGlow = AddImage("Glow", masteryRoot, AssetDatabase.LoadAssetAtPath<Sprite>(GlowPath), new Color(1f, 0.84f, 0.42f, 0.95f), Image.Type.Simple);
                Place(toastGlow.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(66f, 4f), new Vector2(230f, 230f));
                var glowGroup = toastGlow.gameObject.AddComponent<CanvasGroup>();
                glowGroup.alpha = 0f;
                var masteryIcon = AddImage("Icon", masteryRoot, AssetDatabase.LoadAssetAtPath<Sprite>(MasteryIconPath), Color.white, Image.Type.Simple);
                Place(masteryIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(66f, 4f), new Vector2(100f, 100f));
                var masteryLevel = AddText("Level", masteryRoot, TextRole.NumberSmall, 46f, tokens.textPrimary, "1");
                Place(masteryLevel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(124f, 2f), new Vector2(80f, 64f));
                var toastBar = AddImage("Bar", masteryRoot, pill, tokens.panelSecondary, Image.Type.Sliced);
                toastBar.pixelsPerUnitMultiplier = 5f;
                Place(toastBar.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(212f, 0f), new Vector2(168f, 26f));
                var toastFill = AddImage("Fill", toastBar.rectTransform, pill, tokens.currencyLight, Image.Type.Sliced);
                toastFill.pixelsPerUnitMultiplier = 5f;
                LeftFill(toastFill.rectTransform, 26f);
                var masteryXp = AddText("Xp", masteryRoot, TextRole.NumberSmall, 40f, new Color(0.88f, 0.54f, 0.17f, 1f), "+2");
                masteryXp.alignment = TextAlignmentOptions.MidlineRight;
                Place(masteryXp.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-26f, 2f), new Vector2(90f, 60f));
                hud.SetMasteryReferences(masteryRoot, masteryGroup, masteryIcon.rectTransform, masteryLevel, masteryXp,
                    toastBar.rectTransform, toastFill.rectTransform, glowGroup);

                // T34 notice under the Light chip: the device is warm, the picture is lighter. Own canvas, hidden by default.
                var noticeRoot = Rect("Notice", safe);
                noticeRoot.gameObject.AddComponent<Canvas>();
                var noticeGroup = noticeRoot.gameObject.AddComponent<CanvasGroup>();
                noticeGroup.interactable = false;
                noticeGroup.blocksRaycasts = false;
                noticeGroup.alpha = 0f;
                Place(noticeRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -198f), new Vector2(860f, 112f));
                var noticeBack = AddImage("Background", noticeRoot, pill, tokens.panelPrimary, Image.Type.Sliced);
                Stretch(noticeBack.rectTransform);
                var noticeLabel = AddText("Label", noticeRoot, TextRole.Label, 36f, tokens.textPrimary, "");
                noticeLabel.alignment = TextAlignmentOptions.Center;
                noticeLabel.textWrappingMode = TextWrappingModes.Normal;
                Stretch(noticeLabel.rectTransform);
                noticeLabel.rectTransform.offsetMin = new Vector2(32f, 8f);
                noticeLabel.rectTransform.offsetMax = new Vector2(-32f, -8f);
                noticeRoot.gameObject.SetActive(false);
                hud.SetNoticeReferences(noticeRoot, noticeGroup, noticeLabel);

                // T34A: reveal banner, toasts, tutorial hand, collect motes, egg odds button.
                FeedbackUiPipeline.AddHudFeedback(root, safe, action, chip, chipIcon.rectTransform, hud, tokens);

                Directory.CreateDirectory(Path.GetDirectoryName(HudPrefabPath));
                return PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath).GetComponent<RefugeHud>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// PF_UI_OfflineReturn: scrim, cream card, floating Light icon on a soft glow, title, amount, Collect.
        /// Own overlay canvas above the HUD; content saved inactive so a closed screen costs nothing.
        /// </summary>
        public static OfflineReturnScreen BuildOfflineReturnPrefab(UiColorTokens tokens)
        {
            var panel = AssetDatabase.LoadAssetAtPath<Sprite>(PanelPath);
            var button = AssetDatabase.LoadAssetAtPath<Sprite>(ButtonPrimaryPath);
            var glow = AssetDatabase.LoadAssetAtPath<Sprite>(GlowPath);
            var lightIcon = AssetDatabase.LoadAssetAtPath<Sprite>(LightIconPath);
            if (panel == null || button == null || glow == null || lightIcon == null)
                throw new InvalidOperationException("Offline return sprites missing.");

            var root = new GameObject("PF_UI_OfflineReturn", typeof(RectTransform));
            try
            {
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 30;
                AddScaler(root);
                root.AddComponent<GraphicRaycaster>();
                var screen = root.AddComponent<OfflineReturnScreen>();

                var content = Rect("Content", root.transform);
                Stretch(content);
                var group = content.gameObject.AddComponent<CanvasGroup>();

                var scrim = Rect("Scrim", content).gameObject.AddComponent<Image>();
                Stretch(scrim.rectTransform);
                scrim.color = tokens.darkOverlay;
                scrim.raycastTarget = true; // the modal swallows touches meant for the HUD
                scrim.maskable = false;

                var card = Rect("Card", content);
                Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(760f, 560f));
                var back = AddImage("Background", card, panel, tokens.backgroundLight, Image.Type.Sliced);
                Stretch(back.rectTransform);

                var glowImage = AddImage("Glow", card, glow, new Color(1f, 0.83f, 0.42f, 0.85f), Image.Type.Simple);
                Place(glowImage.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 24f), new Vector2(400f, 400f));
                var icon = AddImage("Icon", card, lightIcon, Color.white, Image.Type.Simple);
                Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 24f), new Vector2(200f, 200f));

                var title = AddText("Title", card, TextRole.Subheading, 56f, tokens.textSecondary, "Welcome back", 44f);
                title.alignment = TextAlignmentOptions.Center;
                Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -168f), new Vector2(680f, 76f));

                var amount = AddText("Amount", card, TextRole.NumberLarge, 124f, tokens.textPrimary, "+0");
                amount.alignment = TextAlignmentOptions.Center;
                Place(amount.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -272f), new Vector2(700f, 140f));

                var collectImage = AddImage("Collect", card, button, tokens.accentPrimary, Image.Type.Sliced);
                collectImage.raycastTarget = true;
                Place(collectImage.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 52f), new Vector2(460f, 150f));
                var collect = collectImage.gameObject.AddComponent<Button>();
                collect.targetGraphic = collectImage;
                collect.transition = Selectable.Transition.ColorTint;
                var colors = ColorBlock.defaultColorBlock;
                colors.normalColor = Color.white;
                colors.highlightedColor = Color.white;
                colors.selectedColor = Color.white;
                colors.pressedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
                colors.fadeDuration = 0.08f;
                collect.colors = colors;
                var navigation = collect.navigation;
                navigation.mode = Navigation.Mode.None;
                collect.navigation = navigation;
                var label = AddText("Label", collectImage.rectTransform, TextRole.Button, 58f, tokens.textPrimary, "Collect", 44f);
                label.alignment = TextAlignmentOptions.Center;
                Place(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(400f, 100f));

                screen.SetReferences(canvas, content.gameObject, group, card, icon.rectTransform, title, amount, collect, label);
                content.gameObject.SetActive(false);
                canvas.enabled = false;

                Directory.CreateDirectory(Path.GetDirectoryName(OfflineReturnPrefabPath));
                return PrefabUtility.SaveAsPrefabAsset(root, OfflineReturnPrefabPath).GetComponent<OfflineReturnScreen>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        internal static void ConfigureButton(Button button, Image target)
        {
            button.targetGraphic = target;
            button.transition = Selectable.Transition.ColorTint;
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
        }

        /// <summary>
        /// PF_UI_Codex: opaque full screen, header (back, title, completion), virtualized list of biome
        /// headers and 3-card rows (templates only, rows are pooled at runtime), species sheet with 12 slots.
        /// </summary>
        public static CodexScreen BuildCodexPrefab(UiColorTokens tokens)
        {
            var panel = AssetDatabase.LoadAssetAtPath<Sprite>(PanelPath);
            var pill = AssetDatabase.LoadAssetAtPath<Sprite>(PillPath);
            var circle = AssetDatabase.LoadAssetAtPath<Sprite>(CirclePath);
            var star = AssetDatabase.LoadAssetAtPath<Sprite>(StarPath);
            var back = AssetDatabase.LoadAssetAtPath<Sprite>(BackIconPath);
            var visuals = AssetDatabase.LoadAssetAtPath<CodexVisualCatalog>("Assets/_Game/Content/Visuals/DEF_CodexVisuals.asset");
            if (panel == null || pill == null || circle == null || star == null || back == null || visuals == null)
                throw new InvalidOperationException("Codex sprites or DEF_CodexVisuals missing (run Render Animal Thumbnails).");

            var root = new GameObject("PF_UI_Codex", typeof(RectTransform));
            try
            {
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 20;
                AddScaler(root);
                root.AddComponent<GraphicRaycaster>();
                var screen = root.AddComponent<CodexScreen>();

                var content = Rect("Content", root.transform);
                Stretch(content);
                var group = content.gameObject.AddComponent<CanvasGroup>();
                var background = Rect("Background", content).gameObject.AddComponent<Image>();
                Stretch(background.rectTransform);
                background.color = tokens.backgroundLight;
                background.raycastTarget = true;
                background.maskable = false;

                var safe = Rect("SafeArea", content);
                Stretch(safe);

                // Header.
                var header = Rect("Header", safe);
                header.anchorMin = new Vector2(0f, 1f);
                header.anchorMax = new Vector2(1f, 1f);
                header.pivot = new Vector2(0.5f, 1f);
                header.anchoredPosition = Vector2.zero;
                header.sizeDelta = new Vector2(0f, 300f);
                var backImage = AddImage("Back", header, circle, Color.white, Image.Type.Simple);
                backImage.raycastTarget = true;
                Place(backImage.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(110f, -100f), new Vector2(140f, 140f));
                var backButton = backImage.gameObject.AddComponent<Button>();
                ConfigureButton(backButton, backImage);
                var chevron = AddImage("Chevron", backImage.rectTransform, back, tokens.textPrimary, Image.Type.Simple);
                Place(chevron.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-4f, 6f), new Vector2(64f, 64f));
                var title = AddText("Title", header, TextRole.Heading, 76f, tokens.textPrimary, "Codex");
                title.alignment = TextAlignmentOptions.Center;
                Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -100f), new Vector2(600f, 100f));
                var progressBack = AddImage("ProgressBack", header, pill, tokens.panelSecondary, Image.Type.Sliced);
                Place(progressBack.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-50f, -205f), new Vector2(520f, 44f));
                progressBack.pixelsPerUnitMultiplier = 2.6f;
                var progressFill = AddImage("Fill", progressBack.rectTransform, pill, tokens.success, Image.Type.Sliced);
                progressFill.pixelsPerUnitMultiplier = 2.6f;
                var fillRect = progressFill.rectTransform;
                fillRect.anchorMin = new Vector2(0f, 0f);
                fillRect.anchorMax = new Vector2(0f, 1f);
                fillRect.pivot = new Vector2(0f, 0.5f);
                fillRect.anchoredPosition = Vector2.zero;
                fillRect.sizeDelta = new Vector2(44f, 0f);
                var progressLabel = AddText("ProgressLabel", header, TextRole.NumberSmall, 42f, tokens.textSecondary, "0/0");
                Place(progressLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 0.5f), new Vector2(238f, -202f), new Vector2(200f, 60f));

                // Virtualized list.
                var viewport = Rect("Viewport", safe);
                viewport.anchorMin = Vector2.zero;
                viewport.anchorMax = Vector2.one;
                viewport.offsetMin = Vector2.zero;
                viewport.offsetMax = new Vector2(0f, -300f);
                viewport.gameObject.AddComponent<RectMask2D>();
                var dragArea = viewport.gameObject.AddComponent<Image>();
                dragArea.color = new Color(1f, 1f, 1f, 0f);
                dragArea.raycastTarget = true;
                var listContent = Rect("List", viewport);
                listContent.anchorMin = new Vector2(0f, 1f);
                listContent.anchorMax = new Vector2(1f, 1f);
                listContent.pivot = new Vector2(0.5f, 1f);
                listContent.anchoredPosition = Vector2.zero;
                listContent.sizeDelta = new Vector2(0f, 400f);
                // Own canvas (T34C, Codex 5000): scrolling moves it without rebuilding the header, and a row that
                // changes rebuilds only the list. Its raycaster keeps the cards tappable.
                listContent.gameObject.AddComponent<Canvas>();
                listContent.gameObject.AddComponent<GraphicRaycaster>();
                var scroll = viewport.gameObject.AddComponent<ScrollRect>();
                ConfigureScroll(scroll, viewport, listContent);

                var headerRow = BuildHeaderRow(listContent, tokens);
                var cardRow = BuildCardRow(listContent, tokens, panel, pill);

                // Species sheet, scrollable for species with many variants.
                var sheetRect = Rect("Sheet", safe);
                sheetRect.anchorMin = Vector2.zero;
                sheetRect.anchorMax = Vector2.one;
                sheetRect.offsetMin = Vector2.zero;
                sheetRect.offsetMax = new Vector2(0f, -300f);
                sheetRect.gameObject.AddComponent<RectMask2D>();
                var sheetBack = sheetRect.gameObject.AddComponent<Image>();
                sheetBack.color = tokens.backgroundLight;
                sheetBack.raycastTarget = true;
                var sheetContent = Rect("SheetContent", sheetRect);
                sheetContent.anchorMin = new Vector2(0f, 1f);
                sheetContent.anchorMax = new Vector2(1f, 1f);
                sheetContent.pivot = new Vector2(0.5f, 1f);
                sheetContent.anchoredPosition = Vector2.zero;
                sheetContent.sizeDelta = new Vector2(0f, 1600f);
                var sheetScroll = sheetRect.gameObject.AddComponent<ScrollRect>();
                ConfigureScroll(sheetScroll, sheetRect, sheetContent);

                var portraitBack = AddImage("PortraitBack", sheetContent, panel, Color.white, Image.Type.Sliced);
                Place(portraitBack.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(500f, 500f));
                var portrait = AddImage("Portrait", portraitBack.rectTransform, null, Color.white, Image.Type.Simple);
                Place(portrait.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(440f, 440f));
                var sheetName = AddText("Name", sheetContent, TextRole.Heading, 72f, tokens.textPrimary, "Rabbit");
                sheetName.alignment = TextAlignmentOptions.Center;
                Place(sheetName.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -585f), new Vector2(900f, 96f));
                var owned = AddText("Owned", sheetContent, TextRole.NumberSmall, 44f, tokens.textSecondary, "x0");
                owned.alignment = TextAlignmentOptions.Center;
                Place(owned.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -655f), new Vector2(400f, 60f));
                var masteryRow = Rect("Mastery", sheetContent);
                Place(masteryRow, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -728f), new Vector2(760f, 84f));
                var rowIcon = AddImage("Icon", masteryRow, AssetDatabase.LoadAssetAtPath<Sprite>(MasteryIconPath), Color.white, Image.Type.Simple);
                Place(rowIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-322f, 2f), new Vector2(80f, 80f));
                var rowLevel = AddText("Level", masteryRow, TextRole.NumberSmall, 46f, tokens.textPrimary, "1");
                Place(rowLevel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-272f, 2f), new Vector2(90f, 64f));
                var rowBar = AddImage("Bar", masteryRow, pill, tokens.panelSecondary, Image.Type.Sliced);
                rowBar.pixelsPerUnitMultiplier = 4.4f;
                Place(rowBar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-170f, 0f), new Vector2(360f, 30f));
                var rowFill = AddImage("Fill", rowBar.rectTransform, pill, tokens.currencyLight, Image.Type.Sliced);
                rowFill.pixelsPerUnitMultiplier = 4.4f;
                LeftFill(rowFill.rectTransform, 30f);
                var rowXp = AddText("Xp", masteryRow, TextRole.NumberSmall, 36f, tokens.textSecondary, "0/20");
                rowXp.alignment = TextAlignmentOptions.MidlineRight;
                Place(rowXp.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), new Vector2(380f, 2f), new Vector2(180f, 56f));

                var variantsTitle = AddText("VariantsTitle", sheetContent, TextRole.Subheading, 46f, tokens.textSecondary, "Variants");
                Place(variantsTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(64f, -812f), new Vector2(600f, 60f));

                var slots = new CodexVariantSlotView[12];
                for (int i = 0; i < slots.Length; i++)
                {
                    int column = i % CodexScreen.SheetColumns;
                    int row = i / CodexScreen.SheetColumns;
                    var position = new Vector2(60f + column * 330f, -(CodexScreen.SheetSlotsTop + row * CodexScreen.SheetSlotPitch));
                    slots[i] = BuildSlot(sheetContent, "Slot" + i, position, tokens, panel, pill, star);
                }
                sheetRect.gameObject.SetActive(false);

                screen.SetReferences(tokens, visuals, content.gameObject, group, safe, backButton, title, progressBack.rectTransform,
                    fillRect, progressLabel, scroll, viewport, listContent, headerRow, cardRow, sheetRect.gameObject, sheetScroll,
                    sheetContent, portrait, sheetName, owned, variantsTitle, slots);
                screen.SetMasteryReferences(masteryRow.gameObject, rowLevel, rowBar.rectTransform, rowFill.rectTransform, rowXp);
                content.gameObject.SetActive(false);

                Directory.CreateDirectory(Path.GetDirectoryName(CodexPrefabPath));
                return PrefabUtility.SaveAsPrefabAsset(root, CodexPrefabPath).GetComponent<CodexScreen>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        internal static void ConfigureScroll(ScrollRect scroll, RectTransform viewport, RectTransform content)
        {
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.08f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 40f;
        }

        private static CodexHeaderView BuildHeaderRow(RectTransform parent, UiColorTokens tokens)
        {
            var rect = Rect("HeaderRowTemplate", parent);
            TopStretch(rect, CodexScreen.HeaderRowHeight);
            var view = rect.gameObject.AddComponent<CodexHeaderView>();
            var title = AddText("Title", rect, TextRole.Subheading, 50f, tokens.textPrimary, "Meadow");
            Place(title.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(64f, 18f), new Vector2(600f, 70f));
            var count = AddText("Count", rect, TextRole.NumberSmall, 40f, tokens.textSecondary, "0/0");
            count.alignment = TextAlignmentOptions.MidlineRight;
            Place(count.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-64f, 20f), new Vector2(300f, 64f));
            view.SetReferences(rect, title, count);
            rect.gameObject.SetActive(false);
            return view;
        }

        private static CodexCardRow BuildCardRow(RectTransform parent, UiColorTokens tokens, Sprite panel, Sprite pill)
        {
            var rect = Rect("CardRowTemplate", parent);
            TopStretch(rect, CodexScreen.CardRowHeight);
            var row = rect.gameObject.AddComponent<CodexCardRow>();
            var cards = new CodexCardView[CodexCardRow.Columns];
            for (int c = 0; c < cards.Length; c++)
            {
                var card = Rect("Card" + c, rect);
                Place(card, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f + c * 330f, -10f), new Vector2(300f, 400f));
                var cardBack = AddImage("Background", card, panel, Color.white, Image.Type.Sliced);
                Stretch(cardBack.rectTransform);
                cardBack.raycastTarget = true;
                cardBack.pixelsPerUnitMultiplier = 1.4f;
                var button = cardBack.gameObject.AddComponent<Button>();
                ConfigureButton(button, cardBack);
                var thumb = AddImage("Thumbnail", card, null, Color.white, Image.Type.Simple);
                Place(thumb.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(240f, 240f));
                var name = AddText("Name", card, TextRole.Label, 42f, tokens.textPrimary, "Rabbit", 36f);
                name.alignment = TextAlignmentOptions.Center;
                Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -296f), new Vector2(270f, 56f));
                var count = AddText("Count", card, TextRole.NumberSmall, 36f, tokens.textSecondary, "0/0");
                count.alignment = TextAlignmentOptions.Center;
                Place(count.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -350f), new Vector2(200f, 46f));
                var badge = BuildBadge(card, tokens, pill, out var badgeLabel);
                var view = card.gameObject.AddComponent<CodexCardView>();
                view.SetReferences(cardBack, thumb, name, count, badge, badgeLabel, button);
                cards[c] = view;
            }
            row.SetReferences(rect, cards);
            rect.gameObject.SetActive(false);
            return row;
        }

        private static CodexVariantSlotView BuildSlot(RectTransform parent, string name, Vector2 position, UiColorTokens tokens,
            Sprite panel, Sprite pill, Sprite star)
        {
            var slot = Rect(name, parent);
            Place(slot, new Vector2(0f, 1f), new Vector2(0f, 1f), position, new Vector2(300f, 340f));
            var back = AddImage("Background", slot, panel, Color.white, Image.Type.Sliced);
            back.pixelsPerUnitMultiplier = 1.4f;
            Stretch(back.rectTransform);
            var thumb = AddImage("Thumbnail", slot, null, Color.white, Image.Type.Simple);
            Place(thumb.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(200f, 200f));
            var label = AddText("Name", slot, TextRole.Label, 40f, tokens.textPrimary, "Brown", 36f);
            label.alignment = TextAlignmentOptions.Center;
            Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -246f), new Vector2(270f, 52f));
            var stars = new Image[6];
            for (int i = 0; i < stars.Length; i++)
            {
                stars[i] = AddImage("Star" + i, slot, star, Color.white, Image.Type.Simple);
                Place(stars[i].rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                    new Vector2((i - 2.5f) * CodexVariantSlotView.StarPitch, -300f), new Vector2(36f, 36f));
            }
            var badge = BuildBadge(slot, tokens, pill, out var badgeLabel);
            var view = slot.gameObject.AddComponent<CodexVariantSlotView>();
            view.SetReferences(back, thumb, label, stars, badge, badgeLabel);
            slot.gameObject.SetActive(false);
            return view;
        }

        private static GameObject BuildBadge(RectTransform parent, UiColorTokens tokens, Sprite pill, out TMP_Text label)
        {
            var badge = AddImage("NewBadge", parent, pill, tokens.accentPrimary, Image.Type.Sliced);
            badge.pixelsPerUnitMultiplier = 2.2f;
            Place(badge.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(14f, 18f), new Vector2(200f, 60f));
            var text = AddText("Label", badge.rectTransform, TextRole.Label, 36f, tokens.textPrimary, "New");
            text.alignment = TextAlignmentOptions.Center;
            Place(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 3f), new Vector2(184f, 48f));
            badge.gameObject.SetActive(false);
            label = text;
            return badge.gameObject;
        }

        /// <summary>Progress fill growing from the left edge of its bar (width set at runtime).</summary>
        internal static void LeftFill(RectTransform rect, float height)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(height, 0f);
        }

        private static void TopStretch(RectTransform rect, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, height);
        }

        internal static void AddScaler(GameObject root)
        {
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;
        }

        // ---------------- Scene ----------------

        public static void SetupScene()
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path != ScenePath && !string.IsNullOrEmpty(active.path) && active.isDirty)
                throw new InvalidOperationException("Save or discard changes in " + active.path + " first.");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            VerticalSliceEntry entry = null;
            RefugeHud hud = null;
            OfflineReturnScreen offline = null;
            CodexScreen codex = null;
            EventSystem events = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                entry = entry ?? root.GetComponent<VerticalSliceEntry>();
                hud = hud ?? root.GetComponent<RefugeHud>();
                offline = offline ?? root.GetComponent<OfflineReturnScreen>();
                codex = codex ?? root.GetComponent<CodexScreen>();
                events = events ?? root.GetComponent<EventSystem>();
            }
            if (entry == null)
                throw new InvalidOperationException("SceneEntry (VerticalSliceEntry) missing in " + ScenePath);

            if (hud == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
                hud = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, scene)).GetComponent<RefugeHud>();
                hud.name = "HUD_Main";
            }
            if (codex == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CodexPrefabPath);
                codex = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, scene)).GetComponent<CodexScreen>();
                codex.name = "Screen_Codex";
            }
            if (offline == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OfflineReturnPrefabPath);
                offline = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, scene)).GetComponent<OfflineReturnScreen>();
                offline.name = "Screen_OfflineReturn";
            }
            if (events == null)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                EditorSceneManager.MoveGameObjectToScene(go, scene);
                events = go.GetComponent<EventSystem>();
            }
            events.sendNavigationEvents = false;
            events.pixelDragThreshold = 12;

            var so = new SerializedObject(entry);
            so.FindProperty("_hud").objectReferenceValue = hud;
            so.FindProperty("_offlineReturn").objectReferenceValue = offline;
            so.FindProperty("_codex").objectReferenceValue = codex;
            so.FindProperty("_hatchConfigKey").stringValue = "animals.hatch";
            so.FindProperty("_eggId").stringValue = "egg.grassland.basic";
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // ---------------- Helpers ----------------

        internal static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        internal static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        internal static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        internal static Image AddImage(string name, Transform parent, Sprite sprite, Color color, Image.Type type)
        {
            var image = Rect(name, parent).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = type;
            image.color = color;
            image.raycastTarget = false;
            image.maskable = false;
            image.preserveAspect = type == Image.Type.Simple;
            if (type == Image.Type.Sliced)
                image.fillCenter = true;
            return image;
        }

        private static TextStyleSheet styles;

        /// <summary>DEF_TextStyles, loaded once per domain (FontPipeline builds it).</summary>
        internal static TextStyleSheet Styles => styles != null ? styles : styles = FontPipeline.LoadStyles();

        /// <summary>
        /// A label in one of the doc 05 roles (font, material and size range from DEF_TextStyles). With
        /// <paramref name="autoSizeMin"/> above zero the label shrinks to fit, never under the role minimum.
        /// </summary>
        internal static TextMeshProUGUI AddText(string name, Transform parent, TextRole role, float size, Color color, string text,
            float autoSizeMin = 0f)
        {
            var label = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (autoSizeMin > 0f)
                Styles.ApplyAutoSize(label, role, size, autoSizeMin);
            else
                Styles.Apply(label, role, size);
            label.color = color;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            label.maskable = false;
            label.richText = false;
            label.parseCtrlCharacters = false;
            label.text = text;
            return label;
        }
    }
}
