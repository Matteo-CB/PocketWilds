using System;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Services.Feedback;
using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// The playable sanctuary loop (T17): animals from the save live in the meadow, an egg waits on its
    /// spawn point, a touch (on the egg or the hatch button) buys and hatches it, the new animal joins the
    /// meadow and the next egg appears. Plain class ticked by VerticalSliceEntry: one Update for the scene,
    /// pooled views, no per-animal logic, no allocation in steady state.
    /// </summary>
    public sealed class SanctuaryController : ISanctuaryActions, IHatchListener, IDisposable
    {
        private const float NextEggDelay = 0.45f;
        private const float RepeatWindowSeconds = 15f;
        private const float EggTapRadius = 0.32f;
        private const float AnimalTapRadius = 0.34f;

        private readonly GameContext _context;
        private readonly WorldChunk _chunk;
        private readonly SanctuaryCameraRig _rig;
        private readonly ISanctuaryHud _hud;
        private readonly ContentId _eggId;
        private readonly EggDefinition _egg;
        private readonly uint _eggVisual;

        private readonly AnimalViewPool _animals;
        private readonly GameObjectPool<EggView> _eggs;
        private readonly VfxPool _vfx;
        private readonly HatchSequencePlayer _hatch;
        private readonly AnimalScheduler _scheduler;
        private readonly BehaviorProfile _profile;
        private readonly int _rabbitHandle;
        private readonly int _visibleBudget;
        private readonly AnimalView[] _views;
        private readonly int[] _agentByView;
        private readonly Transform _root;

        private EggView _waitingEgg;
        private AnimalView _hatchingAnimal;
        private uint _hatchingInstance;
        private uint _hatchingSeed;
        private float _nextEggTime = -1f;
        private float _time;
        private float _lastHatchEnd = -100f;
        private BigValue _eggCost;

        public SanctuaryController(GameContext context, WorldChunk chunk, SanctuaryCameraRig rig, ISanctuaryHud hud,
            HatchPresentationConfig hatchConfig, string eggSourceId, Transform root)
        {
            _context = context;
            _chunk = chunk;
            _rig = rig;
            _hud = hud;
            _root = root;
            _eggId = ContentId.FromSource(eggSourceId);
            _egg = context.Content.Eggs.Get(_eggId);
            context.Palettes.TryGetRow(_eggId, out int eggRow);
            _eggVisual = EggVisualPacking.FromDefinition(_egg, eggRow);

            var catalog = context.VisualCatalog;
            if (catalog == null || !catalog.TryGet("PF_Animal_Rabbit", out var rabbit))
                throw new InvalidOperationException("Visual catalog has no PF_Animal_Rabbit.");
            _profile = rabbit.behavior;

            var settings = SchedulerSettings.ForQuality(context.Quality.CurrentTier, false);
            // Visible population: every tier A slot plus a calm background (Performance Test Matrix P3 shape).
            _visibleBudget = settings.MaxTierA + (context.Quality.CurrentTier == QualityTier.Low ? 10 : 20);
            _views = new AnimalView[_visibleBudget];
            _agentByView = new int[_visibleBudget];
            for (int i = 0; i < _agentByView.Length; i++)
                _agentByView[i] = AnimalScheduler.InvalidHandle;

            _animals = new AnimalViewPool(root);
            _rabbitHandle = _animals.Register(rabbit.visualKey, rabbit.prefab, _visibleBudget + 1, _visibleBudget + 2);
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

            SpawnSavedAnimals();
            PresentEgg();
            _hud?.Bind(this);
            RefreshEggCost();
        }

        public int VisibleAnimals { get; private set; }
        public HatchSequencePlayer Hatch => _hatch;
        public AnimalScheduler Scheduler => _scheduler;
        public EggView WaitingEgg => _waitingEgg;
        public BigValue EggCost => _eggCost;

        public void Tick(float time, float deltaTime)
        {
            _time = time;
            if (_nextEggTime >= 0f && time >= _nextEggTime)
            {
                _nextEggTime = -1f;
                PresentEgg();
            }

            if (_rig != null)
                _scheduler.SetFocus(_rig.Focus);
            _scheduler.Tick(time, deltaTime);
            _hatch.Tick(time, deltaTime);
            _vfx.Tick(time);

            if (_hud != null)
            {
                var economy = _context.Economy;
                _hud.SetLight(economy.Light, economy.GetLightPerSecond());
                bool free = _context.Hatch.IsNextHatchFree;
                // Cached cost (refreshed after each hatch): no price curve evaluation per frame.
                _hud.SetEgg(_eggCost, free, free || economy.CanSpend(CurrencyId.Light, _eggCost));
                _hud.Tick(deltaTime);
            }
        }

        /// <summary>World tap from the camera input (already filtered from UI touches).</summary>
        public void OnTap(Vector2 screenPosition)
        {
            if (_hud != null && _hud.IsOverUi(screenPosition))
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
            if (!_animals.TryAcquire(_rabbitHandle, snapshot, out _hatchingAnimal))
                _hatchingAnimal = null;
            _hatchingInstance = result.Animal.InstanceId;
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

        void IHatchListener.OnHatchRevealed()
        {
        }

        public void OnHatchFinished()
        {
            _lastHatchEnd = _time;
            if (_waitingEgg != null)
            {
                _eggs.Release(_waitingEgg);
                _waitingEgg = null;
            }
            if (_hatchingAnimal != null)
            {
                AddToMeadow(_hatchingAnimal, _hatchingInstance, _hatchingSeed);
                _hatchingAnimal = null;
            }
            _rig?.ReturnToWorld();
            _hud?.SetHatching(false);
            _nextEggTime = _time + NextEggDelay;
        }

        public void Dispose()
        {
            _hatch.Clear();
            _vfx.Dispose();
            _animals.Dispose();
            _eggs.Clear();
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
                    if (!_animals.TryAcquire(_rabbitHandle, snapshot, out var view))
                        return;
                    AddToMeadow(view, animal.InstanceId, animal.Seed);
                }
            }
        }

        private void AddToMeadow(AnimalView view, uint instanceId, uint seed)
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
            _agentByView[slot] = _scheduler.Register(view, instanceId, seed, transform.position, transform.eulerAngles.y, _profile, _time);
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
