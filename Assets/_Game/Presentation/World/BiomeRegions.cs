using System;
using System.Collections.Generic;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T50 the biomes of the world strip by x (frozen decision 1: one continuous strip along +x, the Grassland from 0, the
    /// Forest from x = 105). Each biome after the first starts at the west edge of its westmost chunk. Around each start the
    /// atmosphere, weather, ambience and audio blend over <see cref="BlendHalfWidth"/> on each side (smoothstep, no pop);
    /// the camera's biome (hatch weights, biome visits, audio beds) switches at the start with a dead band of
    /// 2 x <see cref="CameraHysteresis"/>. Pure math on a few floats: no allocation after construction.
    /// </summary>
    public sealed class BiomeRegions
    {
        /// <summary>The blend runs from start - 5 m to start + 5 m (Forest: x 100 to 110).</summary>
        public const float BlendHalfWidth = 5f;

        /// <summary>The camera's biome changes 0.5 m past a start, and changes back 0.5 m before it (a 1 m band).</summary>
        public const float CameraHysteresis = 0.5f;

        private readonly float[] _starts;

        /// <param name="starts">World x where each biome starts, in world order; the first entry is ignored (the first biome
        /// reaches west forever). A start west of the previous one is moved onto it.</param>
        public BiomeRegions(IReadOnlyList<float> starts)
        {
            int count = starts != null && starts.Count > 0 ? starts.Count : 1;
            _starts = new float[count];
            _starts[0] = float.NegativeInfinity;
            for (int i = 1; i < count; i++)
                _starts[i] = Mathf.Max(starts[i], _starts[i - 1]);
        }

        /// <summary>The regions of chunk sets laid along +x (the world's sets in streamer order): each set after the first
        /// starts at its westmost chunk's west edge. A null or empty set starts where the previous one did.</summary>
        public static BiomeRegions FromChunkSets(IReadOnlyList<WorldChunkSet> sets)
        {
            int count = sets != null && sets.Count > 0 ? sets.Count : 1;
            var starts = new float[count];
            starts[0] = float.NegativeInfinity;
            for (int i = 1; i < count; i++)
                starts[i] = WestEdge(sets[i], starts[i - 1]);
            return new BiomeRegions(starts);
        }

        /// <summary>West edge of a chunk set (westmost origin minus the half size); <paramref name="fallback"/> when empty.</summary>
        public static float WestEdge(WorldChunkSet set, float fallback)
        {
            if (set == null || set.Count == 0)
                return fallback;
            float min = float.PositiveInfinity;
            for (int i = 0; i < set.Count; i++)
                min = Mathf.Min(min, set.Get(i).origin.x);
            return min - set.HalfSize;
        }

        public int Count => _starts.Length;

        /// <summary>World x where a biome starts (negative infinity for the first).</summary>
        public float StartOf(int biome) => biome > 0 && biome < _starts.Length ? _starts[biome] : float.NegativeInfinity;

        /// <summary>The biome at x, without blend or hysteresis.</summary>
        public int RegionAt(float x)
        {
            int region = 0;
            for (int i = 1; i < _starts.Length && x >= _starts[i]; i++)
                region = i;
            return region;
        }

        /// <summary>
        /// The two biomes blended at x and the share of the second (0 to 1, smoothstep across the band). Outside every band
        /// both are the biome at x and the share is 0.
        /// </summary>
        public void Resolve(float x, out int from, out int to, out float t)
        {
            int region = 0;
            for (int i = 1; i < _starts.Length; i++)
            {
                float start = _starts[i];
                if (x < start - BlendHalfWidth)
                    break;
                if (x <= start + BlendHalfWidth)
                {
                    from = i - 1;
                    to = i;
                    t = Mathf.SmoothStep(0f, 1f, (x - (start - BlendHalfWidth)) / (2f * BlendHalfWidth));
                    return;
                }
                region = i;
            }
            from = to = region;
            t = 0f;
        }

        /// <summary>Share of one biome at x (0 to 1); the shares of every biome add up to 1.</summary>
        public float Weight(int biome, float x)
        {
            Resolve(x, out int from, out int to, out float t);
            if (from == to)
                return biome == from ? 1f : 0f;
            if (biome == from)
                return 1f - t;
            return biome == to ? t : 0f;
        }

        /// <summary>
        /// The camera's biome after a move to x, from the current one: it changes only once x is past a start by
        /// <see cref="CameraHysteresis"/>, so a camera resting on a start never flickers between two biomes. A current
        /// biome out of range takes the biome at x.
        /// </summary>
        public int CameraBiome(float x, int current)
        {
            int target = RegionAt(x);
            if (current < 0 || current >= _starts.Length || target == current)
                return target;
            if (target > current)
                return x >= _starts[target] + CameraHysteresis ? target : Math.Max(current, target - 1);
            return x <= _starts[target + 1] - CameraHysteresis ? target : Math.Min(current, target + 1);
        }
    }
}
