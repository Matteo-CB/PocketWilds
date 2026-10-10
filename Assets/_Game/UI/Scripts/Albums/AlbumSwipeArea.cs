using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PocketWilds.UI
{
    /// <summary>
    /// T44A: the page's swipe (section 1.12: one page at a time in portrait, a horizontal swipe between pages). The page
    /// follows the finger; on release past <see cref="PocketWilds.Presentation.AlbumLayout.SwipeThreshold"/> the view turns
    /// the page, else it springs back. The slots' buttons are children: their drags bubble up here (a drag is never a tap).
    /// </summary>
    public sealed class AlbumSwipeArea : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private Canvas _root;

        /// <summary>The finger's horizontal travel since the drag began (canvas units).</summary>
        public float Offset { get; private set; }

        public bool Dragging { get; private set; }

        /// <summary>The drag ended with this travel (canvas units).</summary>
        public Action<float> Released;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_root == null)
            {
                var canvas = GetComponentInParent<Canvas>();
                _root = canvas != null ? canvas.rootCanvas : null;
            }
            Dragging = true;
            Offset = 0f;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!Dragging)
                return;
            float scale = _root != null && _root.scaleFactor > 0.0001f ? _root.scaleFactor : 1f;
            Offset += eventData.delta.x / scale;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!Dragging)
                return;
            Dragging = false;
            float offset = Offset;
            Offset = 0f;
            Released?.Invoke(offset);
        }

        /// <summary>Tests: a swipe of <paramref name="travel"/> canvas units.</summary>
        public void Simulate(float travel)
        {
            Dragging = false;
            Offset = 0f;
            Released?.Invoke(travel);
        }

        private void OnDisable()
        {
            Dragging = false;
            Offset = 0f;
        }
    }
}
