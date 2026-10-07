using System;
using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// Central scheduler for visible animals (SimulationScheduler in the Codebase Blueprint).
    /// One Tick per frame from a scene manager; views have no Update and no coroutine.
    /// <list type="bullet">
    /// <item>Decisions run in ticks: tier A agents about 5 times per second, tier B about once,
    /// sliced round-robin across frames so the cost per frame stays flat.</item>
    /// <item>Every agent keeps its own seeded timers, so neighbours never decide in the same frame.</item>
    /// <item>Only walking or turning agents touch their transform each frame (smooth motion between ticks).</item>
    /// <item>T25: a greeting reserves two animals and a meeting marker (two anchors on the line between them);
    /// both walk to their anchor, the first one waits facing the other, then both play Social nose to nose.
    /// Inspect and Eat may walk to a reserved point of interest (rock, tree, flower patch) and face it.</item>
    /// <item>T33C no-collision rule (Docs/Design/Collision_Rule.md): every body is a capsule measured from its mesh.
    /// No pose is ever taken if the body would enter a prop, the water, the egg spot or another animal: walks are
    /// planned around obstacles (one waypoint at most), animals sidestep each other, slide along an edge, and give up
    /// a blocked walk. They stand on the chunk's ground heights, tilted with the slope. No NavMesh, no physics.</item>
    /// </list>
    /// Preallocated arrays only: no allocation after construction. Views may be null (headless tests).
    /// </summary>
    public sealed class AnimalScheduler
    {
        public const int InvalidHandle = -1;
        public const int MaxInterests = 32;
        public const int MaxObstacles = 256;

        /// <summary>Bodies may touch: an overlap under this (m) is a contact, not a collision (float rounding).</summary>
        public const float ContactTolerance = 0.002f;

        private const uint RngSalt = 0x5C4ED001u;
        private const uint TemperamentSalt = 0x7E3A0001u;
        private const float ArriveDistance = 0.04f;
        // Approaches end exactly on their anchor: two greeting animals must stand socialGap apart.
        private const float ApproachArriveDistance = 0.01f;
        private const float MaxFacingErrorToWalk = 50f;
        private const float Acceleration = 2.5f;
        private const float FacedDegrees = 2f;
        private const float HappyAfterGreeting = 0.6f;
        private const float WaypointReach = 0.08f;
        private const float PathMargin = 0.03f;
        private const float DetourMargin = 0.08f;
        private const float LookAhead = 0.55f;
        private const float BlockedGiveUp = 0.8f;
        // A blocked approach this close to its goal counts as arrived (the partner's nose, the rock's side).
        private const float NearEnough = 0.3f;
        private const float MaxTiltDegrees = 14f;
        private const float NoseContact = 0.01f;
        private static readonly float[] SlideAngles = { 0f, 35f, -35f, 70f, -70f };
        private static readonly float[] DetourWidening = { 1f, 1.4f, 1.9f };
        private const int WanderCandidates = 6;
        // Neighbours counted around a wander target: walks lean toward the emptier side, so the herd spreads over the
        // whole area instead of piling up where animals are born. One neighbour more or less does not count.
        private const float SpreadRadius = 1.2f;
        /// <summary>A newborn bounds out of its shell: faster than a stroll, its hop clip sped up to match.</summary>
        public const float EmergeSpeedScale = 2.5f;

        private struct Agent
        {
            public Vector3 Position;
            public Vector3 Target;
            public Vector3 Waypoint;
            public Vector3 Facing;
            public Vector2 SegA;
            public Vector2 SegB;
            public float Yaw;
            public float Speed;
            public float StateEnd;
            public float Blocked;
            public float Scale;
            public float Radius;
            public float Reach;
            public float Height;
            public BodyShape Body;
            public uint Rng;
            public uint InstanceId;
            public int Partner;
            public int Interest;
            public AnimalBehaviorState State;
            public AnimalBehaviorState LastChoice;
            public AnimalBehaviorState Arrive;
            public Temperament Temperament;
            public SimulationTier Tier;
            public sbyte Side;
            public bool Turn;
            public bool HasWaypoint;
            public bool Emerging;
            public bool Newborn;
        }

        private readonly Agent[] _agents;
        private readonly AnimalView[] _views;
        private readonly BehaviorProfile[] _profiles;
        private readonly int[] _active;
        private readonly int[] _activeIndexBySlot;
        private readonly int[] _freeSlots;
        private readonly int[] _tierA;
        private readonly int[] _tierB;
        private readonly float[] _sortKeys;
        private readonly int[] _sortSlots;
        private readonly InterestPoint[] _interests = new InterestPoint[MaxInterests];
        private readonly int[] _interestOwner = new int[MaxInterests];
        private readonly Obstacle[] _obstacles = new Obstacle[MaxObstacles];
        private readonly Vector2[] _obstacleCenter = new Vector2[MaxObstacles];
        private readonly float[] _obstacleReach = new float[MaxObstacles];
        private readonly Vector3[] _candidates = new Vector3[WanderCandidates];
        private readonly int[] _candidateCrowd = new int[WanderCandidates];

        private SchedulerSettings _settings;
        private WanderArea _area;
        private GroundSurface _ground;
        private Vector3 _focus;
        private IAnimalVoiceListener _voices;
        private Vector2 _eggSpot;
        private float _eggRadius;
        private float _eggBodyRadius;
        private float _eggApron;
        private bool _hasEggSpot;
        private int _obstacleCount;
        private int _activeCount;
        private int _freeCount;
        private int _countA;
        private int _countB;
        private int _cursorA;
        private int _cursorB;
        private int _interestCount;
        private float _budgetA;
        private float _budgetB;
        private float _nextTierUpdate;
        private float _now;
        private bool _tiersDirty = true;

        public AnimalScheduler(int capacity, SchedulerSettings settings, WanderArea area)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
            _settings = settings;
            _area = area;
            _ground = GroundSurface.Flat(area.Center.y);
            _focus = area.Center;
            _agents = new Agent[capacity];
            _views = new AnimalView[capacity];
            _profiles = new BehaviorProfile[capacity];
            _active = new int[capacity];
            _activeIndexBySlot = new int[capacity];
            _freeSlots = new int[capacity];
            _tierA = new int[capacity];
            _tierB = new int[capacity];
            _sortKeys = new float[capacity];
            _sortSlots = new int[capacity];
            for (int i = 0; i < capacity; i++)
            {
                _activeIndexBySlot[i] = -1;
                _freeSlots[i] = capacity - 1 - i;
            }
            _freeCount = capacity;
            for (int i = 0; i < MaxInterests; i++)
                _interestOwner[i] = InvalidHandle;
        }

        public int Capacity { get; }
        public int Count => _activeCount;
        public int CountA => _countA;
        public int CountB => _countB;
        public SchedulerSettings Settings => _settings;

        /// <summary>Own decisions during the last Tick (a partner joining a greeting is not one). A synchronized spike shows up here first.</summary>
        public int StateChangesLastTick { get; private set; }

        /// <summary>Agents evaluated during the last Tick, A and B together.</summary>
        public int AgentsTickedLastTick { get; private set; }

        public long TotalStateChanges { get; private set; }
        public long TotalTicksA { get; private set; }
        public long TotalTicksB { get; private set; }

        /// <summary>Greeting pairs formed (both animals reserved).</summary>
        public long SocialInvitations { get; private set; }

        /// <summary>Greetings that reached the nose touch.</summary>
        public long SocialGreetings { get; private set; }

        /// <summary>Arrivals at a point of interest.</summary>
        public long InterestVisits { get; private set; }

        /// <summary>Walks that went around an obstacle through a waypoint.</summary>
        public long Detours { get; private set; }

        /// <summary>Walks ended early because the way stayed blocked (by another animal, mostly).</summary>
        public long BlockedWalks { get; private set; }

        public int InterestCount => _interestCount;
        public int ObstacleCount => _obstacleCount;

        public void SetSettings(SchedulerSettings settings)
        {
            _settings = settings;
            _tiersDirty = true;
        }

        public void SetArea(WanderArea area) => _area = area;

        /// <summary>The ground animals stand on (a chunk's height field). Flat at the area's height by default.</summary>
        public void SetGround(GroundSurface ground) => _ground = ground;

        /// <summary>
        /// World-space obstacles of the current chunk (copied: the ones a walking animal can meet, at most
        /// <see cref="MaxObstacles"/>). Animals already standing are not moved.
        /// </summary>
        public void SetObstacles(Obstacle[] obstacles)
        {
            _obstacleCount = 0;
            if (obstacles == null)
                return;
            foreach (var o in obstacles)
            {
                // Volumes starting above any animal's head (tree crowns, sails) never block a walk.
                if (o.kind == ObstacleKind.Solid && o.bottom > 1.5f)
                    continue;
                if (_obstacleCount == MaxObstacles)
                    break;
                _obstacles[_obstacleCount] = o;
                _obstacleCenter[_obstacleCount] = o.Center;
                _obstacleReach[_obstacleCount] = o.Reach;
                _obstacleCount++;
            }
        }

        /// <summary>
        /// The egg spot: no animal walks into <paramref name="radius"/>; a newborn registered as emerging bounds out of
        /// it. A new egg may appear once no body is within <paramref name="eggRadius"/> (the egg itself). No animal
        /// chooses to stop within <paramref name="apronRadius"/> of it (walking across is fine): however full the
        /// refuge, the newborn always has a way out and the next egg never waits.
        /// </summary>
        public void SetEggSpot(Vector3 center, float radius, float eggRadius, float apronRadius = 0f)
        {
            _eggSpot = new Vector2(center.x, center.z);
            _eggRadius = radius;
            _eggBodyRadius = Mathf.Min(eggRadius, radius);
            _eggApron = Mathf.Max(apronRadius, radius);
            _hasEggSpot = radius > 0f;
        }

        /// <summary>The spot lies in front of the egg, where animals pass but never settle.</summary>
        public bool InEggApron(Vector3 point) => InApron(point, 0f);

        /// <summary>A body of this reach standing here would have some part in front of the egg.</summary>
        private bool InApron(Vector3 point, float reach)
        {
            if (!_hasEggSpot)
                return false;
            float dx = point.x - _eggSpot.x, dz = point.z - _eggSpot.y;
            float limit = _eggApron + reach;
            return dx * dx + dz * dz < limit * limit;
        }

        public void ClearEggSpot() => _hasEggSpot = false;

        /// <summary>No animal body overlaps the egg's own footprint: a new egg may appear there.</summary>
        public bool IsEggSpotClear()
        {
            if (!_hasEggSpot)
                return true;
            for (int i = 0; i < _activeCount; i++)
            {
                ref var a = ref _agents[_active[i]];
                float min = a.Radius + _eggBodyRadius;
                if (CollisionMath.PointSegmentDistanceSq(_eggSpot, a.SegA, a.SegB) < min * min)
                    return false;
            }
            return true;
        }

        /// <summary>World-space points of interest of the current chunk (copied; at most <see cref="MaxInterests"/>).</summary>
        public void SetInterests(InterestPoint[] points)
        {
            for (int i = 0; i < _interestCount; i++)
                ReleaseInterestOwner(i);
            _interestCount = points == null ? 0 : Math.Min(points.Length, MaxInterests);
            for (int i = 0; i < _interestCount; i++)
            {
                _interests[i] = points[i];
                _interestOwner[i] = InvalidHandle;
            }
        }

        public InterestPoint GetInterestPoint(int index) => _interests[index];
        public int GetInterestOwner(int index) => _interestOwner[index];

        /// <summary>Point the camera looks at. Nearest animals become tier A at the next tier update.</summary>
        public void SetFocus(Vector3 focus) => _focus = focus;

        /// <summary>T34B: told when a near animal greets, rejoices or falls asleep (its voice). Null: silent.</summary>
        public void SetVoiceListener(IAnimalVoiceListener listener) => _voices = listener;

        /// <summary>
        /// Adds a configured view (or null for headless use). Returns a handle, or InvalidHandle when full.
        /// The pose is moved to the nearest free spot if the body would overlap anything there; an emerging animal
        /// (a newborn on the egg spot) stays where it is and first walks out of the spot.
        /// </summary>
        public int Register(AnimalView view, uint instanceId, uint seed, Vector3 position, float yaw, BehaviorProfile profile, float time,
            float bodyScale = 1f, bool emerging = false)
        {
            if (_freeCount == 0)
                return InvalidHandle;
            var p = profile ?? throw new ArgumentNullException(nameof(profile));
            int slot = _freeSlots[--_freeCount];

            uint rng = SeedHash.Mix(seed, RngSalt) | 1u;
            float scale = bodyScale > 0f ? bodyScale : 1f;
            var body = p.body;
            var agent = new Agent
            {
                Position = Grounded(_area.Clamp(position)),
                Yaw = yaw,
                InstanceId = instanceId,
                Rng = rng,
                Partner = InvalidHandle,
                Interest = InvalidHandle,
                State = AnimalBehaviorState.Idle,
                LastChoice = AnimalBehaviorState.Idle,
                Arrive = AnimalBehaviorState.Idle,
                Temperament = (Temperament)(SeedHash.Mix(seed, TemperamentSalt) % TemperamentTable.Count),
                Tier = SimulationTier.B,
                Scale = scale,
                Body = body,
                Radius = body.halfWidth * scale,
                Reach = body.Reach * scale,
                Height = body.height * scale,
            };
            agent.Target = agent.Position;
            // First decision anywhere inside one idle window: a freshly spawned group never decides together.
            agent.StateEnd = time + Range(ref agent.Rng, 0.2f, p.idleSeconds.y);

            bool walkOut = false;
            if (emerging)
            {
                if (!Fits(slot, ref agent, agent.Position, agent.Yaw, true))
                {
                    agent.Emerging = true;
                    walkOut = true;
                }
            }
            else if (!Fits(slot, ref agent, agent.Position, agent.Yaw, true) || InApron(agent.Position, agent.Reach))
            {
                agent.Position = FindFreeSpot(slot, ref agent, agent.Position, agent.Yaw);
            }
            UpdateSegment(ref agent);

            _agents[slot] = agent;
            _views[slot] = view;
            _profiles[slot] = p;
            _activeIndexBySlot[slot] = _activeCount;
            _active[_activeCount++] = slot;
            _tiersDirty = true;
            ApplyPose(slot, ref _agents[slot]);

            if (walkOut)
                StartExit(slot, ref _agents[slot], p, time);
            return slot;
        }

        /// <summary>The newborn walks toward the nearest free place outside the egg spot (again after any blocked try).</summary>
        private void StartExit(int slot, ref Agent agent, BehaviorProfile profile, float time)
        {
            agent.Newborn = true;
            agent.Emerging = false;
            // Straight ahead first (it faces the camera when revealed), checked with the heading of the walk there.
            var exit = FindFreeSpot(slot, ref agent, agent.Position, agent.Yaw, true);
            agent.Emerging = true;
            if (Flat(exit - agent.Position).sqrMagnitude < 1e-4f)
            {
                // Nowhere to go yet (crowded): the animals resting in front of the egg move on, and it tries again
                // at its next decision.
                ClearApron(slot, time);
                Play(slot, ref agent, profile, AnimalBehaviorState.Idle, time, false);
                return;
            }
            StartApproach(slot, ref agent, profile, exit, exit + (exit - agent.Position), AnimalBehaviorState.Idle, time, false, exit, false);
        }

        public void Unregister(int handle)
        {
            if (!IsValid(handle))
                return;
            // A greeting partner or a reserved point never stays locked by an animal that left.
            EndActivity(handle, ref _agents[handle], _now);
            int index = _activeIndexBySlot[handle];
            int lastSlot = _active[--_activeCount];
            _active[index] = lastSlot;
            _activeIndexBySlot[lastSlot] = index;
            _activeIndexBySlot[handle] = -1;
            _views[handle] = null;
            _profiles[handle] = null;
            _freeSlots[_freeCount++] = handle;
            RebuildTierLists();
        }

        public bool IsValid(int handle) => handle >= 0 && handle < Capacity && _activeIndexBySlot[handle] >= 0;

        public AnimalBehaviorState GetState(int handle) => _agents[handle].State;
        public SimulationTier GetTier(int handle) => _agents[handle].Tier;
        public Temperament GetTemperament(int handle) => _agents[handle].Temperament;
        public Vector3 GetPosition(int handle) => _agents[handle].Position;
        public float GetYaw(int handle) => _agents[handle].Yaw;
        public uint GetInstanceId(int handle) => _agents[handle].InstanceId;
        public int GetPartner(int handle) => _agents[handle].Partner;
        public int GetInterest(int handle) => _agents[handle].Interest;
        public bool IsEmerging(int handle) => _agents[handle].Emerging;

        /// <summary>Handle of the i-th registered animal (0 to <see cref="Count"/> - 1), for neighbours such as the ambient life.</summary>
        public int GetActiveHandle(int index) => _active[index];

        /// <summary>The animal's body seen from above (capsule axis and radius), its ground height and height.</summary>
        public void GetBody(int handle, out Vector2 start, out Vector2 end, out float radius, out float groundY, out float height)
        {
            ref var a = ref _agents[handle];
            start = a.SegA;
            end = a.SegB;
            radius = a.Radius;
            groundY = a.Position.y;
            height = a.Height;
        }

        /// <summary>Clearance never reports more than this (m): anything farther is skipped by a bounding test.</summary>
        public const float ClearanceHorizon = 1f;

        /// <summary>
        /// Smallest free space between this animal's body and an obstacle, the egg spot (unless it is emerging) or
        /// another animal (m, negative: overlap; at most <see cref="ClearanceHorizon"/>). Tests and the benchmark
        /// check it never goes below the tolerance.
        /// </summary>
        public float Clearance(int handle)
        {
            ref var a = ref _agents[handle];
            var center = new Vector2(a.Position.x, a.Position.z);
            float best = ClearanceHorizon;
            for (int i = 0; i < _obstacleCount; i++)
            {
                float span = a.Reach + _obstacleReach[i] + ClearanceHorizon;
                if ((_obstacleCenter[i] - center).sqrMagnitude >= span * span)
                    continue;
                ref var o = ref _obstacles[i];
                if (o.BlocksWalker(a.Position.y, a.Height))
                    best = Mathf.Min(best, CollisionMath.Gap(a.SegA, a.SegB, a.Radius, o));
            }
            if (_hasEggSpot && !a.Emerging)
                best = Mathf.Min(best, Mathf.Sqrt(CollisionMath.PointSegmentDistanceSq(_eggSpot, a.SegA, a.SegB)) - a.Radius - _eggRadius);
            for (int i = 0; i < _activeCount; i++)
            {
                int other = _active[i];
                if (other == handle)
                    continue;
                ref var o = ref _agents[other];
                float span = a.Reach + o.Reach + ClearanceHorizon;
                if ((new Vector2(o.Position.x, o.Position.z) - center).sqrMagnitude >= span * span)
                    continue;
                best = Mathf.Min(best, CollisionMath.Gap(a.SegA, a.SegB, a.Radius, o.SegA, o.SegB, o.Radius));
            }
            return best;
        }

        /// <summary>The player touched this animal: immediate reaction, outside the tick cadence.</summary>
        public void Poke(int handle, float time)
        {
            if (!IsValid(handle))
                return;
            ref var agent = ref _agents[handle];
            var profile = _profiles[handle];
            EndActivity(handle, ref agent, time);
            if (agent.State == AnimalBehaviorState.Sleep)
                Play(handle, ref agent, profile, AnimalBehaviorState.Wake, time, false);
            else if (agent.State != AnimalBehaviorState.React)
                Play(handle, ref agent, profile, AnimalBehaviorState.React, time, false);
        }

        /// <summary>
        /// Debug and tests: this animal looks for a greeting partner now, whatever its temperament.
        /// Returns true when a pair formed (both reserved and walking to their anchors).
        /// </summary>
        public bool TryStartSocial(int handle, float time)
        {
            if (!IsValid(handle) || _agents[handle].Partner >= 0)
                return false;
            ref var agent = ref _agents[handle];
            EndActivity(handle, ref agent, time);
            return TryFormPair(handle, ref agent, _profiles[handle], time, true);
        }

        public void Tick(float time, float deltaTime)
        {
            _now = time;
            StateChangesLastTick = 0;
            AgentsTickedLastTick = 0;
            if (_activeCount == 0)
                return;

            if (_tiersDirty || time >= _nextTierUpdate)
            {
                UpdateTiers();
                _nextTierUpdate = time + _settings.TierUpdateInterval;
            }

            using (GameProfilerMarkers.SchedulerTickA.Auto())
            {
                int n = Slice(ref _budgetA, _countA, _settings.TickRateA, deltaTime);
                for (int i = 0; i < n; i++)
                {
                    _cursorA = _cursorA + 1 >= _countA ? 0 : _cursorA + 1;
                    TickAgent(_tierA[_cursorA], time);
                }
                TotalTicksA += n;
                AgentsTickedLastTick += n;
                for (int i = 0; i < _countA; i++)
                    Move(_tierA[i], time, deltaTime);
            }

            using (GameProfilerMarkers.SchedulerTickB.Auto())
            {
                int n = Slice(ref _budgetB, _countB, _settings.TickRateB, deltaTime);
                for (int i = 0; i < n; i++)
                {
                    _cursorB = _cursorB + 1 >= _countB ? 0 : _cursorB + 1;
                    TickAgent(_tierB[_cursorB], time);
                }
                TotalTicksB += n;
                AgentsTickedLastTick += n;
                for (int i = 0; i < _countB; i++)
                    Move(_tierB[i], time, deltaTime);
            }
        }

        private static int Slice(ref float budget, int count, float rate, float deltaTime)
        {
            if (count == 0)
            {
                budget = 0f;
                return 0;
            }
            budget += count * rate * deltaTime;
            int n = (int)budget;
            budget -= n;
            if (n > count)
            {
                n = count;
                budget = 0f;
            }
            return n;
        }

        private void TickAgent(int slot, float time)
        {
            ref var agent = ref _agents[slot];
            if (time < agent.StateEnd)
                return;

            var profile = _profiles[slot];
            if (agent.Emerging)
            {
                // Still on the egg spot: nothing else until it has walked off.
                EndActivity(slot, ref agent, time);
                StartExit(slot, ref agent, profile, time);
                return;
            }
            switch (agent.State)
            {
                case AnimalBehaviorState.Sleep:
                    Play(slot, ref agent, profile, AnimalBehaviorState.Wake, time, false);
                    break;
                case AnimalBehaviorState.React:
                    bool happy = Next01(ref agent.Rng) < TemperamentTable.HappyChance(agent.Temperament);
                    Play(slot, ref agent, profile, happy ? AnimalBehaviorState.Happy : AnimalBehaviorState.Idle, time, false);
                    break;
                case AnimalBehaviorState.Wander:
                case AnimalBehaviorState.SocialWait:
                    // Timed out before arriving (area edge) or the partner never came: settle down.
                    EndActivity(slot, ref agent, time);
                    Play(slot, ref agent, profile, AnimalBehaviorState.Idle, time, false);
                    break;
                case AnimalBehaviorState.Social:
                    EndActivity(slot, ref agent, time);
                    bool glad = Next01(ref agent.Rng) < TemperamentTable.HappyChance(agent.Temperament) * HappyAfterGreeting;
                    Play(slot, ref agent, profile, glad ? AnimalBehaviorState.Happy : AnimalBehaviorState.Idle, time, false);
                    break;
                default:
                    EndActivity(slot, ref agent, time);
                    // Standing in front of the egg (an old save, a walk cut short): it moves on before anything else.
                    Enter(slot, ref agent, profile, InApron(agent.Position, agent.Reach) ? AnimalBehaviorState.Wander : Choose(ref agent, profile), time);
                    break;
            }
        }

        /// <summary>A newborn cannot get out: every animal resting in front of the egg walks away now.</summary>
        private void ClearApron(int newborn, float time)
        {
            for (int i = 0; i < _activeCount; i++)
            {
                int other = _active[i];
                if (other == newborn)
                    continue;
                ref var o = ref _agents[other];
                if (o.Emerging || o.State == AnimalBehaviorState.Wander || !InApron(o.Position, o.Reach))
                    continue;
                EndActivity(other, ref o, time);
                Enter(other, ref o, _profiles[other], AnimalBehaviorState.Wander, time);
            }
        }

        private AnimalBehaviorState Choose(ref Agent agent, BehaviorProfile profile)
        {
            bool tierB = agent.Tier == SimulationTier.B;
            float total = 0f;
            for (int c = 0; c < BehaviorProfile.ChoiceCount; c++)
                total += ChoiceWeight(ref agent, profile, c, tierB);
            if (total <= 0f)
                return AnimalBehaviorState.Idle;

            float roll = Next01(ref agent.Rng) * total;
            for (int c = 0; c < BehaviorProfile.ChoiceCount; c++)
            {
                roll -= ChoiceWeight(ref agent, profile, c, tierB);
                if (roll < 0f)
                    return BehaviorProfile.ChoiceState(c);
            }
            return AnimalBehaviorState.Idle;
        }

        private static float ChoiceWeight(ref Agent agent, BehaviorProfile profile, int choice, bool tierB)
        {
            var state = BehaviorProfile.ChoiceState(choice);
            // Never repeat the same activity twice in a row; Idle is the resting point.
            if (state != AnimalBehaviorState.Idle && state == agent.LastChoice)
                return 0f;
            if (tierB)
            {
                if (state == AnimalBehaviorState.IdleLook || state == AnimalBehaviorState.Inspect || state == AnimalBehaviorState.Social)
                    return 0f;
                if (state == AnimalBehaviorState.Wander)
                    return profile.Weight(choice) * profile.tierBWanderScale * TemperamentTable.Multiplier(agent.Temperament, choice);
            }
            return profile.Weight(choice) * TemperamentTable.Multiplier(agent.Temperament, choice);
        }

        /// <summary>A decision: some activities first walk somewhere (a partner, a point of interest).</summary>
        private void Enter(int slot, ref Agent agent, BehaviorProfile profile, AnimalBehaviorState state, float time)
        {
            switch (state)
            {
                case AnimalBehaviorState.Wander:
                    if (TryPickWanderTarget(slot, ref agent, profile))
                    {
                        agent.Arrive = AnimalBehaviorState.Idle;
                        Play(slot, ref agent, profile, AnimalBehaviorState.Wander, time, false);
                    }
                    else
                    {
                        Play(slot, ref agent, profile, AnimalBehaviorState.Idle, time, false);
                    }
                    return;
                case AnimalBehaviorState.Social:
                    if (!TryFormPair(slot, ref agent, profile, time, false))
                    {
                        // Nobody around (or a shy neighbour): looks around instead, and will not retry at once.
                        Play(slot, ref agent, profile, AnimalBehaviorState.IdleLook, time, false);
                        agent.LastChoice = AnimalBehaviorState.Social;
                    }
                    return;
                case AnimalBehaviorState.Inspect:
                    if (agent.Tier == SimulationTier.A && TryApproachInterest(slot, ref agent, profile, InterestKind.Inspect, time))
                        return;
                    break;
                case AnimalBehaviorState.Eat:
                    if (agent.Tier == SimulationTier.A && Next01(ref agent.Rng) < profile.eatAtPointChance
                        && TryApproachInterest(slot, ref agent, profile, InterestKind.Eat, time))
                        return;
                    break;
            }
            Play(slot, ref agent, profile, state, time, false);
        }

        /// <summary>Starts a state now. induced: a consequence of another animal's decision (greeting partner).</summary>
        private void Play(int slot, ref Agent agent, BehaviorProfile profile, AnimalBehaviorState state, float time, bool induced)
        {
            float scale = agent.Tier == SimulationTier.B ? profile.tierBDurationScale : 1f;
            int animatorState;
            float fade = profile.fade;
            float duration;

            switch (state)
            {
                case AnimalBehaviorState.IdleLook:
                    animatorState = AnimalStateHashes.IdleLook;
                    duration = profile.idleLookSeconds;
                    break;
                case AnimalBehaviorState.Wander:
                    animatorState = AnimalStateHashes.Move;
                    fade = profile.moveFade;
                    float distance = agent.HasWaypoint
                        ? Flat(agent.Waypoint - agent.Position).magnitude + Flat(agent.Target - agent.Waypoint).magnitude
                        : Flat(agent.Target - agent.Position).magnitude;
                    // Generous timeout: turning, easing and sidestepping take longer than distance / speed.
                    duration = distance / profile.moveSpeed * 2f + 1.5f;
                    agent.Speed = 0f;
                    agent.Blocked = 0f;
                    agent.Side = 0;
                    break;
                case AnimalBehaviorState.Eat:
                    animatorState = AnimalStateHashes.Eat;
                    duration = Range(ref agent.Rng, profile.eatSeconds.x, profile.eatSeconds.y) * scale;
                    break;
                case AnimalBehaviorState.Sleep:
                    animatorState = AnimalStateHashes.SleepEnter;
                    duration = Range(ref agent.Rng, profile.sleepSeconds.x, profile.sleepSeconds.y) * scale;
                    break;
                case AnimalBehaviorState.Wake:
                    animatorState = AnimalStateHashes.Wake;
                    duration = profile.wakeSeconds;
                    break;
                case AnimalBehaviorState.Inspect:
                    animatorState = AnimalStateHashes.Inspect;
                    duration = profile.inspectSeconds;
                    break;
                case AnimalBehaviorState.React:
                    animatorState = AnimalStateHashes.React;
                    fade = profile.reactFade;
                    duration = profile.reactSeconds;
                    break;
                case AnimalBehaviorState.Happy:
                    animatorState = AnimalStateHashes.Happy;
                    duration = profile.happySeconds;
                    break;
                case AnimalBehaviorState.Social:
                    animatorState = AnimalStateHashes.Social;
                    duration = profile.socialSeconds;
                    break;
                case AnimalBehaviorState.SocialWait:
                    animatorState = AnimalStateHashes.Idle;
                    duration = profile.socialWaitSeconds;
                    break;
                default:
                    state = AnimalBehaviorState.Idle;
                    animatorState = AnimalStateHashes.Idle;
                    duration = Range(ref agent.Rng, profile.idleSeconds.x, profile.idleSeconds.y) * scale;
                    // A walk that ended in front of the egg (blocked, timed out): only a short pause before moving on.
                    if (InApron(agent.Position, agent.Reach))
                        duration = Mathf.Min(duration, 0.6f);
                    break;
            }

            if (state != AnimalBehaviorState.Wander)
            {
                agent.Speed = 0f;
                agent.HasWaypoint = false;
            }
            // An approach walk counts as the activity it leads to.
            var choice = state == AnimalBehaviorState.Wander && agent.Arrive != AnimalBehaviorState.Idle ? agent.Arrive : state;
            if (choice != AnimalBehaviorState.React && choice != AnimalBehaviorState.Wake && choice != AnimalBehaviorState.Happy
                && choice != AnimalBehaviorState.SocialWait)
                agent.LastChoice = choice;

            // One-shots return to Idle inside the Animator by exit time: replaying Idle would restart the loop.
            bool animatorAlreadyIdle = animatorState == AnimalStateHashes.Idle && ReturnsToIdle(agent.State);
            agent.State = state;
            agent.StateEnd = time + duration;
            if (!induced)
                StateChangesLastTick++;
            TotalStateChanges++;

            var view = _views[slot];
            if (view != null)
            {
                if (!animatorAlreadyIdle)
                    view.PlayState(animatorState, fade);
                // The bound out of the shell plays the hop faster, so paws keep pace with the ground.
                view.SetPlaybackSpeed(state == AnimalBehaviorState.Wander && agent.Emerging ? EmergeSpeedScale : 1f);
                // A greeting is voiced once, by the animal that started it (its partner follows, induced).
                if (_voices != null && !induced && agent.Tier == SimulationTier.A
                    && (state == AnimalBehaviorState.Happy || state == AnimalBehaviorState.Social || state == AnimalBehaviorState.Sleep
                        || state == AnimalBehaviorState.Eat))
                    _voices.AnimalVoice(view, state, agent.Position);
            }
        }

        private static bool ReturnsToIdle(AnimalBehaviorState state)
        {
            switch (state)
            {
                case AnimalBehaviorState.Idle:
                case AnimalBehaviorState.IdleLook:
                case AnimalBehaviorState.Inspect:
                case AnimalBehaviorState.React:
                case AnimalBehaviorState.Happy:
                case AnimalBehaviorState.Wake:
                case AnimalBehaviorState.Social:
                case AnimalBehaviorState.SocialWait:
                    return true;
                default:
                    return false;
            }
        }

        // ---------------- Greetings ----------------

        /// <summary>
        /// Nearest free tier-A neighbour of the same social group, idle or looking around, accepts by temperament.
        /// The meeting marker is two anchors on the line between them, socialGap apart (never closer than the two
        /// noses); both anchors must be free of obstacles and reachable.
        /// </summary>
        private bool TryFormPair(int slot, ref Agent agent, BehaviorProfile profile, float time, bool ignoreTemperament)
        {
            if (agent.Tier != SimulationTier.A || profile.socialWeight <= 0f)
                return false;
            float best = profile.socialRadius * profile.socialRadius;
            int partner = InvalidHandle;
            for (int i = 0; i < _activeCount; i++)
            {
                int other = _active[i];
                if (other == slot)
                    continue;
                ref var o = ref _agents[other];
                var op = _profiles[other];
                if (o.Tier != SimulationTier.A || o.Partner >= 0 || o.Emerging || op.socialWeight <= 0f || op.socialGroup != profile.socialGroup)
                    continue;
                if (o.State != AnimalBehaviorState.Idle && o.State != AnimalBehaviorState.IdleLook)
                    continue;
                float d2 = Flat(o.Position - agent.Position).sqrMagnitude;
                if (d2 < best)
                {
                    best = d2;
                    partner = other;
                }
            }
            if (partner == InvalidHandle)
                return false;
            ref var p = ref _agents[partner];
            if (!ignoreTemperament && Next01(ref agent.Rng) >= TemperamentTable.AcceptChance(p.Temperament))
                return false;

            var delta = Flat(p.Position - agent.Position);
            float distance = delta.magnitude;
            float yaw = agent.Yaw * Mathf.Deg2Rad;
            var dir = distance > 1e-3f ? delta / distance : new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
            float gap = Mathf.Max(0.5f * (profile.socialGap + _profiles[partner].socialGap),
                agent.Body.front * agent.Scale + p.Body.front * p.Scale + NoseContact);
            var middle = _area.Clamp((agent.Position + p.Position) * 0.5f);
            var anchorA = Grounded(_area.Clamp(middle - dir * (gap * 0.5f)));
            var anchorB = Grounded(_area.Clamp(middle + dir * (gap * 0.5f)));
            if (Flat(anchorB - anchorA).magnitude < gap - 0.005f)
                return false; // squeezed by the area edge: noses would overlap
            if (InApron(anchorA, agent.Reach) || InApron(anchorB, p.Reach))
                return false; // never a greeting in front of the egg
            if (!Fits(slot, ref agent, anchorA, YawTo(anchorA, anchorB), false) || !Fits(partner, ref p, anchorB, YawTo(anchorB, anchorA), false))
                return false;
            if (!TryPlan(slot, ref agent, anchorA, out var waypointA, out bool detourA)
                || !TryPlan(partner, ref p, anchorB, out var waypointB, out bool detourB))
                return false;

            EndActivity(partner, ref p, time);
            agent.Partner = partner;
            p.Partner = slot;
            SocialInvitations++;
            StartApproach(slot, ref agent, profile, anchorA, anchorB, AnimalBehaviorState.Social, time, false, waypointA, detourA);
            // The approach above may already end in a greeting if the partner was waiting: check the link first.
            if (p.Partner == slot && p.State != AnimalBehaviorState.Social)
                StartApproach(partner, ref p, _profiles[partner], anchorB, anchorA, AnimalBehaviorState.Social, time, true, waypointB, detourB);
            return true;
        }

        // ---------------- Points of interest ----------------

        private bool TryApproachInterest(int slot, ref Agent agent, BehaviorProfile profile, InterestKind kind, float time)
        {
            if (_interestCount == 0)
                return false;
            float reach = profile.interestRadius * profile.interestRadius;
            int pick = InvalidHandle;
            int seen = 0;
            for (int i = 0; i < _interestCount; i++)
            {
                if (_interests[i].kind != kind || _interestOwner[i] != InvalidHandle)
                    continue;
                if (Flat(_interests[i].position - agent.Position).sqrMagnitude > reach)
                    continue;
                // Uniform pick among the candidates without a list.
                seen++;
                if (Next01(ref agent.Rng) * seen < 1f)
                    pick = i;
            }
            if (pick == InvalidHandle)
                return false;

            var point = _interests[pick];
            var toPoint = Flat(point.position - agent.Position);
            float distance = toPoint.magnitude;
            float yaw = agent.Yaw * Mathf.Deg2Rad;
            var dir = distance > 1e-3f ? toPoint / distance : new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
            var wanted = point.position - dir * point.standOff;
            var stand = Grounded(_area.Clamp(wanted));
            // A point outside the area would leave the animal staring from too far away.
            if (Flat(wanted - stand).magnitude > 0.35f)
                return false;
            // The body stops at the prop's side, nose toward it: step back until it fits.
            float faceYaw = YawTo(stand, point.position);
            int steps = 0;
            while (!Fits(slot, ref agent, stand, faceYaw, false))
            {
                if (++steps > 30)
                    return false;
                stand = Grounded(_area.Clamp(stand - dir * 0.04f));
            }
            // Someone already stands there, or it is in front of the egg: inspect or eat where it is instead.
            if (InApron(stand, agent.Reach) || Crowded(slot, stand, profile.personalSpace * profile.personalSpace))
                return false;
            if (!TryPlan(slot, ref agent, stand, out var waypoint, out bool detour))
                return false;

            _interestOwner[pick] = slot;
            agent.Interest = pick;
            StartApproach(slot, ref agent, profile, stand, point.position,
                kind == InterestKind.Eat ? AnimalBehaviorState.Eat : AnimalBehaviorState.Inspect, time, false, waypoint, detour);
            return true;
        }

        private void ReleaseInterestOwner(int index)
        {
            int owner = _interestOwner[index];
            _interestOwner[index] = InvalidHandle;
            if (owner >= 0 && owner < Capacity && _agents[owner].Interest == index)
                _agents[owner].Interest = InvalidHandle;
        }

        // ---------------- Walks, arrivals, endings ----------------

        private void StartApproach(int slot, ref Agent agent, BehaviorProfile profile, Vector3 target, Vector3 facing,
            AnimalBehaviorState arrive, float time, bool induced, Vector3 waypoint, bool hasWaypoint)
        {
            agent.Target = target;
            agent.Facing = facing;
            agent.Arrive = arrive;
            agent.Waypoint = waypoint;
            agent.HasWaypoint = hasWaypoint;
            if (hasWaypoint)
                Detours++;
            if (!hasWaypoint && Flat(target - agent.Position).sqrMagnitude <= ApproachArriveDistance * ApproachArriveDistance)
            {
                Arrive(slot, ref agent, profile, time, induced);
                return;
            }
            Play(slot, ref agent, profile, AnimalBehaviorState.Wander, time, induced);
        }

        private void Arrive(int slot, ref Agent agent, BehaviorProfile profile, float time, bool induced)
        {
            if (agent.Emerging && OverlapsEggSpot(ref agent))
            {
                StartExit(slot, ref agent, profile, time);
                return;
            }
            if (agent.Newborn && !agent.Emerging)
            {
                // Out of its shell, it looks around a moment and goes exploring: the space before the egg stays open.
                agent.Newborn = false;
                agent.Arrive = AnimalBehaviorState.Idle;
                agent.HasWaypoint = false;
                Play(slot, ref agent, profile, AnimalBehaviorState.Idle, time, induced);
                // Still in front of the egg: a quick look only, the next newborn needs the way.
                agent.StateEnd = time + (InApron(agent.Position, agent.Reach) ? Range(ref agent.Rng, 0.15f, 0.4f) : Range(ref agent.Rng, 0.3f, 1f));
                agent.LastChoice = AnimalBehaviorState.Idle;
                return;
            }
            var arrive = agent.Arrive;
            agent.Arrive = AnimalBehaviorState.Idle;
            agent.HasWaypoint = false;
            switch (arrive)
            {
                case AnimalBehaviorState.Inspect:
                case AnimalBehaviorState.Eat:
                    InterestVisits++;
                    agent.Turn = true;
                    Play(slot, ref agent, profile, arrive, time, induced);
                    return;
                case AnimalBehaviorState.Social:
                    int partner = agent.Partner;
                    if (partner >= 0 && IsValid(partner) && _agents[partner].Partner == slot)
                    {
                        ref var p = ref _agents[partner];
                        agent.Turn = true;
                        if (p.State == AnimalBehaviorState.SocialWait)
                        {
                            // Both at their anchors: noses meet.
                            agent.Facing = p.Position;
                            p.Facing = agent.Position;
                            p.Turn = true;
                            SocialGreetings++;
                            Play(slot, ref agent, profile, AnimalBehaviorState.Social, time, induced);
                            Play(partner, ref p, _profiles[partner], AnimalBehaviorState.Social, time, true);
                        }
                        else
                        {
                            // First there: waits, turned toward where the partner is heading.
                            agent.Facing = p.Target;
                            Play(slot, ref agent, profile, AnimalBehaviorState.SocialWait, time, induced);
                        }
                        return;
                    }
                    agent.Partner = InvalidHandle;
                    break;
            }
            Play(slot, ref agent, profile, AnimalBehaviorState.Idle, time, induced);
        }

        /// <summary>Frees the reserved point and breaks the greeting link; a partner left waiting settles down.</summary>
        private void EndActivity(int slot, ref Agent agent, float time)
        {
            if (agent.Interest >= 0)
            {
                if (_interestOwner[agent.Interest] == slot)
                    _interestOwner[agent.Interest] = InvalidHandle;
                agent.Interest = InvalidHandle;
            }
            agent.Arrive = AnimalBehaviorState.Idle;
            agent.Turn = false;

            int partner = agent.Partner;
            if (partner < 0)
                return;
            agent.Partner = InvalidHandle;
            if (!IsValid(partner) || _agents[partner].Partner != slot)
                return;
            ref var p = ref _agents[partner];
            p.Partner = InvalidHandle;
            if (p.State == AnimalBehaviorState.SocialWait)
                Play(partner, ref p, _profiles[partner], AnimalBehaviorState.Idle, time, true);
            else if (p.State == AnimalBehaviorState.Wander && p.Arrive == AnimalBehaviorState.Social)
                p.Arrive = AnimalBehaviorState.Idle;
        }

        private bool TryPickWanderTarget(int slot, ref Agent agent, BehaviorProfile profile)
        {
            float scale = agent.Tier == SimulationTier.B ? profile.tierBWanderScale : 1f;
            float minSpace = profile.personalSpace * profile.personalSpace;
            // In front of the egg: it heads away from the egg, far enough to leave the apron in one walk.
            bool leaving = InApron(agent.Position, agent.Reach);
            var away = new Vector2(agent.Position.x - _eggSpot.x, agent.Position.z - _eggSpot.y);
            float fromEgg = away.magnitude;
            float awayAngle = fromEgg > 1e-3f ? Mathf.Atan2(away.x, away.y) : Mathf.PI;
            // A few random directions, the emptier ones tried first (equal crowds keep their random order).
            int count = 0;
            for (int attempt = 0; attempt < WanderCandidates; attempt++)
            {
                float angle = leaving ? awayAngle + (Next01(ref agent.Rng) - 0.5f) * 2.1f : Next01(ref agent.Rng) * Mathf.PI * 2f;
                float distance = Range(ref agent.Rng, profile.wanderDistance.x, profile.wanderDistance.y) * Mathf.Max(scale, 0.35f);
                if (leaving)
                    distance = Mathf.Max(distance, (_eggApron + agent.Reach + 0.3f - fromEgg) * 2f); // out even 60 degrees off
                var candidate = Grounded(_area.Clamp(agent.Position + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * distance));
                if (Flat(candidate - agent.Position).sqrMagnitude < 0.04f)
                    continue;
                if (InApron(candidate, agent.Reach) || Crowded(slot, candidate, minSpace))
                    continue;
                int crowd = Mathf.Max(0, Neighbours(slot, candidate, SpreadRadius * SpreadRadius) - 1);
                int at = count++;
                while (at > 0 && _candidateCrowd[at - 1] > crowd)
                {
                    _candidates[at] = _candidates[at - 1];
                    _candidateCrowd[at] = _candidateCrowd[at - 1];
                    at--;
                }
                _candidates[at] = candidate;
                _candidateCrowd[at] = crowd;
            }
            for (int i = 0; i < count; i++)
            {
                var candidate = _candidates[i];
                // The body must fit where it stops (facing the way it came) and the way there must be walkable.
                if (!Fits(slot, ref agent, candidate, YawTo(agent.Position, candidate), false))
                    continue;
                if (!TryPlan(slot, ref agent, candidate, out var waypoint, out bool detour))
                    continue;

                agent.Target = candidate;
                agent.Waypoint = waypoint;
                agent.HasWaypoint = detour;
                if (detour)
                    Detours++;
                return true;
            }
            return false;
        }

        /// <summary>Animals standing (or heading) within the given squared distance of this spot.</summary>
        private int Neighbours(int slot, Vector3 spot, float radiusSquared)
        {
            int n = 0;
            for (int i = 0; i < _activeCount; i++)
            {
                int other = _active[i];
                if (other == slot)
                    continue;
                ref var o = ref _agents[other];
                var taken = o.State == AnimalBehaviorState.Wander ? o.Target : o.Position;
                if (Flat(taken - spot).sqrMagnitude < radiusSquared)
                    n++;
            }
            return n;
        }

        /// <summary>Another animal stands (or is heading) within the given squared distance of this spot.</summary>
        private bool Crowded(int slot, Vector3 spot, float minSpaceSquared)
        {
            for (int i = 0; i < _activeCount; i++)
            {
                int other = _active[i];
                if (other == slot)
                    continue;
                ref var o = ref _agents[other];
                var taken = o.State == AnimalBehaviorState.Wander ? o.Target : o.Position;
                if (Flat(taken - spot).sqrMagnitude < minSpaceSquared)
                    return true;
            }
            return false;
        }

        private void Move(int slot, float time, float deltaTime)
        {
            ref var agent = ref _agents[slot];
            if (agent.State != AnimalBehaviorState.Wander)
            {
                if (agent.Turn)
                    TurnToFacing(slot, ref agent, deltaTime);
                return;
            }

            var profile = _profiles[slot];
            var toFinal = Flat(agent.Target - agent.Position);
            float finalDistance = toFinal.magnitude;
            if (agent.HasWaypoint && Flat(agent.Waypoint - agent.Position).magnitude <= WaypointReach)
                agent.HasWaypoint = false;
            if (!agent.HasWaypoint && finalDistance <= (agent.Arrive == AnimalBehaviorState.Idle ? ArriveDistance : ApproachArriveDistance))
            {
                Arrive(slot, ref agent, profile, time, false);
                return;
            }

            var goal = agent.HasWaypoint ? agent.Waypoint : agent.Target;
            var toGoal = Flat(goal - agent.Position);
            float distance = toGoal.magnitude;
            var dir = Steer(slot, ref agent, new Vector2(toGoal.x, toGoal.z) / Mathf.Max(distance, 1e-5f));

            float desiredYaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            float nextYaw = Mathf.MoveTowardsAngle(agent.Yaw, desiredYaw, profile.turnDegreesPerSecond * deltaTime);
            bool blocked = false;
            bool turned = false;
            if (!Mathf.Approximately(nextYaw, agent.Yaw))
            {
                // Turning swings the nose and the tail: only when they stay clear.
                if (Fits(slot, ref agent, agent.Position, nextYaw, true))
                {
                    agent.Yaw = nextYaw;
                    turned = true;
                }
                else
                {
                    blocked = true;
                }
            }
            bool facing = Mathf.Abs(Mathf.DeltaAngle(agent.Yaw, desiredYaw)) < MaxFacingErrorToWalk;

            // Ease in from rest and brake over the last 20 cm so the hop does not stop dead.
            float brake = agent.HasWaypoint ? 1f : Mathf.Clamp(finalDistance / 0.2f, 0.35f, 1f);
            float pace = agent.Emerging ? EmergeSpeedScale : 1f;
            float targetSpeed = facing ? profile.moveSpeed * pace * brake : 0f;
            agent.Speed = Mathf.MoveTowards(agent.Speed, targetSpeed, Acceleration * pace * deltaTime);
            float step = Mathf.Min(agent.Speed * deltaTime, distance);
            bool moved = false;
            if (step > 1e-5f)
            {
                // Straight on, else slide along whatever is in the way (the passing side first).
                float side = agent.Side != 0 ? agent.Side : 1f;
                for (int k = 0; k < SlideAngles.Length && !moved; k++)
                {
                    float angle = SlideAngles[k] * side;
                    var d = Rotate(dir, angle);
                    var candidate = agent.Position + new Vector3(d.x, 0f, d.y) * (step * Mathf.Cos(angle * Mathf.Deg2Rad));
                    candidate = Grounded(_area.Clamp(candidate));
                    if (Fits(slot, ref agent, candidate, agent.Yaw, true))
                    {
                        agent.Position = candidate;
                        moved = true;
                    }
                }
                if (!moved)
                {
                    agent.Speed = 0f;
                    blocked = true;
                }
            }

            if (moved)
                agent.Blocked = 0f;
            else if (blocked)
                agent.Blocked += deltaTime;

            if (moved || turned)
            {
                ApplyPose(slot, ref agent);
                if (agent.Emerging && !OverlapsEggSpot(ref agent))
                    agent.Emerging = false;
            }

            if (agent.Blocked > BlockedGiveUp)
            {
                // The way stays blocked: an approach close to its goal counts as arrived, anything else settles here.
                BlockedWalks++;
                agent.Blocked = 0f;
                agent.HasWaypoint = false;
                float left = Flat(agent.Target - agent.Position).magnitude;
                if (agent.Emerging)
                {
                    StartExit(slot, ref agent, profile, time);
                }
                else if (agent.Arrive == AnimalBehaviorState.Idle || left < NearEnough)
                {
                    Arrive(slot, ref agent, profile, time, false);
                }
                else
                {
                    EndActivity(slot, ref agent, time);
                    Play(slot, ref agent, profile, AnimalBehaviorState.Idle, time, false);
                }
            }
        }

        /// <summary>
        /// Sidestep: an animal standing or walking across the way ahead bends the walk to one side (always the same
        /// side for this walk, the one away from it). Static obstacles are handled by the planned waypoint.
        /// </summary>
        private Vector2 Steer(int slot, ref Agent agent, Vector2 dir)
        {
            var center = new Vector2(agent.Position.x, agent.Position.z);
            var right = new Vector2(dir.y, -dir.x);
            float push = 0f;
            for (int i = 0; i < _activeCount; i++)
            {
                int other = _active[i];
                if (other == slot || other == agent.Partner)
                    continue;
                ref var o = ref _agents[other];
                var rel = new Vector2(o.Position.x, o.Position.z) - center;
                float along = Vector2.Dot(rel, dir);
                float look = agent.Reach + LookAhead + o.Reach;
                if (along <= 0f || along > look)
                    continue;
                float lateral = Vector2.Dot(rel, right);
                if (Mathf.Abs(lateral) >= agent.Radius + o.Reach + 0.05f)
                    continue;
                if (agent.Side == 0)
                    agent.Side = lateral > 0f ? (sbyte)-1 : (sbyte)1;
                push += 1f - along / look;
            }
            if (push <= 0f)
                return dir;
            return (dir + right * (agent.Side * Mathf.Min(push, 1f) * 1.4f)).normalized;
        }

        /// <summary>Turns in place toward the point of interest or the partner, then stops touching the transform.</summary>
        private void TurnToFacing(int slot, ref Agent agent, float deltaTime)
        {
            var to = Flat(agent.Facing - agent.Position);
            if (to.sqrMagnitude < 1e-6f)
            {
                agent.Turn = false;
                return;
            }
            float desired = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            float next = Mathf.MoveTowardsAngle(agent.Yaw, desired, _profiles[slot].turnDegreesPerSecond * deltaTime);
            if (!Fits(slot, ref agent, agent.Position, next, true))
            {
                // The tail would sweep into a neighbour or a rock: it keeps its current heading.
                agent.Turn = false;
                return;
            }
            agent.Yaw = next;
            if (Mathf.Abs(Mathf.DeltaAngle(agent.Yaw, desired)) < FacedDegrees)
                agent.Turn = false;
            ApplyPose(slot, ref agent);
        }

        // ---------------- Collision checks ----------------

        /// <summary>The body fits at this pose: no obstacle, no egg spot (unless emerging), and no other animal.</summary>
        private bool Fits(int slot, ref Agent agent, Vector3 position, float yaw, bool withAgents)
        {
            agent.Body.Segment(position, yaw, agent.Scale, out var s0, out var s1);
            var center = new Vector2(position.x, position.z);
            float radius = agent.Radius - ContactTolerance;
            float reach = agent.Reach;
            for (int i = 0; i < _obstacleCount; i++)
            {
                float span = reach + _obstacleReach[i];
                if ((_obstacleCenter[i] - center).sqrMagnitude >= span * span)
                    continue;
                ref var o = ref _obstacles[i];
                if (!o.BlocksWalker(position.y, agent.Height))
                    continue;
                float min = radius + o.radius;
                if (CollisionMath.SegmentSegmentDistanceSq(s0, s1, o.a, o.b) < min * min)
                    return false;
            }
            if (_hasEggSpot && !agent.Emerging)
            {
                float min = radius + _eggRadius;
                if (CollisionMath.PointSegmentDistanceSq(_eggSpot, s0, s1) < min * min)
                    return false;
            }
            if (!withAgents)
                return true;
            for (int i = 0; i < _activeCount; i++)
            {
                int other = _active[i];
                if (other == slot)
                    continue;
                ref var o = ref _agents[other];
                float span = reach + o.Reach;
                if ((new Vector2(o.Position.x, o.Position.z) - center).sqrMagnitude >= span * span)
                    continue;
                float min = radius + o.Radius;
                if (CollisionMath.SegmentSegmentDistanceSq(s0, s1, o.SegA, o.SegB) < min * min)
                    return false;
            }
            return true;
        }

        private bool OverlapsEggSpot(ref Agent agent)
        {
            if (!_hasEggSpot)
                return false;
            float min = agent.Radius + _eggRadius;
            return CollisionMath.PointSegmentDistanceSq(_eggSpot, agent.SegA, agent.SegB) < min * min;
        }

        /// <summary>
        /// The straight walk from <paramref name="from"/> to <paramref name="to"/>, body along the way, meets no
        /// obstacle and no egg spot. <paramref name="blocker"/>: the first one in the way (-2: the egg spot).
        /// </summary>
        private bool PathClear(ref Agent agent, Vector3 from, Vector3 to, out int blocker)
        {
            blocker = -1;
            var f = new Vector2(from.x, from.z);
            var t = new Vector2(to.x, to.z);
            var delta = t - f;
            float length = delta.magnitude;
            if (length < 1e-4f)
                return true;
            var dir = delta / length;
            // The body sweeps from its tail at the start to its nose at the end.
            var p0 = f + dir * (agent.Body.back * agent.Scale + agent.Radius);
            var p1 = t + dir * (agent.Body.front * agent.Scale - agent.Radius);
            float radius = agent.Radius + PathMargin;
            float groundY = (from.y + to.y) * 0.5f;
            float nearest = float.MaxValue;
            for (int i = 0; i < _obstacleCount; i++)
            {
                ref var o = ref _obstacles[i];
                float span = _obstacleReach[i] + radius;
                if (CollisionMath.PointSegmentDistanceSq(_obstacleCenter[i], p0, p1) >= span * span)
                    continue;
                if (!o.BlocksWalker(groundY, agent.Height))
                    continue;
                float min = radius + o.radius;
                if (CollisionMath.SegmentSegmentDistanceSq(p0, p1, o.a, o.b) >= min * min)
                    continue;
                float along = Vector2.Dot(_obstacleCenter[i] - f, dir);
                if (along < nearest)
                {
                    nearest = along;
                    blocker = i;
                }
            }
            if (_hasEggSpot && !agent.Emerging)
            {
                float min = radius + _eggRadius;
                if (CollisionMath.PointSegmentDistanceSq(_eggSpot, p0, p1) < min * min && Vector2.Dot(_eggSpot - f, dir) < nearest)
                    blocker = -2;
            }
            return blocker == -1;
        }

        /// <summary>
        /// Plans a walk to <paramref name="target"/>: straight when clear, else through one waypoint beside the first
        /// obstacle in the way (the side nearer the straight line first). False when neither works.
        /// </summary>
        private bool TryPlan(int slot, ref Agent agent, Vector3 target, out Vector3 waypoint, out bool hasWaypoint)
        {
            waypoint = target;
            hasWaypoint = false;
            if (PathClear(ref agent, agent.Position, target, out int blocker))
                return true;

            Vector2 a, b;
            float radius;
            if (blocker == -2)
            {
                a = b = _eggSpot;
                radius = _eggRadius;
            }
            else
            {
                a = _obstacles[blocker].a;
                b = _obstacles[blocker].b;
                radius = _obstacles[blocker].radius;
            }
            var f = new Vector2(agent.Position.x, agent.Position.z);
            var t = new Vector2(target.x, target.z);
            var dir = (t - f).normalized;
            var right = new Vector2(dir.y, -dir.x);
            var center = (a + b) * 0.5f;
            var closest = CollisionMath.ClosestOnSegment(CollisionMath.ClosestOnSegment(center, f, t), a, b);
            float clearance = radius + agent.Reach + DetourMargin;
            // Pass on the side opposite to the obstacle's center; wider when a leg would still graze it.
            float first = Vector2.Dot(center - CollisionMath.ClosestOnSegment(center, f, t), right) > 0f ? -1f : 1f;
            for (int k = 0; k < 6; k++)
            {
                float side = (k & 1) == 0 ? first : -first;
                float widen = DetourWidening[k >> 1];
                var n = right * side;
                float extent = Mathf.Max(0f, Mathf.Max(Vector2.Dot(a - closest, n), Vector2.Dot(b - closest, n)));
                var w2 = closest + n * (extent + clearance * widen);
                var w = Grounded(new Vector3(w2.x, 0f, w2.y));
                if (Flat(_area.Clamp(w) - w).sqrMagnitude > 1e-4f)
                    continue;
                if (!Fits(slot, ref agent, w, YawTo(agent.Position, w), false))
                    continue;
                if (!PathClear(ref agent, agent.Position, w, out _) || !PathClear(ref agent, w, target, out _))
                    continue;
                waypoint = w;
                hasWaypoint = true;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Nearest pose around <paramref name="position"/> where the body fits (rings of 12 cm, up to 2 m).
        /// faceTravel: checked with the heading of a walk from <paramref name="position"/> to the spot.
        /// </summary>
        private Vector3 FindFreeSpot(int slot, ref Agent agent, Vector3 position, float yaw, bool faceTravel = false)
        {
            if (!faceTravel && Fits(slot, ref agent, position, yaw, true) && !InApron(position, agent.Reach))
                return position;
            // A walk out starts with the heading it has (ahead, then a little left and right); a spawn anywhere.
            float phase = faceTravel ? Mathf.PI * 0.5f - yaw * Mathf.Deg2Rad : Next01(ref agent.Rng) * Mathf.PI * 2f;
            for (int ring = 1; ring <= 16; ring++)
            {
                float radius = ring * 0.12f;
                int steps = 8 + ring * 2;
                for (int k = 0; k < steps; k++)
                {
                    int step = faceTravel ? ((k & 1) == 0 ? k / 2 : -(k + 1) / 2) : k;
                    float angle = phase + step * Mathf.PI * 2f / steps;
                    var candidate = Grounded(_area.Clamp(position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius));
                    // A spawn never lands in front of the egg; a newborn's way out starts there.
                    if (!faceTravel && InApron(candidate, agent.Reach))
                        continue;
                    if (Fits(slot, ref agent, candidate, faceTravel ? YawTo(position, candidate) : yaw, true))
                        return candidate;
                }
            }
            return position;
        }

        // ---------------- Pose ----------------

        private Vector3 Grounded(Vector3 point) => new Vector3(point.x, _ground.HeightAt(point.x, point.z), point.z);

        private static void UpdateSegment(ref Agent agent) =>
            agent.Body.Segment(agent.Position, agent.Yaw, agent.Scale, out agent.SegA, out agent.SegB);

        /// <summary>Caches the body for the neighbours' checks and moves the view, tilted with the ground slope.</summary>
        private void ApplyPose(int slot, ref Agent agent)
        {
            UpdateSegment(ref agent);
            var view = _views[slot];
            if (view == null)
                return;
            var rotation = Quaternion.Euler(0f, agent.Yaw, 0f);
            if (_ground.HasField)
            {
                var normal = _ground.NormalAt(agent.Position.x, agent.Position.z);
                var tilt = Quaternion.FromToRotation(Vector3.up, normal);
                float angle = Quaternion.Angle(Quaternion.identity, tilt);
                if (angle > MaxTiltDegrees)
                    tilt = Quaternion.Slerp(Quaternion.identity, tilt, MaxTiltDegrees / angle);
                rotation = tilt * rotation;
            }
            view.SetPose(agent.Position, rotation);
        }

        private void UpdateTiers()
        {
            _tiersDirty = false;
            for (int i = 0; i < _activeCount; i++)
            {
                int slot = _active[i];
                _sortSlots[i] = slot;
                _sortKeys[i] = Flat(_agents[slot].Position - _focus).sqrMagnitude;
            }
            Array.Sort(_sortKeys, _sortSlots, 0, _activeCount);
            int maxA = Mathf.Min(_settings.MaxTierA, _activeCount);
            for (int i = 0; i < _activeCount; i++)
                _agents[_sortSlots[i]].Tier = i < maxA ? SimulationTier.A : SimulationTier.B;
            RebuildTierLists();
        }

        private void RebuildTierLists()
        {
            _countA = 0;
            _countB = 0;
            for (int i = 0; i < _activeCount; i++)
            {
                int slot = _active[i];
                if (_agents[slot].Tier == SimulationTier.A)
                    _tierA[_countA++] = slot;
                else
                    _tierB[_countB++] = slot;
            }
            if (_cursorA >= _countA) _cursorA = 0;
            if (_cursorB >= _countB) _cursorB = 0;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private static float YawTo(Vector3 from, Vector3 to)
        {
            var d = to - from;
            return d.x * d.x + d.z * d.z < 1e-8f ? 0f : Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            if (degrees == 0f)
                return v;
            // Positive degrees turn clockwise seen from above, like a yaw.
            float rad = -degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        private static uint NextUInt(ref uint state)
        {
            uint x = state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            state = x;
            return x;
        }

        private static float Next01(ref uint state) => (NextUInt(ref state) >> 8) * (1f / 16777216f);

        private static float Range(ref uint state, float min, float max) => min + (max - min) * Next01(ref state);
    }
}
