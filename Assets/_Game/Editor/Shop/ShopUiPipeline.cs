using System;
using System.Collections.Generic;
using System.IO;
using PocketWilds.Presentation;
using PocketWilds.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using UiPipeline = PocketWilds.Editor.UI.UiPipeline;

namespace PocketWilds.Editor.Shop
{
    /// <summary>
    /// T28 shop UI: PF_UI_ProductCard (Gem packs), PF_UI_ProductCard_Wide (featured offers), PF_UI_Shop,
    /// PF_UI_OfferModal and Atlas_Shop, all from the shared kit (9-slices tinted by color tokens, runtime text,
    /// no art per offer). Then the HUD and the scene are rebuilt and wired. Idempotent.
    /// </summary>
    public static class ShopUiPipeline
    {
        public const string ShopArtFolder = PocketWilds.Editor.Import.TextureImportRules.UiRoot + "Shop";
        public const string ShopAtlasPath = PocketWilds.Editor.Import.TextureImportRules.UiRoot + "Atlases/Atlas_Shop.spriteatlasv2";
        public const string CardPrefabPath = "Assets/_Game/Prefabs/UI/PF_UI_ProductCard.prefab";
        public const string WideCardPrefabPath = "Assets/_Game/Prefabs/UI/PF_UI_ProductCard_Wide.prefab";
        public const string ShopPrefabPath = "Assets/_Game/Prefabs/UI/Screens/PF_UI_Shop.prefab";
        public const string OfferModalPrefabPath = "Assets/_Game/Prefabs/UI/Screens/PF_UI_OfferModal.prefab";
        public const int FeaturedSlots = 2;
        public const int GemSlots = 8;

        private sealed class Kit
        {
            public UiColorTokens Tokens;
            public Sprite Panel, Pill, Circle, Button, Glow, Dot, Gem, Close, Back;
        }

        [MenuItem("Pocket Wilds/Shop/Build Shop UI (T28)", priority = 191)]
        public static void BuildMenu() => Debug.Log("[ShopUiPipeline] " + BuildAll());

        public static string BuildAll()
        {
            string catalog = ShopPipeline.Build();
            UiPipeline.EnsureLocalization();
            AssetDatabase.Refresh();
            EnsureShopAtlas();
            UiPipeline.EnsureAtlas();
            var kit = LoadKit();
            UiPipeline.BuildHudPrefab(kit.Tokens);
            var card = BuildCardPrefab(kit);
            var wide = BuildWideCardPrefab(kit);
            BuildShopPrefab(kit, card, wide);
            BuildOfferModalPrefab(kit);
            AssetDatabase.SaveAssets();
            UiPipeline.SetupScene();
            WireScene();
            return catalog + "; shop UI built and wired.";
        }

        private static Kit LoadKit()
        {
            var kit = new Kit
            {
                Tokens = UiPipeline.EnsureTokens(),
                Panel = AssetDatabase.LoadAssetAtPath<Sprite>(UiPipeline.PanelPath),
                Pill = AssetDatabase.LoadAssetAtPath<Sprite>(UiPipeline.PillPath),
                Circle = AssetDatabase.LoadAssetAtPath<Sprite>(UiPipeline.CirclePath),
                Button = AssetDatabase.LoadAssetAtPath<Sprite>(UiPipeline.ButtonPrimaryPath),
                Glow = AssetDatabase.LoadAssetAtPath<Sprite>(UiPipeline.GlowPath),
                Dot = AssetDatabase.LoadAssetAtPath<Sprite>(UiPipeline.DotPath),
                Gem = AssetDatabase.LoadAssetAtPath<Sprite>(UiPipeline.GemsIconPath),
                Close = AssetDatabase.LoadAssetAtPath<Sprite>(UiPipeline.CloseIconPath),
                Back = AssetDatabase.LoadAssetAtPath<Sprite>(UiPipeline.BackIconPath),
            };
            if (kit.Panel == null || kit.Pill == null || kit.Circle == null || kit.Button == null || kit.Gem == null
                || kit.Close == null || kit.Back == null || kit.Dot == null || kit.Glow == null)
                throw new InvalidOperationException("Shop sprites missing: run SourceArt/UI/Tools/pw_ui_sprites.py first.");
            return kit;
        }

