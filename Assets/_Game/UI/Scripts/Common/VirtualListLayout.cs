using System;

namespace PocketWilds.UI
{
    /// <summary>
    /// Row geometry of a virtualized vertical list: prefix offsets of variable-height rows and the visible
    /// range for a scroll position (binary search). Pure and allocation free after Resize, so a list of
    /// thousands of rows only ever binds the handful on screen (UI doc: scroll lists virtualized).
    /// </summary>
    public sealed class VirtualListLayout
    {
        private float[] _offsets = Array.Empty<float>(); // top of each row; _offsets[count] = content height
        private int _count;

        public int Count => _count;
        public float ContentHeight => _count == 0 ? 0f : _offsets[_count];

        /// <summary>Starts a new layout of <paramref name="count"/> rows. Grows the buffer only when needed.</summary>
        public void Begin(int count)
        {
            if (_offsets.Length < count + 1)
                _offsets = new float[Math.Max(count + 1, _offsets.Length * 2)];
            _count = count;
            _offsets[0] = 0f;
        }

        /// <summary>Rows must be set in order, 0 to Count - 1.</summary>
        public void SetHeight(int row, float height) => _offsets[row + 1] = _offsets[row] + height;

        public float Top(int row) => _offsets[row];
        public float Height(int row) => _offsets[row + 1] - _offsets[row];

        /// <summary>
        /// Rows intersecting [scrollTop, scrollTop + viewport], extended by <paramref name="overscan"/> rows on
        /// each side. Returns false when the list is empty.
        /// </summary>
        public bool GetVisibleRange(float scrollTop, float viewport, int overscan, out int first, out int last)
        {
            first = 0;
            last = -1;
            if (_count == 0)
                return false;
            float top = Math.Max(0f, scrollTop);
            float bottom = top + Math.Max(0f, viewport);
            first = Math.Max(0, RowAt(top) - overscan);
            last = Math.Min(_count - 1, RowAt(bottom) + overscan);
            return true;
        }

        /// <summary>Index of the row containing <paramref name="y"/> (clamped).</summary>
        public int RowAt(float y)
        {
            int lo = 0, hi = _count - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) >> 1;
                if (_offsets[mid] <= y)
                    lo = mid;
                else
                    hi = mid - 1;
            }
            return lo;
        }
    }
}
