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
    /// </summary>
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
        }

        private readonly SpeciesState[] _species;
        private readonly Plane[] _planes = new Plane[6];
        private float _density = 1f;
        private uint _seed = 0x9E3779B9u;

        public AmbientLife(AmbientLifeKit kit, PaletteTable palettes, float density = 1f)
        {
            int count = kit != null && kit.material != null ? kit.species.Length : 0;
            _species = new SpeciesState[count];
            for (int i = 0; i < count; i++)
            {
                var def = kit.species[i];
                int row = palettes != null && palettes.TryGetRow(new ContentId(def.paletteId), out int r) ? r : 0;
                _species[i] = new SpeciesState
                {
                    Def = def,
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
                s.ActiveSlots = _density <= 0f ? 0 : Mathf.Clamp(Mathf.CeilToInt(s.Def.perAnchor * _density - 0.0001f), 1, s.Def.perAnchor);
        }

        /// <summary>Spawns the chunk's individuals at its anchors (allocates: call when the chunk becomes active).</summary>
        public bool Add(WorldChunk chunk)
        {
            if (chunk == null || Contains(chunk))
                return false;
            bool added = false;
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
            return removed;
        }

        public void Clear()
        {
            foreach (var s in _species)
            {
                System.Array.Clear(s.Agents, 0, s.Count);
                s.Count = 0;
            }
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
                        if (a.State == Fly)
                            continue;
                        TakeOff(ref a, away);
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
                            TickInsect(ref a, dt, 1.4f, -0.25f, 0.6f, 2f, 4.5f, 0.55f, 1.6f);
                            break;
                        case AmbientLifeBehaviour.Bee:
                            TickInsect(ref a, dt, 0.8f, -0.2f, 0.12f, 0.5f, 1.3f, 1.3f, 4f);
                            break;
                        default:
                            TickBird(ref a, dt);
                            if (a.State == Fly)
                                airborne++;
                            break;
                    }
                }
            }
            LastActive = active;
            LastAirborne = airborne;
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
                        float bob = s.Def.behaviour == AmbientLifeBehaviour.Bee ? 0.025f : 0.06f;
                        float rate = s.Def.behaviour == AmbientLifeBehaviour.Bee ? 13f : 5.5f;
                        position.y += Mathf.Sin(time * rate + a.Seed * 6.28f) * bob;
                    }
                    var rotation = Quaternion.LookRotation(-a.Forward, Vector3.up) * Quaternion.Euler(-a.Pitch, 0f, 0f);
                    var matrix = Matrix4x4.TRS(position, rotation, scale);
                    if (bird && a.State != Fly)
                        s.Grounded[grounded++] = matrix;
                    else
                        s.Flying[flying++] = matrix;
                }
                if (flying > 0)
                {
                    Graphics.RenderMeshInstanced(s.FlyParams, s.Def.mesh, 0, s.Flying, flying);
                    LastDraws++;
                }
                if (grounded > 0)
                {
                    Graphics.RenderMeshInstanced(s.GroundParams, s.Def.mesh, 0, s.Grounded, grounded);
                    LastDraws++;
                }
                LastInstances += flying + grounded;
            }
        }

        // ---------------- behaviours ----------------

        /// <summary>Heights are relative to the anchor, which marks the middle of the flight layer.</summary>
        private static void TickInsect(ref Agent a, float dt, float radius, float minHeight, float maxHeight, float minTime, float maxTime,
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
                    var disc = RandomDisc(ref a.Random) * radius;
                    a.Target = a.Home + new Vector3(disc.x, Mathf.Lerp(minHeight, maxHeight, Next(ref a.Random)), disc.y);
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
            var flat = new Vector3(a.Velocity.x, 0f, a.Velocity.z);
            if (flat.sqrMagnitude > 0.0025f)
                a.Forward = Vector3.Slerp(a.Forward, flat.normalized, Mathf.Clamp01(dt * 6f));
            a.Pitch = Mathf.Clamp(a.Velocity.y * 40f, -25f, 30f);
        }

        private static void TickBird(ref Agent a, float dt)
        {
            a.Timer += dt;
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
                    if (t >= 1f)
                    {
                        a.Position = a.Target;
                        a.Pitch = 0f;
                        Rest(ref a);
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
                        a.Forward = direction;
                        a.From = a.Position;
                        a.Target = new Vector3(a.Position.x + direction.x * 0.14f, a.Home.y, a.Position.z + direction.z * 0.14f);
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

        private static void TakeOff(ref Agent a, Vector3 away)
        {
            a.State = Fly;
            a.Timer = 0f;
            a.From = a.Position;
            a.Apex = a.Position + away * 2.2f + Vector3.up * 3.4f;
            var disc = RandomDisc(ref a.Random) * 0.6f;
            a.Target = new Vector3(a.Home.x + away.x * 0.9f + disc.x, a.Home.y, a.Home.z + away.z * 0.9f + disc.y);
            a.Forward = away;
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
