using System;

namespace PocketWilds.Core
{
    /// <summary>
    /// How far a finger may move before a touch counts as a drag, in screen pixels (owner report 2026-10-11: on a 440 dpi
    /// phone a fixed 12 px, 0.7 mm, turned the finger's tremor into drags that cancelled taps and started scrolls). Android's
    /// own touch slop: 8 dp, a dp being 1/160 inch.
    /// </summary>
    public static class TouchSlop
    {
        public const float SlopDp = 8f;
        public const int MinPixels = 12;

        /// <summary>The drag threshold for a screen density (dpi 0 or unknown: the minimum).</summary>
        public static int Pixels(float dpi) => dpi <= 0f ? MinPixels : Math.Max(MinPixels, (int)Math.Round(dpi / 160f * SlopDp));
    }
}
