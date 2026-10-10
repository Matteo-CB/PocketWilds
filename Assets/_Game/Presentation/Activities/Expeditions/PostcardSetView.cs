using System;
using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>Everything a postcard set prefab holds, written by PostcardSetPipeline (runtime code never rewires it).</summary>
    [Serializable]
    public sealed class PostcardSetData
    {
        [Header("Set")]
        public Camera camera;
        public Light light;

        /// <summary>The team's ground: the blanket's center on the meadow (y = 0), the camera looking along +Z.</summary>
        public Transform origin;

        /// <summary>The meadow, the backdrop, the hill, the blanket, the basket, the grass, the butterflies: palette rows bound once at load.</summary>
        public WorldChunk.PaletteBinding[] bindings = Array.Empty<WorldChunk.PaletteBinding>();

        [Header("The team (origin frame, m)")]
        /// <summary>The seats of the three team sizes (<see cref="PostcardSetLayout.SeatIndex"/>): x, the root's height on the blanket, z.</summary>
        public Vector3[] seats = new Vector3[PostcardSetLayout.SeatCount];

        /// <summary>Each seat's yaw (180 faces the camera; the outer ones turn a little toward the middle).</summary>
        public float[] seatYaws = new float[PostcardSetLayout.SeatCount];

        /// <summary>The blanket's highest point (m): an animal moved off its seat stands this high, never in the cloth.</summary>
        public float blanketTop = 0.018f;

        /// <summary>The blanket's outline (half size, superellipse power) for an animal moved off its seat: on it or on the meadow.</summary>
        public Vector2 blanketHalf = new Vector2(0.95f, 0.55f);

        public float blanketPower = 7f;

        /// <summary>The props the team keeps clear of (the basket, the hill, the flowers), seen from above, origin frame.</summary>
        public Obstacle[] obstacles = Array.Empty<Obstacle>();

        [Header("Butterflies and framing")]
        public Transform[] butterflies = Array.Empty<Transform>();

        /// <summary>Each butterfly's reach around its pivot at its scale (m): one a posed animal would reach is hidden.</summary>
        public float[] butterflyRadii = Array.Empty<float>();

        /// <summary>The first butterflies fly by the team (shown whenever clear); the others flutter over the hill (the seed picks).</summary>
        public int nearButterflies;

        /// <summary>The half width (m) the camera keeps in view along the team's line; a wider team moves the camera back.</summary>
        public float frameHalfWidth = 1f;

        /// <summary>The team's line (origin frame z) where <see cref="frameHalfWidth"/> is measured.</summary>
        public float teamDepth;

        [Header("Light and sky while the set renders (the Prairie's warm afternoon)")]
        public Color ambientTop = new Color(0.66f, 0.72f, 0.82f);
        public Color ambientEquator = new Color(0.72f, 0.7f, 0.62f);
        public Color ambientBottom = new Color(0.44f, 0.46f, 0.36f);
        public Color skyTop = new Color(0.55f, 0.74f, 0.93f);
        public Color skyHorizon = new Color(1f, 0.9f, 0.78f);
        public Color skyBottom = new Color(0.93f, 0.86f, 0.74f);
        public Color sunGlowTint = new Color(1f, 0.88f, 0.68f);
        [Range(0f, 1f)] public float sunGlow = 0.35f;
        public Color cloudLight = new Color(1f, 0.98f, 0.94f);
        public Color cloudShade = new Color(0.86f, 0.82f, 0.86f);
        [Range(0f, 1f)] public float cloudCoverage = 0.3f;
    }

    /// <summary>
    /// Root of a postcard set prefab (T42, PF_Activity_Postcard_FlowerMeadows): its references and measured numbers. No
    /// Update: the postcard renderer shows it for one frame and hides it again. Binds its palette rows once, like a world
    /// chunk.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PostcardSetView : MonoBehaviour
    {
        [SerializeField] private PostcardSetData _data = new PostcardSetData();

        public PostcardSetData Data => _data;

        /// <summary>Editor/pipeline hook.</summary>
        public void SetAuthoring(PostcardSetData data) => _data = data ?? new PostcardSetData();

        /// <summary>Palette rows of the set's renderers. Returns how many palettes are unknown (they keep defaults).</summary>
        public int Bind(PaletteTable palettes)
        {
            if (palettes == null)
                return 0;
            int missing = 0;
            var bindings = _data.bindings;
            for (int i = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];
                if (binding.renderer == null)
                    continue;
                if (!palettes.TryGetRow(new ContentId(binding.paletteId), out int row))
                {
                    missing++;
                    continue;
                }
                if (binding.renderer is MeshRenderer mesh)
                    mesh.SetShaderUserValue(EnvironmentVisualPacking.Pack(row, binding.brightness, binding.vertexBrightness, binding.rotor));
            }
            return missing;
        }
    }

    /// <summary>
    /// T42: where each animal of a team sits on a postcard set (no-collision rule, Docs/Design/Collision_Rule.md). The set's
    /// seats are measured by PostcardSetPipeline for every species of the catalog up to <see cref="PostcardSetLayout.SeatScale"/>;
    /// a bigger animal (a Giant) is pushed aside along the team's line until no body enters another body or a prop, with
    /// <see cref="PostcardSetLayout.SeatGap"/> to spare. The same code runs in the pipeline's check, the renderer and the tests.
    /// No allocation.
    /// </summary>
    public static class PostcardTeamLayout
    {
        private const int Iterations = 32;

        /// <summary>
        /// Fills <paramref name="positions"/> and <paramref name="yaws"/> (origin frame) for <paramref name="count"/> bodies at
        /// their scales; returns the smallest gap left between two bodies or a body and a prop (m).
        /// </summary>
        public static float Solve(PostcardSetData data, int count, BodyShape[] bodies, float[] scales, Vector3[] positions, float[] yaws)
        {
            count = Mathf.Clamp(count, 0, PostcardSetLayout.MaxTeam);
            for (int i = 0; i < count; i++)
            {
                int seat = PostcardSetLayout.SeatIndex(count, i);
                positions[i] = data.seats != null && seat < data.seats.Length ? data.seats[seat] : new Vector3((i - (count - 1) * 0.5f) * 0.45f, data.blanketTop, 0f);
                yaws[i] = data.seatYaws != null && seat < data.seatYaws.Length ? data.seatYaws[seat] : 180f;
            }
            for (int pass = 0; pass < Iterations; pass++)
            {
                bool moved = false;
                for (int i = 0; i < count; i++)
                {
                    for (int j = i + 1; j < count; j++)
                    {
                        float gap = Gap(bodies, scales, positions, yaws, i, j);
                        if (gap >= PostcardSetLayout.SeatGap)
                            continue;
                        float push = (PostcardSetLayout.SeatGap - gap) * 0.5f + 0.002f;
                        float side = positions[i].x <= positions[j].x ? -1f : 1f;
                        positions[i].x += side * push;
                        positions[j].x -= side * push;
                        moved = true;
                    }
                    var obstacles = data.obstacles;
                    for (int o = 0; obstacles != null && o < obstacles.Length; o++)
                    {
                        float gap = ObstacleGap(bodies[i], scales[i], positions[i], yaws[i], in obstacles[o]);
                        if (gap >= PostcardSetLayout.SeatGap)
                            continue;
                        float side = positions[i].x < obstacles[o].Center.x ? -1f : 1f;
                        positions[i].x += side * (PostcardSetLayout.SeatGap - gap + 0.002f);
                        moved = true;
                    }
                }
                if (!moved)
                    break;
            }
            for (int i = 0; i < count; i++)
            {
                int seat = PostcardSetLayout.SeatIndex(count, i);
                bool onSeat = data.seats != null && seat < data.seats.Length && Mathf.Abs(positions[i].x - data.seats[seat].x) < 0.005f;
                if (!onSeat)
                    positions[i].y = OnBlanket(data, positions[i]) ? data.blanketTop : 0f;
            }
            return SmallestGap(data, count, bodies, scales, positions, yaws);
        }

        /// <summary>The smallest gap between the bodies and between each body and each prop (m; negative: something overlaps).</summary>
        public static float SmallestGap(PostcardSetData data, int count, BodyShape[] bodies, float[] scales, Vector3[] positions, float[] yaws)
        {
            float smallest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                for (int j = i + 1; j < count; j++)
                    smallest = Mathf.Min(smallest, Gap(bodies, scales, positions, yaws, i, j));
                var obstacles = data.obstacles;
                for (int o = 0; obstacles != null && o < obstacles.Length; o++)
                    smallest = Mathf.Min(smallest, ObstacleGap(bodies[i], scales[i], positions[i], yaws[i], in obstacles[o]));
            }
            return smallest;
        }

        /// <summary>The root stands on the blanket (inside its rounded outline, 8 cm in from the hem), else on the meadow.</summary>
        public static bool OnBlanket(PostcardSetData data, Vector3 position)
        {
            float hx = Mathf.Max(0.01f, data.blanketHalf.x - 0.08f), hz = Mathf.Max(0.01f, data.blanketHalf.y - 0.08f);
            float u = Mathf.Abs(position.x) / hx, v = Mathf.Abs(position.z) / hz;
            return Mathf.Pow(u, data.blanketPower) + Mathf.Pow(v, data.blanketPower) <= 1f;
        }

        private static float Gap(BodyShape[] bodies, float[] scales, Vector3[] positions, float[] yaws, int i, int j)
        {
            bodies[i].Segment(positions[i], yaws[i], scales[i], out var a0, out var a1);
            bodies[j].Segment(positions[j], yaws[j], scales[j], out var b0, out var b1);
            return CollisionMath.Gap(a0, a1, bodies[i].halfWidth * scales[i], b0, b1, bodies[j].halfWidth * scales[j]);
        }

        private static float ObstacleGap(in BodyShape body, float scale, Vector3 position, float yaw, in Obstacle obstacle)
        {
            if (!obstacle.BlocksWalker(position.y, body.height * scale))
                return float.MaxValue;
            body.Segment(position, yaw, scale, out var a, out var b);
            return CollisionMath.Gap(a, b, body.halfWidth * scale, in obstacle);
        }
    }
}
