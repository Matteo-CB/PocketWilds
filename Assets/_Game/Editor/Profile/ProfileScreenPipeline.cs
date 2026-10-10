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

namespace PocketWilds.Editor.Profile
{
    /// <summary>
    /// T44B « Succès, titres, cadres, poses » screen pipeline (doc « 01 Succès, titres, cadres, poses et journal », sections
    /// 1, 3, 4, 5; screen I-SCR-020): PF_UI_Achievements (ProfileScreenView: a screen-space canvas at sorting order 23) with
    /// the title and close; the profile card (the portrait in its frame, the selected title and frame under it); four tabs
    /// with their dots; Succès: 14 chips in a horizontal strip (each as wide as its longest text) over the badge grid (12
    /// pooled cells in two columns: the tier's shape with the category's icon, four pips, name, goal, bar, « Plus que 3 », the
    /// claim button with its Gems); Titres and Poses: 12 pooled rows; Cadres: 18 pooled frame cells; the note pill. Frames are
    /// one white ring sprite (agent BA's UI_Frame_Ring) tinted by the palette over a rim copy scaled 1.06, the Double style's
    /// inner ring, and the motifs M_* (0.30 of the ring) on the six fixed points at 225/256 of the radius: agent BA's Editor
    /// <see cref="FrameArt"/> table is baked into the view (<see cref="ProfileFrameStyle"/>). The badge shapes, the ring and
    /// the motifs are the screen's own art (Art/UI/Profile, packed in Atlas_Frames, loaded with the screen); the UI kit's
    /// sprites, the icons and the fonts are named and borrowed at run time. Every label is measured in its real font with
    /// every text it can show in English and French (the names, goals and sources from agent BC's table when it exists); one
    /// overflow and the prefab is not saved. The Addressables group Profile holds the screen (profile.screen). Idempotent.
    /// </summary>
    public static class ProfileScreenPipeline
    {
        public const string PrefabPath = ProfileLayout.PrefabPath;

        public static readonly Color Cream = UiColorTokens.Hex(0xFFF8EC);
        public static readonly Color Card = UiColorTokens.Hex(0xFFFCF4);
        public static readonly Color CardLocked = UiColorTokens.Hex(0xEDE6DA);
        public static readonly Color Text = UiColorTokens.Hex(0x4A3A2C);
        public static readonly Color TextSoft = UiColorTokens.Hex(0x8A7660);
        public static readonly Color Accent = UiColorTokens.Hex(0xFFB15E);
        public static readonly Color Selected = UiColorTokens.Hex(0xFFEDCC);
        public static readonly Color Track = UiColorTokens.Hex(0xE9E1D3);
        public static readonly Color PlainRing = UiColorTokens.Hex(0xFFF2DE);
        public static readonly Color PlainRim = UiColorTokens.Hex(0xE8D6B8);
        public static readonly Color Coral = UiColorTokens.Hex(0xF08A7A);
        public static readonly Color Disc = UiColorTokens.Hex(0xFBF1E2);

        public const string KitPanel = "UI_Panel_Primary_9Slice";
        public const string KitButton = "UI_Button_Primary_9Slice";
        public const string KitPill = "UI_Pill_9Slice";
        public const string KitCircle = "UI_Button_IconCircle";
        public const string KitDot = "UI_Dot";
        public const string KitRing = "UI_Ring_Soft";
        public const string KitClose = "ICO_Close_X";

        /// <summary>The icons by <see cref="ProfileScreenView.ProfileIcon"/>, then the 13 categories, borrowed by name (a second choice after « | »).</summary>
        public static readonly string[] IconNames =
        {
            "ICO_Currency_Gems", "ICO_Lock", "ICO_Check", "ICO_Paw", "ICO_Act_PhotoSafari", "ICO_Feather|ICO_WeeklyJournal", KitCircle,
            "ICO_AlbumStar|ICO_Rarity_Star",
            "ICO_AchCat_Collection|ICO_Codex_Book", "ICO_AchCat_Biomes|ICO_ZoneSign", "ICO_AchCat_Mastery|ICO_Mastery_StarPaw",
            "ICO_AchCat_Hatching|ICO_Hatch_Egg", "ICO_AchCat_Activities|ICO_Play", "ICO_AchCat_Daily|ICO_Today",
            "ICO_AchCat_ExpeditionsGarden|ICO_Act_Expeditions", "ICO_AchCat_Albums|ICO_Album", "ICO_AchCat_Decoration|ICO_Decoration",
            "ICO_AchCat_Bonds|ICO_Favorite_Heart", "ICO_AchCat_Events|ICO_Event_Calendar", "ICO_AchCat_Renewal|ICO_Swap",
            "ICO_AchCat_Refuge|ICO_RefugeLevel",
        };

