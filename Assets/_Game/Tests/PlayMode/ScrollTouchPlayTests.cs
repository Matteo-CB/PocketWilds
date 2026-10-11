using System.Collections;
using System.IO;
using System.Text;
using NUnit.Framework;
using PocketWilds.Bootstrap;
using PocketWilds.Content;
using PocketWilds.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace PocketWilds.Tests.PlayMode
{
    /// <summary>
    /// Owner report (2026-10-11, phone): « le scroll des menus est buggé ». A finger drags the menus' lists through the
    /// real input path (a virtual touchscreen, the UI input module, the ScrollRects) and the content must follow it.
    /// </summary>
    public sealed class ScrollTouchPlayTests
    {
        private string _saveDir;
        private Touchscreen _touch;

        [SetUp]
        public void SetUp()
        {
            _saveDir = Path.Combine(Application.temporaryCachePath, "ScrollTouchTests");
            if (Directory.Exists(_saveDir))
                Directory.Delete(_saveDir, true);
        }

        [TearDown]
        public void TearDown()
        {
            if (_touch != null)
                InputSystem.RemoveDevice(_touch);
            _touch = null;
            foreach (var b in Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None))
                Object.Destroy(b.gameObject);
        }

        [UnityTest]
        public IEnumerator Menus_ListsFollowTheFinger()
        {
#if UNITY_EDITOR
            var database = UnityEditor.AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>("Assets/_Game/Content/DEF_ContentDatabase.asset");
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>("Assets/_Game/Content/Visuals/DEF_AnimalVisualCatalog.asset");
            GameBootstrap.PendingOptions = new BootOptions
            {
                SaveDirectory = _saveDir,
                ContentOverride = database.CreateBuilder().Build(),
                VisualCatalogOverride = catalog,
                LocalizationOverride = database.localization.ToArray(),
                LoadTargetScene = true,
            };
            var bootstrap = new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
            float timeout = Time.realtimeSinceStartup + 120f;
            while (bootstrap.State == BootState.NotStarted || bootstrap.State == BootState.Running)
            {
                Assert.Less(Time.realtimeSinceStartup, timeout, "Boot timed out.");
                yield return null;
            }
            yield return null;
            var refuge = Object.FindAnyObjectByType<VerticalSliceEntry>().Refuge;
            _touch = InputSystem.AddDevice<Touchscreen>();
            var log = new StringBuilder();

            refuge.OpenCodex();
            for (int i = 0; i < 30; i++)
                yield return null;
            yield return Drag(FindScroll("Viewport"), log, "codex");
            log.Append("refuge modal ").Append(refuge.ModalOpen).Append('\n');
            File.WriteAllText(Path.Combine(Application.temporaryCachePath, "scroll_touch.txt"), log.ToString());
            Debug.Log("[ScrollTouch] " + log);
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
        }

        private static ScrollRect FindScroll(string name)
        {
            foreach (var s in Object.FindObjectsByType<ScrollRect>(FindObjectsSortMode.None))
                if (s.isActiveAndEnabled && s.name == name)
                    return s;
            return null;
        }

        private IEnumerator Drag(ScrollRect scroll, StringBuilder log, string label)
        {
            Assert.IsNotNull(scroll, label + ": no active list");
            var viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
            var canvas = scroll.GetComponentInParent<Canvas>().rootCanvas;
            var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 start = RectTransformUtility.WorldToScreenPoint(cam, viewport.TransformPoint(viewport.rect.center));
            float y0 = scroll.content.anchoredPosition.y;
            log.Append(label).Append(": screen ").Append(Screen.width).Append('x').Append(Screen.height)
               .Append(" start ").Append(start).Append(" content ").Append(scroll.content.rect.height.ToString("F0"))
               .Append(" viewport ").Append(viewport.rect.height.ToString("F0")).Append(" y0 ").Append(y0.ToString("F1")).Append('\n');
            Touch(TouchPhase.Began, start);
            yield return null;
            var p = start;
            for (int i = 1; i <= 20; i++)
            {
                p = start + new Vector2(0f, i * 18f);
                Touch(TouchPhase.Moved, p);
                yield return null;
                log.Append("  move ").Append(i).Append(" finger +").Append((i * 18).ToString()).Append(" content ")
                   .Append((scroll.content.anchoredPosition.y - y0).ToString("F1")).Append('\n');
            }
            float moved = scroll.content.anchoredPosition.y - y0;
            Assert.Greater(moved, 60f, label + ": the list follows the finger");
            Touch(TouchPhase.Ended, p);
            for (int i = 0; i < 20; i++)
            {
                yield return null;
                if (i % 4 == 3 && log != null)
                    log.Append("  after ").Append(i).Append(" content ").Append((scroll.content.anchoredPosition.y - y0).ToString("F1"))
                       .Append(" velocity ").Append(scroll.velocity.y.ToString("F0")).Append('\n');
            }
        }

        private void Touch(TouchPhase phase, Vector2 position)
        {
            InputSystem.QueueStateEvent(_touch, new TouchState
            {
                touchId = 1,
                phase = phase,
                position = position,
                pressure = 1f,
            });
        }
    }
}
