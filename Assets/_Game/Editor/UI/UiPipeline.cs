using System;
using System.IO;
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
        public const string HudPrefabPath = "Assets/_Game/Prefabs/UI/Screens/PF_UI_HUD_Main.prefab";
        public const string HatchConfigPath = "Assets/_Game/Content/Visuals/DEF_HatchPresentation.asset";
        public const string ScenePath = "Assets/_Game/Scenes/SC_VerticalSlice_Grassland.unity";
        // TEMP font (LiberationSans, OFL, shipped with TMP Essentials) until the UI typeface is chosen.
        public const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        public static readonly Vector2 ReferenceResolution = new Vector2(1080f, 2340f);

        [MenuItem("Pocket Wilds/UI/Build HUD (T17)", priority = 200)]
        public static void BuildAll()
        {
            ReimportSprites();
            var tokens = EnsureTokens();
            EnsureAtlas();
            BuildHudPrefab(tokens);
            AssetDatabase.SaveAssets();
            SetupScene();
            Debug.Log("[UiPipeline] HUD built and wired.");
        }

        public static void ReimportSprites()
        {
            foreach (var path in new[] { PillPath, CirclePath, LightIconPath, EggIconPath })
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

        public static SanctuaryHud BuildHudPrefab(UiColorTokens tokens)
        {
            var pill = AssetDatabase.LoadAssetAtPath<Sprite>(PillPath);
            var circle = AssetDatabase.LoadAssetAtPath<Sprite>(CirclePath);
            var lightIcon = AssetDatabase.LoadAssetAtPath<Sprite>(LightIconPath);
            var eggIcon = AssetDatabase.LoadAssetAtPath<Sprite>(EggIconPath);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (pill == null || circle == null || lightIcon == null || eggIcon == null || font == null)
                throw new InvalidOperationException("HUD sprites or font missing.");

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
                var hud = root.AddComponent<SanctuaryHud>();

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
                var value = AddText("Value", chip, font, 64f, FontStyles.Bold, tokens.textPrimary, "0");
                Place(value.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(132f, 14f), new Vector2(250f, 70f));
                var rate = AddText("Rate", chip, font, 34f, FontStyles.Bold, tokens.textSecondary, "+0/s");
                Place(rate.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(134f, -34f), new Vector2(250f, 40f));

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
                var cost = AddText("Value", costRow, font, 46f, FontStyles.Bold, tokens.textPrimary, "10");
                Place(cost.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(84f, 3f), new Vector2(132f, 60f));

                hud.SetReferences(tokens, safe, chip, value, rate, button, hatchRoot, buttonImage, egg, costRow.gameObject, cost);

                Directory.CreateDirectory(Path.GetDirectoryName(HudPrefabPath));
                return PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath).GetComponent<SanctuaryHud>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ---------------- Scene ----------------

        public static void SetupScene()
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path != ScenePath && !string.IsNullOrEmpty(active.path) && active.isDirty)
                throw new InvalidOperationException("Save or discard changes in " + active.path + " first.");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            VerticalSliceEntry entry = null;
            SanctuaryHud hud = null;
            EventSystem events = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                entry = entry ?? root.GetComponent<VerticalSliceEntry>();
                hud = hud ?? root.GetComponent<SanctuaryHud>();
                events = events ?? root.GetComponent<EventSystem>();
            }
            if (entry == null)
                throw new InvalidOperationException("SceneEntry (VerticalSliceEntry) missing in " + ScenePath);

            if (hud == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
                hud = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, scene)).GetComponent<SanctuaryHud>();
                hud.name = "HUD_Main";
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
            so.FindProperty("_hatchConfig").objectReferenceValue = AssetDatabase.LoadAssetAtPath<HatchPresentationConfig>(HatchConfigPath);
            so.FindProperty("_eggId").stringValue = "egg.grassland.basic";
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // ---------------- Helpers ----------------

        private static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static Image AddImage(string name, Transform parent, Sprite sprite, Color color, Image.Type type)
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

        private static TextMeshProUGUI AddText(string name, Transform parent, TMP_FontAsset font, float size, FontStyles style, Color color, string text)
        {
            var label = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSharedMaterial = font.material;
            label.fontSize = size;
            label.fontStyle = style;
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
