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
    /// Inspect and Eat may walk to a reserved point of interest (rock, tree, flower patch) and face it.
    /// No NavMesh, no physics: straight walks inside the chunk's wander area.</item>
    /// </list>
    /// Preallocated arrays only: no allocation after construction. Views may be null (headless tests).
    /// </summary>
    public sealed class AnimalScheduler
    {
        public const int InvalidHandle = -1;
        public const int MaxInterests = 32;

        private const uint RngSalt = 0x5C4ED001u;
        private const uint TemperamentSalt = 0x7E3A0001u;
        private const float ArriveDistance = 0.04f;
        // Approaches end exactly on their anchor: two greeting animals must stand socialGap apart.
        private const float ApproachArriveDistance = 0.01f;
        private const float MaxFacingErrorToWalk = 50f;
        private const float Acceleration = 2.5f;
        private const float FacedDegrees = 2f;
        private const float HappyAfterGreeting = 0.6f;

        private struct Agent
        {
            public Vector3 Position;
            public Vector3 Target;
            public Vector3 Facing;
            public float Yaw;
            public float Speed;
            public float StateEnd;
            public uint Rng;
            public uint InstanceId;
            public int Partner;
            public int Interest;
            public AnimalBehaviorState State;
            public AnimalBehaviorState LastChoice;
            public AnimalBehaviorState Arrive;
            public Temperament Temperament;
            public SimulationTier Tier;
            public bool Turn;
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

        private SchedulerSettings _settings;
        private WanderArea _area;
        private Vector3 _focus;
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

        public int InterestCount => _interestCount;

        public void SetSettings(SchedulerSettings settings)
        {
            _settings = settings;
            _tiersDirty = true;
        }

        public void SetArea(WanderArea area) => _area = area;

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

        /// <summary>Adds a configured view (or null for headless use). Returns a handle, or InvalidHandle when full.</summary>
        public int Register(AnimalView view, uint instanceId, uint seed, Vector3 position, float yaw, BehaviorProfile profile, float time)
        {
            if (_freeCount == 0)
                return InvalidHandle;
            int slot = _freeSlots[--_freeCount];
            var p = profile ?? throw new ArgumentNullException(nameof(profile));

            uint rng = SeedHash.Mix(seed, RngSalt) | 1u;
            var agent = new Agent
            {
                Position = _area.Clamp(position),
                Target = position,
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
            };
            // First decision anywhere inside one idle window: a freshly spawned group never decides together.
            agent.StateEnd = time + Range(ref agent.Rng, 0.2f, p.idleSeconds.y);

            _agents[slot] = agent;
            _views[slot] = view;
            if (view != null)
                view.SetPose(agent.Position, agent.Yaw);
            _profiles[slot] = p;
            _activeIndexBySlot[slot] = _activeCount;
            _active[_activeCount++] = slot;
            _tiersDirty = true;
            return slot;
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
                    Enter(slot, ref agent, profile, Choose(ref agent, profile), time);
                    break;
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
                    float distance = Flat(agent.Target - agent.Position).magnitude;
                    // Generous timeout: turning and easing in/out take longer than distance / speed.
                    duration = distance / profile.moveSpeed * 2f + 1.5f;
                    agent.Speed = 0f;
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
                    break;
            }

            if (state != AnimalBehaviorState.Wander)
                agent.Speed = 0f;
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
            if (view != null && !animatorAlreadyIdle)
                view.PlayState(animatorState, fade);
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
        /// The meeting marker is two anchors on the line between them, socialGap apart.
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
                if (o.Tier != SimulationTier.A || o.Partner >= 0 || op.socialWeight <= 0f || op.socialGroup != profile.socialGroup)
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
            float halfGap = 0.25f * (profile.socialGap + _profiles[partner].socialGap);
            var middle = _area.Clamp((agent.Position + p.Position) * 0.5f);
            var anchorA = _area.Clamp(middle - dir * halfGap);
            var anchorB = _area.Clamp(middle + dir * halfGap);

            EndActivity(partner, ref p, time);
            agent.Partner = partner;
            p.Partner = slot;
            SocialInvitations++;
            StartApproach(slot, ref agent, profile, anchorA, anchorB, AnimalBehaviorState.Social, time, false);
            // The approach above may already end in a greeting if the partner was waiting: check the link first.
            if (p.Partner == slot && p.State != AnimalBehaviorState.Social)
                StartApproach(partner, ref p, _profiles[partner], anchorB, anchorA, AnimalBehaviorState.Social, time, true);
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
            var stand = _area.Clamp(point.position - dir * point.standOff);
            // A point outside the area would leave the animal staring from too far away.
            if (Flat(point.position - stand).magnitude > point.standOff + 0.35f)
                return false;
            // Someone already stands there: inspect or eat where it is instead.
            if (Crowded(slot, stand, profile.personalSpace * profile.personalSpace))
                return false;

            _interestOwner[pick] = slot;
            agent.Interest = pick;
            StartApproach(slot, ref agent, profile, stand, point.position,
                kind == InterestKind.Eat ? AnimalBehaviorState.Eat : AnimalBehaviorState.Inspect, time, false);
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
            AnimalBehaviorState arrive, float time, bool induced)
        {
            agent.Target = target;
            agent.Facing = facing;
            agent.Arrive = arrive;
            if (Flat(target - agent.Position).sqrMagnitude <= ApproachArriveDistance * ApproachArriveDistance)
            {
                Arrive(slot, ref agent, profile, time, induced);
                return;
            }
            Play(slot, ref agent, profile, AnimalBehaviorState.Wander, time, induced);
        }

        private void Arrive(int slot, ref Agent agent, BehaviorProfile profile, float time, bool induced)
        {
            var arrive = agent.Arrive;
            agent.Arrive = AnimalBehaviorState.Idle;
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
            for (int attempt = 0; attempt < 4; attempt++)
            {
                float angle = Next01(ref agent.Rng) * Mathf.PI * 2f;
                float distance = Range(ref agent.Rng, profile.wanderDistance.x, profile.wanderDistance.y) * Mathf.Max(scale, 0.35f);
                var candidate = _area.Clamp(agent.Position + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * distance);
                if (Flat(candidate - agent.Position).sqrMagnitude < 0.04f)
                    continue;
                if (Crowded(slot, candidate, minSpace))
                    continue;

                agent.Target = candidate;
                return true;
            }
            return false;
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
            var toTarget = Flat(agent.Target - agent.Position);
            float distance = toTarget.magnitude;
            if (distance <= (agent.Arrive == AnimalBehaviorState.Idle ? ArriveDistance : ApproachArriveDistance))
            {
                Arrive(slot, ref agent, profile, time, false);
                return;
            }

            float desiredYaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
            agent.Yaw = Mathf.MoveTowardsAngle(agent.Yaw, desiredYaw, profile.turnDegreesPerSecond * deltaTime);
            bool facing = Mathf.Abs(Mathf.DeltaAngle(agent.Yaw, desiredYaw)) < MaxFacingErrorToWalk;

            // Ease in from rest and brake over the last 20 cm so the hop does not stop dead.
            float targetSpeed = facing ? profile.moveSpeed * Mathf.Clamp(distance / 0.2f, 0.35f, 1f) : 0f;
            agent.Speed = Mathf.MoveTowards(agent.Speed, targetSpeed, Acceleration * deltaTime);
            float step = Mathf.Min(agent.Speed * deltaTime, distance);
            agent.Position += toTarget * (step / distance);

            var view = _views[slot];
            if (view != null)
                view.SetPose(agent.Position, agent.Yaw);
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
            agent.Yaw = Mathf.MoveTowardsAngle(agent.Yaw, desired, _profiles[slot].turnDegreesPerSecond * deltaTime);
            if (Mathf.Abs(Mathf.DeltaAngle(agent.Yaw, desired)) < FacedDegrees)
                agent.Turn = false;
            var view = _views[slot];
            if (view != null)
                view.SetPose(agent.Position, agent.Yaw);
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
