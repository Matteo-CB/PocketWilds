using System;
using System.IO;
using PocketWilds.Editor.UI;
using PocketWilds.Presentation;
using PocketWilds.Simulation;
using PocketWilds.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.World
{
    /// <summary>
    /// T36 wish bubble in the world (doc 06 screen 002; doc 01 A08 to A10; plan section 7.1): builds PF_World_WishBubble
    /// (WishBubbleView: UI_Bubble_Wish, UI_Bubble_Ring, the wish's icon, the count in Pocket Round Bold), its material
    /// MAT_World_WishBubble (SH_WishBubble, unlit, alpha), and points VerticalSliceEntry._wishBubblePrefab at it in the
    /// vertical slice. Sprites come from SourceArt/UI/Tools/pw_ui_sprites_t36.py (T36 sprites review). Idempotent.
    /// </summary>
    public static class WishBubblePipeline
    {
        public const string PrefabPath = "Assets/_Game/Prefabs/World/PF_World_WishBubble.prefab";
        public const string ShaderName = "PocketWilds/WishBubble";
        public const string MaterialPath = "Assets/_Game/Art/Materials/MAT_World_WishBubble.mat";
        public const string BubbleSprite = "UI_Bubble_Wish";
        public const string RingSprite = "UI_Bubble_Ring";

        /// <summary>Drawn after the world's other transparent things (particles, water): the bubble, its ring, its icon, its count.</summary>
        public const int BubbleOrder = 10;
        public const int RingOrder = 11;
        public const int IconOrder = 12;
        public const int CountOrder = 13;

        /// <summary>The count's text size (TextMeshPro 3D: about 0.1 unit per point; sprite units, 100 px each).</summary>
        public const float CountFontSize = 6f;
        public static readonly Vector3 CountPosition = new Vector3(0.27f, -0.01f, 0f);

        /// <summary>Sky blue while the ring fills, the success green when the wish comes true (UI tokens).</summary>
        private static readonly Color RingSky = UiColorTokens.Hex(0x7EC2EE);

        [MenuItem("Pocket Wilds/World/Build Wish Bubble (T36)", priority = 208)]
        public static void BuildMenu() => Debug.Log("[WishBubblePipeline] " + BuildAll());

        /// <summary>The material, the prefab, and the scene's reference to it.</summary>
        public static string BuildAll()
        {
            var prefab = BuildPrefab();
            WireScene(prefab);
            return $"{PrefabPath} built and wired in {UiPipeline.ScenePath}";
        }

        private static Sprite LoadSprite(string folder, string name)
        {
            string path = folder + "/" + name + ".png";
            if (!File.Exists(path))
                throw new FileNotFoundException("Run SourceArt/UI/Tools/pw_ui_sprites_t36.py (or pw_ui_sprites.py) first.", path);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            return sprite != null ? sprite : throw new InvalidOperationException(path + " is not imported as a sprite (TextureImportRules).");
        }

        public static Material EnsureMaterial()
        {
            var shader = Shader.Find(ShaderName) ?? throw new InvalidOperationException("Shader not found: " + ShaderName + " (Assets/_Game/Art/Shaders/SH_WishBubble.shader).");
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
                material = new Material(shader) { name = "MAT_World_WishBubble" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.shader = shader;
            material.SetFloat("_Fill", 1f);
            material.enableInstancing = false;
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>The icons by WishSystem value, from WishIcons (the card and the Today rows use the same names).</summary>
        public static Sprite[] SystemIcons()
        {
            int max = 0;
            foreach (WishSystem value in Enum.GetValues(typeof(WishSystem)))
                max = Math.Max(max, (int)value);
            var icons = new Sprite[max + 1];
            for (int i = 0; i < icons.Length; i++)
                icons[i] = LoadSprite(UiPipeline.IconFolder, WishIcons.SpriteName((WishSystem)i));
            return icons;
        }

        public static GameObject BuildPrefab()
        {
            var material = EnsureMaterial();
            var tokens = UiPipeline.EnsureTokens();
            var bubbleSprite = LoadSprite(UiPipeline.KitFolder, BubbleSprite);
            var ringSprite = LoadSprite(UiPipeline.KitFolder, RingSprite);
            var icons = SystemIcons();
            var doneIcon = LoadSprite(UiPipeline.IconFolder, WishIcons.Done);
            if (!UiPipeline.Styles.TryGet(TextRole.NumberSmall, out var numberStyle) || numberStyle.font == null)
                throw new InvalidOperationException("DEF_TextStyles has no NumberSmall font: run Pocket Wilds/UI/Fonts/Build Fonts and Styles.");

            var root = new GameObject("PF_World_WishBubble");
            try
            {
                float rest = WishBubblePlacement.NominalRadius / WishBubblePlacement.SpriteRadius;
                root.transform.localScale = new Vector3(rest, rest, rest);
                var view = root.AddComponent<WishBubbleView>();
                var body = new GameObject("Body").transform;
                body.SetParent(root.transform, false);

                var bubble = AddSprite("Bubble", body, bubbleSprite, material, BubbleOrder);
                var ring = AddSprite("Ring", body, ringSprite, material, RingOrder);
                var icon = AddSprite("Icon", body, icons[(int)WishSystem.Hatching], material, IconOrder);

                var countGo = new GameObject("Count", typeof(RectTransform));
                countGo.transform.SetParent(body, false);
                var count = countGo.AddComponent<TextMeshPro>();
                count.font = numberStyle.font;
                count.fontSharedMaterial = numberStyle.font.material;
                count.fontSize = CountFontSize;
                count.enableAutoSizing = false;
                count.alignment = TextAlignmentOptions.Center;
                count.textWrappingMode = TextWrappingModes.NoWrap;
                count.overflowMode = TextOverflowModes.Overflow;
                count.richText = false;
                count.parseCtrlCharacters = false;
                count.isOrthographic = false;
                count.color = tokens.textPrimary;
                count.text = "2";
                count.sortingOrder = CountOrder;
                count.rectTransform.sizeDelta = new Vector2(0.9f, 0.7f);
                count.rectTransform.localPosition = CountPosition;
                var countRenderer = countGo.GetComponent<MeshRenderer>();
                countRenderer.sortingOrder = CountOrder;
                countRenderer.shadowCastingMode = ShadowCastingMode.Off;
                countRenderer.receiveShadows = false;
                countRenderer.lightProbeUsage = LightProbeUsage.Off;
                countRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

                var bubbleTint = tokens.panelPrimary;
                bubbleTint.a = 1f;
                view.SetReferences(body, bubble, ring, icon, countGo.transform, countRenderer, icons, doneIcon, bubbleTint, RingSky,
                    Color.Lerp(RingSky, tokens.success, 0.85f), tokens.success);

                // The count's text goes through a persistent call to the label's text setter (WishBubbleView: the Presentation
                // assembly does not reference TextMeshPro).
                var setter = typeof(TMP_Text).GetProperty(nameof(TMP_Text.text))?.GetSetMethod()
                             ?? throw new InvalidOperationException("TMP_Text.text has no public setter.");
                var call = (UnityAction<string>)Delegate.CreateDelegate(typeof(UnityAction<string>), count, setter);
                var countEvent = view.CountTextEvent;
                while (countEvent.GetPersistentEventCount() > 0)
                    UnityEventTools.RemovePersistentListener(countEvent, 0);
                UnityEventTools.AddPersistentListener(countEvent, call);

                Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static SpriteRenderer AddSprite(string name, Transform parent, Sprite sprite, Material material, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.sortingOrder = order;
            renderer.drawMode = SpriteDrawMode.Simple;
            renderer.maskInteraction = SpriteMaskInteraction.None;
            renderer.spriteSortPoint = SpriteSortPoint.Center;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.allowOcclusionWhenDynamic = false;
            return renderer;
        }

        /// <summary>VerticalSliceEntry._wishBubblePrefab in the vertical slice points at the prefab (the scene is saved).</summary>
        public static void WireScene(GameObject prefab)
        {
            var scene = EditorSceneManager.OpenScene(UiPipeline.ScenePath, OpenSceneMode.Single);
            var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
            if (entry == null)
                throw new InvalidOperationException("SceneEntry (VerticalSliceEntry) missing in " + UiPipeline.ScenePath);
            var serialized = new SerializedObject(entry);
            var property = serialized.FindProperty("_wishBubblePrefab")
                           ?? throw new InvalidOperationException("VerticalSliceEntry has no _wishBubblePrefab field.");
            property.objectReferenceValue = prefab;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(entry);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
