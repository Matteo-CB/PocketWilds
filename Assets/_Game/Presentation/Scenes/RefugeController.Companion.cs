using System;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Services.Feedback;
using PocketWilds.Simulation;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T47 Meadow Pack, the refuge's side. The companion is a normal animal instance (it spawns like any animal, shown first like a
    /// favorite); when <see cref="CompanionService.TryGetPendingArrival"/> reports a new one, it comes in beside the egg on a clear
    /// spot with a heart, a Discovery toast and its greeting, a 2 s staging, then the arrival is marked seen. When the player comes
    /// back (the offline return card closed, or more than a minute away from the app) a visible companion greets once (its Happy
    /// state). Accessories are hung on every view the refuge acquires (<see cref="AccessoryWardrobe"/>). The Cozy Burrow appears
    /// on its measured spot while decor.cozy_burrow is owned, and its measured volume joins the walk obstacles (nothing enters
    /// anything). No allocation per frame.
    /// </summary>
    public sealed partial class RefugeController
    {
        public const string CompanionArriveToastKey = "companion.arrive.toast";

        /// <summary>The staged arrival lasts this long before it is marked seen.</summary>
        public const float CompanionArrivalSeconds = 2f;

        /// <summary>Away from the app longer than this (wall clock): the companion greets the player.</summary>
        public const double CompanionReturnSeconds = 60.0;

        private const float CompanionGreetDelay = 0.8f;

        /// <summary>While animals stand on its footprint, a burrow looks again after this long (it never appears on them).</summary>
        private const float BurrowCheckSeconds = 0.5f;

        private AccessoryWardrobe _wardrobe;
        private CompanionService _companionService;
        private Action<ContentId, uint> _onCompanionArrived;
        private Action<ContentId, uint> _onCompanionLeft;
        private bool _companionCheck;
        private ContentId _arrivingSpecies;
        private uint _arrivingAnimal;
        private float _arrivalStart = -1f;
        private bool _arrivalGreeted;
        private bool _offlineWasOpen;
        private bool _greetPending;
        private long _lastTickUnix;

        private CosmeticVisualCatalog _cosmeticVisuals;
        private GrantService _grants;
        private Action<GrantResult> _onGrantsChanged;
        private ContentId _burrowItem;
        private bool _burrowCheck;
        private float _burrowWaitStart = -1f;
        private GameObject _burrow;
        private Obstacle[] _walkObstacles = Array.Empty<Obstacle>();
        private Obstacle[] _burrowObstacles = Array.Empty<Obstacle>();

        /// <summary>Tests and diagnostics: a companion arrival is being staged.</summary>
        public bool CompanionArrivalPlaying => _arrivalStart >= 0f;

        /// <summary>Tests and diagnostics: greetings played on the player's return.</summary>
        public int CompanionGreetings { get; private set; }

        /// <summary>The Cozy Burrow in the meadow, or null when not owned (or not placed yet).</summary>
        public GameObject Burrow => _burrow;

        /// <summary>The scene's accessory pieces (null without cosmetics data).</summary>
        public AccessoryWardrobe Wardrobe => _wardrobe;

        /// <summary>The visible view of an animal, or null.</summary>
        public AnimalView VisibleViewOf(uint instanceId)
        {
            int index = ViewIndexOf(instanceId);
            return index >= 0 ? _views[index] : null;
        }

        /// <summary>The accessory piece a visible animal wears on a socket, or null.</summary>
        public Transform AccessoryOn(uint instanceId, AccessorySocket socket)
        {
            var view = VisibleViewOf(instanceId);
            var accessories = view != null ? view.Accessories : null;
            return accessories != null ? accessories.GetPiece(socket) : null;
        }

        /// <summary>Constructor step, before the saved animals spawn: wardrobe, companion events, a burrow already owned.</summary>
        private void InitCosmetics()
        {
            _cosmeticVisuals = _context.VisualCatalog != null ? _context.VisualCatalog.cosmetics : null;
            if (_context.Cosmetics != null && _cosmeticVisuals != null)
                _wardrobe = new AccessoryWardrobe(_context.Cosmetics, _context.Content, _context.Palettes, _cosmeticVisuals, _root);
            _companionService = _context.Companions;
            if (_companionService != null)
            {
                _onCompanionArrived = OnCompanionArrived;
                _onCompanionLeft = OnCompanionLeft;
                _companionService.Arrived += _onCompanionArrived;
                _companionService.Left += _onCompanionLeft;
                _companionCheck = true;
            }
            _lastTickUnix = _context.Clock != null ? _context.Clock.UtcNow.ToUnixTimeSeconds() : 0;
            if (_cosmeticVisuals == null || _cosmeticVisuals.burrowPrefab == null || !_cosmeticVisuals.burrowPlaced)
                return;
            _burrowItem = ContentId.FromSource(_cosmeticVisuals.burrowItem);
            _grants = _context.Cosmetics?.Grants ?? _companionService?.Grants ?? _context.Store?.Offers.Grants;
            if (_grants != null)
            {
                _onGrantsChanged = OnGrantsChanged;
                _grants.Granted += _onGrantsChanged;
                _grants.Revoked += _onGrantsChanged;
            }
            // Owned at launch: there from the first frame (its spot is measured clear of the spawn points).
            UpdateBurrow(0f, initial: true);
        }

        private void DisposeCosmetics()
        {
            if (_companionService != null)
            {
                _companionService.Arrived -= _onCompanionArrived;
                _companionService.Left -= _onCompanionLeft;
                _companionService = null;
            }
            if (_grants != null)
            {
                _grants.Granted -= _onGrantsChanged;
                _grants.Revoked -= _onGrantsChanged;
                _grants = null;
            }
            _wardrobe?.Dispose();
            _wardrobe = null;
            DestroyBurrow();
        }

        private void OnGrantsChanged(GrantResult result) => _burrowCheck = true;

        private void OnCompanionArrived(ContentId species, uint animal) => _companionCheck = true;

        /// <summary>A refund took the companion back: it leaves the meadow at once.</summary>
        private void OnCompanionLeft(ContentId species, uint animal)
        {
            if (_arrivingAnimal == animal && _arrivalStart >= 0f)
            {
                _arrivalStart = -1f;
                _arrivingAnimal = 0;
            }
            int index = ViewIndexOf(animal);
            if (index >= 0)
                RemoveViewAt(index);
        }

        private bool IsCompanionAnimal(uint instanceId) => _companionService != null && _companionService.IsCompanion(instanceId);

        /// <summary>A companion whose arrival is still owed is not spawned with the others: it walks in, staged.</summary>
        private bool ArrivalOwed(uint instanceId) =>
            _companionService != null && _companionService.TryGetPendingArrival(out _, out uint animal) && animal == instanceId;

        /// <summary>The scheduler walks around the chunk's obstacles plus the burrow's (rebuilt on a change only).</summary>
        private void SetWalkObstacles(Obstacle[] walk)
        {
            _walkObstacles = walk ?? Array.Empty<Obstacle>();
            ApplyObstacles();
        }

        private void ApplyObstacles()
        {
            if (_burrowObstacles.Length == 0)
            {
                _scheduler.SetObstacles(_walkObstacles);
                return;
            }
            var merged = new Obstacle[_walkObstacles.Length + _burrowObstacles.Length];
            Array.Copy(_walkObstacles, merged, _walkObstacles.Length);
            Array.Copy(_burrowObstacles, 0, merged, _walkObstacles.Length, _burrowObstacles.Length);
            _scheduler.SetObstacles(merged);
        }

        /// <summary>Per frame from <see cref="Tick"/>: flags and two comparisons when nothing happens.</summary>
        private void TickCompanion(float time)
        {
            if (_burrowCheck)
                UpdateBurrow(time, initial: false);
            if (_companionService == null)
                return;

            bool open = _offlineView != null && _offlineView.IsOpen;
            if (_offlineWasOpen && !open)
                _greetPending = true;
            _offlineWasOpen = open;
            if (_context.Clock != null)
            {
                long now = _context.Clock.UtcNow.ToUnixTimeSeconds();
                if (_lastTickUnix > 0 && now - _lastTickUnix > CompanionReturnSeconds)
                    _greetPending = true;
                _lastTickUnix = now;
            }

            if (_arrivalStart >= 0f)
            {
                TickCompanionArrival(time);
                return;
            }
            bool free = !open && !Hatching && !ModalOpen;
            if (_companionCheck && free)
                StartCompanionArrival(time);
            if (_greetPending && free && _arrivalStart < 0f)
                GreetCompanions(time);
        }

        private void StartCompanionArrival(float time)
        {
            if (!_companionService.TryGetPendingArrival(out var species, out uint animalId))
            {
                _companionCheck = false;
                return;
            }
            if (!_context.State.Collection.TryGet(animalId, out var animal))
            {
                _companionService.MarkArrivalSeen(species);
                return;
            }
            // Already standing somewhere (spawned before the arrival was known): it steps out and walks in again, staged.
            int index = ViewIndexOf(animalId);
            if (index >= 0)
                RemoveViewAt(index);
            if (!FindMasteryColorSpot(animalId, out var position, out float yaw))
                return;
            if (!_context.Visuals.TryResolve(animal, position, yaw, out var snapshot) || !TryAcquire(snapshot, out var view))
            {
                _companionService.MarkArrivalSeen(species);
                return;
            }
            AddToMeadow(view, animal.Species, animalId, animal.Seed, RareGlow.KindOf(animal.Rarity, animal.TraitBits), emerging: true);
            _vfx?.Play(VfxId.FavoriteHeart, position + new Vector3(0f, 0.3f, 0f), time);
            _context.Audio.Play(AudioEventId.AnimalHappy, position + new Vector3(0f, 0.2f, 0f), animal.Species);
            _hud?.ShowToast(ToastKind.Discovery, _context.Localization.Get(CompanionArriveToastKey));
            _arrivingSpecies = species;
            _arrivingAnimal = animalId;
            _arrivalStart = time;
            _arrivalGreeted = false;
        }

        private void TickCompanionArrival(float time)
        {
            float t = time - _arrivalStart;
            if (!_arrivalGreeted && t >= CompanionGreetDelay)
            {
                _arrivalGreeted = true;
                int index = ViewIndexOf(_arrivingAnimal);
                if (index >= 0)
                    _scheduler.Poke(_agentByView[index], time);
            }
            if (t < CompanionArrivalSeconds)
                return;
            _companionService.MarkArrivalSeen(_arrivingSpecies);
            _arrivalStart = -1f;
            _arrivingAnimal = 0;
            // It just greeted; another companion may still be owed.
            _greetPending = false;
            _companionCheck = true;
        }

        /// <summary>The player is back: each visible companion plays its greeting (Happy) once, with its happy voice.</summary>
        private void GreetCompanions(float time)
        {
            _greetPending = false;
            var rows = CompanionCatalog.Rows;
            for (int i = 0; i < rows.Length; i++)
            {
                uint animal = _companionService.AnimalOf(rows[i].Species);
                int index = animal != 0 ? ViewIndexOf(animal) : -1;
                if (index < 0)
                    continue;
                _scheduler.Poke(_agentByView[index], time);
                _context.Audio.Play(AudioEventId.AnimalHappy, _scheduler.GetPosition(_agentByView[index]) + new Vector3(0f, 0.2f, 0f),
                    _views[index].Species);
                CompanionGreetings++;
            }
        }

        /// <summary>Owned and missing: placed (once its footprint is free of animals); revoked: removed.</summary>
        private void UpdateBurrow(float time, bool initial)
        {
            _burrowCheck = false;
            if (_cosmeticVisuals == null || _cosmeticVisuals.burrowPrefab == null || !_cosmeticVisuals.burrowPlaced)
                return;
            bool owned = _context.State.Entitlements.Owned.Contains(_burrowItem);
            if (owned == (_burrow != null))
            {
                _burrowWaitStart = -1f;
                return;
            }
            if (!owned)
            {
                DestroyBurrow();
                ApplyObstacles();
                return;
            }
            var origin = _chunk != null ? _chunk.transform.position : Vector3.zero;
            var position = origin + _cosmeticVisuals.burrowPosition;
            if (!initial)
            {
                // Nothing appears on top of an animal (owner rule): it waits for the footprint to be free, checked twice a
                // second; the animals wander, so the spot frees itself soon.
                if (time < _burrowWaitStart)
                {
                    _burrowCheck = true;
                    return;
                }
                if (!_scheduler.IsDiskClear(position, _cosmeticVisuals.burrowRadius, 0.5f, withAnimals: true))
                {
                    _burrowWaitStart = time + BurrowCheckSeconds;
                    _burrowCheck = true;
                    return;
                }
            }
            _burrowWaitStart = -1f;
            _burrow = Object.Instantiate(_cosmeticVisuals.burrowPrefab, position, Quaternion.Euler(0f, _cosmeticVisuals.burrowYaw, 0f), _root);
            _burrow.name = _cosmeticVisuals.burrowPrefab.name;
            uint value = AccessoryWardrobe.Packed(_context.Palettes, _cosmeticVisuals.burrowPalette);
            foreach (var renderer in _burrow.GetComponentsInChildren<MeshRenderer>(true))
                renderer.SetShaderUserValue(value);
            var source = _cosmeticVisuals.burrowObstacles ?? Array.Empty<Obstacle>();
            _burrowObstacles = new Obstacle[source.Length];
            for (int i = 0; i < source.Length; i++)
                _burrowObstacles[i] = source[i].Translated(origin);
            ApplyObstacles();
        }

        private void DestroyBurrow()
        {
            _burrowObstacles = Array.Empty<Obstacle>();
            if (_burrow == null)
                return;
            if (Application.isPlaying)
                Object.Destroy(_burrow);
            else
                Object.DestroyImmediate(_burrow);
            _burrow = null;
        }
    }
}
