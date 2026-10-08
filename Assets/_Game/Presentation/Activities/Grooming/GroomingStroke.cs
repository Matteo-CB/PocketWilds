using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T38 brushing (doc 01 Contenu, S-ACT-001 « Brosser : glisser le doigt »): turns the finger's path on screen into passes
    /// over the coat's zones. A pass counts when the stroke crosses a zone's screen disk and has slid at least
    /// <see cref="PassShare"/> of the zone's radius inside it (grazing an edge does not count); one pass per crossing, and
    /// scrubbing back and forth inside a zone counts each change of direction. A fast swipe that crosses a disk between two
    /// frames counts too (the segment is clipped to the disk). No allocation: the caller owns every array.
    /// </summary>
    public sealed class GroomingStroke
    {
        /// <summary>Share of a zone's radius the finger must travel inside it for a pass.</summary>
        public const float PassShare = 0.35f;

        /// <summary>A turn of more than about 100° inside a zone starts a new pass.</summary>
        private const float ReverseDot = -0.2f;

        private readonly float[] _travel;
        private readonly bool[] _counted;
        private readonly Vector2[] _countDirection;
        private Vector2 _last;
        private bool _down;

        public GroomingStroke(int maxZones)
        {
            _travel = new float[maxZones];
            _counted = new bool[maxZones];
            _countDirection = new Vector2[maxZones];
        }

        public bool Down => _down;

        public Vector2 Position => _last;

        /// <summary>Screen pixels the finger moved in the last <see cref="Move"/> (its direction for the brush's tilt).</summary>
        public Vector2 LastDelta { get; private set; }

        public void Begin(Vector2 screen)
        {
            _down = true;
            _last = screen;
            LastDelta = Vector2.zero;
            Clear();
        }

        public void End()
        {
            _down = false;
            LastDelta = Vector2.zero;
            Clear();
        }

        /// <summary>
        /// The finger moved to <paramref name="screen"/>. Zones with <paramref name="hittable"/> false (turned away) are
        /// skipped. Writes the zones that got a pass into <paramref name="passes"/> and returns how many.
        /// </summary>
        public int Move(Vector2 screen, int zoneCount, Vector2[] centers, float[] radii, bool[] hittable, int[] passes)
        {
            if (!_down)
                return 0;
            var a = _last;
            var d = screen - a;
            _last = screen;
            LastDelta = d;
            float length = d.magnitude;
            if (length < 0.01f)
                return 0;
            var direction = d / length;
            int count = 0;
            for (int z = 0; z < zoneCount; z++)
            {
                if (!hittable[z])
                {
                    _travel[z] = 0f;
                    _counted[z] = false;
                    continue;
                }
                float r = radii[z];
                var c = centers[z];
                float inside = ChordLength(a, d, length, c, r);
                bool endInside = (screen - c).sqrMagnitude < r * r;
                if (inside > 0f)
                {
                    if (_counted[z] && Vector2.Dot(direction, _countDirection[z]) < ReverseDot)
                    {
                        // Scrubbing: the finger turned back inside the zone, a new pass begins.
                        _counted[z] = false;
                        _travel[z] = 0f;
                    }
                    _travel[z] += inside;
                    if (!_counted[z] && _travel[z] >= PassShare * r)
                    {
                        _counted[z] = true;
                        _countDirection[z] = direction;
                        if (count < passes.Length)
                            passes[count++] = z;
                    }
                }
                if (!endInside)
                {
                    _travel[z] = 0f;
                    _counted[z] = false;
                }
            }
            return count;
        }

        /// <summary>Length of the segment a → a + d inside the disk (c, r).</summary>
        public static float ChordLength(Vector2 a, Vector2 d, float length, Vector2 c, float r)
        {
            var f = a - c;
            float qa = length * length;
            float qb = 2f * Vector2.Dot(f, d);
            float qc = f.sqrMagnitude - r * r;
            float disc = qb * qb - 4f * qa * qc;
            if (disc <= 0f || qa <= 0f)
                return 0f;
            float root = Mathf.Sqrt(disc);
            float t1 = Mathf.Clamp01((-qb - root) / (2f * qa));
            float t2 = Mathf.Clamp01((-qb + root) / (2f * qa));
            return t2 > t1 ? (t2 - t1) * length : 0f;
        }

        private void Clear()
        {
            for (int z = 0; z < _travel.Length; z++)
            {
                _travel[z] = 0f;
                _counted[z] = false;
            }
        }
    }
}
