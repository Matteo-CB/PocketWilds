using System.Linq;
using NUnit.Framework;
using PocketWilds.Core;
using PocketWilds.Editor.UI;
using PocketWilds.Presentation;
using PocketWilds.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools.Constraints;
using UnityEngine.UI;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>T17: PF_UI_HUD_Main follows the UI performance rules and is wired into the vertical slice.</summary>
    public class HudTests
    {
        private static GameObject LoadHud() => AssetDatabase.LoadAssetAtPath<GameObject>(UiPipeline.HudPrefabPath);

        [Test]
        public void HudPrefab_SplitsCanvases_AndOnlyButtonsCatchTouches()
        {
            var prefab = LoadHud();
            Assert.IsNotNull(prefab, "Run Pocket Wilds/UI/Build HUD (T17).");
            var root = prefab.GetComponent<Canvas>();
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, root.renderMode);
            var scaler = prefab.GetComponent<CanvasScaler>();
            Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode);
            Assert.AreEqual(UiPipeline.ReferenceResolution, scaler.referenceResolution);
            Assert.IsNull(prefab.GetComponent<GraphicRaycaster>(), "The root canvas holds no interactive element.");

            var hud = prefab.GetComponent<RefugeHud>();
            Assert.IsNotNull(hud);
            var chip = prefab.transform.Find("SafeArea/LightChip");
            var action = prefab.transform.Find("SafeArea/HatchAction");
            Assert.IsNotNull(chip.GetComponent<Canvas>(), "The Light counter must rebuild its own canvas only.");
            Assert.IsNotNull(action.GetComponent<Canvas>());
            Assert.IsNull(chip.GetComponent<GraphicRaycaster>());
            Assert.IsNotNull(action.GetComponent<GraphicRaycaster>());

            var graphics = prefab.GetComponentsInChildren<Graphic>(true);
            var targets = graphics.Where(g => g.raycastTarget).ToArray();
            Assert.AreEqual(4, targets.Length, "Only the hatch button, the egg odds button (T34A) and the Codex and shop shortcuts catch touches.");
            Assert.IsTrue(targets.All(t => t.GetComponent<Button>() != null));
            var shortcuts = prefab.transform.Find("SafeArea/Shortcuts");
            Assert.IsNotNull(shortcuts.GetComponent<Canvas>(), "Shortcuts rebuild on their own canvas.");
            Assert.IsNotNull(shortcuts.GetComponent<GraphicRaycaster>());
            Assert.IsFalse(shortcuts.Find("Codex/Badge").gameObject.activeSelf, "No badge until something new is found.");
            Assert.IsEmpty(prefab.GetComponentsInChildren<LayoutGroup>(true), "No layout recalculation on the HUD.");
            Assert.IsEmpty(prefab.GetComponentsInChildren<ContentSizeFitter>(true));
            Assert.IsEmpty(prefab.GetComponentsInChildren<Animator>(true));
            foreach (var label in prefab.GetComponentsInChildren<TMP_Text>(true))
                Assert.IsFalse(label.richText, label.name + ": numbers only, no rich text parsing.");
        }

        [Test]
        public void HudSprites_AreAtlasedUiSprites_WithoutMipmaps()
        {
            foreach (var path in new[] { UiPipeline.PillPath, UiPipeline.CirclePath, UiPipeline.LightIconPath, UiPipeline.EggIconPath })
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.IsNotNull(importer, path);
                Assert.AreEqual(TextureImporterType.Sprite, importer.textureType, path);
                Assert.IsFalse(importer.mipmapEnabled, path);
                Assert.AreEqual(TextureWrapMode.Clamp, importer.wrapMode, path);
            }
            var pill = AssetDatabase.LoadAssetAtPath<Sprite>(UiPipeline.PillPath);
            Assert.AreEqual(new Vector4(64f, 70f, 64f, 62f), pill.border, "9-slice borders follow pw_ui_sprites.py.");
            Assert.IsNotNull(AssetDatabase.LoadMainAssetAtPath(UiPipeline.AtlasPath), "Atlas_CommonHUD");
            Assert.AreNotEqual(SpritePackerMode.Disabled, EditorSettings.spritePackerMode);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<UiColorTokens>(UiPipeline.TokensPath));
        }

        [Test]
        public void VerticalSlice_HasEventSystem_AndEntryWiredToHud()
        {
            var scene = EditorSceneManager.OpenScene(UiPipeline.ScenePath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var events = roots.Select(r => r.GetComponent<EventSystem>()).FirstOrDefault(e => e != null);
                Assert.IsNotNull(events, "Buttons need an EventSystem.");
                Assert.IsNotNull(events.GetComponent<InputSystemUIInputModule>(), "Input System only.");
                var entry = roots.Select(r => r.GetComponent<VerticalSliceEntry>()).First(e => e != null);
                var so = new SerializedObject(entry);
                Assert.IsInstanceOf<RefugeHud>(so.FindProperty("_hud").objectReferenceValue);
                Assert.IsNotNull(so.FindProperty("_hatchConfig").objectReferenceValue);
                Assert.AreEqual("egg.grassland.basic", so.FindProperty("_eggId").stringValue);
                Assert.IsInstanceOf<OfflineReturnScreen>(so.FindProperty("_offlineReturn").objectReferenceValue);
                Assert.IsInstanceOf<CodexScreen>(so.FindProperty("_codex").objectReferenceValue);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void OfflineReturnPrefab_IsAClosedModal_AboveTheHud()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiPipeline.OfflineReturnPrefabPath);
            Assert.IsNotNull(prefab, "Run Pocket Wilds/UI/Build HUD and Screens.");
            var canvas = prefab.GetComponent<Canvas>();
            Assert.Greater(canvas.sortingOrder, LoadHud().GetComponent<Canvas>().sortingOrder);
            Assert.IsFalse(canvas.enabled, "A closed screen draws nothing.");
            Assert.IsNotNull(prefab.GetComponent<GraphicRaycaster>());
            Assert.IsNotNull(prefab.GetComponent<OfflineReturnScreen>());
            var content = prefab.transform.Find("Content");
            Assert.IsFalse(content.gameObject.activeSelf);

            var targets = prefab.GetComponentsInChildren<Graphic>(true).Where(g => g.raycastTarget).ToArray();
            Assert.AreEqual(2, targets.Length, "Scrim (blocks the world and the HUD) and Collect.");
            Assert.IsTrue(targets.Any(g => g.name == "Scrim"));
            Assert.IsTrue(targets.Any(g => g.GetComponent<Button>() != null));
            Assert.IsEmpty(prefab.GetComponentsInChildren<LayoutGroup>(true));
            Assert.IsEmpty(prefab.GetComponentsInChildren<Animator>(true));
        }

        [Test]
        public void BigValueText_DoesNotAllocate_InSteadyState()
        {
            var go = new GameObject("Label", typeof(RectTransform));
            try
            {
                var label = go.AddComponent<TextMeshProUGUI>();
                var text = new BigValueText(label, "+", "/s");
                var values = new BigValue[64];
                for (int i = 0; i < values.Length; i++)
                    values[i] = BigValue.FromDouble(i * 137.25 + 0.5);
                for (int i = 0; i < values.Length; i++)
                    text.Set(values[i]);

                var same = BigValue.FromDouble(4250);
                Assert.IsTrue(text.Set(same));
                Assert.AreEqual("+4,250/s", label.text);
                Assert.IsFalse(text.Set(same), "Same text: the label is not touched.");
                // Unchanged value (most frames): nothing reaches TMP, nothing allocates.
                Assert.That(() =>
                {
                    for (int i = 0; i < 1000; i++)
                        text.Set(same);
                }, Is.Not.AllocatingGCMemory());

                // Changing values: our formatting is allocation free. TMP's own Editor-only inspector
                // mirror (TMP_Text.SetCharArray, #if UNITY_EDITOR) is outside this measurement.
                var buffer = new BigValueTextBuffer("+", "/s");
                for (int i = 0; i < values.Length; i++)
                    buffer.Update(values[i]);
                Assert.That(() =>
                {
                    for (int k = 0; k < 100; k++)
                        for (int i = 0; i < values.Length; i++)
                            buffer.Update(values[i]);
                }, Is.Not.AllocatingGCMemory());
                Assert.IsTrue(buffer.Update(same));
                Assert.AreEqual("+4,250/s", new string(buffer.Characters, 0, buffer.Length));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
