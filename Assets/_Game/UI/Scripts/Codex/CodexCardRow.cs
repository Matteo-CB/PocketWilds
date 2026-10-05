using PocketWilds.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>A row of up to three species cards.</summary>
    [DisallowMultipleComponent]
    public sealed class CodexCardRow : MonoBehaviour
    {
        public const int Columns = 3;

        [SerializeField] private RectTransform _rect;
        [SerializeField] private CodexCardView[] _cards = new CodexCardView[Columns];

        public RectTransform Rect => _rect;
        public CodexCardView this[int column] => _cards[column];

        public void SetReferences(RectTransform rect, CodexCardView[] cards)
        {
            _rect = rect;
            _cards = cards;
        }
    }
}