        /// <summary>The badge shapes by <see cref="AchievementTier"/> (None: the locked shape).</summary>
        private static readonly string[] BadgeFiles = { "UI_Badge_Locked", "UI_Badge_Bronze", "UI_Badge_Silver", "UI_Badge_Gold", "UI_Badge_Star" };

        private const string RingFile = "UI_Frame_Ring";

        /// <summary>The motif points' angles (degrees) in <see cref="FramePoint"/> order (agent BA's FrameSlot order).</summary>
        private static readonly float[] PointAngles = { 90f, 270f, 135f, 45f, 225f, 315f };

        /// <summary>Goals are measured with this long a threshold in their {n}.</summary>
        private const string LongNumber = "100000";

        [MenuItem("Pocket Wilds/Profile/Build Profile Screen (T44B)", priority = 252)]
        public static void BuildMenu() => Debug.Log("[ProfileScreenPipeline] " + BuildAll());

        public static string BuildAll()
        {
            var report = new List<string>();
            var styles = BakeFrameStyles(report);
            var shapes = new Sprite[BadgeFiles.Length];
            int found = 0;
            for (int i = 0; i < BadgeFiles.Length; i++)
            {
                shapes[i] = LoadSprite(ProfileLayout.ArtFolder + "/" + BadgeFiles[i] + ".png") ?? LoadSprite(ProfileLayout.BadgesFolder + "/" + BadgeFiles[i] + ".png");
                if (shapes[i] != null)
                    found++;
            }
            report.Add(found == BadgeFiles.Length ? "5 badge shapes" : $"WARNING: {found}/5 badge shapes (the round kit shape stands in)");
            var view = BuildPrefab(styles, shapes, report);
            if (view != null)
                report.Add(EnsureAddressables(view.gameObject));
            AssetDatabase.SaveAssets();
            return string.Join("; ", report);
        }

