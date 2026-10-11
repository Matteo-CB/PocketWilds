using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PocketWilds.Editor.UI;
using PocketWilds.Presentation;
using PocketWilds.Simulation;
using PocketWilds.UI;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.Albums
{
    /// <summary>
    /// T44A « Albums » screen pipeline (doc « 01 Albums et autocollants », sections 1.2, 1.7, 1.10, 1.12, 1.13): PF_UI_Album
    /// (AlbumScreenView: a screen-space canvas at sorting order 23) with the stars chip, the title, close and back; the shelf
    /// (16 pooled covers in two columns, each with its progress bar and « 37/120 », a lock and a check; « Choisir un
    /// autocollant » with its price); a page (12 pooled slots 3 × 4 in a masked, swiped panel: frame, double cream border,
    /// art with the pearl sheen, silhouette, hint icon, rarity sign, name, rarity word, « Bientôt », « Nouveau », price) with
    /// its « 6/12 » bar and the half and full rewards; the choose list (60 pooled rows) and the confirmation; the note pill;
    /// and the reveal layer on its own canvas (sorting order 31: the dim, the cream pouch with its round tab, the sticker,
    /// « Nouveau », « Déjà collé », the banner, a soft ring, 6 round confetti, 8 pearly bubbles, 6 round stars: 21 particles).
    /// SH_UI_StickerPearl and SH_UI_StickerSilhouette get their shared materials (Art/Materials); the prefab carries them and
    /// DEF_StickerArt (strings only). Nothing of the scene is referenced: the UI kit's sprites, the icons and the fonts are
    /// named and borrowed at run time. Every label is measured in its real font with every text it can show in English and
    /// French (the albums', pages' and stickers' names from agent AC's table when it exists); one overflow and the prefab is
    /// not saved. The Addressables group Albums holds the screen (albums.screen). Idempotent.
    /// </summary>
    public static class AlbumScreenPipeline
    {
        public const string PrefabPath = AlbumLayout.PrefabPath;

        public static readonly Color Cream = UiColorTokens.Hex(0xFFF8EC);
        public static readonly Color Card = UiColorTokens.Hex(0xFFFCF4);
        public static readonly Color CardLocked = UiColorTokens.Hex(0xEDE6DA);
        public static readonly Color Text = UiColorTokens.Hex(0x4A3A2C);
        public static readonly Color TextSoft = UiColorTokens.Hex(0x8A7660);
        public static readonly Color Accent = UiColorTokens.Hex(0xFFB15E);
        public static readonly Color Disabled = UiColorTokens.Hex(0xCFC6B8);
        public static readonly Color Track = UiColorTokens.Hex(0xE9E1D3);
        public static readonly Color FrameCream = UiColorTokens.Hex(0xFAEBCC);
        public static readonly Color InnerCream = UiColorTokens.Hex(0xEDD6AE);
        public static readonly Color RareDot = UiColorTokens.Hex(0xDB8C80);
        public static readonly Color EmptyFrame = UiColorTokens.Hex(0xF3EBDE);
        public static readonly Color Pouch = UiColorTokens.Hex(0xFBEFD9);
        public static readonly Color PouchFlap = UiColorTokens.Hex(0xF3E2C2);
        public static readonly Color PouchTab = UiColorTokens.Hex(0xF2B48A);
        public static readonly Color Pearl = UiColorTokens.Hex(0xF3EEF6);
        public static readonly Color Coral = UiColorTokens.Hex(0xF08A7A);
        public static readonly Color Dim = new Color(0.20f, 0.14f, 0.10f, 0f);

        public const string KitPanel = "UI_Panel_Primary_9Slice";
        public const string KitButton = "UI_Button_Primary_9Slice";
        public const string KitPill = "UI_Pill_9Slice";
        public const string KitCircle = "UI_Button_IconCircle";
        public const string KitDot = "UI_Dot";
        public const string KitRing = "UI_Ring_Soft";
        public const string KitBubble = "UI_Bubble_Ring";
        public const string KitClose = "ICO_Close_X";

        /// <summary>The icons by <see cref="AlbumScreenView.AlbumIcon"/>, borrowed by name (a second choice after « | »).</summary>
        public static readonly string[] IconNames =
        {
            "ICO_Play", "ICO_Record", "ICO_Star_Rating|ICO_Challenge", "ICO_Wish", "ICO_Expedition|ICO_Act_Expeditions", "ICO_Gift",
            "ICO_Event_Calendar|ICO_Calendar_Days", "ICO_Visitor|ICO_Paw", "ICO_Garden|ICO_Act_Garden", "ICO_World_Map|ICO_ZoneSign", "ICO_Paw",
            "ICO_AlbumStar|ICO_Rarity_Star", "ICO_Album|ICO_Codex_Book", "ICO_Back_Chevron", "ICO_Check", "ICO_Gift", "ICO_Lock",
            "ICO_ZoneSign|ICO_BiomeMastery", "ICO_Event_Calendar|ICO_Calendar_Days", "ICO_Season|ICO_Today", "ICO_Play", "ICO_Paw",
        };

        [MenuItem("Pocket Wilds/Albums/Build Albums Screen (T44A)", priority = 250)]
        public static void BuildMenu() => Debug.Log("[AlbumScreenPipeline] " + BuildAll());

        public static string BuildAll()
        {
            var report = new List<string>();
            var pearl = EnsureMaterial(AlbumLayout.PearlMaterialPath, AlbumLayout.PearlShaderPath, AlbumLayout.PearlShaderName, report);
            var silhouette = EnsureMaterial(AlbumLayout.SilhouetteMaterialPath, AlbumLayout.SilhouetteShaderPath, AlbumLayout.SilhouetteShaderName, report);
            var catalog = AssetDatabase.LoadAssetAtPath<StickerArtCatalog>(StickerArtCatalog.AssetPath);
            report.Add(catalog != null
                ? $"sticker art: {catalog.entries.Length} captured stickers"
                : "WARNING: no " + StickerArtCatalog.AssetPath + " yet (every sticker shows its silhouette)");
            var view = BuildPrefab(pearl, silhouette, catalog, report);
            if (view != null)
                report.Add(EnsureAddressables(view.gameObject));
            AssetDatabase.SaveAssets();
            return string.Join("; ", report);
        }

        /// <summary>The shared UI material of a sticker shader (created once, its shader kept up to date).</summary>
        public static Material EnsureMaterial(string path, string shaderPath, string shaderName, List<string> report)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath) ?? Shader.Find(shaderName);
            if (shader == null)
            {
                report.Add("ERROR: shader " + shaderName + " not found (" + shaderPath + ")");
                return null;
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }
            EditorUtility.SetDirty(material);
            report.Add(Path.GetFileNameWithoutExtension(path) + " (" + shaderName + ")");
            return material;
        }

        // ---------------------------------------------------------------- prefab

        private sealed class Borrow
        {
            public readonly List<Image> Images = new List<Image>();
            public readonly List<string> Names = new List<string>();

            public Image Kit(Image image, string name)
            {
                Images.Add(image);
                Names.Add(name);
                return image;
            }
        }

        private sealed class Fit
        {
            public TMP_Text Label;
            public Vector2 Box;
            public int Lines;
            public string[] Texts;
        }

        private static readonly List<Fit> Fits = new List<Fit>();

        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        private static readonly Vector2 TopRight = new Vector2(1f, 1f);
        private static readonly Vector2 BottomCenter = new Vector2(0.5f, 0f);
        private static readonly Vector2 LeftMiddle = new Vector2(0f, 0.5f);
        private static readonly Vector2 RightMiddle = new Vector2(1f, 0.5f);

        private static float LineBox(float size, int lines) => Mathf.Ceil(size * (1.2f * lines + 0.15f));

        public static AlbumScreenView BuildPrefab(Material pearl, Material silhouette, StickerArtCatalog catalog, List<string> report)
        {
            Fits.Clear();
            var root = new GameObject(AlbumLayout.ScreenPrefabName, typeof(RectTransform));
            root.layer = 5;
            try
            {
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = AlbumLayout.SortingOrder;
                canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1;
                UiPipeline.AddScaler(root);
                root.AddComponent<GraphicRaycaster>();
                var view = root.AddComponent<AlbumScreenView>();
                var borrow = new Borrow();
                var p = new AlbumScreenParts { canvas = canvas };

                // Everything under an inactive Content: no label wakes up (its font references are cleared before the save).
                var content = UiPipeline.Rect("Content", root.transform);
                content.gameObject.SetActive(false);
                UiPipeline.Stretch(content);
                p.content = content.gameObject;

                var screen = UiPipeline.Rect("Screen", content);
                UiPipeline.Stretch(screen);
                p.screen = screen.gameObject;
                p.background = UiPipeline.AddImage("Background", screen, null, Cream, Image.Type.Simple);
                p.background.raycastTarget = true;
                UiPipeline.Stretch(p.background.rectTransform);
                var safe = UiPipeline.Rect("SafeArea", screen);
                UiPipeline.Stretch(safe);
                p.safeArea = safe;

                BuildTop(p, safe, borrow);
                BuildShelf(p, safe, borrow);
                BuildPage(p, safe, borrow, pearl, silhouette);
                BuildChooseList(p, safe, borrow);
                BuildConfirm(p, screen, borrow, silhouette);
                BuildNote(p, safe, borrow);
                BuildReveal(p, content, borrow, silhouette);

                var overflows = Measure();
                if (overflows.Count > 0)
                {
                    report.Add("ERROR: labels too long for their boxes, the prefab is not saved: " + string.Join("; ", overflows));
                    Debug.LogError("[AlbumScreenPipeline] labels overflow:\n" + string.Join("\n", overflows));
                    return null;
                }
                report.Add($"{Fits.Count} labels measured, every text fits");
                int names = AlbumTexts.ContentNames().GetLength(0);
                report.Add(names > 0 ? $"{names} album, page and sticker names measured" : "WARNING: agent AC's names table not found (names not measured yet)");

                view.SetParts(p, pearl, silhouette, catalog);
                var labels = root.GetComponentsInChildren<TMP_Text>(true);
                var fonts = new string[labels.Length];
                var materials = new string[labels.Length];
                for (int i = 0; i < labels.Length; i++)
                {
                    fonts[i] = labels[i].font != null ? labels[i].font.name : string.Empty;
                    materials[i] = labels[i].fontSharedMaterial != null ? labels[i].fontSharedMaterial.name : string.Empty;
                    ClearFont(labels[i]);
                }
                view.SetBorrowed(borrow.Images.ToArray(), borrow.Names.ToArray(), labels, fonts, materials, (string[])IconNames.Clone());
                view.SetColors(Cream, Card, CardLocked, Text, TextSoft, Accent, Disabled, FrameCream, InnerCream, RareDot, EmptyFrame);
                canvas.enabled = false;

                Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
                UiPipeline.MaskScrollContent(root);
                var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath).GetComponent<AlbumScreenView>();
                report.Add($"{AlbumLayout.ScreenPrefabName}: {p.covers.Length} covers, {p.slots.Length} slots, {p.rows.Length} choice rows, "
                           + $"{p.reveal.confetti.Length + p.reveal.bubbles.Length + p.reveal.stars.Length + 2} reveal particles, {labels.Length} labels, "
                           + $"{borrow.Names.Distinct().Count()} kit sprites borrowed" + (saved.Valid ? "" : " (INCOMPLETE)"));
                report.Add(CheckDependencies());
                return saved;
            }
            finally
            {
                Fits.Clear();
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>The prefab references nothing of the scene's UI (kit, icons, HUD atlas, fonts, tokens, thumbnails).</summary>
        public static string CheckDependencies()
        {
            var bad = AssetDatabase.GetDependencies(PrefabPath, true)
                .Where(d => d.Contains("/Fonts/") || d.Contains("Atlas_CommonHUD") || d.Contains("/Art/UI/Kit/") || d.Contains("/Art/UI/Icons/")
                            || d.Contains("/Thumbnails/") || d.Contains("DEF_UiColorTokens") || d.Contains("DEF_CodexVisuals"))
                .ToList();
            return bad.Count == 0 ? "no scene asset referenced" : "WARNING: the prefab references scene assets: " + string.Join(", ", bad);
        }

        private static void ClearFont(TMP_Text label)
        {
            var so = new SerializedObject(label);
            foreach (var name in new[] { "m_fontAsset", "m_sharedMaterial", "m_fontMaterial", "m_Material", "m_baseMaterial" })
            {
                var property = so.FindProperty(name);
                if (property != null && property.propertyType == SerializedPropertyType.ObjectReference)
                    property.objectReferenceValue = null;
            }
            foreach (var name in new[] { "m_fontSharedMaterials", "m_fontMaterials" })
            {
                var property = so.FindProperty(name);
                if (property != null && property.isArray)
                    property.arraySize = 0;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- measuring

        private static List<string> Measure()
        {
            var problems = new List<string>();
            var probe = new GameObject("AlbumMeasure", typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var m = probe.AddComponent<TextMeshProUGUI>();
                m.richText = false;
                m.margin = Vector4.zero;
                foreach (var fit in Fits)
                {
                    var label = fit.Label;
                    m.font = label.font;
                    m.fontSharedMaterial = label.fontSharedMaterial;
                    m.fontStyle = label.fontStyle;
                    m.characterSpacing = label.characterSpacing;
                    m.wordSpacing = label.wordSpacing;
                    m.lineSpacing = label.lineSpacing;
                    m.enableAutoSizing = false;
                    m.fontSize = label.fontSize;
                    m.textWrappingMode = fit.Lines > 1 ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
                    foreach (var text in fit.Texts)
                    {
                        if (string.IsNullOrEmpty(text))
                            continue;
                        var size = m.GetPreferredValues(text, fit.Box.x, 10000f);
                        if (size.x > fit.Box.x + 0.5f || size.y > fit.Box.y + 0.5f)
                            problems.Add($"{label.name} « {text.Replace("\n", " / ")} » {size.x:0}×{size.y:0} in {fit.Box.x:0}×{fit.Box.y:0}");
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }
            return problems;
        }

        /// <summary>The texts of keys in English and French (AlbumTexts).</summary>
        private static string[] T(params string[] keys)
        {
            var found = new List<string>();
            var table = AlbumTexts.Texts;
            foreach (var key in keys)
                for (int i = 0; i < table.GetLength(0); i++)
                    if (table[i, 0] == key)
                    {
                        found.Add(table[i, 1]);
                        found.Add(table[i, 2]);
                    }
            return found.ToArray();
        }

        /// <summary>A counted template's texts with its number filled by the longest values.</summary>
        private static string[] Filled(string key, params string[] numbers) =>
            T(key, key + ".one", key + ".other").SelectMany(t => numbers.Select(n => t.Replace("{n}", n).Replace("{0}", n))).ToArray();

        /// <summary>Agent AC's names whose key starts with <paramref name="prefix"/> (« album. », « page. », « sticker. »).</summary>
        private static string[] Names(string prefix)
        {
            var table = AlbumTexts.ContentNames();
            var found = new List<string>();
            for (int i = 0; i < table.GetLength(0); i++)
            {
                string key = table[i, 0];
                if (key == null || !key.StartsWith(prefix, StringComparison.Ordinal) || !key.EndsWith(".name", StringComparison.Ordinal))
                    continue;
                found.Add(table[i, 1]);
                found.Add(table[i, 2]);
            }
            return found.ToArray();
        }

        private static string[] Hints() =>
            T(Enumerable.Range(0, 11).Select(i => AlbumText.Hint((StickerHint)i))
                .Concat(new[] { AlbumText.Closed, AlbumText.NotEnoughStars, AlbumText.Soon }).ToArray());

        // ---------------------------------------------------------------- building helpers

        private static TextMeshProUGUI Label(string name, Transform parent, TextRole role, float size, Color color, TextAlignmentOptions align,
            Vector2 anchor, Vector2 position, Vector2 box, int lines, params string[][] texts)
        {
            var label = UiPipeline.AddText(name, parent, role, size, color, string.Empty);
            label.alignment = align;
            label.textWrappingMode = lines > 1 ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            UiPipeline.Place(label.rectTransform, anchor, Center, position, box);
            var all = texts.SelectMany(t => t).ToArray();
            if (all.Length > 0)
                Fits.Add(new Fit { Label = label, Box = box, Lines = lines, Texts = all });
            return label;
        }

        /// <summary>The 16 covers in album order (13 biomes, then events, seasons and activities).</summary>
        private static readonly string[] CoverNames =
        {
            "Grassland", "Forest", "Lake", "Mountain", "Snow", "Jungle", "Desert", "Sakura", "Mushrooms", "CloudIslands", "Lunar",
            "Prehistoric", "StarrySky", "Events", "Seasons", "Activities",
        };

        private static Image Img(string name, Transform parent, Sprite sprite, Color color, Image.Type type, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var image = UiPipeline.AddImage(name, parent, sprite, color, type);
            image.raycastTarget = false;
            UiPipeline.Place(image.rectTransform, anchor, Center, position, size);
            return image;
        }

        private static Button ButtonOn(Image target)
        {
            target.raycastTarget = true;
            var button = target.gameObject.AddComponent<Button>();
            UiPipeline.ConfigureButton(button, target);
            return button;
        }

        /// <summary>A horizontal bar filled from the left (the kit's pill, borrowed).</summary>
        private static Image Bar(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Borrow borrow, out Image fill)
        {
            var track = borrow.Kit(Img(name, parent, null, Track, Image.Type.Sliced, anchor, position, size), KitPill);
            fill = borrow.Kit(Img("Fill", track.transform, null, Accent, Image.Type.Filled, Center, Vector2.zero, size), KitPill);
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0.5f;
            return track;
        }

        private static RectTransform StretchRect(string name, Transform parent, float left, float bottom, float right, float top)
        {
            var rect = UiPipeline.Rect(name, parent);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        /// <summary>A vertical scrolling list: its masked viewport and the content its pooled views sit in.</summary>
        private static RectTransform ScrollList(string name, Transform parent, float left, float bottom, float right, float top, float height, out ScrollRect scroll)
        {
            var viewport = StretchRect(name, parent, left, bottom, right, top);
            viewport.gameObject.AddComponent<RectMask2D>();
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0f);
            var list = UiPipeline.Rect("Items", viewport);
            list.anchorMin = TopCenter;
            list.anchorMax = TopCenter;
            list.pivot = TopCenter;
            list.anchoredPosition = Vector2.zero;
            list.sizeDelta = new Vector2(1000f, height);
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            UiPipeline.ConfigureScroll(scroll, viewport, list);
            scroll.horizontal = false;
            scroll.vertical = true;
            return list;
        }

        private static StickerUv Uv(Image image)
        {
            var uv = image.gameObject.AddComponent<StickerUv>();
            return uv;
        }

        // ---------------------------------------------------------------- layout (reference 1080 × 2340)
        //
        // From the top of the safe area: the bar (stars, title, close) to 180, then the shelf down to « Choisir un
        // autocollant » (its 300 at the bottom); a page: the back button, the album's and the page's names, the « 6/12 » row
        // with its rewards and arrows (to 440), then the 3 × 4 slots in the swiped panel. Sizes never go under their role's
        // minimum (Label, NumberSmall and Caption 36, Body 38, Subheading and Button 44).

        private static void BuildTop(AlbumScreenParts p, RectTransform safe, Borrow borrow)
        {
            p.title = Label("Title", safe, TextRole.Heading, 64f, Text, TextAlignmentOptions.Center, TopCenter, new Vector2(0f, -90f),
                new Vector2(520f, LineBox(64f, 1)), 1, T(AlbumText.Title));
            var closeBack = borrow.Kit(Img("Close", safe, null, Card, Image.Type.Simple, TopRight, new Vector2(-90f, -90f), new Vector2(120f, 120f)), KitCircle);
            p.close = ButtonOn(closeBack);
            p.closeIcon = borrow.Kit(Img("Icon", closeBack.transform, null, TextSoft, Image.Type.Simple, Center, Vector2.zero, new Vector2(56f, 56f)), KitClose);
            p.starsChip = borrow.Kit(Img("Stars", safe, null, Card, Image.Type.Sliced, TopLeft, new Vector2(180f, -90f), new Vector2(280f, 88f)), KitPill);
            p.starsIcon = Img("Icon", p.starsChip.transform, null, Color.white, Image.Type.Simple, LeftMiddle, new Vector2(48f, 0f), new Vector2(62f, 62f));
            p.stars = Label("Amount", p.starsChip.transform, TextRole.NumberSmall, 40f, Text, TextAlignmentOptions.MidlineLeft, LeftMiddle,
                new Vector2(174f, 2f), new Vector2(184f, LineBox(40f, 1)), 1, new[] { "99999" });
            var backBack = borrow.Kit(Img("Back", safe, null, Card, Image.Type.Simple, TopLeft, new Vector2(90f, -240f), new Vector2(110f, 110f)), KitCircle);
            p.back = ButtonOn(backBack);
            p.backIcon = Img("Icon", backBack.transform, null, TextSoft, Image.Type.Simple, Center, Vector2.zero, new Vector2(56f, 56f));
        }

        private static void BuildShelf(AlbumScreenParts p, RectTransform safe, Borrow borrow)
        {
            var shelf = StretchRect("Shelf", safe, 0f, 0f, 0f, 0f);
            p.shelf = shelf.gameObject;
            int rows = (AlbumLayout.AlbumCapacity + 1) / 2;
            p.shelfContent = ScrollList("Covers", shelf, 40f, 300f, 40f, 190f, rows * AlbumScreenView.CoverPitchY + 2f * AlbumScreenView.ListPadding,
                out p.shelfScroll);
            var albumNames = Names("album.");
            p.covers = new AlbumCoverParts[AlbumLayout.AlbumCapacity];
            for (int i = 0; i < p.covers.Length; i++)
            {
                int column = i % 2, row = i / 2;
                var cover = new AlbumCoverParts();
                var rootRect = UiPipeline.Rect("Cover" + i, p.shelfContent);
                UiPipeline.Place(rootRect, TopCenter, Center,
                    new Vector2((column - 0.5f) * AlbumScreenView.CoverPitchX, -AlbumScreenView.ListPadding - AlbumScreenView.CoverPitchY * (row + 0.5f)),
                    new Vector2(470f, 344f));
                cover.root = rootRect;
                cover.card = borrow.Kit(Img("Card", rootRect, null, Card, Image.Type.Sliced, Center, Vector2.zero, new Vector2(470f, 344f)), KitPanel);
                cover.button = ButtonOn(cover.card);
                cover.art = borrow.Kit(Img("Art", rootRect, null, FrameCream, Image.Type.Sliced, TopCenter, new Vector2(0f, -110f), new Vector2(430f, 180f)), KitPanel);
                cover.kindIcon = Img("Kind", cover.art.transform, null, Color.white, Image.Type.Simple, Center, Vector2.zero, new Vector2(124f, 124f));
                cover.kindIcon.preserveAspect = true;
                // The album's hand-drawn cover (agent AI, pw_ui_sprites_t44.py), in album order; none: the kind icon shows.
                var coverSprite = i < CoverNames.Length
                    ? AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/UI/Albums/Covers/UI_AlbumCover_" + CoverNames[i] + ".png")
                    : null;
                cover.coverArt = Img("Cover", cover.art.transform, coverSprite, Color.white, Image.Type.Simple, Center, Vector2.zero, new Vector2(136f, 170f));
                cover.coverArt.preserveAspect = true;
                cover.coverArt.raycastTarget = false;
                cover.coverArt.enabled = coverSprite != null;
                cover.lockIcon = Img("Lock", cover.art.transform, null, TextSoft, Image.Type.Simple, Center, new Vector2(0f, 0f), new Vector2(80f, 80f));
                cover.check = Img("Check", rootRect, null, Color.white, Image.Type.Simple, TopRight, new Vector2(-36f, -36f), new Vector2(60f, 60f));
                cover.dot = borrow.Kit(Img("Dot", rootRect, null, Coral, Image.Type.Simple, TopLeft, new Vector2(30f, -30f), new Vector2(40f, 40f)), KitDot);
                cover.title = Label("Title", rootRect, TextRole.Body, 38f, Text, TextAlignmentOptions.Center, TopCenter, new Vector2(0f, -236f),
                    new Vector2(430f, LineBox(38f, 1)), 1, albumNames);
                // The largest album holds 216 stickers (the activities: 18 pages).
                cover.barTrack = Bar("Bar", rootRect, BottomCenter, new Vector2(-90f, 42f), new Vector2(250f, 24f), borrow, out cover.barFill);
                cover.ratio = Label("Ratio", rootRect, TextRole.NumberSmall, 36f, TextSoft, TextAlignmentOptions.MidlineRight, BottomCenter,
                    new Vector2(135f, 44f), new Vector2(190f, LineBox(36f, 1)), 1, new[] { "120/120", "216/216" });
                cover.lockIcon.gameObject.SetActive(false);
                cover.check.gameObject.SetActive(false);
                cover.dot.gameObject.SetActive(false);
                p.covers[i] = cover;
            }
            p.chooseBack = borrow.Kit(Img("Choose", shelf, null, Accent, Image.Type.Sliced, BottomCenter, new Vector2(0f, 150f), new Vector2(720f, 136f)), KitButton);
            p.choose = ButtonOn(p.chooseBack);
            p.chooseLabel = Label("Label", p.chooseBack.transform, TextRole.Button, 44f, Text, TextAlignmentOptions.Center, Center, new Vector2(-60f, 2f),
                new Vector2(560f, LineBox(44f, 1)), 1, T(AlbumText.Choose));
            p.chooseStar = Img("Star", p.chooseBack.transform, null, Color.white, Image.Type.Simple, Center, new Vector2(232f, 0f), new Vector2(64f, 64f));
            p.choosePrice = Label("Price", p.chooseBack.transform, TextRole.NumberSmall, 44f, Text, TextAlignmentOptions.MidlineLeft, Center,
                new Vector2(312f, 2f), new Vector2(80f, LineBox(44f, 1)), 1, new[] { "10", "99" });
        }

        private static void BuildPage(AlbumScreenParts p, RectTransform safe, Borrow borrow, Material pearl, Material silhouette)
        {
            var page = StretchRect("Page", safe, 0f, 0f, 0f, 0f);
            p.page = page.gameObject;
            p.pageAlbum = Label("Album", page, TextRole.Label, 36f, TextSoft, TextAlignmentOptions.Center, TopCenter, new Vector2(0f, -208f),
                new Vector2(680f, LineBox(36f, 1)), 1, Names("album."), T(AlbumText.ChooseHint));
            p.pageTitle = Label("PageTitle", page, TextRole.Subheading, 48f, Text, TextAlignmentOptions.Center, TopCenter, new Vector2(0f, -266f),
                new Vector2(700f, LineBox(48f, 1)), 1, Names("page."));
            const float rowY = -378f;
            var previous = borrow.Kit(Img("Previous", page, null, Card, Image.Type.Simple, TopCenter, new Vector2(-460f, rowY), new Vector2(96f, 96f)), KitCircle);
            p.previous = ButtonOn(previous);
            p.previousIcon = Img("Icon", previous.transform, null, TextSoft, Image.Type.Simple, Center, Vector2.zero, new Vector2(48f, 48f));
            var next = borrow.Kit(Img("Next", page, null, Card, Image.Type.Simple, TopCenter, new Vector2(460f, rowY), new Vector2(96f, 96f)), KitCircle);
            p.next = ButtonOn(next);
            p.nextIcon = Img("Icon", next.transform, null, TextSoft, Image.Type.Simple, Center, Vector2.zero, new Vector2(48f, 48f));
            p.nextIcon.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            p.barTrack = Bar("Bar", page, TopCenter, new Vector2(-40f, rowY), new Vector2(560f, 28f), borrow, out p.barFill);
            // The half and full rewards on the bar, ticked once reached.
            p.halfIcon = Img("Half", p.barTrack.transform, null, Color.white, Image.Type.Simple, Center, Vector2.zero, new Vector2(64f, 64f));
            p.halfCheck = Img("Check", p.halfIcon.transform, null, Color.white, Image.Type.Simple, Center, new Vector2(22f, -22f), new Vector2(32f, 32f));
            p.fullIcon = Img("Full", p.barTrack.transform, null, Color.white, Image.Type.Simple, RightMiddle, Vector2.zero, new Vector2(64f, 64f));
            p.fullCheck = Img("Check", p.fullIcon.transform, null, Color.white, Image.Type.Simple, Center, new Vector2(22f, -22f), new Vector2(32f, 32f));
            p.ratio = Label("Ratio", page, TextRole.NumberSmall, 40f, Text, TextAlignmentOptions.MidlineLeft, TopCenter, new Vector2(330f, rowY + 2f),
                new Vector2(140f, LineBox(40f, 1)), 1, new[] { "12/12", "0/12" });

            // The swiped panel: a masked viewport that catches drags, the 12 pooled slots inside.
            var viewport = StretchRect("Slots", page, 0f, 40f, 0f, 440f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var catcher = viewport.gameObject.AddComponent<Image>();
            catcher.color = new Color(1f, 1f, 1f, 0f);
            catcher.raycastTarget = true;
            p.pageViewport = viewport;
            p.swipe = viewport.gameObject.AddComponent<AlbumSwipeArea>();
            var panel = UiPipeline.Rect("Panel", viewport);
            UiPipeline.Place(panel, Center, Center, Vector2.zero, new Vector2(1020f, 1800f));
            p.pagePanel = panel;
            p.pageGroup = panel.gameObject.AddComponent<CanvasGroup>();
            var stickerNames = Names("sticker.");
            var rarities = T(AlbumText.Common, AlbumText.Rare, AlbumText.Shiny);
            p.slots = new AlbumSlotParts[AlbumLayout.SlotsPerPage];
            for (int i = 0; i < p.slots.Length; i++)
            {
                int column = i % AlbumLayout.Columns, row = i / AlbumLayout.Columns;
                var s = new AlbumSlotParts();
                var cell = UiPipeline.Rect("Slot" + i, panel);
                UiPipeline.Place(cell, Center, Center, new Vector2((column - 1) * 340f, (1.5f - row) * 450f), new Vector2(320f, 430f));
                s.root = cell;
                var hit = Img("Hit", cell, null, new Color(1f, 1f, 1f, 0f), Image.Type.Simple, Center, Vector2.zero, new Vector2(320f, 430f));
                s.button = ButtonOn(hit);
                var frameAt = new Vector2(0f, 80f);
                s.frame = borrow.Kit(Img("Frame", cell, null, Color.white, Image.Type.Sliced, Center, frameAt, new Vector2(270f, 270f)), KitPanel);
                s.inner = borrow.Kit(Img("Inner", s.frame.transform, null, InnerCream, Image.Type.Sliced, Center, Vector2.zero, new Vector2(254f, 254f)), KitPanel);
                s.art = Img("Art", s.frame.transform, null, Color.white, Image.Type.Simple, Center, Vector2.zero, new Vector2(230f, 230f));
                s.art.preserveAspect = true;
                s.art.material = pearl;
                s.artUv = Uv(s.art);
                s.silhouette = Img("Silhouette", s.frame.transform, null, Color.white, Image.Type.Simple, Center, Vector2.zero, new Vector2(230f, 230f));
                s.silhouette.preserveAspect = true;
                s.silhouette.material = silhouette;
                s.silhouetteUv = Uv(s.silhouette);
                s.hintIcon = Img("Hint", s.frame.transform, null, Color.white, Image.Type.Simple, Center, Vector2.zero, new Vector2(96f, 96f));
                s.hintIcon.preserveAspect = true;
                s.sign = borrow.Kit(Img("Sign", s.frame.transform, null, RareDot, Image.Type.Simple, Center, new Vector2(104f, 104f), new Vector2(34f, 34f)), KitDot);
                s.signLight = borrow.Kit(Img("Light", s.sign.transform, null, Color.white, Image.Type.Simple, Center, new Vector2(-5f, 5f), new Vector2(12f, 12f)), KitDot);
                s.soon = Label("Soon", s.frame.transform, TextRole.Label, 36f, TextSoft, TextAlignmentOptions.Center, Center, Vector2.zero,
                    new Vector2(240f, LineBox(36f, 1)), 1, T(AlbumText.Soon));
                s.newPill = borrow.Kit(Img("New", cell, null, Accent, Image.Type.Sliced, Center, frameAt + new Vector2(-70f, 128f), new Vector2(200f, 60f)), KitPill);
                s.newLabel = Label("Label", s.newPill.transform, TextRole.Label, 36f, Text, TextAlignmentOptions.Center, Center, new Vector2(0f, 2f),
                    new Vector2(184f, LineBox(36f, 1)), 1, T(AlbumText.New));
                s.priceChip = borrow.Kit(Img("Price", cell, null, Card, Image.Type.Sliced, Center, frameAt + new Vector2(0f, -135f), new Vector2(170f, 64f)), KitPill);
                s.priceStar = Img("Star", s.priceChip.transform, null, Color.white, Image.Type.Simple, Center, new Vector2(-38f, 0f), new Vector2(44f, 44f));
                s.price = Label("Amount", s.priceChip.transform, TextRole.NumberSmall, 36f, Text, TextAlignmentOptions.MidlineLeft, Center,
                    new Vector2(30f, 2f), new Vector2(76f, LineBox(36f, 1)), 1, new[] { "10", "99" });
                s.name = Label("Name", cell, TextRole.Label, 36f, Text, TextAlignmentOptions.Center, Center, new Vector2(0f, -105f),
                    // Up to 3 lines: long names (« L'Arbre centenaire de la Clairière ») break in three at the role size.
                    new Vector2(310f, LineBox(36f, 3)), 3, stickerNames);
                s.rarity = Label("Rarity", cell, TextRole.Caption, 36f, TextSoft, TextAlignmentOptions.Center, Center, new Vector2(0f, -182f),
                    new Vector2(300f, LineBox(36f, 1)), 1, rarities);
                foreach (var go in new[] { s.inner.gameObject, s.art.gameObject, s.hintIcon.gameObject, s.sign.gameObject, s.soon.gameObject,
                             s.newPill.gameObject, s.priceChip.gameObject })
                    go.SetActive(false);
                p.slots[i] = s;
            }
        }

        private static void BuildChooseList(AlbumScreenParts p, RectTransform safe, Borrow borrow)
        {
            var list = StretchRect("ChooseList", safe, 0f, 0f, 0f, 0f);
            p.chooseList = list.gameObject;
            p.chooseTitle = Label("Title", list, TextRole.Subheading, 48f, Text, TextAlignmentOptions.Center, TopCenter, new Vector2(0f, -240f),
                new Vector2(680f, LineBox(48f, 1)), 1, T(AlbumText.Choose));
            p.chooseContent = ScrollList("Pages", list, 40f, 40f, 40f, 320f,
                AlbumLayout.PageCapacity * AlbumScreenView.RowPitch + 2f * AlbumScreenView.ListPadding, out p.chooseScroll);
            var pageNames = Names("page.");
            var missing = Filled(AlbumText.Missing, "12", "1");
            p.rows = new AlbumChoiceRowParts[AlbumLayout.PageCapacity];
            for (int i = 0; i < p.rows.Length; i++)
            {
                var r = new AlbumChoiceRowParts();
                var rootRect = UiPipeline.Rect("Row" + i, p.chooseContent);
                UiPipeline.Place(rootRect, TopCenter, Center, new Vector2(0f, -AlbumScreenView.ListPadding - AlbumScreenView.RowPitch * (i + 0.5f)),
                    new Vector2(1000f, 150f));
                r.root = rootRect;
                r.card = borrow.Kit(Img("Card", rootRect, null, Card, Image.Type.Sliced, Center, Vector2.zero, new Vector2(1000f, 150f)), KitPanel);
                r.button = ButtonOn(r.card);
                r.title = Label("Title", rootRect, TextRole.Body, 40f, Text, TextAlignmentOptions.MidlineLeft, LeftMiddle, new Vector2(370f, 20f),
                    new Vector2(660f, LineBox(40f, 1)), 1, pageNames);
                r.missing = Label("Missing", rootRect, TextRole.Label, 36f, TextSoft, TextAlignmentOptions.MidlineRight, RightMiddle,
                    new Vector2(-170f, 20f), new Vector2(280f, LineBox(36f, 1)), 1, missing);
                r.barTrack = Bar("Bar", rootRect, LeftMiddle, new Vector2(370f, -40f), new Vector2(660f, 20f), borrow, out r.barFill);
                rootRect.gameObject.SetActive(false);
                p.rows[i] = r;
            }
        }

        private static void BuildConfirm(AlbumScreenParts p, RectTransform screen, Borrow borrow, Material silhouette)
        {
            var confirm = StretchRect("Confirm", screen, 0f, 0f, 0f, 0f);
            p.confirm = confirm.gameObject;
            var dim = UiPipeline.AddImage("Dim", confirm, null, new Color(0.20f, 0.14f, 0.10f, 0.35f), Image.Type.Simple);
            UiPipeline.Stretch(dim.rectTransform);
            p.confirmDim = ButtonOn(dim);
            p.confirmCard = borrow.Kit(Img("Card", confirm, null, Card, Image.Type.Sliced, Center, Vector2.zero, new Vector2(840f, 780f)), KitPanel);
            p.confirmCard.raycastTarget = true;
            var card = p.confirmCard.transform;
            p.confirmArt = Img("Art", card, null, Color.white, Image.Type.Simple, Center, new Vector2(0f, 180f), new Vector2(280f, 280f));
            p.confirmArt.preserveAspect = true;
            p.confirmArt.material = silhouette;
            p.confirmArtUv = Uv(p.confirmArt);
            p.confirmText = Label("Question", card, TextRole.Body, 44f, Text, TextAlignmentOptions.Center, Center, new Vector2(0f, -30f),
                new Vector2(720f, LineBox(44f, 2)), 2, T(AlbumText.ChooseConfirm));
            p.confirmStar = Img("Star", card, null, Color.white, Image.Type.Simple, Center, new Vector2(-40f, -140f), new Vector2(60f, 60f));
            p.confirmPrice = Label("Price", card, TextRole.NumberSmall, 44f, Text, TextAlignmentOptions.MidlineLeft, Center, new Vector2(40f, -138f),
                new Vector2(90f, LineBox(44f, 1)), 1, new[] { "10", "99" });
            var yes = borrow.Kit(Img("Yes", card, null, Accent, Image.Type.Sliced, Center, new Vector2(-205f, -280f), new Vector2(380f, 124f)), KitButton);
            p.yes = ButtonOn(yes);
            p.yesLabel = Label("Label", yes.transform, TextRole.Button, 44f, Text, TextAlignmentOptions.Center, Center, new Vector2(0f, 2f),
                new Vector2(370f, LineBox(44f, 1)), 1, T(AlbumText.Yes));
            var no = borrow.Kit(Img("No", card, null, Cream, Image.Type.Sliced, Center, new Vector2(205f, -280f), new Vector2(380f, 124f)), KitButton);
            p.no = ButtonOn(no);
            p.noLabel = Label("Label", no.transform, TextRole.Button, 44f, Text, TextAlignmentOptions.Center, Center, new Vector2(0f, 2f),
                new Vector2(378f, LineBox(44f, 1)), 1, T(AlbumText.No));
            confirm.gameObject.SetActive(false);
        }

        private static void BuildNote(AlbumScreenParts p, RectTransform safe, Borrow borrow)
        {
            p.notePill = borrow.Kit(Img("Note", safe, null, Card, Image.Type.Sliced, BottomCenter, new Vector2(0f, 330f), new Vector2(940f, 124f)), KitPill);
            p.noteGroup = p.notePill.gameObject.AddComponent<CanvasGroup>();
            p.noteGroup.blocksRaycasts = false;
            p.noteGroup.interactable = false;
            p.noteLabel = Label("Label", p.notePill.transform, TextRole.Body, 38f, Text, TextAlignmentOptions.Center, Center, new Vector2(0f, 2f),
                new Vector2(880f, LineBox(38f, 2)), 2, Hints());
            p.notePill.gameObject.SetActive(false);
        }

        private static void BuildReveal(AlbumScreenParts p, RectTransform content, Borrow borrow, Material silhouette)
        {
            var v = p.reveal;
            var root = UiPipeline.Rect("Reveal", content);
            UiPipeline.Stretch(root);
            v.root = root;
            v.canvas = root.gameObject.AddComponent<Canvas>();
            v.canvas.overrideSorting = true;
            v.canvas.sortingOrder = AlbumLayout.RevealSortingOrder;
            v.canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1;
            root.gameObject.AddComponent<GraphicRaycaster>();
            v.dim = UiPipeline.AddImage("Dim", root, null, Dim, Image.Type.Simple);
            UiPipeline.Stretch(v.dim.rectTransform);
            v.tap = ButtonOn(v.dim);
            var colors = v.tap.colors;
            colors.pressedColor = Color.white;
            v.tap.colors = colors;

            // The cream paper pouch closed by a small round tab.
            var home = new Vector2(0f, 60f);
            var pouch = UiPipeline.Rect("Pouch", root);
            UiPipeline.Place(pouch, Center, Center, home, new Vector2(360f, 440f));
            v.pouch = pouch;
            v.pouchBack = borrow.Kit(Img("Paper", pouch, null, Pouch, Image.Type.Sliced, Center, Vector2.zero, new Vector2(360f, 440f)), KitPanel);
            var flap = UiPipeline.Rect("Flap", pouch);
            flap.anchorMin = TopCenter;
            flap.anchorMax = TopCenter;
            flap.pivot = TopCenter;
            flap.anchoredPosition = Vector2.zero;
            flap.sizeDelta = new Vector2(360f, 170f);
            v.flap = flap;
            v.flapImage = borrow.Kit(UiPipeline.AddImage("Paper", flap, null, PouchFlap, Image.Type.Sliced), KitPanel);
            v.flapImage.raycastTarget = false;
            UiPipeline.Stretch(v.flapImage.rectTransform);
            v.tab = borrow.Kit(Img("Tab", flap, null, PouchTab, Image.Type.Simple, BottomCenter, Vector2.zero, new Vector2(84f, 84f)), KitDot);

            // The sticker.
            var card = UiPipeline.Rect("Sticker", root);
            UiPipeline.Place(card, Center, Center, home, new Vector2(420f, 420f));
            v.card = card;
            v.frame = borrow.Kit(Img("Frame", card, null, Color.white, Image.Type.Sliced, Center, Vector2.zero, new Vector2(420f, 420f)), KitPanel);
            v.inner = borrow.Kit(Img("Inner", card, null, InnerCream, Image.Type.Sliced, Center, Vector2.zero, new Vector2(396f, 396f)), KitPanel);
            v.art = Img("Art", card, null, Color.white, Image.Type.Simple, Center, Vector2.zero, new Vector2(360f, 360f));
            v.art.preserveAspect = true;
            v.artUv = Uv(v.art);
            v.silhouette = Img("Silhouette", card, null, Color.white, Image.Type.Simple, Center, Vector2.zero, new Vector2(360f, 360f));
            v.silhouette.material = silhouette;
            v.silhouetteUv = Uv(v.silhouette);
            v.sign = borrow.Kit(Img("Sign", card, null, RareDot, Image.Type.Simple, Center, new Vector2(164f, 164f), new Vector2(46f, 46f)), KitDot);
            v.signLight = borrow.Kit(Img("Light", v.sign.transform, null, Color.white, Image.Type.Simple, Center, new Vector2(-7f, 7f), new Vector2(16f, 16f)), KitDot);
            v.cardBack = borrow.Kit(Img("Back", card, null, Pouch, Image.Type.Sliced, Center, Vector2.zero, new Vector2(420f, 420f)), KitPanel);
            v.cardBackPaw = Img("Paw", v.cardBack.transform, null, new Color(0.93f, 0.84f, 0.68f, 1f), Image.Type.Simple, Center, Vector2.zero, new Vector2(160f, 160f));
            v.name = Label("Name", card, TextRole.Subheading, 48f, Text, TextAlignmentOptions.Center, Center, new Vector2(0f, -268f),
                new Vector2(900f, LineBox(48f, 2)), 2, Names("sticker."));
            v.rarity = Label("Rarity", card, TextRole.Label, 38f, TextSoft, TextAlignmentOptions.Center, Center, new Vector2(0f, -328f),
                new Vector2(500f, LineBox(38f, 1)), 1, T(AlbumText.Common, AlbumText.Rare, AlbumText.Shiny));
            v.newPill = borrow.Kit(Img("New", root, null, Accent, Image.Type.Sliced, Center, home + new Vector2(0f, 290f), new Vector2(240f, 70f)), KitPill);
            v.newLabel = Label("Label", v.newPill.transform, TextRole.Label, 38f, Text, TextAlignmentOptions.Center, Center, new Vector2(0f, 2f),
                new Vector2(214f, LineBox(38f, 1)), 1, T(AlbumText.New));
            v.duplicate = Label("Duplicate", root, TextRole.Body, 44f, Text, TextAlignmentOptions.Center, Center, home + new Vector2(0f, -420f),
                new Vector2(940f, LineBox(44f, 1)), 1, Filled(AlbumText.Duplicate, "1", "2", "3", "6"));
            v.banner = Label("Banner", root, TextRole.Subheading, 52f, Text, TextAlignmentOptions.Center, Center, home + new Vector2(0f, 400f),
                new Vector2(940f, LineBox(52f, 1)), 1, T(AlbumText.PageHalf, AlbumText.PageComplete, AlbumText.AlbumComplete));

            // H-VFX-038 and H-VFX-074: the kit's soft ring, round dots and bubbles (no new texture).
            v.ring = borrow.Kit(Img("Ring", root, null, Cream, Image.Type.Simple, Center, home, new Vector2(600f, 600f)), KitRing);
            v.landRing = borrow.Kit(Img("LandRing", root, null, Cream, Image.Type.Simple, Center, home, new Vector2(260f, 260f)), KitRing);
            v.confetti = new Image[AlbumLayout.Confetti];
            for (int i = 0; i < v.confetti.Length; i++)
                v.confetti[i] = borrow.Kit(Img("Confetti" + i, root, null, Accent, Image.Type.Simple, Center, home, new Vector2(34f, 34f)), KitDot);
            v.bubbles = new Image[AlbumLayout.Bubbles];
            for (int i = 0; i < v.bubbles.Length; i++)
                v.bubbles[i] = borrow.Kit(Img("Bubble" + i, root, null, Pearl, Image.Type.Simple, Center, home + new Vector2(-150f + i * 43f, -120f),
                    new Vector2(i % 2 == 0 ? 64f : 46f, i % 2 == 0 ? 64f : 46f)), KitBubble);
            v.stars = new Image[AlbumLayout.FlyingStars];
            for (int i = 0; i < v.stars.Length; i++)
                v.stars[i] = Img("Star" + i, root, null, Color.white, Image.Type.Simple, Center, home, new Vector2(84f, 84f));
            foreach (var image in v.confetti.Concat(v.bubbles).Concat(v.stars).Concat(new[] { v.ring, v.landRing, v.newPill, v.cardBack }))
                image.gameObject.SetActive(false);
            v.duplicate.gameObject.SetActive(false);
            v.banner.gameObject.SetActive(false);
            root.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- Addressables

        /// <summary>Group Albums (shared with the page atlases of the sticker capture): the screen, loaded when it opens.</summary>
        public static string EnsureAddressables(GameObject prefab)
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var group = EnsureGroup(settings, AlbumLayout.Group);
            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab)), group, false, false);
            entry.address = AlbumLayout.ScreenAddress;
            // The 16 hand-drawn covers packed (and compressed) in their own atlas, in the screen's group.
            // It is not an entry of its own: the group packs entries separately, so the atlas follows the screen's bundle as its
            // dependency (an entry would put the covers in two bundles).
            string covers = EnsureCoversAtlas();
            if (covers != null)
                settings.RemoveAssetEntry(AssetDatabase.AssetPathToGUID(covers), false);
            EditorUtility.SetDirty(group);
            EditorUtility.SetDirty(settings);
            return $"Addressables {AlbumLayout.Group}: {AlbumLayout.ScreenAddress}" + (covers != null ? " (with Atlas_AlbumCovers)" : "");
        }

        private const string CoversFolder = "Assets/_Game/Art/UI/Albums/Covers";
        private const string CoversAtlasPath = "Assets/_Game/Art/UI/Albums/Atlas_AlbumCovers.spriteatlasv2";

        /// <summary>Atlas_AlbumCovers (ASTC 4x4 on phones): the covers would otherwise ship uncompressed (RGBA32).</summary>
        private static string EnsureCoversAtlas()
        {
            var textures = new List<Object>();
            foreach (string name in CoverNames)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(CoversFolder + "/UI_AlbumCover_" + name + ".png");
                if (texture != null)
                    textures.Add(texture);
            }
            if (textures.Count == 0)
                return null;
            var atlas = new UnityEditor.U2D.SpriteAtlasAsset();
            atlas.Add(textures.ToArray());
            UnityEditor.U2D.SpriteAtlasAsset.Save(atlas, CoversAtlasPath);
            AssetDatabase.ImportAsset(CoversAtlasPath, ImportAssetOptions.ForceUpdate);
            var importer = (UnityEditor.U2D.SpriteAtlasImporter)AssetImporter.GetAtPath(CoversAtlasPath);
            importer.includeInBuild = true;
            var packing = importer.packingSettings;
            packing.enableRotation = false;
            packing.enableTightPacking = false;
            packing.padding = 4;
            importer.packingSettings = packing;
            var textureSettings = importer.textureSettings;
            textureSettings.generateMipMaps = false;
            textureSettings.filterMode = FilterMode.Bilinear;
            textureSettings.sRGB = true;
            importer.textureSettings = textureSettings;
            foreach (var platform in new[] { "Android", "iPhone" })
            {
                var platformSettings = importer.GetPlatformSettings(platform);
                platformSettings.overridden = true;
                platformSettings.maxTextureSize = 2048;
                platformSettings.format = TextureImporterFormat.ASTC_4x4;
                importer.SetPlatformSettings(platformSettings);
            }
            importer.SaveAndReimport();
            return CoversAtlasPath;
        }

        private static AddressableAssetGroup EnsureGroup(AddressableAssetSettings settings, string name)
        {
            var group = settings.FindGroup(name) ?? settings.CreateGroup(name, false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var bundled = group.GetSchema<BundledAssetGroupSchema>();
            if (bundled != null)
            {
                // One bundle per entry: a page's atlas loads alone with its page (and the screen alone when it opens).
                bundled.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
                bundled.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
                bundled.IncludeInBuild = true;
                bundled.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
                bundled.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            }
            EditorUtility.SetDirty(group);
            return group;
        }
    }
}
