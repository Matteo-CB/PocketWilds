using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// Owner rule (2026-10-11): a text never touches the edges of its background, in any language. Every text inside a
    /// primary button keeps a clear margin from the button's visible pill: UI_Button_Primary_9Slice draws 6 px of clear
    /// space above the pill and 35 px of shadow under it (168 px of borders, scaled down on short buttons and by the
    /// image's pixels per unit multiplier). Labels are single lines that shrink to fit (the pipelines' fit gates), so the
    /// line's height at its set size holds for every language.
    /// </summary>
    public sealed class ButtonTextMarginTests
    {
        private const string PrimarySprite = "UI_Button_Primary_9Slice";
        private const float MinMargin = 8f;

        [Test]
        public void EveryPrimaryButton_KeepsItsTextsInsideThePill()
        {
            var problems = new List<string>();
            int texts = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game/Prefabs" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var image in root.GetComponentsInChildren<Image>(true))
                    {
                        if (image.sprite == null || image.sprite.name != PrimarySprite)
                            continue;
                        var button = (RectTransform)image.transform;
                        var r = button.rect;
                        float s = Mathf.Min(1f / Mathf.Max(0.01f, image.pixelsPerUnitMultiplier), r.height / 168f);
                        float top = r.yMax - 6f * s, bottom = r.yMin + 35f * s, left = r.xMin + 8f * s, right = r.xMax - 8f * s;
                        foreach (var text in image.GetComponentsInChildren<TMP_Text>(true))
                        {
                            var box = text.rectTransform.rect;
                            var size = text.GetPreferredValues(text.text, box.width, 0f);
                            if (size.y <= 0.01f)
                                continue;
                            texts++;
                            float w = Mathf.Min(size.x, box.width), h = size.y;
                            var align = text.alignment.ToString();
                            float cy = align.StartsWith("Top") ? box.yMax - h * 0.5f : align.StartsWith("Bottom") ? box.yMin + h * 0.5f : box.center.y;
                            float cx = align.EndsWith("Left") ? box.xMin + w * 0.5f : align.EndsWith("Right") ? box.xMax - w * 0.5f : box.center.x;
                            // Local transforms up to the button: screens start hidden at scale 0, so world space says nothing.
                            var m = Matrix4x4.identity;
                            for (var t = text.transform; t != null && t != button; t = t.parent)
                                m = Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale) * m;
                            var lo = m.MultiplyPoint3x4(new Vector3(cx - w * 0.5f, cy - h * 0.5f, 0f));
                            var hi = m.MultiplyPoint3x4(new Vector3(cx + w * 0.5f, cy + h * 0.5f, 0f));
                            float margin = Mathf.Min(Mathf.Min(top - hi.y, lo.y - bottom), Mathf.Min(lo.x - left, right - hi.x));
                            if (margin < MinMargin)
                                problems.Add($"{root.name}/{AnimationUtility.CalculateTransformPath(text.transform, root.transform)}: " +
                                             $"{margin:F0} from the pill's edge (top {top - hi.y:F0}, bottom {lo.y - bottom:F0}, " +
                                             $"left {lo.x - left:F0}, right {right - hi.x:F0})");
                        }
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            Assert.Greater(texts, 40, "The primary buttons' texts were found.");
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }
    }
}
