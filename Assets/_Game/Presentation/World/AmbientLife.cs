using System.Collections.Generic;
using PocketWilds.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// AmbientLife (doc 07, doc 04): butterflies, bees and birds around the active chunks' anchors, drawn with GPU
    /// instancing. No GameObject, no Animator: agents are structs moved by a few lines of steering, wings flap in the
    /// vertex shader (SH_AmbientLife). The scene entry calls <see cref="Tick"/> and <see cref="Render"/> once per
    /// frame. The quality tier keeps a share of each anchor's individuals. Nothing allocates per frame; adding a
    /// chunk allocates its agents once. A tap near them makes them flee (<see cref="Startle"/>).
    /// T33C no-collision rule: insects fly around the props' volumes (never through a bush, a crown or an animal) and
    /// keep apart; birds hop and land only on free ground (no prop, no water, no animal, no other bird), stand on the
    /// chunk's ground heights, fly off when an animal comes near, and pick flight paths that clear every crown.
    /// </summary>
    /// <summary>T34B2: told when a small bird flies off (its wing beats are heard near the view).</summary>
    public interface IAmbientLifeListener
    {
        void BirdTookOff(Vector3 position);
    }

    public sealed class AmbientLife
    {
        private static readonly int PaletteRowId = Shader.PropertyToID("_PaletteRow");
        private static readonly int FlapId = Shader.PropertyToID("_Flap");
        private static readonly int ShoulderId = Shader.PropertyToID("_Shoulder");

        private const byte Wander = 0, Flee = 1;
        private const byte Idle = 0, Hop = 1, Peck = 2, Fly = 3;
        private const float FlightDuration = 3.2f;
        private const float HopDuration = 0.22f;
        private const float PeckDuration = 0.45f;
        // Obstacles within this distance of an agent's home are the ones it may ever meet (flights included).
        private const float NearRadius = 4.5f;
        private const float BirdHeight = 0.2f;
        // An animal this close (beyond both bodies) sends a grounded bird off.
        private const float BirdShyDistance = 0.35f;
        private static readonly float[] FlightAngles = { 0f, 40f, -40f, 80f, -80f, 130f, -130f, 180f };

        private struct Agent
        {
            public WorldChunk Chunk;
            public int Slot;
            public Vector3 Home;
            public Vector3 Position;
            public Vector3 Velocity;
            public Vector3 Forward;
            public Vector3 From;
            public Vector3 Apex;
            public Vector3 Target;
            public float Timer;
            public float Duration;
            public float Pitch;
            public float Seed;
            public byte State;
            public uint Random;
            public GroundSurface Ground;
            public int NearStart;
            public int NearCount;
            /// <summary>A bird that found no clear flight waits this long before trying again.</summary>
            public float Retry;
        }

        private sealed class SpeciesState
        {
            public AmbientLifeKit.Species Def;
            public Agent[] Agents = new Agent[8];
            public int Count;
            public int ActiveSlots;
            public Matrix4x4[] Flying = new Matrix4x4[8];
            public Matrix4x4[] Grounded = new Matrix4x4[8];
            public RenderParams FlyParams;
            public RenderParams GroundParams;
            public Bounds Bounds;
            /// <summary>Body radius seen from above (mesh extent times scale).</summary>
            public float Radius;
        }

        private readonly SpeciesState[] _species;
        private readonly Plane[] _planes = new Plane[6];
        private readonly List<WorldChunk> _chunks = new List<WorldChunk>();
        private Obstacle[] _obstacles = new Obstacle[0];
        private int[] _near = new int[0];
        private AnimalScheduler _animals;
        private IAmbientLifeListener _listener;
        private float _density = 1f;
        private uint _seed = 0x9E3779B9u;

        /// <param name="seasons">T48B: adds the fireflies of Soleil evenings (off until <see cref="SetFireflies"/>). Optional.</param>
        public AmbientLife(AmbientLifeKit kit, PaletteTable palettes, float density = 1f, SeasonLookSet seasons = null)
        {
            int count = kit != null && kit.material != null ? kit.species.Length : 0;
            bool fireflies = seasons != null && seasons.fireflyMaterial != null && seasons.quadMesh != null;
            _species = new SpeciesState[count + (fireflies ? 1 : 0)];
            if (fireflies)
            {
                var def = seasons.fireflies;
                def.mesh = seasons.quadMesh;
                def.behaviour = AmbientLifeBehaviour.Firefly;
                def.perAnchor = Mathf.Max(1, def.perAnchor);
                var extents = def.mesh.bounds.extents;
                _species[count] = new SpeciesState
                {
                    Def = def,
                    // The quad stands in x and y: its glow core is the body that keeps out of every volume.
                    Radius = Mathf.Max(extents.x, extents.y) * def.scale,
                    FlyParams = Params(seasons.fireflyMaterial, 0, Vector3.zero, Vector2.zero),
                    GroundParams = Params(seasons.fireflyMaterial, 0, Vector3.zero, Vector2.zero),
                };
            }
            for (int i = 0; i < count; i++)
            {
                var def = kit.species[i];
                int row = palettes != null && palettes.TryGetRow(new ContentId(def.paletteId), out int r) ? r : 0;
                var extents = def.mesh != null ? def.mesh.bounds.extents : Vector3.one * 0.05f;
                _species[i] = new SpeciesState
                {
                    Def = def,
                    Radius = Mathf.Max(extents.x, extents.z) * def.scale,
                    FlyParams = Params(kit.material, row, def.flap, def.shoulder),
                    GroundParams = Params(kit.material, row, def.flapGrounded, def.shoulder),
                };
            }
            SetDensity(density);
        }

        /// <summary>Individuals moved by the last <see cref="Tick"/>.</summary>
        public int LastActive { get; private set; }
        /// <summary>Birds in flight during the last <see cref="Tick"/>.</summary>
        public int LastAirborne { get; private set; }
        /// <summary>Instances submitted by the last <see cref="Render"/>.</summary>
        public int LastInstances { get; private set; }
        /// <summary>Instanced draws submitted by the last <see cref="Render"/>.</summary>
        public int LastDraws { get; private set; }
        /// <summary>Editor captures only (overdraw review): every individual drawn with this material. Null in the game.</summary>
        public Material OverrideMaterial { get; set; }
        public int AgentCount
        {
            get
            {
                int n = 0;
                foreach (var s in _species)
                    n += s.Count;
                return n;
            }
        }
        public float Density => _density;

        /// <summary>Share of each anchor's individuals: 0.25 Fluide, 0.6 Équilibrée, 1 Magnifique (at least one).</summary>
        public void SetDensity(float density)
        {
            _density = Mathf.Clamp01(density);
            foreach (var s in _species)
            {
                float share = s.Def.behaviour == AmbientLifeBehaviour.Firefly ? _density * _fireflies : _density;
                s.ActiveSlots = share <= 0f ? 0 : Mathf.Clamp(Mathf.CeilToInt(s.Def.perAnchor * share - 0.0001f), 1, s.Def.perAnchor);
            }
        }

        private float _fireflies;

        /// <summary>T48B: share of the fireflies out (0 none, 1 all; SeasonPresenter, Soleil evenings). No allocation.</summary>
        public void SetFireflies(float weight)
        {
            weight = Mathf.Clamp01(weight);
            if (Mathf.Abs(weight - _fireflies) < 0.02f && !(weight == 0f && _fireflies > 0f) && !(weight == 1f && _fireflies < 1f))
                return;
            _fireflies = weight;
            SetDensity(_density);
        }

        public float FireflyWeight => _fireflies;

        /// <summary>T48B: fireflies flying now (tests).</summary>
        public int ActiveFireflies
        {
            get
            {
                int n = 0;
                foreach (var s in _species)
                {
                    if (s.Def.behaviour != AmbientLifeBehaviour.Firefly)
                        continue;
                    for (int i = 0; i < s.Count; i++)
                    {
                        if (s.Agents[i].Slot < s.ActiveSlots)
                            n++;
                    }
                }
                return n;
            }
        }

        /// <summary>The refuge's animals: insects fly around them, birds keep away from them.</summary>
        public void SetAnimals(AnimalScheduler animals) => _animals = animals;

        /// <summary>T36: the wish bubbles above animals; fliers keep out of them like out of a crown (Collision rule).</summary>
        public void SetBubbles(WishBubbleLayer bubbles) => _bubbles = bubbles;

        private WishBubbleLayer _bubbles;

        private bool InsideBubble(Vector3 p, float r)
        {
            if (_bubbles == null)
                return false;
            for (int i = 0; i < _bubbles.AirCount; i++)
            {
                _bubbles.GetAir(i, out var center, out float radius);
                float min = radius + r;
                if ((p - center).sqrMagnitude < min * min)
                    return true;
            }
            return false;
        }

        /// <summary>Pushes a flier out of any bubble it entered and takes away the speed that carried it in.</summary>
        private bool PushOutOfBubbles(ref Agent a, float r)
        {
            if (_bubbles == null)
                return false;
            bool hit = false;
            for (int i = 0; i < _bubbles.AirCount; i++)
            {
                _bubbles.GetAir(i, out var center, out float radius);
                var d = a.Position - center;
                float min = radius + r;
                float sq = d.sqrMagnitude;
                if (sq >= min * min)
                    continue;
                float length = Mathf.Sqrt(sq);
                var normal = length > 1e-4f ? d / length : Vector3.up;
                a.Position = center + normal * (min + 0.002f);
                float into = Vector3.Dot(a.Velocity, normal);
                if (into < 0f)
                    a.Velocity -= normal * into;
                hit = true;
            }
            return hit;
        }

        /// <summary>T34B2: who hears the birds fly off. Null: silent.</summary>
        public void SetListener(IAmbientLifeListener listener) => _listener = listener;

        /// <summary>Spawns the chunk's individuals at its anchors (allocates: call when the chunk becomes active).</summary>
        public bool Add(WorldChunk chunk)
        {
            if (chunk == null || Contains(chunk))
                return false;
            bool added = false;
            _chunks.Add(chunk);
            var anchors = chunk.VfxAnchors;
            foreach (var s in _species)
            {
                if (string.IsNullOrEmpty(s.Def.anchorPrefix) || s.Def.mesh == null)
                    continue;
                foreach (var anchor in anchors)
                {
                    if (anchor == null || !anchor.name.StartsWith(s.Def.anchorPrefix, System.StringComparison.Ordinal))
                        continue;
                    if (s.Def.spawnChance < 1f && AnchorHash(anchor.position) >= s.Def.spawnChance)
                        continue;
                    for (int slot = 0; slot < s.Def.perAnchor; slot++)
                    {
                        Grow(s, s.Count + 1);
                        s.Agents[s.Count++] = Spawn(s.Def.behaviour, chunk, slot, anchor.position);
                        added = true;
                    }
                }
                UpdateBounds(s);
            }
            RebuildObstacles();
            // Birds start on free ground.
            foreach (var s in _species)
            {
                if (s.Def.behaviour != AmbientLifeBehaviour.Bird)
                    continue;
                for (int i = 0; i < s.Count; i++)
                {
                    ref var a = ref s.Agents[i];
                    if (a.Chunk != chunk)
                        continue;
                    a.Position.y = a.Ground.HeightAt(a.Position.x, a.Position.z);
                    for (int attempt = 0; attempt < 12 && !GroundFree(ref a, s, i, a.Position); attempt++)
                    {
                        var disc = RandomDisc(ref a.Random) * 1.2f;
                        a.Position = new Vector3(a.Home.x + disc.x, 0f, a.Home.z + disc.y);
                        a.Position.y = a.Ground.HeightAt(a.Position.x, a.Position.z);
                    }
                    a.Target = a.Position;
                }
            }
            return added;
        }

        public bool Remove(WorldChunk chunk)
        {
            bool removed = false;
            foreach (var s in _species)
            {
                int write = 0;
                for (int i = 0; i < s.Count; i++)
                {
                    if (s.Agents[i].Chunk == chunk)
                    {
                        removed = true;
                        continue;
                    }
                    s.Agents[write++] = s.Agents[i];
                }
                for (int i = write; i < s.Count; i++)
                    s.Agents[i] = default;
                s.Count = write;
                UpdateBounds(s);
            }
            if (_chunks.Remove(chunk))
                RebuildObstacles();
            return removed;
        }

        public void Clear()
        {
            foreach (var s in _species)
            {
                System.Array.Clear(s.Agents, 0, s.Count);
                s.Count = 0;
            }
            _chunks.Clear();
        }

        /// <summary>Everything within <paramref name="radius"/> of the point flees: insects dart away, birds fly off.</summary>
        public int Startle(Vector3 point, float radius)
        {
            int startled = 0;
            float radiusSq = radius * radius;
            foreach (var s in _species)
            {
                for (int i = 0; i < s.Count; i++)
                {
                    ref var a = ref s.Agents[i];
                    if (a.Slot >= s.ActiveSlots)
                        continue;
                    var offset = a.Position - point;
                    offset.y = 0f;
                    if (offset.sqrMagnitude > radiusSq)
                        continue;
                    var away = offset.sqrMagnitude > 1e-4f ? offset.normalized : RandomDirection(ref a.Random);
                    if (s.Def.behaviour == AmbientLifeBehaviour.Bird)
                    {
                        if (a.State == Fly || !TryTakeOff(ref a, s, i, away))
                            continue;
                    }
                    else
                    {
                        a.State = Flee;
                        a.Timer = 1.2f;
                        a.Velocity = away * (s.Def.behaviour == AmbientLifeBehaviour.Bee ? 2.2f : 1.6f) + Vector3.up * 0.7f;
                    }
                    startled++;
                }
            }
            return startled;
        }

        /// <summary>Moves the active individuals. No allocation.</summary>
        public void Tick(float deltaTime, float time)
        {
            using var marker = GameProfilerMarkers.AmbientLifeTick.Auto();
            float dt = Mathf.Min(deltaTime, 0.1f);
            int active = 0, airborne = 0;
            foreach (var s in _species)
            {
                for (int i = 0; i < s.Count; i++)
                {
                    ref var a = ref s.Agents[i];
                    if (a.Slot >= s.ActiveSlots)
                        continue;
                    active++;
                    switch (s.Def.behaviour)
                    {
                        case AmbientLifeBehaviour.Butterfly:
                            TickInsect(ref a, s.Radius, dt, 1.4f, -0.25f, 0.6f, 2f, 4.5f, 0.55f, 1.6f);
                            break;
                        case AmbientLifeBehaviour.Bee:
                            TickInsect(ref a, s.Radius, dt, 0.8f, -0.2f, 0.12f, 0.5f, 1.3f, 1.3f, 4f);
                            break;
                        case AmbientLifeBehaviour.Firefly:
                            // Slow wide drifts from the flowers' height up to under the crowns' reach.
                            TickInsect(ref a, s.Radius, dt, 1.3f, -0.1f, 0.85f, 2.5f, 5.5f, 0.3f, 0.9f);
                            break;
                        default:
                            TickBird(ref a, s, i, dt);
                            if (a.State == Fly)
                                airborne++;
                            break;
                    }
                }
                if (s.Def.behaviour != AmbientLifeBehaviour.Bird)
                    Separate(s);
            }
            LastActive = active;
            LastAirborne = airborne;
        }

        private RenderParams WithOverride(RenderParams source)
        {
            if (OverrideMaterial != null)
                source.material = OverrideMaterial;
            return source;
        }

        /// <summary>Submits the visible individuals for this frame. No allocation.</summary>
        public void Render(Camera camera, float time)
        {
            LastInstances = 0;
            LastDraws = 0;
            if (camera == null)
                return;
            using var marker = GameProfilerMarkers.AmbientLifeRender.Auto();
            GeometryUtility.CalculateFrustumPlanes(camera, _planes);
            foreach (var s in _species)
            {
                if (s.Count == 0 || s.ActiveSlots == 0 || !GeometryUtility.TestPlanesAABB(_planes, s.Bounds))
                    continue;
                int flying = 0, grounded = 0;
                bool bird = s.Def.behaviour == AmbientLifeBehaviour.Bird;
                var scale = new Vector3(s.Def.scale, s.Def.scale, s.Def.scale);
                for (int i = 0; i < s.Count; i++)
                {
                    ref var a = ref s.Agents[i];
                    if (a.Slot >= s.ActiveSlots)
                        continue;
                    var position = a.Position;
                    if (!bird)
                    {
                        // Bob in flight: butterflies float, bees buzz.
                        float bob = s.Def.behaviour == AmbientLifeBehaviour.Bee ? 0.025f : s.Def.behaviour == AmbientLifeBehaviour.Firefly ? 0.04f : 0.06f;
                        float rate = s.Def.behaviour == AmbientLifeBehaviour.Bee ? 13f : s.Def.behaviour == AmbientLifeBehaviour.Firefly ? 1.7f : 5.5f;
                        position.y += Mathf.Sin(time * rate + a.Seed * 6.28f) * bob;
                    }
                    var rotation = Quaternion.LookRotation(-a.Forward, Vector3.up) * Quaternion.Euler(-a.Pitch, 0f, 0f);
                    var matrix = Matrix4x4.TRS(position, rotation, scale);
                    if (bird && a.State != Fly)
                        s.Grounded[grounded++] = matrix;
                    else
                        s.Flying[flying++] = matrix;
                }
                s.FlyParams.camera = camera;
                s.GroundParams.camera = camera;
                if (flying > 0)
                {
                    Graphics.RenderMeshInstanced(WithOverride(s.FlyParams), s.Def.mesh, 0, s.Flying, flying);
                    LastDraws++;
                }
                if (grounded > 0)
                {
                    Graphics.RenderMeshInstanced(WithOverride(s.GroundParams), s.Def.mesh, 0, s.Grounded, grounded);
                    LastDraws++;
                }
                LastInstances += flying + grounded;
            }
        }

        /// <summary>
        /// Smallest free space between an active individual and a solid volume, an animal or one of its kind (m,
        /// negative: overlap). Tests check it never goes below zero.
        /// </summary>
        public float MinClearance() => Clearance(false, out _);

        /// <summary>Same, with what the closest pair is (tests: allocates strings).</summary>
        public float MinClearance(out string closest) => Clearance(true, out closest);

        private float Clearance(bool describe, out string closest)
        {
            float best = float.MaxValue;
            string where = null;
            void Keep(float gap, SpeciesState s, ref Agent a, string what)
            {
                if (gap >= best)
                    return;
                best = gap;
                if (describe)
                    where = $"{s.Def.id} ({StateName(s, ref a)}) at {a.Position} vs {what}";
            }
            foreach (var s in _species)
            {
                for (int i = 0; i < s.Count; i++)
                {
                    ref var a = ref s.Agents[i];
                    if (a.Slot >= s.ActiveSlots)
                        continue;
                    var c = new Vector2(a.Position.x, a.Position.z);
                    bool grounded = s.Def.behaviour == AmbientLifeBehaviour.Bird && a.State != Fly;
                    for (int k = 0; k < a.NearCount; k++)
                    {
                        ref var o = ref _obstacles[_near[a.NearStart + k]];
                        bool blocks = grounded
                            ? o.BlocksWalker(a.Position.y, BirdHeight)
                            : o.IsSolid && a.Position.y + s.Radius > o.WorldBottom && a.Position.y - s.Radius < o.WorldTop;
                        if (blocks)
                            Keep(Mathf.Sqrt(CollisionMath.PointSegmentDistanceSq(c, o.a, o.b)) - o.radius - s.Radius, s, ref a,
                                describe ? $"{o.kind} obstacle {o.Center} r{o.radius:F2} y{o.WorldBottom:F2}-{o.WorldTop:F2}" : null);
                    }
                    if (_animals != null)
                    {
                        for (int j = 0; j < _animals.Count; j++)
                        {
                            int handle = _animals.GetActiveHandle(j);
                            _animals.GetBody(handle, out var s0, out var s1, out float radius, out float groundY, out float height);
                            if (a.Position.y - s.Radius > groundY + height)
                                continue;
                            Keep(Mathf.Sqrt(CollisionMath.PointSegmentDistanceSq(c, s0, s1)) - radius - s.Radius, s, ref a, describe ? $"animal {handle}" : null);
                        }
                    }
                    for (int j = i + 1; j < s.Count; j++)
                    {
                        ref var b = ref s.Agents[j];
                        if (b.Slot < s.ActiveSlots)
                            Keep(Vector3.Distance(a.Position, b.Position) - 2f * s.Radius, s, ref a, describe ? $"its kind at {b.Position}" : null);
                    }
                }
            }
            closest = where;
            return best;
        }

        private static string StateName(SpeciesState s, ref Agent a) =>
            s.Def.behaviour != AmbientLifeBehaviour.Bird ? (a.State == Flee ? "flee" : "wander")
            : a.State == Fly ? "fly" : a.State == Hop ? "hop" : a.State == Peck ? "peck" : "idle";

        // ---------------- behaviours ----------------

        /// <summary>Heights are relative to the anchor, which marks the middle of the flight layer.</summary>
        private void TickInsect(ref Agent a, float bodyRadius, float dt, float radius, float minHeight, float maxHeight, float minTime, float maxTime,
            float speed, float response)
        {
            a.Timer -= dt;
            if (a.State == Flee)
            {
                a.Velocity = Vector3.Lerp(a.Velocity, a.Velocity.normalized * speed, dt * 1.5f);
                if (a.Timer <= 0f)
                {
                    a.State = Wander;
                    a.Timer = 0f;
                }
            }
            else
            {
                if (a.Timer <= 0f || (a.Target - a.Position).sqrMagnitude < 0.01f)
                {
                    // A free point of air: never inside a bush, a crown or a rock.
                    for (int attempt = 0; attempt < 4; attempt++)
                    {
                        var disc = RandomDisc(ref a.Random) * radius;
                        a.Target = a.Home + new Vector3(disc.x, Mathf.Lerp(minHeight, maxHeight, Next(ref a.Random)), disc.y);
                        if (!InsideVolume(ref a, a.Target, bodyRadius + 0.08f))
                            break;
                    }
                    a.Timer = Mathf.Lerp(minTime, maxTime, Next(ref a.Random));
                }
                var desired = a.Target - a.Position;
                float distance = desired.magnitude;
                var wanted = distance > 1e-4f ? desired * (speed * Mathf.Clamp01(distance * 2f + 0.3f) / distance) : Vector3.zero;
                a.Velocity = Vector3.Lerp(a.Velocity, wanted, Mathf.Clamp01(dt * response));
            }
            a.Position += a.Velocity * dt;
            // Never below the grass tops, never lost: a fleeing insect ends up circling home again.
            float floor = a.Home.y + minHeight;
            if (a.Position.y < floor)
                a.Position.y = floor;
            KeepOut(ref a, bodyRadius);
            var flat = new Vector3(a.Velocity.x, 0f, a.Velocity.z);
            if (flat.sqrMagnitude > 0.0025f)
                a.Forward = Vector3.Slerp(a.Forward, flat.normalized, Mathf.Clamp01(dt * 6f));
            a.Pitch = Mathf.Clamp(a.Velocity.y * 40f, -25f, 30f);
        }

        private void TickBird(ref Agent a, SpeciesState s, int index, float dt)
        {
            a.Timer += dt;
            a.Retry -= dt;
            // An animal coming near: the bird flies off, away from it.
            if (a.State != Fly && a.Retry <= 0f && NearestAnimal(a.Position, s.Radius + BirdShyDistance, out var animal))
            {
                var away = a.Position - animal;
                away.y = 0f;
                if (TryTakeOff(ref a, s, index, away.sqrMagnitude > 1e-4f ? away.normalized : RandomDirection(ref a.Random)))
                    return;
                a.Retry = 0.5f;
            }
            switch (a.State)
            {
                case Hop:
                {
                    float t = Mathf.Clamp01(a.Timer / HopDuration);
                    a.Position = Vector3.Lerp(a.From, a.Target, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.06f);
                    if (t >= 1f)
                        Rest(ref a);
                    break;
                }
                case Peck:
                {
                    float t = Mathf.Clamp01(a.Timer / PeckDuration);
                    a.Pitch = Mathf.Sin(t * Mathf.PI) * 38f;
                    if (t >= 1f)
                        Rest(ref a);
                    break;
                }
                case Fly:
                {
                    float t = Mathf.Clamp01(a.Timer / FlightDuration);
                    float u = 1f - t;
                    a.Position = u * u * a.From + 2f * u * t * a.Apex + t * t * a.Target;
                    var tangent = 2f * u * (a.Apex - a.From) + 2f * t * (a.Target - a.Apex);
                    var flat = new Vector3(tangent.x, 0f, tangent.z);
                    if (flat.sqrMagnitude > 1e-4f)
                        a.Forward = Vector3.Slerp(a.Forward, flat.normalized, Mathf.Clamp01(dt * 5f));
                    a.Pitch = Mathf.Clamp(-tangent.y * 6f, -25f, 25f);
                    // Coming down, an animal walks under the way or onto the landing spot: the bird pulls up and lands
                    // elsewhere; with no way out, it holds above the animal until it has passed.
                    if (t > 0.5f && t < 1f && (AnimalInTheWay(a.Position, s.Radius + 0.12f, out var below, out float top) || !GroundFree(ref a, s, index, a.Target)))
                    {
                        var away = a.Position - (below == default ? a.Target : below);
                        away.y = 0f;
                        if (TryTakeOff(ref a, s, index, away.sqrMagnitude > 1e-4f ? away.normalized : a.Forward))
                            break;
                        if (top > float.MinValue)
                        {
                            a.Timer -= dt;
                            a.Position.y = Mathf.Max(a.Position.y, top + s.Radius + 0.02f);
                            break;
                        }
                    }
                    if (t >= 1f)
                    {
                        a.Position = a.Target;
                        a.Pitch = 0f;
                        Rest(ref a);
                        // Someone took the landing spot meanwhile: off again.
                        if (!GroundFree(ref a, s, index, a.Position))
                            TryTakeOff(ref a, s, index, a.Forward);
                    }
                    break;
                }
                default:
                {
                    if (a.Timer < a.Duration)
                        break;
                    float roll = Next(ref a.Random);
                    if (roll < 0.62f)
                    {
                        // Hop: mostly forward, turning a little, drawn back toward home when it strays.
                        var toHome = a.Home - a.Position;
                        toHome.y = 0f;
                        var direction = toHome.sqrMagnitude > 1.2f * 1.2f
                            ? toHome.normalized
                            : Quaternion.Euler(0f, (Next(ref a.Random) - 0.5f) * 100f, 0f) * a.Forward;
                        var target = new Vector3(a.Position.x + direction.x * 0.14f, 0f, a.Position.z + direction.z * 0.14f);
                        target.y = a.Ground.HeightAt(target.x, target.z);
                        if (!GroundFree(ref a, s, index, target))
                        {
                            // A stone, a root, water or a neighbour in the way: it turns around instead.
                            a.Forward = Quaternion.Euler(0f, 150f + (Next(ref a.Random) - 0.5f) * 60f, 0f) * a.Forward;
                            Rest(ref a);
                            break;
                        }
                        a.Forward = direction;
                        a.From = a.Position;
                        a.Target = target;
                        a.State = Hop;
                    }
                    else if (roll < 0.92f)
                    {
                        a.State = Peck;
                    }
                    else
                    {
                        a.Forward = Quaternion.Euler(0f, (Next(ref a.Random) - 0.5f) * 220f, 0f) * a.Forward;
                        Rest(ref a);
                        break;
                    }
                    a.Timer = 0f;
                    break;
                }
            }
        }

        private static void Rest(ref Agent a)
        {
            a.State = Idle;
            a.Timer = 0f;
            a.Pitch = 0f;
            a.Duration = Mathf.Lerp(0.3f, 1.8f, Next(ref a.Random));
        }

        /// <summary>
        /// Flies off roughly away from a disturbance: the first direction (then a higher arc) whose whole curve clears
        /// every volume and that lands on free ground. False when none does (the bird stays).
        /// </summary>
        private bool TryTakeOff(ref Agent a, SpeciesState s, int index, Vector3 away)
        {
            float clearance = s.Radius + 0.1f;
            // An animal already over the bird (both placed there at scene start): the flight starts above its back.
            var from = a.Position;
            if (AnimalInTheWay(from, s.Radius, out _, out float animalTop) && from.y < animalTop + s.Radius)
                from.y = animalTop + s.Radius + 0.02f;
            for (int lift = 0; lift < 2; lift++)
            {
                float height = lift == 0 ? 3.4f : 5.6f;
                foreach (float angle in FlightAngles)
                {
                    var dir = Quaternion.Euler(0f, angle, 0f) * away;
                    var apex = from + dir * 2.2f + Vector3.up * height;
                    var disc = RandomDisc(ref a.Random) * 0.6f;
                    var target = new Vector3(a.Home.x + dir.x * 0.9f + disc.x, 0f, a.Home.z + dir.z * 0.9f + disc.y);
                    target.y = a.Ground.HeightAt(target.x, target.z);
                    if (!GroundFree(ref a, s, index, target) || !FlightClear(ref a, from, apex, target, clearance))
                        continue;
                    a.State = Fly;
                    a.Timer = 0f;
                    a.Position = from;
                    a.From = from;
                    a.Apex = apex;
                    a.Target = target;
                    a.Forward = dir;
                    _listener?.BirdTookOff(a.Position);
                    return true;
                }
            }
            return false;
        }

        private Agent Spawn(AmbientLifeBehaviour behaviour, WorldChunk chunk, int slot, Vector3 anchor)
        {
            _seed = _seed * 1664525u + 1013904223u;
            var a = new Agent { Chunk = chunk, Slot = slot, Random = _seed | 1u };
            a.Seed = Next(ref a.Random);
            var disc = RandomDisc(ref a.Random) * (behaviour == AmbientLifeBehaviour.Bird ? 0.5f : 0.6f);
            a.Home = anchor;
            a.Position = anchor + new Vector3(disc.x, 0f, disc.y);
            if (behaviour != AmbientLifeBehaviour.Bird)
                a.Position.y = anchor.y + (a.Seed - 0.5f) * 0.2f;
            var dir = RandomDisc(ref a.Random);
            a.Forward = dir.sqrMagnitude > 1e-4f ? new Vector3(dir.x, 0f, dir.y).normalized : Vector3.forward;
            a.Duration = Mathf.Lerp(0.2f, 1.5f, Next(ref a.Random));
            a.Target = a.Position;
            return a;
        }

        // ---------------- no-collision checks ----------------

        private Obstacle[] _extraObstacles = System.Array.Empty<Obstacle>();

        /// <summary>T35A: volumes outside the chunks (a zone's hedge and sign), world space. Rebuilds the near lists.</summary>
        public void SetExtraObstacles(Obstacle[] volumes)
        {
            _extraObstacles = volumes ?? System.Array.Empty<Obstacle>();
            RebuildObstacles();
        }

        /// <summary>All active chunks' obstacles, and for each agent the ones near its home (allocates: Add/Remove only).</summary>
        private void RebuildObstacles()
        {
            int total = _extraObstacles.Length;
            foreach (var chunk in _chunks)
                total += chunk != null ? chunk.ObstacleCount : 0;
            if (_obstacles.Length < total)
                _obstacles = new Obstacle[total];
            int count = 0;
            foreach (var chunk in _chunks)
            {
                if (chunk == null)
                    continue;
                var origin = chunk.transform.position;
                for (int i = 0; i < chunk.ObstacleCount; i++)
                    _obstacles[count++] = chunk.GetObstacle(i).Translated(origin);
            }
            for (int i = 0; i < _extraObstacles.Length; i++)
                _obstacles[count++] = _extraObstacles[i];
            var near = new List<int>();
            foreach (var s in _species)
            {
                for (int i = 0; i < s.Count; i++)
                {
                    ref var a = ref s.Agents[i];
                    a.Ground = a.Chunk != null ? a.Chunk.Ground : GroundSurface.Flat(a.Home.y);
                    a.NearStart = near.Count;
                    var home = new Vector2(a.Home.x, a.Home.z);
                    for (int k = 0; k < count; k++)
                    {
                        float span = _obstacles[k].Reach + NearRadius;
                        if ((_obstacles[k].Center - home).sqrMagnitude < span * span)
                            near.Add(k);
                    }
                    a.NearCount = near.Count - a.NearStart;
                }
            }
            _near = near.ToArray();
        }

        /// <summary>Inside a solid volume (inflated by r) near this agent. Fliers pass over water.</summary>
        private bool InsideVolume(ref Agent a, Vector3 p, float r)
        {
            if (InsideBubble(p, r))
                return true;
            var c = new Vector2(p.x, p.z);
            for (int k = 0; k < a.NearCount; k++)
            {
                ref var o = ref _obstacles[_near[a.NearStart + k]];
                if (!o.IsSolid || p.y + r < o.WorldBottom || p.y - r > o.WorldTop)
                    continue;
                float min = o.radius + r;
                if (CollisionMath.PointSegmentDistanceSq(c, o.a, o.b) < min * min)
                    return true;
            }
            return false;
        }

        private bool FlightClear(ref Agent a, Vector3 from, Vector3 apex, Vector3 to, float r)
        {
            for (int k = 1; k < 12; k++)
            {
                float t = k / 12f, u = 1f - t;
                if (InsideVolume(ref a, u * u * from + 2f * u * t * apex + t * t * to, r))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Pushes a flying insect out of any volume and any animal it entered (sideways to the nearest surface) and
        /// takes away the speed that carried it in: it glides along the leaves instead of passing through. Wedged
        /// between two of them, it rises over the lower one.
        /// </summary>
        private void KeepOut(ref Agent a, float r)
        {
            PushOutOfBubbles(ref a, r);
            for (int pass = 0; pass < 3; pass++)
            {
                if (!PushOutAll(ref a, r, out _))
                    return;
            }
            if (PushOutAll(ref a, r, out float top))
            {
                a.Position.y = top + r + 0.01f;
                if (a.Velocity.y < 0f)
                    a.Velocity.y = 0f;
            }
        }

        /// <summary>One push out of everything overlapping; true when something was hit (top: the highest one).</summary>
        private bool PushOutAll(ref Agent a, float r, out float top)
        {
            bool hit = false;
            top = float.MinValue;
            for (int k = 0; k < a.NearCount; k++)
            {
                ref var o = ref _obstacles[_near[a.NearStart + k]];
                if (!o.IsSolid || a.Position.y + r < o.WorldBottom || a.Position.y - r > o.WorldTop)
                    continue;
                if (PushOut(ref a, o.a, o.b, o.radius + r))
                {
                    hit = true;
                    top = Mathf.Max(top, o.WorldTop);
                }
            }
            if (_animals == null)
                return hit;
            for (int i = 0; i < _animals.Count; i++)
            {
                _animals.GetBody(_animals.GetActiveHandle(i), out var s0, out var s1, out float radius, out float groundY, out float height);
                if (a.Position.y - r > groundY + height)
                    continue;
                if (PushOut(ref a, s0, s1, radius + r))
                {
                    hit = true;
                    top = Mathf.Max(top, groundY + height);
                }
            }
            return hit;
        }

        private static bool PushOut(ref Agent a, Vector2 s0, Vector2 s1, float min)
        {
            var c = new Vector2(a.Position.x, a.Position.z);
            var closest = CollisionMath.ClosestOnSegment(c, s0, s1);
            var d = c - closest;
            float distance = d.magnitude;
            if (distance >= min)
                return false;
            var n = distance > 1e-4f ? d / distance : new Vector2(a.Forward.x, a.Forward.z);
            n = n.sqrMagnitude > 1e-6f ? n.normalized : Vector2.right;
            c = closest + n * (min + 0.0005f);
            a.Position.x = c.x;
            a.Position.z = c.y;
            var v = new Vector2(a.Velocity.x, a.Velocity.z);
            float inward = Vector2.Dot(v, n);
            if (inward < 0f)
            {
                v -= n * inward;
                a.Velocity.x = v.x;
                a.Velocity.z = v.y;
            }
            return true;
        }

        /// <summary>
        /// Insects of one species never fly through each other: overlapping pairs are pushed apart (and out of the
        /// leaves again), a few passes when a little crowd gathers on one flower patch.
        /// </summary>
        private void Separate(SpeciesState s)
        {
            float min = s.Radius * 2f;
            for (int pass = 0; pass < 4; pass++)
            {
                bool any = false;
                for (int i = 0; i < s.Count; i++)
                {
                    ref var a = ref s.Agents[i];
                    if (a.Slot >= s.ActiveSlots)
                        continue;
                    for (int j = i + 1; j < s.Count; j++)
                    {
                        ref var b = ref s.Agents[j];
                        if (b.Slot >= s.ActiveSlots)
                            continue;
                        var d = b.Position - a.Position;
                        float distanceSq = d.sqrMagnitude;
                        if (distanceSq >= min * min)
                            continue;
                        any = true;
                        float distance = Mathf.Sqrt(distanceSq);
                        var n = distance > 1e-4f ? d / distance : Vector3.up;
                        var shift = n * ((min - distance) * 0.5f + 0.0005f);
                        a.Position -= shift;
                        b.Position += shift;
                        KeepOut(ref a, s.Radius);
                        KeepOut(ref b, s.Radius);
                    }
                }
                if (!any)
                    return;
            }
        }

        /// <summary>Free ground for a bird: no prop at its feet, no water, no animal close, no other bird there.</summary>
        private bool GroundFree(ref Agent a, SpeciesState s, int self, Vector3 p)
        {
            var c = new Vector2(p.x, p.z);
            float r = s.Radius;
            for (int k = 0; k < a.NearCount; k++)
            {
                ref var o = ref _obstacles[_near[a.NearStart + k]];
                if (!o.BlocksWalker(p.y, BirdHeight))
                    continue;
                float min = o.radius + r;
                if (CollisionMath.PointSegmentDistanceSq(c, o.a, o.b) < min * min)
                    return false;
            }
            if (NearestAnimal(p, r + BirdShyDistance, out _))
                return false;
            for (int j = 0; j < s.Count; j++)
            {
                if (j == self)
                    continue;
                ref var b = ref s.Agents[j];
                if (b.Slot >= s.ActiveSlots || b.State == Fly)
                    continue;
                var spot = b.State == Hop ? b.Target : b.Position;
                if (new Vector2(spot.x - p.x, spot.z - p.z).sqrMagnitude < 4f * r * r)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// An animal body as high as this point and within <paramref name="distance"/> of it (seen from above): its
        /// root position and the top of its body (float.MinValue when none).
        /// </summary>
        private bool AnimalInTheWay(Vector3 p, float distance, out Vector3 root, out float top)
        {
            root = default;
            top = float.MinValue;
            if (_animals == null)
                return false;
            var c = new Vector2(p.x, p.z);
            for (int i = 0; i < _animals.Count; i++)
            {
                int handle = _animals.GetActiveHandle(i);
                _animals.GetBody(handle, out var s0, out var s1, out float radius, out float groundY, out float height);
                if (p.y - distance > groundY + height)
                    continue;
                float min = radius + distance;
                if (CollisionMath.PointSegmentDistanceSq(c, s0, s1) < min * min)
                {
                    root = _animals.GetPosition(handle);
                    top = groundY + height;
                    return true;
                }
            }
            return false;
        }

        /// <summary>An animal body within <paramref name="distance"/> of the point (seen from above); its root position.</summary>
        private bool NearestAnimal(Vector3 p, float distance, out Vector3 root)
        {
            root = default;
            if (_animals == null)
                return false;
            var c = new Vector2(p.x, p.z);
            for (int i = 0; i < _animals.Count; i++)
            {
                int handle = _animals.GetActiveHandle(i);
                _animals.GetBody(handle, out var s0, out var s1, out float radius, out _, out _);
                float min = radius + distance;
                if (CollisionMath.PointSegmentDistanceSq(c, s0, s1) < min * min)
                {
                    root = _animals.GetPosition(handle);
                    return true;
                }
            }
            return false;
        }

        // ---------------- helpers ----------------

        private static RenderParams Params(Material material, int row, Vector3 flap, Vector2 shoulder)
        {
            var properties = new MaterialPropertyBlock();
            properties.SetFloat(PaletteRowId, row);
            properties.SetVector(FlapId, new Vector4(flap.x, flap.y, flap.z, 0f));
            properties.SetVector(ShoulderId, new Vector4(shoulder.x, shoulder.y, 0f, 0f));
            return new RenderParams(material)
            {
                matProps = properties,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = true,
            };
        }

        private static void Grow(SpeciesState s, int needed)
        {
            if (needed <= s.Agents.Length)
                return;
            int size = Mathf.Max(needed, s.Agents.Length * 2);
            System.Array.Resize(ref s.Agents, size);
            System.Array.Resize(ref s.Flying, size);
            System.Array.Resize(ref s.Grounded, size);
        }

        private static void UpdateBounds(SpeciesState s)
        {
            if (s.Count == 0)
            {
                s.Bounds = default;
                return;
            }
            var bounds = new Bounds(s.Agents[0].Home, Vector3.zero);
            for (int i = 1; i < s.Count; i++)
                bounds.Encapsulate(s.Agents[i].Home);
            // Room for wandering, flights and the flight apex.
            bounds.Expand(new Vector3(7f, 9f, 7f));
            s.Bounds = bounds;
            s.FlyParams.worldBounds = bounds;
            s.GroundParams.worldBounds = bounds;
        }

        private bool Contains(WorldChunk chunk)
        {
            foreach (var s in _species)
            {
                for (int i = 0; i < s.Count; i++)
                {
                    if (s.Agents[i].Chunk == chunk)
                        return true;
                }
            }
            return false;
        }

        /// <summary>Stable 0-1 value per anchor position: a rare species always lives at the same anchors.</summary>
        private static float AnchorHash(Vector3 position)
        {
            uint h = (uint)Mathf.RoundToInt(position.x * 10f) * 73856093u ^ (uint)Mathf.RoundToInt(position.z * 10f) * 19349663u;
            h ^= h >> 13;
            h *= 0x5bd1e995u;
            h ^= h >> 15;
            return (h & 0xFFFFu) / 65536f;
        }

        private static float Next(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state & 0xFFFFFFu) / 16777216f;
        }

        private static Vector2 RandomDisc(ref uint state)
        {
            float angle = Next(ref state) * Mathf.PI * 2f;
            float radius = Mathf.Sqrt(Next(ref state));
            return new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        }

        private static Vector3 RandomDirection(ref uint state)
        {
            float angle = Next(ref state) * Mathf.PI * 2f;
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }
    }
}
