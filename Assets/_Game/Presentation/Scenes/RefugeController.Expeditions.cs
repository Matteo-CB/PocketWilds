using System;
using PocketWilds.Services.Feedback;
using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T42 « Expéditions », the refuge's side (doc 01 Contenu, section 7.1; doc 05 sheet 23): an animal away is never shown
    /// (it keeps producing through the aggregates), a team leaving gives its places to animals of the reserve, and a team
    /// welcomed walks in from the side of the view, then hops happily on arrival with a small puff of dust.
    /// </summary>
    public sealed partial class RefugeController : IExpeditionWorld
    {
        /// <summary>The walk from the side of the view to the welcome spot (doc 05 sheet 23: the team is in by 1 200 ms).</summary>
        public const float ArrivalSeconds = 1.2f;

        /// <summary>How far to the side of its welcome spot an animal appears (about what it walks in 1.2 s).</summary>
        private const float ArrivalRun = 1.8f;

        /// <summary>The welcome spots: side by side across the view's centre, this far apart.</summary>
        private const float ArrivalSpacing = 1.05f;

        /// <summary>An animal that cannot finish its walk (blocked) arrives where it stands after this long.</summary>
        private const float ArrivalTimeout = 3f;

        private const int MaxArrivals = 3;

        /// <summary>The Prairie's soil (the dust puff's tint; the other biomes bring theirs with their zones).</summary>
        private static readonly Color ArrivalSoil = new Color(0.8f, 0.71f, 0.55f, 1f);

        private readonly int[] _arrivalHandle = new int[MaxArrivals];
        private readonly float[] _arrivalUntil = new float[MaxArrivals];
        private int _arrivals;

        /// <summary>T42: the expeditions (from the game context); null in scenes started without them.</summary>
        public IExpeditions Expeditions => _context.Expeditions;

        private bool IsAway(uint instanceId) => Expeditions != null && Expeditions.IsAway(instanceId);

        /// <summary>The team left (the expeditions screen covers the world): their views go back, the reserve fills the places.</summary>
        public void TeamLeft(uint[] team, int count)
        {
            bool removed = false;
            for (int k = 0; k < count && k < team.Length; k++)
            {
                int index = ViewIndexOf(team[k]);
                if (index < 0)
                    continue;
                RemoveViewAt(index);
                removed = true;
            }
            if (removed && !Hatching)
                SpawnSaved(_walkArea);
        }

        /// <summary>
        /// The team is welcomed: each animal appears a little to the side of the view's centre and walks to its spot there;
        /// Reduce Motion: it appears at its spot. Returns the seconds until the last one is in (1.2 at most).
        /// </summary>
        public float TeamBack(uint[] team, int count)
        {
            if (Hatching)
                return 0f;
            bool reduce = _context.State.Settings.ReduceMotion;
            var camera = _rig != null ? _rig.Camera : null;
            var right = Vector3.right;
            if (camera != null)
            {
                right = camera.transform.right;
                right.y = 0f;
                right = right.sqrMagnitude > 1e-4f ? right.normalized : Vector3.right;
            }
            var focus = ViewFocus;
            float seconds = 0f;
            int shown = 0;
            for (int k = 0; k < count && k < team.Length; k++)
                if (!IsVisible(team[k]) && _context.State.Collection.TryGet(team[k], out _))
                    shown++;
            int slot = 0;
            for (int k = 0; k < count && k < team.Length; k++)
            {
                uint id = team[k];
                if (IsVisible(id) || IsAway(id) || !_context.State.Collection.TryGet(id, out var animal))
                    continue;
                float across = (slot - (shown - 1) * 0.5f) * ArrivalSpacing;
                var home = Grounded(_walkArea.Clamp(focus + right * across));
                // From the nearer side: the left ones come from the left, the right ones from the right.
                float side = across < 0f || (across == 0f && (animal.Seed & 1u) == 0u) ? -1f : 1f;
                var start = reduce ? home : FreeArrivalPoint(home, right * side);
                slot++;
                float yaw = Quaternion.LookRotation(home - start == Vector3.zero ? -right * side : home - start).eulerAngles.y;
                if (!_context.Visuals.TryResolve(animal, start, yaw, out var snapshot) || !TryAcquire(snapshot, out var view))
                    continue;
                AddToMeadow(view, animal.Species, id, animal.Seed, RareGlow.KindOf(animal.Rarity, animal.TraitBits), emerging: false);
                int index = ViewIndexOf(id);
                if (index < 0)
                    continue;
                int handle = _agentByView[index];
                if (!reduce && _scheduler.TryWalkTo(handle, home, _time) && _arrivals < MaxArrivals)
                {
                    _arrivalHandle[_arrivals] = handle;
                    _arrivalUntil[_arrivals] = _time + ArrivalTimeout;
                    _arrivals++;
                    seconds = ArrivalSeconds;
                }
                else
                {
                    Arrive(handle, _time);
                }
            }
            return seconds;
        }

        /// <summary>A free spot on the walkable ground, <see cref="ArrivalRun"/> to the side of <paramref name="home"/> or nearer.</summary>
        private Vector3 FreeArrivalPoint(Vector3 home, Vector3 outward)
        {
            for (int step = 0; step < 4; step++)
            {
                float run = ArrivalRun * (1f - step * 0.22f);
                var point = Grounded(_walkArea.Clamp(home + outward * run));
                if (_scheduler.IsDiskClear(point, 0.35f, 0.5f, true))
                    return point;
            }
            return home;
        }

        private Vector3 Grounded(Vector3 point)
        {
            point.y = _scheduler.GroundHeight(point);
            return point;
        }

        /// <summary>Per frame: the walkers who reached their spot (or gave up) arrive.</summary>
        private void TickArrivals(float time)
        {
            for (int i = _arrivals - 1; i >= 0; i--)
            {
                int handle = _arrivalHandle[i];
                bool valid = _scheduler.IsValid(handle);
                bool walking = valid && _scheduler.GetGoalLegs(handle) > 0 && time < _arrivalUntil[i];
                if (walking)
                    continue;
                if (valid)
                    Arrive(handle, time);
                _arrivals--;
                _arrivalHandle[i] = _arrivalHandle[_arrivals];
                _arrivalUntil[i] = _arrivalUntil[_arrivals];
            }
        }

        /// <summary>Home: a small puff of dust in the soil's colour, a happy hop and voice.</summary>
        private void Arrive(int handle, float time)
        {
            var position = _scheduler.GetPosition(handle);
            _vfx?.Play(VfxId.ArrivalDust, position + new Vector3(0f, 0.03f, 0f), time, ArrivalSoil);
            _scheduler.Poke(handle, time);
            for (int i = 0; i < _views.Length; i++)
            {
                if (_views[i] != null && _agentByView[i] == handle)
                {
                    _context.Audio.Play(AudioEventId.AnimalHappy, position + new Vector3(0f, 0.2f, 0f), _views[i].Species);
                    break;
                }
            }
        }

        /// <summary>A visible animal leaves the meadow at once (its view back to the pool, the arrays kept packed).</summary>
        private void RemoveViewAt(int index)
        {
            _scheduler.Unregister(_agentByView[index]);
            if (_actorsHidden)
                ShowActor(_views[index], true);
            _animals.Release(_views[index]);
            if (_tappedView == index)
                _tappedView = -1;
            else if (_tappedView > index)
                _tappedView--;
            VisibleAnimals--;
            int last = VisibleAnimals;
            Array.Copy(_views, index + 1, _views, index, last - index);
            Array.Copy(_agentByView, index + 1, _agentByView, index, last - index);
            Array.Copy(_rareByView, index + 1, _rareByView, index, last - index);
            _views[last] = null;
            _agentByView[last] = AnimalScheduler.InvalidHandle;
            _rareByView[last] = RareGlowKind.None;
        }
    }
}