        /// <summary>Atlas_Shop (UI doc ATLAS PLAN): shop-only art, loaded with the shop screens only.</summary>
        private static void EnsureShopAtlas()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ShopAtlasPath));
            var atlas = new SpriteAtlasAsset();
            atlas.Add(new[] { AssetDatabase.LoadAssetAtPath<Object>(ShopArtFolder) });
            SpriteAtlasAsset.Save(atlas, ShopAtlasPath);
            AssetDatabase.ImportAsset(ShopAtlasPath, ImportAssetOptions.ForceUpdate);
            var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(ShopAtlasPath);
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

        private static ShopIconSet.Entry[] Icons()
        {
            var entries = new List<ShopIconSet.Entry>();
            foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { ShopArtFolder }))
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guid));
                entries.Add(new ShopIconSet.Entry { key = sprite.name, sprite = sprite });
            }
            entries.Add(new ShopIconSet.Entry { key = "ICO_Currency_Gems", sprite = AssetDatabase.LoadAssetAtPath<Sprite>(UiPipeline.GemsIconPath) });
            return entries.ToArray();
        }

        // ---------------- Cards ----------------

        private static Button BuyButton(Kit kit, RectTransform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size,
            float fontSize, out Image background, out TextMeshProUGUI price, out Image priceGem, out RectTransform wait)
        {
            background = UiPipeline.AddImage("Buy", parent, kit.Button, kit.Tokens.accentPrimary, Image.Type.Sliced);
            background.raycastTarget = true;
            UiPipeline.Place(background.rectTransform, anchor, pivot, position, size);
            var button = background.gameObject.AddComponent<Button>();
            UiPipeline.ConfigureButton(button, background);
            price = UiPipeline.AddText("Price", background.rectTransform, TextRole.Button, fontSize, kit.Tokens.textPrimary, "0,99 €");
            price.alignment = TextAlignmentOptions.Center;
            UiPipeline.Place(price.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), size - new Vector2(40f, 10f));
            priceGem = UiPipeline.AddImage("Gem", background.rectTransform, kit.Gem, Color.white, Image.Type.Simple);
            UiPipeline.Place(priceGem.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(54f, 4f), new Vector2(56f, 56f));
            priceGem.gameObject.SetActive(false);
            wait = UiPipeline.Rect("Wait", background.rectTransform);
            UiPipeline.Place(wait, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(120f, 40f));
            for (int i = 0; i < 3; i++)
            {
                var dot = UiPipeline.AddImage("Dot" + i, wait, kit.Dot, kit.Tokens.textPrimary, Image.Type.Simple);
                UiPipeline.Place(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 38f, 0f), new Vector2(26f, 26f));
            }
            wait.gameObject.SetActive(false);
            return button;
        }

        private static GameObject Chip(Kit kit, RectTransform parent, string name, Color color, Vector2 anchor, Vector2 pivot, Vector2 position,
            Vector2 size, float fontSize, string sample, out TextMeshProUGUI label)
        {
            var chip = UiPipeline.AddImage(name, parent, kit.Pill, color, Image.Type.Sliced);
            chip.pixelsPerUnitMultiplier = 1.6f;
            UiPipeline.Place(chip.rectTransform, anchor, pivot, position, size);
            label = UiPipeline.AddText("Label", chip.rectTransform, TextRole.Label, fontSize, Color.white, sample);
            label.alignment = TextAlignmentOptions.Center;
            UiPipeline.Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(12f, 4f);
            label.rectTransform.offsetMax = new Vector2(-12f, 0f);
            chip.gameObject.SetActive(false);
            return chip.gameObject;
        }

        /// <summary>PF_UI_ProductCard: Gem pack card (pile art, amount with gem, value chip, badge, price).</summary>
        private static ShopCardView BuildCardPrefab(Kit kit)
        {
            var root = new GameObject("PF_UI_ProductCard", typeof(RectTransform));
            try
            {
                root.layer = 5;
                var rect = (RectTransform)root.transform;
                rect.sizeDelta = new Vector2(466f, 470f);
                var view = root.AddComponent<ShopCardView>();
                var back = UiPipeline.AddImage("Background", rect, kit.Panel, kit.Tokens.panelPrimary, Image.Type.Sliced);
                UiPipeline.Stretch(back.rectTransform);
                var glow = UiPipeline.AddImage("Glow", rect, kit.Glow, new Color(0.75f, 0.86f, 1f, 0.55f), Image.Type.Simple);
                UiPipeline.Place(glow.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(300f, 300f));
                var art = UiPipeline.AddImage("Art", rect, null, Color.white, Image.Type.Simple);
                UiPipeline.Place(art.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(220f, 220f));
                var titleIcon = UiPipeline.AddImage("TitleGem", rect, kit.Gem, Color.white, Image.Type.Simple);
                UiPipeline.Place(titleIcon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-80f, -306f), new Vector2(58f, 58f));
                var title = UiPipeline.AddText("Amount", rect, TextRole.NumberLarge, 62f, kit.Tokens.textPrimary, "550");
                title.alignment = TextAlignmentOptions.Center;
                UiPipeline.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(20f, -304f), new Vector2(360f, 80f));
                var badge = Chip(kit, rect, "Badge", kit.Tokens.currencyGems, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f),
                    new Vector2(430f, 62f), 38f, "Best value", out var badgeLabel);
                var bonus = Chip(kit, rect, "Bonus", kit.Tokens.success, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f, -88f),
                    new Vector2(136f, 62f), 38f, "+30%", out var bonusLabel);
                var buy = BuyButton(kit, rect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(380f, 104f), 44f,
                    out var buyBack, out var price, out var priceGem, out var wait);
                view.SetReferences(kit.Tokens, art, null, title, null, null, bonus, bonusLabel, badge, badgeLabel, buy, buyBack, price, priceGem, wait,
                    titleIcon.rectTransform, null);
                Maskable(root);
                Directory.CreateDirectory(Path.GetDirectoryName(CardPrefabPath));
                return PrefabUtility.SaveAsPrefabAsset(root, CardPrefabPath).GetComponent<ShopCardView>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>PF_UI_ProductCard_Wide: featured offer (hero art, name, what you get, one-time line, price).</summary>
        private static ShopCardView BuildWideCardPrefab(Kit kit)
        {
            var root = new GameObject("PF_UI_ProductCard_Wide", typeof(RectTransform));
            try
            {
                root.layer = 5;
                var rect = (RectTransform)root.transform;
                rect.sizeDelta = new Vector2(960f, 430f);
                var view = root.AddComponent<ShopCardView>();
                BuildWideContent(kit, view, rect, artSize: 290f, titleSize: 58f, compact: true);
                Maskable(root);
                Directory.CreateDirectory(Path.GetDirectoryName(WideCardPrefabPath));
                return PrefabUtility.SaveAsPrefabAsset(root, WideCardPrefabPath).GetComponent<ShopCardView>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildWideContent(Kit kit, ShopCardView view, RectTransform rect, float artSize, float titleSize, bool compact)
        {
            var back = UiPipeline.AddImage("Background", rect, kit.Panel, kit.Tokens.panelPrimary, Image.Type.Sliced);
            UiPipeline.Stretch(back.rectTransform);
            Image art, gemBadge;
            TextMeshProUGUI title, contents, caption;
            Button buy;
            Image buyBack, priceGem;
            TextMeshProUGUI price;
            RectTransform wait;
            GameObject badge;
            TextMeshProUGUI badgeLabel;
            if (compact)
            {
                // Art left, text right, price bottom right.
                var glow = UiPipeline.AddImage("Glow", rect, kit.Glow, new Color(1f, 0.93f, 0.72f, 0.75f), Image.Type.Simple);
                UiPipeline.Place(glow.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(200f, 6f), new Vector2(380f, 380f));
                art = UiPipeline.AddImage("Art", rect, null, Color.white, Image.Type.Simple);
                UiPipeline.Place(art.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(200f, 14f), new Vector2(artSize, artSize));
                gemBadge = UiPipeline.AddImage("GemsBadge", rect, kit.Gem, Color.white, Image.Type.Simple);
                UiPipeline.Place(gemBadge.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(300f, -100f), new Vector2(110f, 110f));
                title = UiPipeline.AddText("Title", rect, TextRole.Heading, titleSize, kit.Tokens.textPrimary, "Starter Pack");
                UiPipeline.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(390f, -46f), new Vector2(540f, 76f));
                contents = UiPipeline.AddText("Contents", rect, TextRole.Body, 40f, kit.Tokens.textSecondary, "500 Gems + Flower Crown");
                contents.textWrappingMode = TextWrappingModes.Normal;
                contents.alignment = TextAlignmentOptions.TopLeft;
                UiPipeline.Place(contents.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(392f, -130f), new Vector2(530f, 110f));
                caption = UiPipeline.AddText("Caption", rect, TextRole.Caption, 36f, kit.Tokens.textSecondary, "One-time offer");
                UiPipeline.Place(caption.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(392f, -246f), new Vector2(530f, 50f));
                buy = BuyButton(kit, rect, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-34f, 30f), new Vector2(400f, 108f), 46f,
                    out buyBack, out price, out priceGem, out wait);
                badge = Chip(kit, rect, "Badge", kit.Tokens.success, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-22f, -22f),
                    new Vector2(220f, 62f), 38f, "Owned", out badgeLabel);
            }
            else
            {
                // Offer modal: hero art on top, then name, contents, one-time line and a large price button.
                var glow = UiPipeline.AddImage("Glow", rect, kit.Glow, new Color(1f, 0.93f, 0.72f, 0.85f), Image.Type.Simple);
                UiPipeline.Place(glow.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -280f), new Vector2(560f, 560f));
                art = UiPipeline.AddImage("Art", rect, null, Color.white, Image.Type.Simple);
                UiPipeline.Place(art.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -270f), new Vector2(artSize, artSize));
                gemBadge = UiPipeline.AddImage("GemsBadge", rect, kit.Gem, Color.white, Image.Type.Simple);
                UiPipeline.Place(gemBadge.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(160f, -400f), new Vector2(150f, 150f));
                title = UiPipeline.AddText("Title", rect, TextRole.Heading, titleSize, kit.Tokens.textPrimary, "Starter Pack");
                title.alignment = TextAlignmentOptions.Center;
                UiPipeline.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -560f), new Vector2(820f, 96f));
                contents = UiPipeline.AddText("Contents", rect, TextRole.Body, 46f, kit.Tokens.textSecondary, "500 Gems + Flower Crown");
                contents.alignment = TextAlignmentOptions.Center;
                contents.textWrappingMode = TextWrappingModes.Normal;
                UiPipeline.Place(contents.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -656f), new Vector2(800f, 120f));
                caption = UiPipeline.AddText("Caption", rect, TextRole.Caption, 38f, kit.Tokens.textSecondary, "One-time offer");
                caption.alignment = TextAlignmentOptions.Center;
                UiPipeline.Place(caption.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -746f), new Vector2(800f, 56f));
                buy = BuyButton(kit, rect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(560f, 136f), 58f,
                    out buyBack, out price, out priceGem, out wait);
                badge = Chip(kit, rect, "Badge", kit.Tokens.success, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f),
                    new Vector2(240f, 64f), 40f, "Owned", out badgeLabel);
            }
            view.SetReferences(kit.Tokens, art, null, title, contents, caption, null, null, badge, badgeLabel, buy, buyBack, price, priceGem, wait,
                null, gemBadge.gameObject);
        }

        private static void Maskable(GameObject root)
        {
            foreach (var graphic in root.GetComponentsInChildren<MaskableGraphic>(true))
                graphic.maskable = true;
        }

        // ---------------- Screens ----------------

        /// <summary>PF_UI_Shop: header (back, title, Gems), scrolled Featured and Gems sections, Restore, feedback line.</summary>
        private static ShopScreen BuildShopPrefab(Kit kit, ShopCardView cardPrefab, ShopCardView widePrefab)
        {
            var root = new GameObject("PF_UI_Shop", typeof(RectTransform));
            try
            {
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 22;
                UiPipeline.AddScaler(root);
                root.AddComponent<GraphicRaycaster>();
                var screen = root.AddComponent<ShopScreen>();

                var content = UiPipeline.Rect("Content", root.transform);
                UiPipeline.Stretch(content);
                var background = UiPipeline.Rect("Background", content).gameObject.AddComponent<Image>();
                UiPipeline.Stretch(background.rectTransform);
                background.color = kit.Tokens.backgroundLight;
                background.raycastTarget = true;
                background.maskable = false;
                var safe = UiPipeline.Rect("SafeArea", content);
                UiPipeline.Stretch(safe);

                var header = UiPipeline.Rect("Header", safe);
                header.anchorMin = new Vector2(0f, 1f);
                header.anchorMax = new Vector2(1f, 1f);
                header.pivot = new Vector2(0.5f, 1f);
                header.anchoredPosition = Vector2.zero;
                header.sizeDelta = new Vector2(0f, 220f);
                var backImage = UiPipeline.AddImage("Back", header, kit.Circle, Color.white, Image.Type.Simple);
                backImage.raycastTarget = true;
                UiPipeline.Place(backImage.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(110f, -100f), new Vector2(140f, 140f));
                var backButton = backImage.gameObject.AddComponent<Button>();
                UiPipeline.ConfigureButton(backButton, backImage);
                var chevron = UiPipeline.AddImage("Chevron", backImage.rectTransform, kit.Back, kit.Tokens.textPrimary, Image.Type.Simple);
                UiPipeline.Place(chevron.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-4f, 6f), new Vector2(64f, 64f));
                var title = UiPipeline.AddText("Title", header, TextRole.Heading, 76f, kit.Tokens.textPrimary, "Shop");
                title.alignment = TextAlignmentOptions.Center;
                UiPipeline.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -100f), new Vector2(520f, 100f));
                var gemsChip = UiPipeline.AddImage("Gems", header, kit.Pill, kit.Tokens.panelPrimary, Image.Type.Sliced);
                UiPipeline.Place(gemsChip.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-40f, -100f), new Vector2(260f, 100f));
                var gemIcon = UiPipeline.AddImage("Icon", gemsChip.rectTransform, kit.Gem, Color.white, Image.Type.Simple);
                UiPipeline.Place(gemIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(52f, 2f), new Vector2(76f, 76f));
                var gemsLabel = UiPipeline.AddText("Value", gemsChip.rectTransform, TextRole.NumberSmall, 48f, kit.Tokens.textPrimary, "0");
                UiPipeline.Place(gemsLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(98f, 4f), new Vector2(150f, 64f));

                var viewport = UiPipeline.Rect("Viewport", safe);
                viewport.anchorMin = Vector2.zero;
                viewport.anchorMax = Vector2.one;
                viewport.offsetMin = Vector2.zero;
                viewport.offsetMax = new Vector2(0f, -220f);
                viewport.gameObject.AddComponent<RectMask2D>();
                var dragArea = viewport.gameObject.AddComponent<Image>();
                dragArea.color = new Color(1f, 1f, 1f, 0f);
                dragArea.raycastTarget = true;
                var list = UiPipeline.Rect("List", viewport);
                list.anchorMin = new Vector2(0f, 1f);
                list.anchorMax = new Vector2(1f, 1f);
                list.pivot = new Vector2(0.5f, 1f);
                list.offsetMin = new Vector2(60f, 0f);
                list.offsetMax = new Vector2(-60f, 0f);
                list.anchoredPosition = new Vector2(0f, -10f);
                list.sizeDelta = new Vector2(-120f, 2000f);
                var scroll = viewport.gameObject.AddComponent<ScrollRect>();
                UiPipeline.ConfigureScroll(scroll, viewport, list);

                var featuredHeader = UiPipeline.AddText("FeaturedHeader", list, TextRole.Subheading, 50f, kit.Tokens.textSecondary, "Featured");
                featuredHeader.alignment = TextAlignmentOptions.BottomLeft;
                var featured = new ShopCardView[FeaturedSlots];
                for (int i = 0; i < featured.Length; i++)
                {
                    featured[i] = ((GameObject)PrefabUtility.InstantiatePrefab(widePrefab.gameObject, list)).GetComponent<ShopCardView>();
                    featured[i].name = "Featured" + i;
                }
                var gemsHeader = UiPipeline.AddText("GemsHeader", list, TextRole.Subheading, 50f, kit.Tokens.textSecondary, "Gems");
                gemsHeader.alignment = TextAlignmentOptions.BottomLeft;
                var gems = new ShopCardView[GemSlots];
                for (int i = 0; i < gems.Length; i++)
                {
                    gems[i] = ((GameObject)PrefabUtility.InstantiatePrefab(cardPrefab.gameObject, list)).GetComponent<ShopCardView>();
                    gems[i].name = "GemPack" + i;
                }

                var restoreRoot = UiPipeline.Rect("Restore", list);
                var restoreImage = UiPipeline.AddImage("Button", restoreRoot, kit.Pill, kit.Tokens.panelSecondary, Image.Type.Sliced);
                restoreImage.raycastTarget = true;
                UiPipeline.Place(restoreImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560f, 96f));
                var restoreButton = restoreRoot.gameObject.AddComponent<Button>();
                UiPipeline.ConfigureButton(restoreButton, restoreImage);
                var restoreLabel = UiPipeline.AddText("Label", restoreImage.rectTransform, TextRole.Button, 44f, kit.Tokens.textSecondary, "Restore purchases");
                restoreLabel.alignment = TextAlignmentOptions.Center;
                UiPipeline.Stretch(restoreLabel.rectTransform);
                // Everything in the scrolled list is clipped by the viewport mask (the kit helpers default to unmasked).
                Maskable(list.gameObject);

                var toast = UiPipeline.Rect("Toast", safe);
                UiPipeline.Place(toast, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(940f, 120f));
                var toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
                toastGroup.alpha = 0f;
                toastGroup.blocksRaycasts = false;
                toastGroup.interactable = false;
                var toastBack = UiPipeline.AddImage("Background", toast, kit.Pill, kit.Tokens.panelPrimary, Image.Type.Sliced);
                UiPipeline.Stretch(toastBack.rectTransform);
                var toastLabel = UiPipeline.AddText("Label", toast, TextRole.Label, 42f, kit.Tokens.textPrimary, "Thank you. +550 Gems");
                toastLabel.alignment = TextAlignmentOptions.Center;
                UiPipeline.Stretch(toastLabel.rectTransform);

                screen.SetReferences(kit.Tokens, content.gameObject, safe, backButton, title, gemsLabel, scroll, list, featuredHeader, featured,
                    gemsHeader, gems, restoreButton, restoreLabel, toastGroup, toastLabel, Icons());
                content.gameObject.SetActive(false);
                Directory.CreateDirectory(Path.GetDirectoryName(ShopPrefabPath));
                return PrefabUtility.SaveAsPrefabAsset(root, ShopPrefabPath).GetComponent<ShopScreen>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>PF_UI_OfferModal: scrim, centered card with close, hero art, contents and price, feedback line.</summary>
        private static OfferModalView BuildOfferModalPrefab(Kit kit)
        {
            var root = new GameObject("PF_UI_OfferModal", typeof(RectTransform));
            try
            {
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 24;
                UiPipeline.AddScaler(root);
                root.AddComponent<GraphicRaycaster>();
                var modal = root.AddComponent<OfferModalView>();

                var content = UiPipeline.Rect("Content", root.transform);
                UiPipeline.Stretch(content);
                var scrim = UiPipeline.Rect("Scrim", content).gameObject.AddComponent<Image>();
                UiPipeline.Stretch(scrim.rectTransform);
                scrim.color = kit.Tokens.darkOverlay;
                scrim.raycastTarget = true;
                scrim.maskable = false;

                var panel = UiPipeline.Rect("Panel", content);
                UiPipeline.Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(920f, 1060f));
                var card = panel.gameObject.AddComponent<ShopCardView>();
                BuildWideContent(kit, card, panel, artSize: 400f, titleSize: 72f, compact: false);
                var closeImage = UiPipeline.AddImage("Close", panel, kit.Circle, kit.Tokens.panelSecondary, Image.Type.Simple);
                closeImage.raycastTarget = true;
                UiPipeline.Place(closeImage.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-24f, -24f), new Vector2(128f, 128f));
                var closeButton = closeImage.gameObject.AddComponent<Button>();
                UiPipeline.ConfigureButton(closeButton, closeImage);
                var cross = UiPipeline.AddImage("Cross", closeImage.rectTransform, kit.Close, kit.Tokens.textSecondary, Image.Type.Simple);
                UiPipeline.Place(cross.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(60f, 60f));

                var toast = UiPipeline.Rect("Toast", content);
                UiPipeline.Place(toast, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, -530f), new Vector2(900f, 116f));
                var toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
                toastGroup.alpha = 0f;
                toastGroup.blocksRaycasts = false;
                toastGroup.interactable = false;
                var toastBack = UiPipeline.AddImage("Background", toast, kit.Pill, kit.Tokens.panelPrimary, Image.Type.Sliced);
                UiPipeline.Stretch(toastBack.rectTransform);
                var toastLabel = UiPipeline.AddText("Label", toast, TextRole.Label, 42f, kit.Tokens.textPrimary, "Thank you. +500 Gems");
                toastLabel.alignment = TextAlignmentOptions.Center;
                UiPipeline.Stretch(toastLabel.rectTransform);

                modal.SetReferences(kit.Tokens, content.gameObject, closeButton, card, panel, toastGroup, toastLabel, Icons());
                content.gameObject.SetActive(false);
                Directory.CreateDirectory(Path.GetDirectoryName(OfferModalPrefabPath));
                return PrefabUtility.SaveAsPrefabAsset(root, OfferModalPrefabPath).GetComponent<OfferModalView>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>Adds the shop screens to SC_VerticalSlice_Grassland and hands them to the scene entry.</summary>
        private static void WireScene()
        {
            var scene = EditorSceneManager.OpenScene(UiPipeline.ScenePath, OpenSceneMode.Single);
            VerticalSliceEntry entry = null;
            ShopScreen shop = null;
            OfferModalView modal = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                entry = entry != null ? entry : root.GetComponent<VerticalSliceEntry>();
                shop = shop != null ? shop : root.GetComponent<ShopScreen>();
                modal = modal != null ? modal : root.GetComponent<OfferModalView>();
            }
            if (entry == null)
                throw new InvalidOperationException("SceneEntry (VerticalSliceEntry) missing.");
            if (shop == null)
            {
                shop = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ShopPrefabPath), scene)).GetComponent<ShopScreen>();
                shop.name = "Screen_Shop";
            }
            if (modal == null)
            {
                modal = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(OfferModalPrefabPath), scene)).GetComponent<OfferModalView>();
                modal.name = "Screen_OfferModal";
            }
            var so = new SerializedObject(entry);
            so.FindProperty("_shop").objectReferenceValue = shop;
            so.FindProperty("_offerModal").objectReferenceValue = modal;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
