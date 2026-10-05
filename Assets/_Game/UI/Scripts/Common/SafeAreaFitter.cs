using UnityEngine;

namespace PocketWilds.UI
{
    /// <summary>
    /// Keeps a full-screen RectTransform inside Screen.safeArea (notches, rounded corners, gesture bars).
    /// Plain helper ticked by its screen: touches the RectTransform only when the safe area changes.
    /// </summary>
    public sealed class SafeAreaFitter
    {
        private Rect _applied;
        private Vector2Int _screen;

        public void Fit(RectTransform target)
        {
            if (target == null)
                return;
            var screen = new Vector2Int(Screen.width, Screen.height);
            var safe = Screen.safeArea;
            if (safe == _applied && screen == _screen)
                return;
            _applied = safe;
            _screen = screen;
            if (screen.x <= 0 || screen.y <= 0)
                return;
            target.anchorMin = new Vector2(safe.xMin / screen.x, safe.yMin / screen.y);
            target.anchorMax = new Vector2(safe.xMax / screen.x, safe.yMax / screen.y);
            target.offsetMin = Vector2.zero;
            target.offsetMax = Vector2.zero;
        }
    }
}