        private static Sprite LoadSprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        /// <summary>Agent BA's FrameArt table as the view's data: palette, Double inner ring, the motif sprite and tint of each point.</summary>
        public static ProfileFrameStyle[] BakeFrameStyles(List<string> report)
        {
            var styles = new List<ProfileFrameStyle>();
            var missing = new List<string>();
            foreach (var art in FrameArt.All)
            {
                if (art == null || string.IsNullOrEmpty(art.Key))
                    continue;
                var style = new ProfileFrameStyle
                {
                    key = art.Key,
                    ring = art.RingColor,
                    rim = art.RimColor,
                    inner = art.Style == FrameRingStyle.Double && art.StyleColors != null && art.StyleColors.Length > 0
                        ? (Color)art.StyleColors[0]
                        : new Color(1f, 1f, 1f, 0f),
                };
                for (int i = 0; i < ProfileLayout.MotifPoints; i++)
                    style.tints[i] = Color.white;
                if (art.Motifs != null)
                    foreach (var motif in art.Motifs)
                    {
                        int point = (int)motif.Slot;
                        if ((uint)point >= ProfileLayout.MotifPoints || string.IsNullOrEmpty(motif.Sprite))
                            continue;
                        string name = motif.Sprite.StartsWith("M_", StringComparison.Ordinal) ? motif.Sprite : "M_" + motif.Sprite;
                        var sprite = LoadSprite(ProfileLayout.FramesFolder + "/" + name + ".png");
                        if (sprite == null && !missing.Contains(name))
                            missing.Add(name);
                        style.motifs[point] = sprite;
                        style.tints[point] = motif.Tint;
                    }
                styles.Add(style);
            }
            report.Add($"{styles.Count} frame styles baked from FrameArt" + (missing.Count > 0 ? " (WARNING: motif sprites missing: " + string.Join(", ", missing) + ")" : ""));
            return styles.ToArray();
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
        private static Sprite s_ring;

        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        private static readonly Vector2 TopRight = new Vector2(1f, 1f);
        private static readonly Vector2 BottomCenter = new Vector2(0.5f, 0f);
        private static readonly Vector2 LeftMiddle = new Vector2(0f, 0.5f);
        private static readonly Vector2 RightMiddle = new Vector2(1f, 0.5f);

        private static float LineBox(float size, int lines) => Mathf.Ceil(size * (1.2f * lines + 0.15f));

        public static ProfileScreenView BuildPrefab(ProfileFrameStyle[] styles, Sprite[] shapes, List<string> report)
        {
            Fits.Clear();
            s_ring = LoadSprite(ProfileLayout.FramesFolder + "/" + RingFile + ".png");
            if (s_ring == null)
                report.Add("WARNING: no " + RingFile + " (the kit's soft ring stands in)");
            var root = new GameObject(ProfileLayout.ScreenPrefabName, typeof(RectTransform));
            root.layer = 5;
            try
            {
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = ProfileLayout.SortingOrder;
                canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1;
                UiPipeline.AddScaler(root);
                root.AddComponent<GraphicRaycaster>();
                var view = root.AddComponent<ProfileScreenView>();
                var borrow = new Borrow();
                var p = new ProfileScreenParts { canvas = canvas };

                // Everything under an inactive Content: no label wakes up (its font references are cleared before the save).
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
                BuildCard(p, safe, borrow);
                BuildTabs(p, safe, borrow);
                BuildAchievements(p, safe, borrow);
                BuildRows(p, safe, borrow);
                BuildFrames(p, safe, borrow);
                BuildNote(p, safe, borrow);

                var overflows = Measure();
                if (overflows.Count > 0)
                {
                    report.Add("ERROR: labels too long for their boxes, the prefab is not saved: " + string.Join("; ", overflows));
                    Debug.LogError("[ProfileScreenPipeline] labels overflow:\n" + string.Join("\n", overflows));
                    return null;
                }
                report.Add($"{Fits.Count} labels measured, every text fits");
                int names = ProfileScreenTexts.ContentNames().GetLength(0);
                report.Add(names > 0 ? $"{names} achievement, title, frame and pose texts measured" : "WARNING: agent BC's ProfileTexts table not found (names not measured yet)");

                view.SetParts(p, styles, shapes);
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
                view.SetColors(Card, CardLocked, Text, TextSoft, Accent, Selected, PlainRing, PlainRim);
                canvas.enabled = false;

                Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
                var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath).GetComponent<ProfileScreenView>();
                report.Add($"{ProfileLayout.ScreenPrefabName}: {p.badges.Length} badge cells, {p.rows.Length} rows, {p.frames.Length} frame cells, "
                           + $"{p.chips.Length} chips, {labels.Length} labels, {borrow.Names.Distinct().Count()} kit sprites borrowed"
                           + (saved.Valid ? "" : " (INCOMPLETE)"));
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
            var probe = new GameObject("ProfileMeasure", typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave };
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

        /// <summary>The texts of keys in English and French (ProfileScreenTexts).</summary>
        private static string[] T(params string[] keys)
        {
            var found = new List<string>();
            var table = ProfileScreenTexts.Texts;
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
            T(key, key + ".one", key + ".other").SelectMany(t => numbers.Select(n => t.Replace("{n}", n))).ToArray();

        /// <summary>Agent BC's texts whose key starts with <paramref name="prefix"/> and ends with <paramref name="suffix"/>.</summary>
        private static string[] Names(string prefix, string suffix)
        {
            var table = ProfileScreenTexts.ContentNames();
            var found = new List<string>();
            for (int i = 0; i < table.GetLength(0); i++)
            {
                string key = table[i, 0];
                if (key == null || !key.StartsWith(prefix, StringComparison.Ordinal) || !key.EndsWith(suffix, StringComparison.Ordinal))
                    continue;
                found.Add(table[i, 1]);
                found.Add(table[i, 2]);
            }
            return found.ToArray();
        }

        private static string[] Goals() => Names("achievement.", ".goal").Select(t => t.Replace("{n}", LongNumber)).ToArray();

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

        /// <summary>A vertical scrolling list: its masked viewport and the content its pooled cells sit in.</summary>
        private static RectTransform ScrollList(string name, Transform parent, float left, float bottom, float right, float top, out ScrollRect scroll,
            out RectTransform viewport)
        {
            viewport = StretchRect(name, parent, left, bottom, right, top);
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

        /// <summary>A portrait in its frame (section 4): disc, portrait, rim (1.06), ring, inner ring (0.86), 6 motif points.</summary>
        private static ProfileFrameParts Frame(string name, Transform parent, Vector2 anchor, Vector2 position, float ring, Borrow borrow)
        {
            var f = new ProfileFrameParts();
            var rootRect = UiPipeline.Rect(name, parent);
            UiPipeline.Place(rootRect, anchor, Center, position, new Vector2(ring, ring));
            f.root = rootRect;
            f.disc = borrow.Kit(Img("Disc", rootRect, null, Disc, Image.Type.Simple, Center, Vector2.zero, new Vector2(ring * 0.8f, ring * 0.8f)), KitCircle);
            f.portrait = Img("Portrait", rootRect, null, Color.white, Image.Type.Simple, Center, Vector2.zero, new Vector2(ring * 0.72f, ring * 0.72f));
            f.portrait.preserveAspect = true;
            f.rim = RingImage("Rim", rootRect, PlainRim, ring * 1.06f, borrow);
            f.ring = RingImage("Ring", rootRect, PlainRing, ring, borrow);
            f.inner = RingImage("Inner", rootRect, PlainRim, ring * 0.86f, borrow);
            f.inner.gameObject.SetActive(false);
            f.motifs = new Image[ProfileLayout.MotifPoints];
            float radius = ring * 0.5f * (225f / 256f);
            float size = ring * 0.30f;
            for (int i = 0; i < f.motifs.Length; i++)
            {
                float a = PointAngles[i] * Mathf.Deg2Rad;
                var motif = Img("Motif_" + (FramePoint)i, rootRect, null, Color.white, Image.Type.Simple, Center,
                    new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius), new Vector2(size, size));
                motif.preserveAspect = true;
                motif.gameObject.SetActive(false);
                f.motifs[i] = motif;
            }
            return f;
        }

        private static Image RingImage(string name, Transform parent, Color color, float size, Borrow borrow)
        {
            var image = Img(name, parent, s_ring, color, Image.Type.Simple, Center, Vector2.zero, new Vector2(size, size));
            if (s_ring == null)
                borrow.Kit(image, KitRing);
            return image;
        }

        // ---------------------------------------------------------------- layout (reference 1080 × 2340)
        //
        // From the top of the safe area: the bar (title, close) to 180; the card (portrait in its frame, title, frame's name)
        // from 200 to 640; the tabs at 720; then a tab's page: the chips at 840 and the badges from 900 (Succès), the rows
        // from 820 (900 under « Mode photo bientôt »), the frames from 820. Sizes never go under their role's minimum (Label,
        // NumberSmall and Caption 36, Body 38, Subheading and Button 44).

        private static void BuildTop(ProfileScreenParts p, RectTransform safe, Borrow borrow)
        {
            p.title = Label("Title", safe, TextRole.Heading, 64f, Text, TextAlignmentOptions.Center, TopCenter, new Vector2(0f, -90f),
                new Vector2(640f, LineBox(64f, 1)), 1, T(ProfileText.Title));
            var closeBack = borrow.Kit(Img("Close", safe, null, Card, Image.Type.Simple, TopRight, new Vector2(-90f, -90f), new Vector2(120f, 120f)), KitCircle);
            p.close = ButtonOn(closeBack);
            p.closeIcon = borrow.Kit(Img("Icon", closeBack.transform, null, TextSoft, Image.Type.Simple, Center, Vector2.zero, new Vector2(56f, 56f)), KitClose);
        }

        private static void BuildCard(ProfileScreenParts p, RectTransform safe, Borrow borrow)
        {
            p.card = borrow.Kit(Img("Card", safe, null, Card, Image.Type.Sliced, TopCenter, new Vector2(0f, -420f), new Vector2(1000f, 440f)), KitPanel);
            var card = p.card.transform;
            p.cardFrame = Frame("Portrait", card, Center, new Vector2(0f, 68f), 300f, borrow);
            p.cardTitle = Label("TitleName", card, TextRole.Subheading, 48f, Text, TextAlignmentOptions.Center, Center, new Vector2(0f, -128f),
                new Vector2(920f, LineBox(48f, 2)), 2, Names("title.", ".name"), T(ProfileText.NoTitle));
            p.cardFrameName = Label("FrameName", card, TextRole.Label, 36f, TextSoft, TextAlignmentOptions.Center, Center, new Vector2(0f, -192f),
                new Vector2(920f, LineBox(36f, 1)), 1, Names("frame.", ".name"), T(ProfileText.PlainFrame));
        }

        private static void BuildTabs(ProfileScreenParts p, RectTransform safe, Borrow borrow)
        {
            // « Succès » / « Achievements » is the long one: its tab is wider.
            float[] widths = { 300f, 230f, 230f, 230f };
            const float gap = 10f;
            float total = widths.Sum() + gap * (widths.Length - 1);
            float x = -total * 0.5f;
            p.tabs = new ProfileTabParts[4];
            for (int i = 0; i < 4; i++)
            {
                var t = new ProfileTabParts();
                float w = widths[i];
                t.back = borrow.Kit(Img("Tab" + i, safe, null, Card, Image.Type.Sliced, TopCenter, new Vector2(x + w * 0.5f, -720f), new Vector2(w, 104f)), KitPill);
                t.button = ButtonOn(t.back);
                t.label = Label("Label", t.back.transform, TextRole.Label, 36f, Text, TextAlignmentOptions.Center, Center, new Vector2(0f, 2f),
                    new Vector2(w - 24f, LineBox(36f, 1)), 1, T(ProfileText.Tab((ProfileTab)i)));
                t.dot = borrow.Kit(Img("Dot", t.back.transform, null, Coral, Image.Type.Simple, TopRight, new Vector2(-14f, -14f), new Vector2(34f, 34f)), KitDot);
                t.dot.gameObject.SetActive(false);
                p.tabs[i] = t;
                x += w + gap;
            }
        }

        private static void BuildAchievements(ProfileScreenParts p, RectTransform safe, Borrow borrow)
        {
            var page = StretchRect("Achievements", safe, 0f, 0f, 0f, 0f);
            p.achievements = page.gameObject;

            // The chips: a horizontal strip, each chip as wide as its longest text.
            var strip = UiPipeline.Rect("Chips", page);
            strip.anchorMin = new Vector2(0f, 1f);
            strip.anchorMax = new Vector2(1f, 1f);
            strip.pivot = TopCenter;
            strip.offsetMin = new Vector2(30f, -890f);
            strip.offsetMax = new Vector2(-30f, -790f);
            strip.gameObject.AddComponent<RectMask2D>();
            var stripImage = strip.gameObject.AddComponent<Image>();
            stripImage.color = new Color(1f, 1f, 1f, 0f);
            var chipContent = UiPipeline.Rect("Items", strip);
            chipContent.anchorMin = LeftMiddle;
            chipContent.anchorMax = LeftMiddle;
            chipContent.pivot = LeftMiddle;
            chipContent.anchoredPosition = Vector2.zero;
            p.chipContent = chipContent;
            p.chipScroll = strip.gameObject.AddComponent<ScrollRect>();
            UiPipeline.ConfigureScroll(p.chipScroll, strip, chipContent);
            p.chipScroll.horizontal = true;
            p.chipScroll.vertical = false;
            p.chips = new ProfileChipParts[ProfileLayout.Chips];
            float x = 8f;
            for (int i = 0; i < p.chips.Length; i++)
            {
                var c = new ProfileChipParts();
                var texts = i == 0 ? T(ProfileText.All) : T(ProfileText.Category((AchievementCategory)(i - 1)));
                var rootRect = UiPipeline.Rect("Chip" + i, chipContent);
                c.root = rootRect;
                c.back = borrow.Kit(UiPipeline.AddImage("Back", rootRect, null, Card, Image.Type.Sliced), KitPill);
                UiPipeline.Stretch(c.back.rectTransform);
                c.button = ButtonOn(c.back);
                c.icon = Img("Icon", rootRect, null, Color.white, Image.Type.Simple, LeftMiddle, new Vector2(48f, 0f), new Vector2(56f, 56f));
                c.icon.preserveAspect = true;
                var label = UiPipeline.AddText("Label", rootRect, TextRole.Label, 36f, TextSoft, string.Empty);
                // The probe has no font yet (its measure is near zero): about 0.62 em per character at the role size.
                float textWidth = texts.Length == 0 ? 120f : Mathf.Ceil(texts.Max(t => t.Length) * 36f * 0.62f) + 16f;
                Object.DestroyImmediate(label.gameObject);
                float width = 84f + textWidth + 28f;
                UiPipeline.Place(rootRect, LeftMiddle, LeftMiddle, new Vector2(x, 0f), new Vector2(width, 84f));
                c.label = Label("Label", rootRect, TextRole.Label, 36f, TextSoft, TextAlignmentOptions.MidlineLeft, LeftMiddle,
                    new Vector2(84f + textWidth * 0.5f, 2f), new Vector2(textWidth, LineBox(36f, 1)), 1, texts);
                c.dot = borrow.Kit(Img("Dot", rootRect, null, Coral, Image.Type.Simple, TopRight, new Vector2(-12f, -10f), new Vector2(28f, 28f)), KitDot);
                c.dot.gameObject.SetActive(false);
                p.chips[i] = c;
                x += width + 16f;
            }
            chipContent.sizeDelta = new Vector2(x + 8f, 100f);

            // The badges: 12 pooled cells in two columns, recycled over the 200 achievements.
            p.badgeContent = ScrollList("Badges", page, 30f, 30f, 30f, 910f, out p.badgeScroll, out _);
            var names = Names("achievement.", ".name");
            var goals = Goals();
            var toGo = Filled(ProfileText.ToGo, LongNumber, "1").Concat(T(ProfileText.Complete)).ToArray();
            p.badges = new ProfileBadgeParts[ProfileLayout.BadgeCells];
            for (int i = 0; i < p.badges.Length; i++)
            {
                var b = new ProfileBadgeParts();
                int column = i % ProfileLayout.BadgeColumns, row = i / ProfileLayout.BadgeColumns;
                var rootRect = UiPipeline.Rect("Badge" + i, p.badgeContent);
                UiPipeline.Place(rootRect, TopCenter, Center,
                    new Vector2((column - 0.5f) * ProfileScreenView.BadgePitchX, -ProfileScreenView.ListPadding - ProfileScreenView.BadgePitchY * (row + 0.5f)),
                    new Vector2(490f, 600f));
                b.root = rootRect;
                b.card = borrow.Kit(Img("Card", rootRect, null, Card, Image.Type.Sliced, Center, Vector2.zero, new Vector2(490f, 600f)), KitPanel);
                b.button = ButtonOn(b.card);
                b.shape = Img("Shape", rootRect, null, Color.white, Image.Type.Simple, Center, new Vector2(0f, 180f), new Vector2(170f, 170f));
                b.shape.preserveAspect = true;
                b.icon = Img("Icon", b.shape.transform, null, Color.white, Image.Type.Simple, Center, Vector2.zero, new Vector2(84f, 84f));
                b.icon.preserveAspect = true;
                b.pips = new Image[4];
                for (int k = 0; k < 4; k++)
                    b.pips[k] = borrow.Kit(Img("Pip" + k, rootRect, null, Track, Image.Type.Simple, Center, new Vector2((k - 1.5f) * 32f, 76f), new Vector2(20f, 20f)), KitDot);
                b.name = Label("Name", rootRect, TextRole.Body, 38f, Text, TextAlignmentOptions.Center, Center, new Vector2(0f, 10f),
                    new Vector2(450f, LineBox(38f, 2)), 2, names);
                b.goal = Label("Goal", rootRect, TextRole.Caption, 36f, TextSoft, TextAlignmentOptions.Center, Center, new Vector2(0f, -124f),
                    new Vector2(450f, LineBox(36f, 4)), 4, goals);
                b.barTrack = Bar("Bar", rootRect, Center, new Vector2(0f, -226f), new Vector2(420f, 22f), borrow, out b.barFill);
                b.toGo = Label("ToGo", rootRect, TextRole.Label, 36f, Text, TextAlignmentOptions.Center, Center, new Vector2(0f, -264f),
                    new Vector2(440f, LineBox(36f, 1)), 1, toGo);
                b.claimBack = borrow.Kit(Img("Claim", rootRect, null, Accent, Image.Type.Sliced, Center, new Vector2(0f, -240f), new Vector2(440f, 104f)), KitButton);
                b.claim = ButtonOn(b.claimBack);
                b.claimLabel = Label("Label", b.claimBack.transform, TextRole.Button, 44f, Text, TextAlignmentOptions.Center, Center, new Vector2(-84f, 2f),
                    new Vector2(240f, LineBox(44f, 1)), 1, T(ProfileText.Claim));
                b.claimGem = Img("Gem", b.claimBack.transform, null, Color.white, Image.Type.Simple, Center, new Vector2(72f, 0f), new Vector2(56f, 56f));
                b.claimGems = Label("Gems", b.claimBack.transform, TextRole.NumberSmall, 44f, Text, TextAlignmentOptions.MidlineLeft, Center,
                    new Vector2(156f, 2f), new Vector2(110f, LineBox(44f, 1)), 1, new[] { "+85", "+50" });
                b.claimBack.gameObject.SetActive(false);
                rootRect.gameObject.SetActive(false);
                p.badges[i] = b;
            }
        }

        private static void BuildRows(ProfileScreenParts p, RectTransform safe, Borrow borrow)
        {
            var page = StretchRect("Rows", safe, 0f, 0f, 0f, 0f);
            p.rowsPage = page.gameObject;
            p.photoSoon = Label("PhotoSoon", page, TextRole.Body, 38f, TextSoft, TextAlignmentOptions.Center, TopCenter, new Vector2(0f, -852f),
                new Vector2(940f, LineBox(38f, 1)), 1, T(ProfileText.PhotoSoon));
            p.rowContent = ScrollList("List", page, 30f, 30f, 30f, ProfileScreenView.RowsTop, out p.rowScroll, out p.rowViewport);
            var names = Names("title.", ".name").Concat(Names("pose.", ".name")).Concat(T(ProfileText.NoTitle)).ToArray();
            var sources = Names("title.", ".source").Concat(Names("pose.", ".source")).Concat(T(ProfileText.NoTitleSource)).ToArray();
            p.rows = new ProfileRowParts[ProfileLayout.RowCells];
            for (int i = 0; i < p.rows.Length; i++)
            {
                var r = new ProfileRowParts();
                var rootRect = UiPipeline.Rect("Row" + i, p.rowContent);
                UiPipeline.Place(rootRect, TopCenter, Center, new Vector2(0f, -ProfileScreenView.ListPadding - ProfileScreenView.RowPitch * (i + 0.5f)),
                    new Vector2(1000f, 240f));
                r.root = rootRect;
                r.card = borrow.Kit(Img("Card", rootRect, null, Card, Image.Type.Sliced, Center, Vector2.zero, new Vector2(1000f, 240f)), KitPanel);
                r.button = ButtonOn(r.card);
                r.icon = Img("Icon", rootRect, null, Color.white, Image.Type.Simple, LeftMiddle, new Vector2(80f, 0f), new Vector2(96f, 96f));
                r.icon.preserveAspect = true;
                r.lockIcon = Img("Lock", rootRect, null, TextSoft, Image.Type.Simple, LeftMiddle, new Vector2(80f, 0f), new Vector2(72f, 72f));
                r.name = Label("Name", rootRect, TextRole.Body, 40f, Text, TextAlignmentOptions.MidlineLeft, LeftMiddle, new Vector2(540f, 66f),
                    new Vector2(760f, LineBox(40f, 1)), 1, names);
                r.source = Label("Source", rootRect, TextRole.Caption, 36f, TextSoft, TextAlignmentOptions.TopLeft, LeftMiddle, new Vector2(500f, -36f),
                    new Vector2(680f, LineBox(36f, 3)), 3, sources);
                r.newPill = borrow.Kit(Img("New", rootRect, null, Accent, Image.Type.Sliced, TopRight, new Vector2(-120f, -2f), new Vector2(200f, 56f)), KitPill);
                r.newLabel = Label("Label", r.newPill.transform, TextRole.Label, 36f, Text, TextAlignmentOptions.Center, Center, new Vector2(0f, 2f),
                    new Vector2(188f, LineBox(36f, 1)), 1, T(ProfileText.New));
                r.check = Img("Check", rootRect, null, Color.white, Image.Type.Simple, RightMiddle, new Vector2(-80f, -34f), new Vector2(64f, 64f));
                r.check.preserveAspect = true;
                foreach (var go in new[] { r.lockIcon.gameObject, r.newPill.gameObject, r.check.gameObject, rootRect.gameObject })
                    go.SetActive(false);
                p.rows[i] = r;
            }
        }

        private static void BuildFrames(ProfileScreenParts p, RectTransform safe, Borrow borrow)
        {
            var page = StretchRect("Frames", safe, 0f, 0f, 0f, 0f);
            p.framesPage = page.gameObject;
            p.frameContent = ScrollList("Grid", page, 30f, 30f, 30f, ProfileScreenView.RowsTop, out p.frameScroll, out _);
            var names = Names("frame.", ".name");
            p.frames = new ProfileFrameCellParts[ProfileLayout.FrameCells];
            for (int i = 0; i < p.frames.Length; i++)
            {
                var f = new ProfileFrameCellParts();
                int column = i % ProfileLayout.FrameColumns, row = i / ProfileLayout.FrameColumns;
                var rootRect = UiPipeline.Rect("Frame" + i, p.frameContent);
                UiPipeline.Place(rootRect, TopCenter, Center,
                    new Vector2((column - 1) * ProfileScreenView.FramePitchX, -ProfileScreenView.ListPadding - ProfileScreenView.FramePitchY * (row + 0.5f)),
                    new Vector2(320f, 380f));
                f.root = rootRect;
                f.card = borrow.Kit(Img("Card", rootRect, null, Card, Image.Type.Sliced, Center, Vector2.zero, new Vector2(320f, 380f)), KitPanel);
                f.button = ButtonOn(f.card);
                f.frame = Frame("Preview", rootRect, Center, new Vector2(0f, 50f), 220f, borrow);
                f.name = Label("Name", rootRect, TextRole.Label, 36f, Text, TextAlignmentOptions.Center, Center, new Vector2(0f, -132f),
                    new Vector2(300f, LineBox(36f, 2)), 2, names);
                f.lockIcon = Img("Lock", rootRect, null, TextSoft, Image.Type.Simple, TopRight, new Vector2(-38f, -38f), new Vector2(52f, 52f));
                f.check = Img("Check", rootRect, null, Color.white, Image.Type.Simple, TopRight, new Vector2(-38f, -38f), new Vector2(56f, 56f));
                f.check.preserveAspect = true;
                f.newPill = borrow.Kit(Img("New", rootRect, null, Accent, Image.Type.Sliced, TopLeft, new Vector2(104f, -30f), new Vector2(196f, 52f)), KitPill);
                f.newLabel = Label("Label", f.newPill.transform, TextRole.Label, 36f, Text, TextAlignmentOptions.Center, Center, new Vector2(0f, 2f),
                    new Vector2(186f, LineBox(36f, 1)), 1, T(ProfileText.New));
                foreach (var go in new[] { f.lockIcon.gameObject, f.check.gameObject, f.newPill.gameObject, rootRect.gameObject })
                    go.SetActive(false);
                p.frames[i] = f;
            }
            page.gameObject.SetActive(false);
        }

        private static void BuildNote(ProfileScreenParts p, RectTransform safe, Borrow borrow)
        {
            p.notePill = borrow.Kit(Img("Note", safe, null, Card, Image.Type.Sliced, BottomCenter, new Vector2(0f, 150f), new Vector2(980f, 124f)), KitPill);
            p.noteGroup = p.notePill.gameObject.AddComponent<CanvasGroup>();
            p.noteGroup.blocksRaycasts = false;
            p.noteGroup.interactable = false;
            var notes = T(ProfileText.PhotoSoon, ProfileText.NoRoom, ProfileText.Locked)
                .Concat(Filled(ProfileText.Claimed, "85", "5"))
                .Concat(Names("title.", ".source")).Concat(Names("frame.", ".source")).Concat(Names("pose.", ".source")).ToArray();
            p.noteLabel = Label("Label", p.notePill.transform, TextRole.Body, 38f, Text, TextAlignmentOptions.Center, Center, new Vector2(0f, 2f),
                new Vector2(920f, LineBox(38f, 2)), 2, notes);
            p.notePill.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- Addressables

        public static string EnsureAddressables(GameObject prefab)
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var group = EnsureGroup(settings, ProfileLayout.Group);
            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab)), group, false, false);
            entry.address = ProfileLayout.ScreenAddress;
            // The ring, the motifs and the badge shapes packed (and compressed) in Atlas_Frames, in the screen's group. Not an
            // entry of its own: the atlas follows the screen's bundle as its dependency, loaded when the profile opens.
            string atlas = EnsureFramesAtlas();
            if (atlas != null)
                settings.RemoveAssetEntry(AssetDatabase.AssetPathToGUID(atlas), false);
            EditorUtility.SetDirty(group);
            EditorUtility.SetDirty(settings);
            return $"Addressables {ProfileLayout.Group}: {ProfileLayout.ScreenAddress}" + (atlas != null ? " (with Atlas_Frames)" : "");
        }

