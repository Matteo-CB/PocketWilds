using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using PocketWilds.Editor.UI;
using PocketWilds.Presentation;
using PocketWilds.UI;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.Journal
{
    /// <summary>
    /// T44C « Journal du refuge » screen pipeline (doc « 01 Succès, titres, cadres, poses et journal », section 6; screen
    /// I-SCR-037): PF_UI_Journal (JournalScreenView: a screen-space canvas at sorting order 24, over the profile screen) with
    /// the title and close, two tabs; the timeline: 6 pooled page cards (the picture square at 300 with its soft frame, the
    /// stored picture as a raw image and the thumbnail over it, the date, the memory on up to five lines, the system's icon on
    /// a soft disc, « Nouveau ») and 4 pooled month headers, with the empty line; « Moments »: 12 pooled rows and the empty
    /// state « Les petits moments du monde apparaîtront ici ». The UI kit's sprites, the icons and the fonts are named and
    /// borrowed at run time. Every label is measured in its real font with every text it can show in English and French (the
    /// memories from agent CC's JournalTexts with long values in their variables, when the table exists); one overflow and the
    /// prefab is not saved. The Addressables group Journal holds the screen (journal.screen). Idempotent.
    /// </summary>
    public static class JournalScreenPipeline
    {
        public const string PrefabPath = JournalLayout.PrefabPath;

        public static readonly Color Cream = UiColorTokens.Hex(0xFFF8EC);
        public static readonly Color Card = UiColorTokens.Hex(0xFFFCF4);
        public static readonly Color CardNew = UiColorTokens.Hex(0xFFF1DC);
        public static readonly Color Text = UiColorTokens.Hex(0x4A3A2C);
        public static readonly Color TextSoft = UiColorTokens.Hex(0x8A7660);
        public static readonly Color Accent = UiColorTokens.Hex(0xFFB15E);
        public static readonly Color Disc = UiColorTokens.Hex(0xFBF1E2);
        public static readonly Color Rule = UiColorTokens.Hex(0xE9E1D3);

        /// <summary>The soft discs behind the systems' icons (wrapping over <see cref="JournalSystem"/>).</summary>
        public static readonly Color[] SystemTints =
        {
            UiColorTokens.Hex(0xEEF2DC), UiColorTokens.Hex(0xFCEBD6), UiColorTokens.Hex(0xE5EEF6), UiColorTokens.Hex(0xE0F0DF),
            UiColorTokens.Hex(0xFBE6DF), UiColorTokens.Hex(0xF2EADB), UiColorTokens.Hex(0xF4E4EE), UiColorTokens.Hex(0xE3F1EE),
        };

        public const string KitPanel = "UI_Panel_Primary_9Slice";
        public const string KitPill = "UI_Pill_9Slice";
        public const string KitCircle = "UI_Button_IconCircle";
        public const string KitClose = "ICO_Close_X";

        /// <summary>The icons by <see cref="JournalScreenView.JournalIcon"/>, then one per <see cref="JournalSystem"/> (a second choice after « | »).</summary>
        public static readonly string[] IconNames =
        {
            "ICO_Paw", "ICO_Moment|ICO_Favorite_Heart|ICO_Paw",
            "ICO_AchCat_Refuge|ICO_RefugeLevel", "ICO_AchCat_Hatching|ICO_Hatch_Egg", "ICO_AchCat_Collection|ICO_Codex_Book",
            "ICO_AchCat_Biomes|ICO_ZoneSign", "ICO_AchCat_Activities|ICO_Play", "ICO_Act_Expeditions|ICO_AchCat_ExpeditionsGarden",
            "ICO_Act_Garden|ICO_AchCat_ExpeditionsGarden", "ICO_AchCat_Albums|ICO_Album", "ICO_AchCat_Decoration|ICO_Decoration",
            "ICO_AchCat_Bonds|ICO_Favorite_Heart", "ICO_AchCat_Events|ICO_Event_Calendar", "ICO_Visitor|ICO_Paw",
            "ICO_AchCat_Mastery|ICO_Mastery_StarPaw", "ICO_AchCat_Renewal|ICO_Swap", "ICO_Achievement|ICO_AlbumStar|ICO_Rarity_Star",
            "ICO_AchCat_Daily|ICO_Today", "ICO_Act_PhotoSafari", "ICO_Moment|ICO_Favorite_Heart",
        };

        /// <summary>Memories are measured with these in their variables (long names, big numbers, the longest dates).</summary>
        private const string LongName = "Petit Écureuil d'automne";

        private const string LongNumber = "100000";

        [MenuItem("Pocket Wilds/Journal/Build Journal Screen (T44C)", priority = 253)]
        public static void BuildMenu() => Debug.Log("[JournalScreenPipeline] " + BuildAll());

        public static string BuildAll()
        {
            var report = new List<string>();
            var view = BuildPrefab(report);
            if (view != null)
                report.Add(EnsureAddressables(view.gameObject));
            AssetDatabase.SaveAssets();
            return string.Join("; ", report);
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
        private static readonly Vector2 TopRight = new Vector2(1f, 1f);
        private static readonly Vector2 LeftMiddle = new Vector2(0f, 0.5f);

        private static float LineBox(float size, int lines) => Mathf.Ceil(size * (1.2f * lines + 0.15f));

        public static JournalScreenView BuildPrefab(List<string> report)
        {
            Fits.Clear();
            var root = new GameObject(JournalLayout.ScreenPrefabName, typeof(RectTransform));
            root.layer = 5;
            try
            {
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = JournalLayout.SortingOrder;
                canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1;
                UiPipeline.AddScaler(root);
                root.AddComponent<GraphicRaycaster>();
                var view = root.AddComponent<JournalScreenView>();
                var borrow = new Borrow();
                var p = new JournalScreenParts { canvas = canvas };

                var content = UiPipeline.Rect("Content", root.transform);
                content.gameObject.SetActive(false);
                UiPipeline.Stretch(content);
                p.content = content.gameObject;
                p.background = UiPipeline.AddImage("Background", content, null, Cream, Image.Type.Simple);
                p.background.raycastTarget = true;
                UiPipeline.Stretch(p.background.rectTransform);
                var safe = UiPipeline.Rect("SafeArea", content);
                UiPipeline.Stretch(safe);
                p.safeArea = safe;

                BuildTop(p, safe, borrow);
                BuildTabs(p, safe, borrow);
                BuildTimeline(p, safe, borrow);
                BuildMoments(p, safe, borrow);

                var overflows = Measure();
                if (overflows.Count > 0)
                {
                    report.Add("ERROR: labels too long for their boxes, the prefab is not saved: " + string.Join("; ", overflows));
                    Debug.LogError("[JournalScreenPipeline] labels overflow:\n" + string.Join("\n", overflows));
                    return null;
                }
                report.Add($"{Fits.Count} labels measured, every text fits");
                int memories = JournalScreenTexts.ContentOf("memory.", ".text").Length / 2;
                report.Add(memories > 0 ? $"{memories} memories measured with long values" : "WARNING: agent CC's JournalTexts not found (memories not measured yet)");

                view.SetParts(p);
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
                view.SetColors(Card, CardNew, Text, TextSoft, Accent, Disc, SystemTints);
                canvas.enabled = false;

                Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
                var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath).GetComponent<JournalScreenView>();
                report.Add($"{JournalLayout.ScreenPrefabName}: {p.cards.Length} cards, {p.headers.Length} headers, {p.moments.Length} moment rows, "
                           + $"{labels.Length} labels, {borrow.Names.Distinct().Count()} kit sprites borrowed" + (saved.Valid ? "" : " (INCOMPLETE)"));
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
            var probe = new GameObject("JournalMeasure", typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave };
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

        private static string[] T(params string[] keys) => JournalScreenTexts.Of(keys);

        /// <summary>Every date the cards can show (the longest days, every month) in English and French.</summary>
        private static string[] Dates()
        {
            var table = JournalScreenTexts.Texts;
            string Row(string key, int column)
            {
                for (int i = 0; i < table.GetLength(0); i++)
                    if (table[i, 0] == key)
                        return table[i, column];
                return key;
            }
            var dates = new List<string>();
            for (int column = 1; column <= 2; column++)
            {
                string date = Row(JournalText.Date, column), header = Row(JournalText.MonthHeader, column);
                for (int m = 0; m < 12; m++)
                {
                    foreach (string day in new[] { "28", "1er" })
                        dates.Add(date.Replace("{day}", column == 1 && day == "1er" ? "1" : day).Replace("{month}", Row(JournalText.MonthsInDate[m], column))
                            .Replace("{year}", "2026"));
                    dates.Add("H" + header.Replace("{month}", Row(JournalText.Months[m], column)).Replace("{year}", "2026"));
                }
            }
            return dates.ToArray();
        }

        private static readonly Regex Variable = new Regex(@"\{[a-z0-9_]+\}", RegexOptions.Compiled);

        /// <summary>Agent CC's memories with long values in their variables (a fallback sample when the table is not there yet).</summary>
        private static string[] Memories()
        {
            var texts = JournalScreenTexts.ContentOf("memory.", ".text");
            if (texts.Length == 0)
                texts = new[]
                {
                    "{name} came out of its egg on a calm morning. The whole refuge came to say hello.",
                    "{name} est sorti de son œuf par un matin tranquille. Tout le refuge est venu dire bonjour.",
                };
            return texts.Select(t => Variable.Replace(t, v =>
                v.Value is "{count}" or "{score}" or "{level}" or "{page}" ? LongNumber : v.Value == "{date}" ? "28 septembre 2026" : LongName)).ToArray();
        }

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

        private static RectTransform StretchRect(string name, Transform parent, float left, float bottom, float right, float top)
        {
            var rect = UiPipeline.Rect(name, parent);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        private static RectTransform ScrollList(string name, Transform parent, float left, float bottom, float right, float top, out ScrollRect scroll)
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
            list.sizeDelta = new Vector2(1020f, 1000f);
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            UiPipeline.ConfigureScroll(scroll, viewport, list);
            scroll.horizontal = false;
            scroll.vertical = true;
            return list;
        }

        // ---------------------------------------------------------------- layout (reference 1080 × 2340)
        //
        // From the top of the safe area: the bar (title, close) to 180; the tabs at 250; the lists from 330. A card is 1000 ×
        // 360: the picture square (300, its frame and the picture at 284) on the left, the date and the system's icon along the
        // top right, the memory under them on up to five lines, « Nouveau » in the bottom right corner.

        private static void BuildTop(JournalScreenParts p, RectTransform safe, Borrow borrow)
        {
            p.title = Label("Title", safe, TextRole.Heading, 64f, Text, TextAlignmentOptions.Center, TopCenter, new Vector2(0f, -90f),
                new Vector2(720f, LineBox(64f, 1)), 1, T(JournalText.Title));
            var closeBack = borrow.Kit(Img("Close", safe, null, Card, Image.Type.Simple, TopRight, new Vector2(-90f, -90f), new Vector2(120f, 120f)), KitCircle);
            p.close = ButtonOn(closeBack);
            p.closeIcon = borrow.Kit(Img("Icon", closeBack.transform, null, TextSoft, Image.Type.Simple, Center, Vector2.zero, new Vector2(56f, 56f)), KitClose);
        }

        private static void BuildTabs(JournalScreenParts p, RectTransform safe, Borrow borrow)
        {
            p.tabs = new JournalTabParts[2];
            var keys = new[] { JournalText.TabPages, JournalText.TabMoments };
            for (int i = 0; i < 2; i++)
            {
                var t = new JournalTabParts();
                t.back = borrow.Kit(Img("Tab" + i, safe, null, Card, Image.Type.Sliced, TopCenter, new Vector2((i - 0.5f) * 330f, -250f), new Vector2(310f, 104f)),
                    KitPill);
                t.button = ButtonOn(t.back);
                t.label = Label("Label", t.back.transform, TextRole.Label, 38f, Text, TextAlignmentOptions.Center, Center, new Vector2(0f, 2f),
                    new Vector2(280f, LineBox(38f, 1)), 1, T(keys[i]));
                p.tabs[i] = t;
            }
        }

        private static void BuildTimeline(JournalScreenParts p, RectTransform safe, Borrow borrow)
        {
            var page = StretchRect("Pages", safe, 0f, 0f, 0f, 0f);
            p.pagesPage = page.gameObject;
            p.empty = Label("Empty", page, TextRole.Body, 40f, TextSoft, TextAlignmentOptions.Center, TopCenter, new Vector2(0f, -720f),
                new Vector2(900f, LineBox(40f, 3)), 3, T(JournalText.Empty));
            p.timelineContent = ScrollList("Timeline", page, 30f, 30f, 30f, 330f, out p.timelineScroll);
            var dates = Dates();
            var memories = Memories();
            p.headers = new JournalHeaderParts[JournalLayout.HeaderCells];
            for (int i = 0; i < p.headers.Length; i++)
            {
                var h = new JournalHeaderParts();
                var rootRect = UiPipeline.Rect("Month" + i, p.timelineContent);
                UiPipeline.Place(rootRect, TopCenter, Center, new Vector2(0f, -JournalLayout.ListPadding - JournalLayout.HeaderPitch * (i + 0.5f)),
                    new Vector2(1000f, JournalLayout.HeaderPitch));
                h.root = rootRect;
                h.label = Label("Label", rootRect, TextRole.Subheading, 48f, Text, TextAlignmentOptions.MidlineLeft, LeftMiddle, new Vector2(390f, 8f),
                    new Vector2(740f, LineBox(48f, 1)), 1, dates.Where(d => d.StartsWith("H", StringComparison.Ordinal)).Select(d => d.Substring(1)).ToArray());
                h.rule = borrow.Kit(Img("Rule", rootRect, null, Rule, Image.Type.Sliced, Center, new Vector2(0f, -40f), new Vector2(960f, 6f)), KitPill);
                rootRect.gameObject.SetActive(false);
                p.headers[i] = h;
            }
            p.cards = new JournalCardParts[JournalLayout.CardCells];
            var dateTexts = dates.Where(d => !d.StartsWith("H", StringComparison.Ordinal)).ToArray();
            float pictureX = -500f + 30f + JournalLayout.PictureShown * 0.5f;
            for (int i = 0; i < p.cards.Length; i++)
            {
                var c = new JournalCardParts();
                var rootRect = UiPipeline.Rect("Card" + i, p.timelineContent);
                UiPipeline.Place(rootRect, TopCenter, Center, new Vector2(0f, -JournalLayout.ListPadding - JournalLayout.CardPitch * (i + 0.5f)),
                    new Vector2(1000f, 360f));
                c.root = rootRect;
                c.card = borrow.Kit(Img("Card", rootRect, null, Card, Image.Type.Sliced, Center, Vector2.zero, new Vector2(1000f, 360f)), KitPanel);
                c.pictureBack = borrow.Kit(Img("PictureFrame", rootRect, null, Disc, Image.Type.Sliced, Center, new Vector2(pictureX, 0f),
                    new Vector2(JournalLayout.PictureShown, JournalLayout.PictureShown)), KitPanel);
                var raw = new GameObject("Picture", typeof(RectTransform), typeof(RawImage));
                raw.layer = 5;
                raw.transform.SetParent(rootRect, false);
                c.picture = raw.GetComponent<RawImage>();
                c.picture.raycastTarget = false;
                c.picture.enabled = false;
                UiPipeline.Place(c.picture.rectTransform, Center, Center, new Vector2(pictureX, 0f),
                    new Vector2(JournalLayout.PictureShown - 16f, JournalLayout.PictureShown - 16f));
                c.thumb = Img("Thumb", rootRect, null, Color.white, Image.Type.Simple, Center, new Vector2(pictureX, 0f),
                    new Vector2(JournalScreenView.IconSize, JournalScreenView.IconSize));
                c.thumb.preserveAspect = true;
                c.date = Label("Date", rootRect, TextRole.Caption, 36f, TextSoft, TextAlignmentOptions.MidlineLeft, Center, new Vector2(115f, 128f),
                    new Vector2(510f, LineBox(36f, 1)), 1, dateTexts);
                c.systemBack = borrow.Kit(Img("SystemDisc", rootRect, null, SystemTints[0], Image.Type.Simple, Center, new Vector2(432f, 126f), new Vector2(88f, 88f)),
                    KitCircle);
                c.systemIcon = Img("SystemIcon", c.systemBack.transform, null, Color.white, Image.Type.Simple, Center, Vector2.zero, new Vector2(56f, 56f));
                c.systemIcon.preserveAspect = true;
                c.text = Label("Memory", rootRect, TextRole.Body, 38f, Text, TextAlignmentOptions.TopLeft, Center, new Vector2(160f, -34f),
                    new Vector2(600f, LineBox(38f, 5)), 5, memories);
                c.newPill = borrow.Kit(Img("New", rootRect, null, Accent, Image.Type.Sliced, Center, new Vector2(388f, -142f), new Vector2(196f, 52f)), KitPill);
                c.newLabel = Label("Label", c.newPill.transform, TextRole.Label, 36f, Text, TextAlignmentOptions.Center, Center, new Vector2(0f, 2f),
                    new Vector2(184f, LineBox(36f, 1)), 1, T(JournalText.New));
                c.newPill.gameObject.SetActive(false);
                rootRect.gameObject.SetActive(false);
                p.cards[i] = c;
            }
        }

        private static void BuildMoments(JournalScreenParts p, RectTransform safe, Borrow borrow)
        {
            var page = StretchRect("Moments", safe, 0f, 0f, 0f, 0f);
            p.momentsPage = page.gameObject;
            p.momentsEmpty = Label("Empty", page, TextRole.Body, 40f, TextSoft, TextAlignmentOptions.Center, TopCenter, new Vector2(0f, -720f),
                new Vector2(900f, LineBox(40f, 3)), 3, T(JournalText.MomentsEmpty));
            p.momentContent = ScrollList("List", page, 30f, 30f, 30f, 330f, out p.momentScroll);
            var names = JournalScreenTexts.ContentOf("surprise.", ".name");
            p.moments = new JournalMomentParts[JournalLayout.MomentCells];
            for (int i = 0; i < p.moments.Length; i++)
            {
                var m = new JournalMomentParts();
                var rootRect = UiPipeline.Rect("Moment" + i, p.momentContent);
                UiPipeline.Place(rootRect, TopCenter, Center, new Vector2(0f, -JournalLayout.ListPadding - JournalLayout.MomentPitch * (i + 0.5f)),
                    new Vector2(1000f, 136f));
                m.root = rootRect;
                m.card = borrow.Kit(Img("Card", rootRect, null, Card, Image.Type.Sliced, Center, Vector2.zero, new Vector2(1000f, 136f)), KitPanel);
                m.icon = Img("Icon", rootRect, null, Color.white, Image.Type.Simple, LeftMiddle, new Vector2(80f, 0f), new Vector2(76f, 76f));
                m.icon.preserveAspect = true;
                m.name = Label("Name", rootRect, TextRole.Body, 40f, Text, TextAlignmentOptions.MidlineLeft, LeftMiddle, new Vector2(560f, 2f),
                    new Vector2(800f, LineBox(40f, 2)), 2, names);
                rootRect.gameObject.SetActive(false);
                p.moments[i] = m;
            }
            page.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- Addressables

        public static string EnsureAddressables(GameObject prefab)
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var group = settings.FindGroup(JournalLayout.Group) ?? settings.CreateGroup(JournalLayout.Group, false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var bundled = group.GetSchema<BundledAssetGroupSchema>();
            if (bundled != null)
            {
                bundled.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
                bundled.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
                bundled.IncludeInBuild = true;
                bundled.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
                bundled.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            }
            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab)), group, false, false);
            entry.address = JournalLayout.ScreenAddress;
            EditorUtility.SetDirty(group);
            EditorUtility.SetDirty(settings);
            return $"Addressables {JournalLayout.Group}: {JournalLayout.ScreenAddress}";
        }
    }
}
