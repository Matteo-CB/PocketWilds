using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// What the bubble placement needs from the camera, taken once per frame (no Camera call inside the resolver, so
    /// EditMode tests build one from numbers). Unity's GL-style projection: clip = projection × view × point.
    /// </summary>
    public struct BubbleCamera
    {
        public Vector3 Position;
        public Vector3 Right;
        public Vector3 Up;
        public Vector3 Forward;
        public Quaternion Rotation;
        public Matrix4x4 WorldToClip;
        public float ScreenWidth;
        public float ScreenHeight;

        /// <summary>tan(vertical field of view / 2): half the world height seen at one meter.</summary>
        public float TanHalfFov;

        public static BubbleCamera From(Camera camera)
        {
            var transform = camera.transform;
            return new BubbleCamera
            {
                Position = transform.position,
                Right = transform.right,
                Up = transform.up,
                Forward = transform.forward,
                Rotation = transform.rotation,
                WorldToClip = camera.projectionMatrix * camera.worldToCameraMatrix,
                ScreenWidth = camera.pixelWidth,
                ScreenHeight = camera.pixelHeight,
                TanHalfFov = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad),
            };
        }

        /// <summary>A perspective camera from its pose (tests, captures): same matrices as a Camera would give.</summary>
        public static BubbleCamera Create(Vector3 position, Quaternion rotation, float fieldOfView, float width, float height)
        {
            var view = Matrix4x4.Inverse(Matrix4x4.TRS(position, rotation, new Vector3(1f, 1f, -1f)));
            var projection = Matrix4x4.Perspective(fieldOfView, width / height, 0.3f, 200f);
            return new BubbleCamera
            {
                Position = position,
                Right = rotation * Vector3.right,
                Up = rotation * Vector3.up,
                Forward = rotation * Vector3.forward,
                Rotation = rotation,
                WorldToClip = projection * view,
                ScreenWidth = width,
                ScreenHeight = height,
                TanHalfFov = Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad),
            };
        }

        /// <summary>Screen point in pixels (origin bottom left) and depth along the view; false behind the camera.</summary>
        public bool ToScreen(Vector3 world, out Vector2 screen, out float depth)
        {
            var clip = WorldToClip * new Vector4(world.x, world.y, world.z, 1f);
            depth = clip.w;
            if (depth <= 1e-4f)
            {
                screen = default;
                return false;
            }
            screen = new Vector2((clip.x / depth * 0.5f + 0.5f) * ScreenWidth, (clip.y / depth * 0.5f + 0.5f) * ScreenHeight);
            return true;
        }

        /// <summary>Screen pixels per world meter at this depth.</summary>
        public float PixelsPerMeter(float depth) => ScreenHeight / (2f * Mathf.Max(depth, 1e-3f) * TanHalfFov);
    }

    /// <summary>One bubble to place: the animal under it and where it stood at the last pass.</summary>
    public struct BubbleRequest
    {
        /// <summary>The animal's scheduler handle.</summary>
        public int Handle;

        /// <summary>The candidate it held at the last pass (-1: none, a new or hidden bubble).</summary>
        public int Candidate;

        /// <summary>A fulfilled wish: it reserves room for its soft pulse and its burst.</summary>
        public bool Done;

        /// <summary>A bursting bubble keeps <see cref="Offset"/> (it only reserves its room for the others).</summary>
        public bool Fixed;

        public Vector3 Offset;
        public bool Flip;
    }

    /// <summary>Where a bubble goes: its center relative to the animal's root, and the side its trailing dots point to.</summary>
    public struct BubbleSpot
    {
        public bool Placed;
        public int Candidate;
        public Vector3 Offset;

        /// <summary>The bubble stands on the animal's left (on screen): the trailing dots are mirrored to point at it.</summary>
        public bool Flip;
    }

    /// <summary>Scratch arrays of the resolver, allocated once (no allocation per pass or per frame).</summary>
    public sealed class BubblePlacementBuffers
    {
        public BubblePlacementBuffers(int maxBubbles, int maxAnimals)
        {
            Order = new int[maxBubbles];
            Keys = new float[maxBubbles];
            PlacedCenters = new Vector3[maxBubbles * 2];
            PlacedRadii = new float[maxBubbles * 2];
            PlacedScreen = new Vector2[maxBubbles];
            PlacedScreenRadius = new float[maxBubbles];
            PlacedOnScreen = new bool[maxBubbles];
            Faces = new Vector3[maxAnimals];
            FaceHandles = new int[maxAnimals];
        }

        /// <summary>Requests by priority after <see cref="WishBubblePlacement.Resolve"/>: fixed ones, then nearest first.</summary>
        public readonly int[] Order;

        internal readonly float[] Keys;
        internal readonly Vector3[] PlacedCenters;
        internal readonly float[] PlacedRadii;
        internal readonly Vector2[] PlacedScreen;
        internal readonly float[] PlacedScreenRadius;
        internal readonly bool[] PlacedOnScreen;
        internal readonly Vector3[] Faces;
        internal readonly int[] FaceHandles;
        internal int PlacedCount;
        internal int FaceCount;

        /// <summary>Bubbles placed so far in this pass (or this frame's check).</summary>
        public int Placed => PlacedCount;

        public void ClearPlaced() => PlacedCount = 0;
    }

    /// <summary>
    /// T36 wish bubbles, no-collision placement (plan section 7.1; doc 06 screen 002: in the world above the animals;
    /// Docs/Design/Collision_Rule.md). Pure static and allocation-free, EditMode-testable.
    /// <list type="bullet">
    /// <item>A bubble's room is two spheres: its circle (UI_Bubble_Wish, outline included) at the largest scale it
    /// reaches (pop-in, burst), and its two trailing dots, which lie in the camera's plane under it toward the animal, at
    /// every scale they are drawn at. Both are widened by its bob and the rule's 2 mm tolerance.</item>
    /// <item>Anchor: straight above the animal's measured body, high enough for the tail's room to clear the top of the
    /// body by <see cref="TailClearance"/>.</item>
    /// <item>Candidates, in order: up, up + 0.12, up + 0.24 (m at the nominal size); then ± 0.18 along the camera's right
    /// at each height (dots mirrored on the left side); then 0.15 back (away from the camera) at each height.</item>
    /// <item>A candidate is free when no sphere enters a solid volume, an animal's body column or the egg's room
    /// (<see cref="AnimalScheduler.IsAirFree"/>) nor a bubble placed before it in this pass (nearest to the camera first),
    /// and its circle on screen stays clear of the other bubbles' circles (legibility) and of every other animal's face
    /// (never covering one). None free: the bubble fades out until a later pass finds a place.</item>
    /// <item>The pass prefers places with <see cref="LookAhead"/> to spare, so bubbles glide aside before an animal comes
    /// close. A bubble keeps its candidate while it stays free; it comes back to a better one only once that one is free
    /// with <see cref="SwitchMargin"/> more room (no flicker between two places).</item>
    /// </list>
    /// </summary>
    public static class WishBubblePlacement
    {
        /// <summary>Bubble radius in the world at the nominal size (plan section 7.1, about 0.13 m).</summary>
        public const float NominalRadius = 0.13f;

        /// <summary>UI_Bubble_Wish's circle with its outline: 62.5 px at 100 px per unit (T36 sprites review).</summary>
        public const float SpriteRadius = 0.625f;

        /// <summary>The bubble's diameter on screen stays between these fractions of the screen height (every zoom).</summary>
        public const float MinScreenDiameter = 0.04f;
        public const float MaxScreenDiameter = 0.058f;

        /// <summary>Scales the bubble reaches: pop-in overshoot, the done pulse, the burst (doc 05 sheet 17: 1.15).</summary>
        public const float PopScale = 1.08f;
        public const float PulseScale = 1.05f;
        public const float BurstScale = 1.15f;

        /// <summary>Bob amplitude, a fraction of the radius (calm: under a centimeter).</summary>
        public const float BobAmplitude = 0.06f;

        /// <summary>Free space between the lowest trailing dot and the top of the animal's body (m).</summary>
        public const float TailClearance = 0.03f;

        public const float RiseStep = 0.12f;
        public const float LateralStep = 0.18f;
        public const float BackStep = 0.15f;

        /// <summary>A better candidate is taken back once it is free with this much more room (m).</summary>
        public const float SwitchMargin = 0.03f;

        /// <summary>
        /// The 10 Hz pass looks for places with this much room to spare (m: what two walkers close in 100 ms), so a bubble
        /// glides aside before an animal reaches it; a place without it is taken only when no other is free.
        /// </summary>
        public const float LookAhead = 0.05f;

        /// <summary>Gap kept between two bubbles' circles on screen, a fraction of their radii.</summary>
        public const float ScreenGap = 0.08f;

        /// <summary>A face on screen: a circle at the front of the body, this high up the body, this fraction of its width.</summary>
        public const float FaceHeight = 0.7f;
        public const float FaceScale = 0.85f;

        public const float Tolerance = AnimalScheduler.ContactTolerance;

        // The two trailing dots of UI_Bubble_Wish (pw_ui_sprites_t36.py: circles of 10 and 6.2 px at (-18, -77) and
        // (-31, -94), outline 4.5 px), in units of the circle's radius (62.5 px).
        public static readonly Vector2 Dot1 = new Vector2(-0.288f, -1.232f);
        public const float Dot1Radius = 0.232f;
        public static readonly Vector2 Dot2 = new Vector2(-0.496f, -1.504f);
        public const float Dot2Radius = 0.171f;

        public const int CandidateCount = 12;
        private static readonly sbyte[] Rise = { 0, 1, 2, 0, 0, 1, 1, 2, 2, 0, 1, 2 };
        private static readonly sbyte[] Side = { 0, 0, 0, 1, -1, 1, -1, 1, -1, 0, 0, 0 };
        private static readonly sbyte[] Back = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1 };

        /// <summary>
        /// The bubble's world radius for this camera distance: nominal, held between <see cref="MinScreenDiameter"/> and
        /// <see cref="MaxScreenDiameter"/> of the screen height (readable far away, never huge up close).
        /// </summary>
        public static float WorldRadius(float distance, float tanHalfFov)
        {
            float worldHeight = 2f * Mathf.Max(distance, 0.1f) * tanHalfFov;
            return Mathf.Clamp(NominalRadius, 0.5f * MinScreenDiameter * worldHeight, 0.5f * MaxScreenDiameter * worldHeight);
        }

        /// <summary>The scale a bubble may reach in its state (its room is reserved for it).</summary>
        public static float ReserveScale(bool done) => done ? BurstScale : PopScale;

        /// <summary>Smallest scale it is drawn at: the start of the pop-in.</summary>
        public const float MinScale = 0.55f;

        // The tail's room: one sphere holding both trailing dots at every scale from MinScale to the state's largest
        // (in units of the radius; x toward the dots' side, z its radius), worked out once.
        private static readonly Vector3 TailPending = TailBound(PopScale);
        private static readonly Vector3 TailDone = TailBound(BurstScale);

        private static Vector3 TailBound(float maxScale)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            for (int k = 0; k < 4; k++)
            {
                var dot = k < 2 ? Dot1 : Dot2;
                float s = (k & 1) == 0 ? MinScale : maxScale;
                float r = (k < 2 ? Dot1Radius : Dot2Radius) * s;
                minX = Mathf.Min(minX, dot.x * s - r);
                maxX = Mathf.Max(maxX, dot.x * s + r);
                minY = Mathf.Min(minY, dot.y * s - r);
                maxY = Mathf.Max(maxY, dot.y * s + r);
            }
            var center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            float radius = 0f;
            for (int k = 0; k < 4; k++)
            {
                var dot = k < 2 ? Dot1 : Dot2;
                float s = (k & 1) == 0 ? MinScale : maxScale;
                float r = (k < 2 ? Dot1Radius : Dot2Radius) * s;
                radius = Mathf.Max(radius, (dot * s - center).magnitude + r);
            }
            return new Vector3(center.x, center.y, radius);
        }

        /// <summary>Radius of the circle's sphere tested for a bubble of this radius (motion, bob and tolerance included).</summary>
        public static float ReserveRadius(float radius, bool done) => radius * (ReserveScale(done) + BobAmplitude) + Tolerance;

        /// <summary>
        /// The room a bubble centered at <paramref name="center"/> reserves: the sphere of its circle at its largest scale,
        /// and one sphere holding its two trailing dots at every scale they are drawn at (pop-in to burst), both widened by
        /// its bob, the rule's tolerance and <paramref name="margin"/> (m). The dots lie in the camera's plane.
        /// </summary>
        public static void Spheres(in BubbleCamera camera, Vector3 center, bool flip, float radius, bool done, float margin,
            out Vector3 c0, out float r0, out Vector3 c1, out float r1)
        {
            var tail = done ? TailDone : TailPending;
            float extra = radius * BobAmplitude + Tolerance + margin;
            c0 = center;
            r0 = radius * ReserveScale(done) + extra;
            c1 = center + camera.Right * (tail.x * (flip ? -radius : radius)) + camera.Up * (tail.y * radius);
            r1 = tail.z * radius + extra;
        }

        /// <summary>The three spheres of a bubble exactly as drawn at this radius (tests): no inflation at all.</summary>
        public static void VisualSpheres(in BubbleCamera camera, Vector3 center, bool flip, float radius,
            out Vector3 c0, out float r0, out Vector3 c1, out float r1, out Vector3 c2, out float r2)
        {
            float side = flip ? -radius : radius;
            c0 = center;
            r0 = radius;
            c1 = center + camera.Right * (Dot1.x * side) + camera.Up * (Dot1.y * radius);
            r1 = Dot1Radius * radius;
            c2 = center + camera.Right * (Dot2.x * side) + camera.Up * (Dot2.y * radius);
            r2 = Dot2Radius * radius;
        }

        /// <summary>How far above the top of the body the bubble's center sits: its tail's room clears the body.</summary>
        public static float Lift(in BubbleCamera camera, float radius)
        {
            // Reserved for a fulfilled wish whatever the state: a wish coming true never moves its bubble.
            var tail = TailDone;
            float extra = radius * BobAmplitude + Tolerance;
            float main = radius * BurstScale;
            float low = -(camera.Up.y * tail.y * radius) + Mathf.Abs(camera.Right.y * tail.x * radius) + tail.z * radius;
            return TailClearance + extra + Mathf.Max(main, low);
        }

        /// <summary>Center of candidate <paramref name="candidate"/> relative to the animal's root, and its side.</summary>
        public static Vector3 CandidateOffset(in BubbleCamera camera, float bodyTop, float rootY, float radius, int candidate, out bool flip)
        {
            float step = radius / NominalRadius;
            var right = new Vector3(camera.Right.x, 0f, camera.Right.z);
            right = right.sqrMagnitude > 1e-6f ? right.normalized : Vector3.right;
            var back = new Vector3(camera.Forward.x, 0f, camera.Forward.z);
            back = back.sqrMagnitude > 1e-6f ? back.normalized : Vector3.forward;
            float up = bodyTop - rootY + Lift(camera, radius) + Rise[candidate] * RiseStep * step;
            flip = Side[candidate] < 0;
            return new Vector3(0f, up, 0f) + right * (Side[candidate] * LateralStep * step) + back * (Back[candidate] * BackStep * step);
        }

        /// <summary>
        /// Places every request (<paramref name="count"/> of them) in one pass and returns how many found a place.
        /// Fixed requests (bursting bubbles) are reserved first, then the others nearest to the camera first.
        /// <paramref name="screenRules"/>: also keep circles apart on screen and off the animals' faces (the 10 Hz pass;
        /// the per-frame check only keeps the world rule).
        /// </summary>
        public static int Resolve(AnimalScheduler scheduler, in BubbleCamera camera, float radius, BubbleRequest[] requests, int count,
            BubbleSpot[] spots, BubblePlacementBuffers buffers, bool screenRules = true)
        {
            buffers.ClearPlaced();
            if (screenRules)
                CollectFaces(scheduler, camera, buffers);
            else
                buffers.FaceCount = 0;

            // Fixed first, then by distance to the camera (insertion sort: nine at most, no allocation).
            int n = 0;
            for (int i = 0; i < count; i++)
            {
                var root = scheduler.IsValid(requests[i].Handle) ? scheduler.GetPosition(requests[i].Handle) : camera.Position;
                float key = (root - camera.Position).sqrMagnitude - (requests[i].Fixed ? 1e9f : 0f);
                int at = n++;
                while (at > 0 && buffers.Keys[at - 1] > key)
                {
                    buffers.Keys[at] = buffers.Keys[at - 1];
                    buffers.Order[at] = buffers.Order[at - 1];
                    at--;
                }
                buffers.Keys[at] = key;
                buffers.Order[at] = i;
            }

            int placed = 0;
            for (int k = 0; k < n; k++)
            {
                int i = buffers.Order[k];
                ref var request = ref requests[i];
                if (!scheduler.IsValid(request.Handle))
                {
                    spots[i] = new BubbleSpot { Placed = false, Candidate = -1, Offset = request.Offset, Flip = request.Flip };
                    continue;
                }
                if (request.Fixed)
                {
                    var center = scheduler.GetPosition(request.Handle) + request.Offset;
                    AddPlaced(camera, buffers, center, request.Flip, radius, true, screenRules);
                    spots[i] = new BubbleSpot { Placed = true, Candidate = request.Candidate, Offset = request.Offset, Flip = request.Flip };
                    placed++;
                    continue;
                }
                if (TryPlace(scheduler, camera, radius, request.Handle, request.Candidate, request.Done, buffers, screenRules, out spots[i], LookAhead))
                {
                    AddPlaced(camera, buffers, scheduler.GetPosition(request.Handle) + spots[i].Offset, spots[i].Flip, radius, request.Done, screenRules);
                    placed++;
                }
                else
                {
                    spots[i].Offset = request.Offset;
                    spots[i].Flip = request.Flip;
                }
            }
            return placed;
        }

        /// <summary>
        /// The first free candidate for one bubble, against the world and the bubbles already placed in
        /// <paramref name="buffers"/> (the caller adds it with <see cref="AddPlaced"/> once taken). In order: a better
        /// place than the current one with <see cref="SwitchMargin"/> more room; the current one with
        /// <paramref name="lookAhead"/>; any with <paramref name="lookAhead"/>; then the same without it.
        /// </summary>
        public static bool TryPlace(AnimalScheduler scheduler, in BubbleCamera camera, float radius, int handle, int current, bool done,
            BubblePlacementBuffers buffers, bool screenRules, out BubbleSpot spot, float lookAhead = 0f)
        {
            scheduler.GetBody(handle, out _, out _, out _, out float groundY, out float height);
            var root = scheduler.GetPosition(handle);
            float top = groundY + height;
            bool hasCurrent = current >= 0 && current < CandidateCount;
            if (hasCurrent)
            {
                // A better place is taken back only with room to spare; the current one is kept while it stays free.
                for (int k = 0; k < current; k++)
                {
                    if (Fits(scheduler, camera, buffers, root, top, radius, k, done, handle, screenRules, lookAhead + SwitchMargin, out spot))
                        return true;
                }
            }
            for (int round = lookAhead > 0f ? 0 : 1; round < 2; round++)
            {
                float margin = round == 0 ? lookAhead : 0f;
                if (hasCurrent && Fits(scheduler, camera, buffers, root, top, radius, current, done, handle, screenRules, margin, out spot))
                    return true;
                for (int k = 0; k < CandidateCount; k++)
                {
                    if (k == current)
                        continue;
                    if (Fits(scheduler, camera, buffers, root, top, radius, k, done, handle, screenRules, margin, out spot))
                        return true;
                }
            }
            spot = new BubbleSpot { Placed = false, Candidate = -1 };
            return false;
        }

        private static bool Fits(AnimalScheduler scheduler, in BubbleCamera camera, BubblePlacementBuffers buffers, Vector3 root, float top,
            float radius, int candidate, bool done, int owner, bool screenRules, float margin, out BubbleSpot spot)
        {
            var offset = CandidateOffset(camera, top, root.y, radius, candidate, out bool flip);
            spot = new BubbleSpot { Placed = true, Candidate = candidate, Offset = offset, Flip = flip };
            return IsFree(scheduler, camera, buffers, root + offset, flip, radius, done, owner, screenRules, margin);
        }

        /// <summary>
        /// A bubble centered here enters nothing: no volume, no other animal's body column (<paramref name="owner"/>'s is
        /// below every candidate by construction), not the egg's room, no bubble in <paramref name="buffers"/>; with
        /// <paramref name="screenRules"/>, its circle on screen also stays clear of those bubbles and of the faces of the
        /// animals other than <paramref name="owner"/>.
        /// </summary>
        public static bool IsFree(AnimalScheduler scheduler, in BubbleCamera camera, BubblePlacementBuffers buffers, Vector3 center, bool flip,
            float radius, bool done, int owner, bool screenRules, float margin = 0f)
        {
            Spheres(camera, center, flip, radius, done, margin, out var c0, out float r0, out var c1, out float r1);
            // Its own animal is skipped: every candidate stands above its body by construction (Lift), whatever the margin;
            // the tests check the drawn bubble against it too.
            if (!scheduler.IsAirFree(c0, r0, owner) || !scheduler.IsAirFree(c1, r1, owner))
                return false;
            for (int j = 0; j < buffers.PlacedCount; j++)
            {
                if (Overlaps(buffers, j, c0, r0) || Overlaps(buffers, j, c1, r1))
                    return false;
            }
            if (!screenRules || !camera.ToScreen(center, out var screen, out float depth))
                return true;
            float screenRadius = radius * ReserveScale(done) * camera.PixelsPerMeter(depth);
            for (int j = 0; j < buffers.PlacedCount; j++)
            {
                if (!buffers.PlacedOnScreen[j])
                    continue;
                float min = (screenRadius + buffers.PlacedScreenRadius[j]) * (1f + ScreenGap);
                if ((buffers.PlacedScreen[j] - screen).sqrMagnitude < min * min)
                    return false;
            }
            for (int f = 0; f < buffers.FaceCount; f++)
            {
                if (buffers.FaceHandles[f] == owner)
                    continue;
                var face = buffers.Faces[f];
                float min = screenRadius + face.z;
                float dx = face.x - screen.x, dy = face.y - screen.y;
                if (dx * dx + dy * dy < min * min)
                    return false;
            }
            return true;
        }

        private static bool Overlaps(BubblePlacementBuffers buffers, int placed, Vector3 center, float radius)
        {
            for (int s = 0; s < 2; s++)
            {
                float min = radius + buffers.PlacedRadii[placed * 2 + s];
                if ((buffers.PlacedCenters[placed * 2 + s] - center).sqrMagnitude < min * min)
                    return true;
            }
            return false;
        }

        /// <summary>Reserves a bubble's room (its three spheres and, with screen rules, its circle on screen).</summary>
        public static void AddPlaced(in BubbleCamera camera, BubblePlacementBuffers buffers, Vector3 center, bool flip, float radius, bool done,
            bool screenRules)
        {
            int j = buffers.PlacedCount;
            if (j >= buffers.PlacedScreen.Length)
                return;
            // Reserved without the tolerance: two touching reserves may touch (the tolerance is counted once, on the tested one).
            Spheres(camera, center, flip, radius, done, -Tolerance, out var c0, out float r0, out var c1, out float r1);
            buffers.PlacedCenters[j * 2] = c0;
            buffers.PlacedRadii[j * 2] = r0;
            buffers.PlacedCenters[j * 2 + 1] = c1;
            buffers.PlacedRadii[j * 2 + 1] = r1;
            float depth = 0f;
            bool onScreen = screenRules && camera.ToScreen(center, out buffers.PlacedScreen[j], out depth);
            buffers.PlacedOnScreen[j] = onScreen;
            buffers.PlacedScreenRadius[j] = onScreen ? radius * ReserveScale(done) * camera.PixelsPerMeter(depth) : 0f;
            buffers.PlacedCount = j + 1;
        }

        /// <summary>Every animal's face on screen (x, y, radius in pixels), once per pass.</summary>
        private static void CollectFaces(AnimalScheduler scheduler, in BubbleCamera camera, BubblePlacementBuffers buffers)
        {
            int n = 0;
            int count = Mathf.Min(scheduler.Count, buffers.Faces.Length);
            for (int i = 0; i < count; i++)
            {
                int handle = scheduler.GetActiveHandle(i);
                scheduler.GetBody(handle, out _, out var front, out float bodyRadius, out float groundY, out float height);
                var head = new Vector3(front.x, groundY + FaceHeight * height, front.y);
                if (!camera.ToScreen(head, out var screen, out float depth))
                    continue;
                buffers.Faces[n] = new Vector3(screen.x, screen.y, bodyRadius * FaceScale * camera.PixelsPerMeter(depth));
                buffers.FaceHandles[n] = handle;
                n++;
            }
            buffers.FaceCount = n;
        }
    }
}
