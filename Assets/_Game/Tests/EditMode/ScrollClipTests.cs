using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// Owner report (2026-10-11, phone): on the profile and the Codex, scrolled cells spilled over the screen's header.
    /// The UI pipelines make graphics non-maskable to save work, so every graphic inside a scroll list's viewport must be
    /// maskable again, or its RectMask2D clips nothing.
    /// </summary>
    public sealed class ScrollClipTests
    {
        [Test]
        public void EveryScrollList_ClipsItsCells()
        {
            var problems = new List<string>();
            int lists = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game/Prefabs" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (var scroll in prefab.GetComponentsInChildren<ScrollRect>(true))
                {
                    lists++;
                    var viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
                    if (viewport.GetComponent<RectMask2D>() == null && viewport.GetComponent<Mask>() == null)
                        problems.Add($"{prefab.name}/{scroll.name}: no mask on its viewport");
                    int loose = 0;
                    foreach (var graphic in viewport.GetComponentsInChildren<MaskableGraphic>(true))
                        if (!graphic.maskable)
                            loose++;
                    if (loose > 0)
                        problems.Add($"{prefab.name}/{scroll.name}: {loose} graphics would draw outside the list");
                }
            }
            Assert.Greater(lists, 10, "The menus' scroll lists were found.");
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }
    }
}
