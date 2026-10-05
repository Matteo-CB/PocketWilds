using System;
using System.Collections.Generic;
using System.Threading;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Services.Analytics;
using PocketWilds.Services.Feedback;
using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// The playable sanctuary loop (T17): animals from the save live in the meadow, an egg waits on its
    /// spawn point, a touch (on the egg or the hatch button) buys and hatches it, the new animal joins the
    /// meadow and the next egg appears. Plain class ticked by VerticalSliceEntry: one Update for the scene,
    /// pooled views, no per-animal logic, no allocation in steady state. Away time comes back through the
    /// offline return screen; its Light counts up into the HUD when collected (T18).
    /// </summary>
    public sealed class SanctuaryController : ISanctuaryActions, IHatchListener, IOfflineReturnActions, ICodexActions, IDisposable
    {
        private const float NextEggDelay = 0.45f;
        private const float RepeatWindowSeconds = 15f;
        private const float EggTapRadius = 0.32f;
        private const float AnimalTapRadius = 0.34f;
        private const float CollectCountSeconds = 0.8f;

        /// <summary>Delay after a hatch before a moment offer may show: the new animal has landed first.</summary>
        private const float MomentOfferDelay = 1.6f;

        private readonly GameContext _context;
        private readonly WorldChunk _chunk;
        private readonly SanctuaryCameraRig _rig;
        private readonly ISanctuaryHud _hud;
        private readonly IOfflineReturnView _offlineView;
        private readonly ICodexView _codexView;
        private readonly SanctuaryCameraInput _input;
        private readonly ContentId _eggId;
        private readonly EggDefinition _egg;
        private readonly uint _eggVisual;

        private readonly AnimalViewPool _animals;
        private readonly GameObjectPool<EggView> _eggs;
        private readonly VfxPool _vfx;
        private readonly HatchSequencePlayer _hatch;
        private readonly AnimalScheduler _scheduler;
        // One pool and behavior profile per species with a visual (T23): the visible budget is shared.
        private readonly int[] _speciesHandles;
        private readonly BehaviorProfile[] _speciesProfiles;
        private readonly Dictionary<ContentId, int> _speciesSlot;
        private readonly int _visibleBudget;
        private readonly AnimalView[] _views;
        private readonly int[] _agentByView;
        private readonly Transform _root;

        private EggView _waitingEgg;
        private AnimalView _hatchingAnimal;
        private uint _hatchingInstance;
        private ContentId _hatchingSpecies;
        private uint _hatchingSeed;
        private float _nextEggTime = -1f;
        private float _time;
        private float _lastHatchEnd = -100f;
        private BigValue _eggCost;
        private BigValue _heldLight;
        private BigValue _heldAtCollect;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private MasteryGain _pendingMastery;
        private bool _masteryShown = true;
        private float _collectTime = -1f;
        private readonly ShopPresenter _shop;
        private float _momentOfferTime = -1f;

        public SanctuaryController(GameContext context, WorldChunk chunk, SanctuaryCameraRig rig, ISanctuaryHud hud,
            HatchPresentationConfig hatchConfig, string eggSourceId, Transform root, IOfflineReturnView offlineView = null,
            ICodexView codexView = null, IShopView shopView = null, IOfferModalView offerView = null)
        {
            _context = context;
            _chunk = chunk;
            _rig = rig;
            _hud = hud;
            _offlineView = offlineView;
            _codexView = codexView;
            _input = rig != null ? rig.GetComponent<SanctuaryCameraInput>() : null;
            _root = root;
            _eggId = ContentId.FromSource(eggSourceId);
            _egg = context.Content.Eggs.Get(_eggId);
            context.Palettes.TryGetRow(_eggId, out int eggRow);
            _eggVisual = EggVisualPacking.FromDefinition(_egg, eggRow);

            var settings = SchedulerSettings.ForQuality(context.Quality.CurrentTier, false);
            // Visible population: every tier A slot plus a calm background (Performance Test Matrix P3 shape).
            _visibleBudget = settings.MaxTierA + (context.Quality.CurrentTier == QualityTier.Low ? 10 : 20);
            _views = new AnimalView[_visibleBudget];
            _agentByView = new int[_visibleBudget];
            for (int i = 0; i < _agentByView.Length; i++)
                _agentByView[i] = AnimalScheduler.InvalidHandle;

            _animals = new AnimalViewPool(root);
            var catalog = context.VisualCatalog;
            if (catalog == null)
                throw new InvalidOperationException("No animal visual catalog.");
            var handles = new List<int>(8);
            var profiles = new List<BehaviorProfile>(8);
            _speciesSlot = new Dictionary<ContentId, int>(8);
            var table = context.Content.Species;
            for (int i = 0; i < table.Count; i++)
            {
                var species = table[i];
                // Each species may show the whole budget: its pool is sized like the budget.
                if (!catalog.TryGet(species.VisualKey, out var entry) || entry.prefab == null)
                    continue;
                _speciesSlot.Add(species.Id, handles.Count);
                handles.Add(_animals.Register(entry.visualKey, entry.prefab, _visibleBudget + 1, _visibleBudget + 2));
                profiles.Add(entry.behavior);
            }
            if (handles.Count == 0)
                throw new InvalidOperationException("Visual catalog has no species prefab.");
            _speciesHandles = handles.ToArray();
            _speciesProfiles = profiles.ToArray();
            _eggs = new GameObjectPool<EggView>(hatchConfig.eggPrefab, root, 2, 2);
            _eggs.Warmup(2);
            float burst = context.Quality.CurrentTier == QualityTier.Low ? 0.5f : 1f;
            _vfx = new VfxPool(root, 12, burst);
            int crack = _vfx.Register(hatchConfig.crackVfx, 2, 3);
            int common = _vfx.Register(hatchConfig.hatchCommonVfx, 1, 2);
            int rare = _vfx.Register(hatchConfig.hatchRareVfx, 1, 2);
            _vfx.Warmup(crack, 2);
            _vfx.Warmup(common, 1);
            _vfx.Warmup(rare, 1);
            _hatch = new HatchSequencePlayer(hatchConfig, _vfx, crack, common, rare, context.Audio, context.Haptics);
            _scheduler = new AnimalScheduler(_visibleBudget + 2, settings, chunk.WanderArea);
            _scheduler.SetInterests(chunk.GetWorldInterests());

            if (context.Store != null && (shopView != null || offerView != null))
            {
                _shop = new ShopPresenter(context.Store, context.Localization, shopView, offerView, context.Audio, context.Haptics,
                    SetWorldVisible, _lifetime.Token, context.Analytics);
                _shop.Prewarm();
            }

            SpawnSavedAnimals();
            WarmupSpares();
            PresentEgg();
            _hud?.Bind(this);
            RefreshEggCost();
        }

        public int VisibleAnimals { get; private set; }
        public HatchSequencePlayer Hatch => _hatch;
        public AnimalScheduler Scheduler => _scheduler;

        /// <summary>Time of the last Tick, the clock the scheduler runs on.</summary>
        public float SimulationTime => _time;
        public EggView WaitingEgg => _waitingEgg;
        public BigValue EggCost => _eggCost;

        /// <summary>Light the HUD shows: the wallet minus offline Light not counted in yet.</summary>
        public BigValue DisplayedLight
        {
            get
            {
                var light = _context.Economy.Light;
                if (_heldLight.IsZero)
                    return light;
                return BigValue.Max(BigValue.Zero, BigValue.Subtract(light, _heldLight));
            }
        }

        public bool OfflineScreenOpen => _offlineView != null && _offlineView.IsOpen;
        public bool CodexOpen => _codexView != null && _codexView.IsOpen;
        public bool ShopOpen => _shop != null && (_shop.ShopOpen || _shop.OfferOpen);
        public ShopPresenter Shop => _shop;

        /// <summary>A screen covers the sanctuary: world touches and hatching wait.</summary>
        public bool ModalOpen => OfflineScreenOpen || CodexOpen || ShopOpen;

        /// <summary>Collected offline Light is still counting into the HUD.</summary>
        public bool OfflineCollecting => _collectTime >= 0f;

        public void Tick(float time, float deltaTime)
        {
            _time = time;
            if (_nextEggTime >= 0f && time >= _nextEggTime)
            {
                _nextEggTime = -1f;
                PresentEgg();
            }

            UpdateOfflineReturn(deltaTime);
            _codexView?.Tick(deltaTime);
            if (_shop != null)
            {
                _shop.Tick(deltaTime);
                if (_momentOfferTime >= 0f && time >= _momentOfferTime && !ModalOpen && !_hatch.IsPlaying)
                {
                    _momentOfferTime = -1f;
                    _shop.TryShowMomentOffer();
                }
            }

            if (_rig != null)
                _scheduler.SetFocus(_rig.Focus);
            _scheduler.Tick(time, deltaTime);
            _hatch.Tick(time, deltaTime);
            _vfx.Tick(time);

            if (_hud != null)
            {
                var economy = _context.Economy;
                _hud.SetLight(DisplayedLight, economy.GetLightPerSecond());
                bool free = _context.Hatch.IsNextHatchFree;
                // Cached cost (refreshed after each hatch): no price curve evaluation per frame.
                _hud.SetEgg(_eggCost, free, free || economy.CanSpend(CurrencyId.Light, _eggCost));
                if (_context.Codex != null)
                    _hud.SetCodexBadge(_context.Codex.HasNew);
                if (_shop != null)
                {
                    bool unlocked = _shop.Unlocked;
                    _hud.SetShop(unlocked, _context.State.Wallet.Gems);
                    if (unlocked)
                        _context.Analytics.Milestone(FunnelMilestone.FirstShopExposure);
                }
                _hud.Tick(deltaTime);
            }
        }

        /// <summary>World tap from the camera input (already filtered from UI touches).</summary>
        public void OnTap(Vector2 screenPosition)
        {
            if (ModalOpen || (_hud != null && _hud.IsOverUi(screenPosition)))
                return;
            if (_hatch.IsPlaying)
            {
                _hatch.Skip();
                return;
            }
            var camera = _rig != null ? _rig.Camera : Camera.main;
            if (camera == null)
                return;
            var ray = camera.ScreenPointToRay(screenPosition);

            if (_waitingEgg != null && HitsSphere(ray, _waitingEgg.VfxPoint.position, EggTapRadius))
            {
                RequestHatch();
                return;
            }

            int bestView = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < _views.Length; i++)
            {
                if (_views[i] == null || _agentByView[i] == AnimalScheduler.InvalidHandle)
                    continue;
                var center = _scheduler.GetPosition(_agentByView[i]) + new Vector3(0f, 0.25f, 0f);
                float along = Vector3.Dot(center - ray.origin, ray.direction);
                if (along <= 0f || along >= bestDistance)
                    continue;
                if (HitsSphere(ray, center, AnimalTapRadius))
                {
                    bestDistance = along;
                    bestView = i;
                }
            }
            if (bestView >= 0)
            {
                _scheduler.Poke(_agentByView[bestView], _time);
                _context.Haptics.Play(HapticEventId.AnimalTouch);
            }
        }

        public void RequestHatch()
        {
            if (ModalOpen)
                return;
            if (_hatch.IsPlaying)
            {
                _hatch.Skip();
                return;
            }
            if (_waitingEgg == null)
                return;
            if (!_context.Hatch.CanHatch(_eggId))
            {
                _hatch.Nudge();
                _hud?.PulseNotEnough();
                _context.Haptics.Play(HapticEventId.ErrorSoft);
                return;
            }

            var result = _context.Hatch.Hatch(_eggId);
            if (!result.Succeeded)
            {
                _hatch.Nudge();
                _hud?.PulseNotEnough();
                return;
            }
            RefreshEggCost();

            var reveal = _waitingEgg.RevealPoint;
            _context.Visuals.TryResolve(result.Animal, reveal.position, reveal.eulerAngles.y, out var snapshot);
            if (!TryAcquire(snapshot, out _hatchingAnimal))
                _hatchingAnimal = null;
            _pendingMastery = result.Mastery;
            _masteryShown = false;
            _hatchingInstance = result.Animal.InstanceId;
            _hatchingSpecies = result.Animal.Species;
            _hatchingSeed = result.Animal.Seed;

            bool repeat = _time - _lastHatchEnd < RepeatWindowSeconds;
            if (_hatchingAnimal != null && _hatch.Hatch(_hatchingAnimal, result.Animal.Species, result.Animal.Rarity, repeat, this))
            {
                _rig?.FocusOn(_waitingEgg.VfxPoint.position, SanctuaryCameraState.HatchReveal);
                _hud?.SetHatching(true);
            }
            else
            {
                // Presentation could not stage it (pool exhausted): the animal is saved, show it directly.
                OnHatchFinished();
            }
        }

        /// <summary>
        /// HUD shortcut. The Codex is opaque and full screen: the world camera stops while it is open
        /// (no GPU spent on hidden pixels) and restarts instantly on close.
        /// </summary>
        public void OpenCodex()
        {
            if (_codexView == null || _context.Codex == null || ModalOpen || _hatch.IsPlaying)
                return;
            _codexView.Open(_context.Codex, _context.Localization, this);
            _context.Analytics.ScreenView(ScreenId.Codex);
            _context.Audio.Play(AudioEventId.PanelOpen, Vector3.zero, default);
            SetWorldVisible(false);
        }

        public void CodexClosed() => SetWorldVisible(true);

        /// <summary>HUD shortcut. Like the Codex, the shop is opaque: the world camera stops while it is open.</summary>
        public void OpenShop()
        {
            if (_shop == null || ModalOpen || _hatch.IsPlaying)
                return;
            _shop.OpenShop();
        }

        private void SetWorldVisible(bool visible)
        {
            if (_rig != null && _rig.Camera != null)
                _rig.Camera.enabled = visible;
            if (_input != null)
                _input.Blocked = !visible;
        }

        /// <summary>Collect button: the held Light counts up into the HUD counter.</summary>
        public void CollectOffline()
        {
            if (_heldLight.IsZero || _collectTime >= 0f)
                return;
            _collectTime = 0f;
            _context.Analytics.Milestone(FunnelMilestone.FirstCollect);
            _context.Audio.Play(AudioEventId.CollectLight, Vector3.zero, default);
            _context.Haptics.Play(HapticEventId.Confirm);
        }

        void IHatchListener.OnHatchRevealed() => ShowMastery();

        /// <summary>Species progress right after the reveal: every hatch moves something forward.</summary>
        private void ShowMastery()
        {
            if (_masteryShown)
                return;
            _masteryShown = true;
            if (!_pendingMastery.HasGain)
                return;
            _hud?.ShowMastery(_pendingMastery);
            if (_pendingMastery.LeveledUp)
            {
                _context.Audio.Play(AudioEventId.MasteryLevelUp, Vector3.zero, _pendingMastery.Species);
                _context.Haptics.Play(_pendingMastery.Milestone > 0 ? HapticEventId.Rare : HapticEventId.Confirm);
            }
        }

        public void OnHatchFinished()
        {
            ShowMastery();
            _lastHatchEnd = _time;
            if (_waitingEgg != null)
            {
                _eggs.Release(_waitingEgg);
                _waitingEgg = null;
            }
            if (_hatchingAnimal != null)
            {
                AddToMeadow(_hatchingAnimal, _hatchingSpecies, _hatchingInstance, _hatchingSeed);
                _hatchingAnimal = null;
            }
            _rig?.ReturnToWorld();
            _hud?.SetHatching(false);
            _nextEggTime = _time + NextEggDelay;
            // A hatch is a happy moment (doc 09): the only time a moment offer may appear.
            _momentOfferTime = _time + MomentOfferDelay;
        }

        /// <summary>
        /// Fills the rest of the visible budget with pooled views for every species the egg can give, about
        /// two per frame in total right after entering: later hatches never instantiate during gameplay
        /// (Performance Bible: pooling, no runtime Instantiate). Species are warmed side by side.
        /// </summary>
        private void WarmupSpares()
        {
            int spares = _visibleBudget + 1 - VisibleAnimals;
            if (spares <= 0)
                return;
            int warmed = 0;
            foreach (var entry in _egg.Pool)
                if (_speciesSlot.ContainsKey(entry.Species))
                    warmed++;
            int perFrame = Math.Max(1, 2 / Math.Max(1, warmed));
            foreach (var entry in _egg.Pool)
                if (_speciesSlot.TryGetValue(entry.Species, out int slot))
                    _ = WarmupSpeciesAsync(_speciesHandles[slot], spares, perFrame);
        }

        private async Awaitable WarmupSpeciesAsync(int handle, int count, int perFrame)
        {
            try
            {
                int missing = count - _animals.GetPool(handle).Stats.Inactive;
                if (missing > 0)
                    await _animals.WarmupAsync(handle, missing, perFrame, _lifetime.Token);
            }
            catch (OperationCanceledException)
            {
            }
        }

        /// <summary>Spares of the least warmed species the egg can give: the next hatch is covered while this is above 0.</summary>
        public int PooledSpares
        {
            get
            {
                int min = int.MaxValue;
                foreach (var entry in _egg.Pool)
                    if (_speciesSlot.TryGetValue(entry.Species, out int slot))
                        min = Math.Min(min, _animals.GetPool(_speciesHandles[slot]).Stats.Inactive);
                return min == int.MaxValue ? 0 : min;
            }
        }

        /// <summary>Spares of one species (tests and diagnostics).</summary>
        public int PooledSparesOf(ContentId species) =>
            _speciesSlot.TryGetValue(species, out int slot) ? _animals.GetPool(_speciesHandles[slot]).Stats.Inactive : 0;

        private bool TryAcquire(in AnimalVisualSnapshot snapshot, out AnimalView view)
        {
            view = null;
            return _speciesSlot.TryGetValue(snapshot.Species, out int slot)
                   && _animals.TryAcquire(_speciesHandles[slot], snapshot, out view);
        }

        private BehaviorProfile ProfileOf(ContentId species) =>
            _speciesSlot.TryGetValue(species, out int slot) ? _speciesProfiles[slot] : _speciesProfiles[0];

        public void Dispose()
        {
            _shop?.Dispose();
            _lifetime.Cancel();
            _lifetime.Dispose();
            _hatch.Clear();
            _vfx.Dispose();
            _animals.Dispose();
            _eggs.Clear();
        }

        private void UpdateOfflineReturn(float deltaTime)
        {
            if (_offlineView == null)
                return;
            if (!_offlineView.IsOpen && !CodexOpen && !ShopOpen && _collectTime < 0f && !_hatch.IsPlaying
                && _context.Offline != null && _context.Offline.TryTakePending(out var report))
            {
                // Already in the wallet (saved at once); the HUD holds it back until Collect.
                _heldLight = report.Light;
                _offlineView.Show(report.Light, _context.Localization, this);
                _context.Analytics.ScreenView(ScreenId.OfflineReturn);
                _context.Audio.Play(AudioEventId.PanelOpen, Vector3.zero, default);
            }
            _offlineView.Tick(deltaTime);

            if (_collectTime >= 0f)
            {
                _collectTime += deltaTime;
                float t = Mathf.Clamp01(_collectTime / CollectCountSeconds);
                float eased = 1f - (1f - t) * (1f - t) * (1f - t);
                if (t >= 1f)
                {
                    _heldLight = BigValue.Zero;
                    _collectTime = -1f;
                    _heldAtCollect = BigValue.Zero;
                }
                else
                {
                    if (_heldAtCollect.IsZero)
                        _heldAtCollect = _heldLight;
                    _heldLight = BigValue.Multiply(_heldAtCollect, 1.0 - eased);
                }
            }

            bool modal = ModalOpen;
            if (_input != null && _input.Blocked != modal)
                _input.Blocked = modal;
        }

        private void PresentEgg()
        {
            if (_waitingEgg != null || _chunk.EggSpawn == null || !_eggs.TryAcquire(out _waitingEgg))
                return;
            // The egg faces the camera (its stage-1 crack is on its front).
            _waitingEgg.Configure(_chunk.EggSpawn.position, 180f, _eggVisual);
            _hatch.Present(_waitingEgg);
        }

        private void RefreshEggCost() => _eggCost = _context.Hatch.GetCost(_eggId);

        private void SpawnSavedAnimals()
        {
            var collection = _context.State.Collection;
            var area = _chunk.WanderArea;
            // Favorites first, then the newest animals, up to the visible budget. The rest lives in the
            // logical reserve and keeps producing through the aggregates.
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = collection.Count - 1; i >= 0 && VisibleAnimals < _visibleBudget; i--)
                {
                    ref readonly var animal = ref collection[i];
                    if (animal.IsFavorite != (pass == 0))
                        continue;
                    float angle = SeedHash.Unit(animal.Seed, 0x51A70001u) * Mathf.PI * 2f;
                    float radius = Mathf.Sqrt(SeedHash.Unit(animal.Seed, 0x51A70002u)) * area.Radius * 0.9f;
                    var position = area.Center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    if (!_context.Visuals.TryResolve(animal, position, SeedHash.Unit(animal.Seed, 0x51A70003u) * 360f, out var snapshot))
                        continue;
                    if (!TryAcquire(snapshot, out var view))
                        continue;
                    AddToMeadow(view, animal.Species, animal.InstanceId, animal.Seed);
                }
            }
        }

        private void AddToMeadow(AnimalView view, ContentId species, uint instanceId, uint seed)
        {
            int slot = Array.IndexOf(_views, null);
            if (slot < 0)
            {
                // Budget full: the oldest visible animal steps back into the reserve.
                _scheduler.Unregister(_agentByView[0]);
                _animals.Release(_views[0]);
                VisibleAnimals--;
                Array.Copy(_views, 1, _views, 0, _views.Length - 1);
                Array.Copy(_agentByView, 1, _agentByView, 0, _agentByView.Length - 1);
                slot = _views.Length - 1;
            }
            var transform = view.transform;
            _views[slot] = view;
            _agentByView[slot] = _scheduler.Register(view, instanceId, seed, transform.position, transform.eulerAngles.y, ProfileOf(species), _time);
            VisibleAnimals++;
        }

        private static bool HitsSphere(Ray ray, Vector3 center, float radius)
        {
            var toCenter = center - ray.origin;
            float along = Vector3.Dot(toCenter, ray.direction);
            if (along < 0f)
                return false;
            return (toCenter - ray.direction * along).sqrMagnitude <= radius * radius;
        }
    }
}
