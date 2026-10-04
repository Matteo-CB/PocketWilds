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
    /// <item>Only walking agents touch their transform each frame (smooth motion between ticks).</item>
    /// </list>
    /// Preallocated arrays only: no allocation after construction. Views may be null (headless tests).
    /// </summary>
    public sealed class AnimalScheduler
    {
        public const int InvalidHandle = -1;

        private const uint RngSalt = 0x5C4ED001u;
        private const uint TemperamentSalt = 0x7E3A0001u;
        private const float ArriveDistance = 0.04f;
        private const float MaxFacingErrorToWalk = 50f;
        private const float Acceleration = 2.5f;

        private struct Agent
        {
            public Vector3 Position;
            public Vector3 Target;
            public float Yaw;
            public float Speed;
            public float StateEnd;
            public uint Rng;
            public uint InstanceId;
            public AnimalBehaviorState State;
            public AnimalBehaviorState LastChoice;
            public Temperament Temperament;
            public SimulationTier Tier;
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

        private SchedulerSettings _settings;
        private WanderArea _area;
        private Vector3 _focus;
        private int _activeCount;
        private int _freeCount;
        private int _countA;
        private int _countB;
        private int _cursorA;
        private int _cursorB;
        private float _budgetA;
        private float _budgetB;
        private float _nextTierUpdate;
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
        }

        public int Capacity { get; }
        public int Count => _activeCount;
        public int CountA => _countA;
        public int CountB => _countB;
        public SchedulerSettings Settings => _settings;

        /// <summary>State changes during the last Tick. A synchronized spike shows up here first.</summary>
        public int StateChangesLastTick { get; private set; }

        /// <summary>Agents evaluated during the last Tick, A and B together.</summary>
        public int AgentsTickedLastTick { get; private set; }

        public long TotalStateChanges { get; private set; }
        public long TotalTicksA { get; private set; }
        public long TotalTicksB { get; private set; }

        public void SetSettings(SchedulerSettings settings)
        {
            _settings = settings;
            _tiersDirty = true;
        }

        public void SetArea(WanderArea area) => _area = area;

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
                State = AnimalBehaviorState.Idle,
                LastChoice = AnimalBehaviorState.Idle,
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

        /// <summary>The player touched this animal: immediate reaction, outside the tick cadence.</summary>
        public void Poke(int handle, float time)
        {
            if (!IsValid(handle))
                return;
            ref var agent = ref _agents[handle];
            var profile = _profiles[handle];
            if (agent.State == AnimalBehaviorState.Sleep)
                Enter(handle, ref agent, profile, AnimalBehaviorState.Wake, time);
            else if (agent.State != AnimalBehaviorState.React)
                Enter(handle, ref agent, profile, AnimalBehaviorState.React, time);
        }

        public void Tick(float time, float deltaTime)
        {
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
                    Enter(slot, ref agent, profile, AnimalBehaviorState.Wake, time);
                    break;
                case AnimalBehaviorState.React:
                    bool happy = Next01(ref agent.Rng) < TemperamentTable.HappyChance(agent.Temperament);
                    Enter(slot, ref agent, profile, happy ? AnimalBehaviorState.Happy : AnimalBehaviorState.Idle, time);
                    break;
                case AnimalBehaviorState.Wander:
                    // Timed out before arriving (blocked by the area edge): settle down.
                    Enter(slot, ref agent, profile, AnimalBehaviorState.Idle, time);
                    break;
                default:
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
                if (state == AnimalBehaviorState.IdleLook || state == AnimalBehaviorState.Inspect)
                    return 0f;
                if (state == AnimalBehaviorState.Wander)
                    return profile.Weight(choice) * profile.tierBWanderScale * TemperamentTable.Multiplier(agent.Temperament, choice);
            }
            return profile.Weight(choice) * TemperamentTable.Multiplier(agent.Temperament, choice);
        }

        private void Enter(int slot, ref Agent agent, BehaviorProfile profile, AnimalBehaviorState state, float time)
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
                    if (!TryPickWanderTarget(slot, ref agent, profile))
                    {
                        goto default;
                    }
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
                default:
                    state = AnimalBehaviorState.Idle;
                    animatorState = AnimalStateHashes.Idle;
                    duration = Range(ref agent.Rng, profile.idleSeconds.x, profile.idleSeconds.y) * scale;
                    break;
            }

            if (state != AnimalBehaviorState.Wander)
                agent.Speed = 0f;
            if (state != AnimalBehaviorState.React && state != AnimalBehaviorState.Wake && state != AnimalBehaviorState.Happy)
                agent.LastChoice = state;

            // One-shots return to Idle inside the Animator by exit time: replaying Idle would restart the loop.
            bool animatorAlreadyIdle = state == AnimalBehaviorState.Idle && ReturnsToIdle(agent.State);
            agent.State = state;
            agent.StateEnd = time + duration;
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
                    return true;
                default:
                    return false;
            }
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

                bool crowded = false;
                for (int i = 0; i < _activeCount && !crowded; i++)
                {
                    int other = _active[i];
                    if (other == slot)
                        continue;
                    ref var o = ref _agents[other];
                    var spot = o.State == AnimalBehaviorState.Wander ? o.Target : o.Position;
                    crowded = Flat(spot - candidate).sqrMagnitude < minSpace;
                }
                if (crowded)
                    continue;

                agent.Target = candidate;
                return true;
            }
            return false;
        }

        private void Move(int slot, float time, float deltaTime)
        {
            ref var agent = ref _agents[slot];
            if (agent.State != AnimalBehaviorState.Wander)
                return;

            var profile = _profiles[slot];
            var toTarget = Flat(agent.Target - agent.Position);
            float distance = toTarget.magnitude;
            if (distance <= ArriveDistance)
            {
                Enter(slot, ref agent, profile, AnimalBehaviorState.Idle, time);
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
