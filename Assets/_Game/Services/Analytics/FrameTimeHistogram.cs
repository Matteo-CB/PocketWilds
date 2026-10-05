#nullable enable
using System;

namespace PocketWilds.Services.Analytics
{
    /// <summary>
    /// Frame times in 0.5 ms buckets up to 100 ms (one overflow bucket above). Adding a frame is one increment, the
    /// 95th percentile is read from the buckets: no per-frame list, no sort, no allocation.
    /// </summary>
    public sealed class FrameTimeHistogram
    {
        public const float BucketMs = 0.5f;
        public const float SlowFrameMs = 33.4f;
        private const int Buckets = 200;

        private readonly int[] _counts = new int[Buckets + 1];

        public int Frames { get; private set; }
        public double SumMs { get; private set; }
        public float MaxMs { get; private set; }

        /// <summary>Frames longer than 33.4 ms (under 30 FPS).</summary>
        public int SlowFrames { get; private set; }

        public double AverageMs => Frames > 0 ? SumMs / Frames : 0.0;

        public void Add(float ms)
        {
            if (!(ms >= 0f))
                return;
            int bucket = (int)(ms / BucketMs);
            _counts[bucket < Buckets ? bucket : Buckets]++;
            Frames++;
            SumMs += ms;
            if (ms > MaxMs)
                MaxMs = ms;
            if (ms > SlowFrameMs)
                SlowFrames++;
        }

        /// <summary>Upper edge of the bucket holding the given percentile (0..1). The overflow bucket returns the max.</summary>
        public float Percentile(double p)
        {
            if (Frames == 0)
                return 0f;
            int rank = (int)Math.Ceiling(Frames * Math.Min(Math.Max(p, 0.0), 1.0));
            if (rank < 1)
                rank = 1;
            int seen = 0;
            for (int i = 0; i < Buckets; i++)
            {
                seen += _counts[i];
                if (seen >= rank)
                    return Math.Min((i + 1) * BucketMs, MaxMs);
            }
            return MaxMs;
        }

        public void Reset()
        {
            Array.Clear(_counts, 0, _counts.Length);
            Frames = 0;
            SumMs = 0.0;
            MaxMs = 0f;
            SlowFrames = 0;
        }
    }
}
