using PocketWilds.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>Biome header row of the Codex list ("Meadow 1/6"). Pooled, bound on demand.</summary>
    [DisallowMultipleComponent]
    public sealed class CodexHeaderView : MonoBehaviour
    {
        [SerializeField] private RectTransform _rect;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _count;

        private readonly UiNumberText _numbers = new UiNumberText();

        public RectTransform Rect => _rect;

        public void Bind(string title, int found, int total)
        {
            _title.text = title;
            _numbers.SetRatio(_count, found, total);
        }

        public void SetReferences(RectTransform rect, TMP_Text title, TMP_Text count)
        {
            _rect = rect;
            _title = title;
            _count = count;
        }
    }
}
