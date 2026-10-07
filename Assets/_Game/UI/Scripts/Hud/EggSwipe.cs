using UnityEngine;
using UnityEngine.EventSystems;

namespace PocketWilds.UI
{
    /// <summary>
    /// A horizontal swipe on the hatch button changes the egg (doc 02 Œufs, section 3). A swipe never hatches: once a drag
    /// starts, the button's click is not sent.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EggSwipe : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private const float MinPixels = 48f;

        [SerializeField] private EggChoiceBar _bar;

        private Vector2 _start;

        public void SetBar(EggChoiceBar bar) => _bar = bar;

        public void OnBeginDrag(PointerEventData eventData) => _start = eventData.position;

        public void OnDrag(PointerEventData eventData)
        {
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            var delta = eventData.position - _start;
            float min = MinPixels * Mathf.Max(1f, Screen.dpi / 160f) * 0.5f;
            if (_bar != null && Mathf.Abs(delta.x) > min && Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                _bar.Step(delta.x < 0f ? 1 : -1);
        }
    }
}