        /// <summary>Atlas_Frames (ASTC 4x4 on phones): the ring, the 42 motifs and the 5 badge shapes.</summary>
        private static string EnsureFramesAtlas()
        {
            var textures = new List<Object>();
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ProfileLayout.FramesFolder }))
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
                if (texture != null)
                    textures.Add(texture);
            }
            foreach (string name in BadgeFiles)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ProfileLayout.ArtFolder + "/" + name + ".png")
                              ?? AssetDatabase.LoadAssetAtPath<Texture2D>(ProfileLayout.BadgesFolder + "/" + name + ".png");
                if (texture != null)
                    textures.Add(texture);
            }
            if (textures.Count == 0)
                return null;
            var atlas = new UnityEditor.U2D.SpriteAtlasAsset();
            atlas.Add(textures.ToArray());
            UnityEditor.U2D.SpriteAtlasAsset.Save(atlas, ProfileLayout.FramesAtlasPath);
            AssetDatabase.ImportAsset(ProfileLayout.FramesAtlasPath, ImportAssetOptions.ForceUpdate);
            var importer = (UnityEditor.U2D.SpriteAtlasImporter)AssetImporter.GetAtPath(ProfileLayout.FramesAtlasPath);
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
            return ProfileLayout.FramesAtlasPath;
        }

        private static AddressableAssetGroup EnsureGroup(AddressableAssetSettings settings, string name)
        {
            var group = settings.FindGroup(name) ?? settings.CreateGroup(name, false, false, true, null,
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
            EditorUtility.SetDirty(group);
            return group;
        }
    }
}
