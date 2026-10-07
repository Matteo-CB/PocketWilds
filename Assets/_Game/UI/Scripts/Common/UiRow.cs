using TMPro;
using UnityEngine;

namespace PocketWilds.UI
{
    /// <summary>
    /// T35A: an icon and a label centered together on their row ("[gem] +10 Gems", "[light] 1.2K"), from the label's
    /// preferred width. Called when the text changes (opening a window), never per frame; no layout group.
    /// </summary>
    public static class UiRow
    {
        public static void Center(RectTransform icon, TMP_Text label, float gap)
        {
            if (icon == null || label == null)
                return;
            float iconWidth = icon.sizeDelta.x;
            float textWidth = Mathf.Min(label.GetPreferredValues(label.text).x, label.rectTransform.sizeDelta.x);
            float left = -(iconWidth + gap + textWidth) * 0.5f;
            Pin(icon, left);
            Pin(label.rectTransform, left + iconWidth + gap);
        }

        private static void Pin(RectTransform rect, float x)
        {
            rect.anchorMin = new Vector2(0.5f, rect.anchorMin.y);
            rect.anchorMax = new Vector2(0.5f, rect.anchorMax.y);
            rect.pivot = new Vector2(0f, rect.pivot.y);
            rect.anchoredPosition = new Vector2(x, rect.anchoredPosition.y);
        }
    }
}
