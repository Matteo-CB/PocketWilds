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
    /// The playable refuge loop (T17): animals from the save live in the meadow, an egg waits on its
    /// spawn point, a touch (on the egg or the hatch button) buys and hatches it, the new animal joins the
    /// meadow and the next egg appears. Plain class ticked by VerticalSliceEntry: one Update for the scene,
    /// pooled views, no per-animal logic, no allocation in steady state. Away time comes back through the
    /// offline return screen; its Light counts up into the HUD when collected (T18).
    /// </summary>
    public sealed class RefugeController : IRefugeActions, IHatchListener, IOfflineReturnActions, ICodexActions, IAnimalSheetActions,
        IModalActions, IAnimalVoiceListener, IDisposable
    {
        private const float NextEggDelay = 0.45f;
        // Room around the egg spot no animal enters: the egg (0.33 m) or a giant newborn squirrel standing on it.
        public const float EggSpotRadius = 0.55f;
        // The egg's own footprint: the next egg appears once no body is within it (the newborn is bounding off).
        public const float EggBodyRadius = 0.36f;
        // Animals cross the space in front of the egg but never settle in it: the newborn always has a way out, the
        // next egg never waits and the egg stays in sight however full the refuge is.
        public const float EggApronRadius = 1.3f;
        private const float RepeatWindowSeconds = 15f;
        private const float EggTapRadius = 0.32f;
        private const float AnimalTapRadius = 0.34f;
        private const float CollectCountSeconds = 0.8f;
        // T34A rare glints: one visible rare animal at a time, every 2.2 s (twice as long on Low: half the particles).
        private const float RareGlintSeconds = 2.2f;
        // T34A: a second touch on the same animal within this time opens its sheet (doc 05: reaction first, then the sheet).
        private const float SecondTapSeconds = 2.5f;
        /// <summary>T34B: after a touch, that animal does not add its own chatter (the touch already spoke).</summary>
        private const float AfterTouchQuietSeconds = 2.5f;
        // T34A: the tutorial hand waits this long on the first egg before showing.
        private const float TapHintDelay = 1.2f;
        private const float NotEnoughToastSeconds = 4f;
        // Bond levels on the sheet: the economy's bond range (1000 points) in five steps.
        private const int BondPointsPerLevel = 200;
        private const int BondPointsForMax = 1000;

        /// <summary>Delay after a hatch before a moment offer may show: the new animal has landed first.</summary>
        private const float MomentOfferDelay = 1.6f;

        private readonly GameContext _context;
        private readonly WorldChunk _chunk;
        private readonly RefugeCameraRig _rig;
        private readonly IRefugeHud _hud;
        private readonly IOfflineReturnView _offlineView;
        private readonly ICodexView _codexView;
        private readonly IAnimalSheetView _sheetView;
        private readonly IModalView _modalView;
        private readonly RefugeCameraInput _input;
        private readonly ContentId _eggId;
        private readonly EggDefinition _egg;
        private readonly uint _eggVisual;

        private readonly AnimalViewPool _animals;
        private readonly GameObjectPool<EggView> _eggs;
        private readonly FeedbackVfx _vfx;
        private readonly HatchSequencePlayer _hatch;
        private readonly AnimalScheduler _scheduler;
        // One pool and behavior profile per species with a visual (T23): the visible budget is shared.
        private readonly int[] _speciesHandles;
        private readonly BehaviorProfile[] _speciesProfiles;
        private readonly Dictionary<ContentId, int> _speciesSlot;
        private readonly int _visibleBudget;
        private readonly AnimalView[] _views;
        private readonly int[] _agentByView;
        private readonly RareGlowKind[] _rareByView;
        private readonly Transform _root;

        private EggView _waitingEgg;
        private bool _noticeShown;
        private AnimalView _hatchingAnimal;
        private uint _hatchingInstance;
        private ContentId _hatchingSpecies;
        private uint _hatchingSeed;
        private RareGlowKind _hatchingRare;
        private int _glintCursor;
        private float _nextGlintTime;
        private int _tappedView = -1;
        private float _tappedTime = -100f;
        private float _idleOnEgg;
        private bool _tapHintShown;
        private float _lastNotEnoughToast = -100f;
        private int _shopUnlockState = -1;
        private RevealInfo _pendingReveal;
        private bool _hasPendingReveal;
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
        // A hatch ended: the moment offer may come once the next egg is ready and the player pauses (T33C: the egg
        // now waits for the newborn to bound off its spot, so the pause is counted from the egg, not the hatch).
        private bool _momentOfferArmed;

        public RefugeController(GameContext context, WorldChunk chunk, RefugeCameraRig rig, IRefugeHud hud,
            HatchPresentationConfig hatchConfig, string eggSourceId, Transform root, IOfflineReturnView offlineView = null,
            ICodexView codexView = null, IShopView shopView = null, IOfferModalView offerView = null,
            IAnimalSheetView sheetView = null, IModalView modalView = null)
        {
            _sheetView = sheetView;
            _modalView = modalView;
            _context = context;
            _chunk = chunk;
            _rig = rig;
            _hud = hud;
            _offlineView = offlineView;
            _codexView = codexView;
            _input = rig != null ? rig.GetComponent<RefugeCameraInput>() : null;
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
            _rareByView = new RareGlowKind[_visibleBudget];
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
            // T34A: every feedback effect warmed here, counts by the device tier (Gate 10: nothing instantiates in play).
            _vfx = hatchConfig.vfx != null ? new FeedbackVfx(hatchConfig.vfx, root, PresentationQuality.Tier) : null;
            _hatch = new HatchSequencePlayer(hatchConfig, _vfx, context.Audio, context.Haptics);
            _scheduler = new AnimalScheduler(_visibleBudget + 2, settings, chunk.WanderArea);
            _scheduler.SetVoiceListener(this);
            _scheduler.SetInterests(chunk.GetWorldInterests());
            // T33C no-collision rule: animals stand on the chunk's ground, never enter a prop, the water or the egg spot.
            _scheduler.SetGround(chunk.Ground);
            _scheduler.SetObstacles(chunk.GetWorldObstacles());
            if (chunk.EggSpawn != null)
                _scheduler.SetEggSpot(chunk.EggSpawn.position, EggSpotRadius, EggBodyRadius, EggApronRadius);

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
            // T34A: the sheet, the modal and the HUD's hidden parts are built once now (T28 lesson: first opens never hitch).
            var loc = context.Localization;
            _sheetView?.Prewarm(loc);
            _modalView?.Prewarm(loc);
            _hud?.PrewarmFeedback(loc.Get(FeedbackText.RevealNewSpecies), loc.Get(FeedbackText.ToastNotEnoughLight));
        }

        public int VisibleAnimals { get; private set; }
        public HatchSequencePlayer Hatch => _hatch;
        public FeedbackVfx Vfx => _vfx;
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
        public bool SheetOpen => _sheetView != null && _sheetView.IsOpen;
        public bool DialogOpen => _modalView != null && _modalView.IsOpen;

        /// <summary>A screen covers the refuge: world touches and hatching wait.</summary>
        public bool ModalOpen => OfflineScreenOpen || CodexOpen || ShopOpen || SheetOpen || DialogOpen;

        /// <summary>The tutorial hand shows on the first egg (tests).</summary>
        public bool TapHintShown => _tapHintShown;

        /// <summary>Collected offline Light is still counting into the HUD.</summary>
        public bool OfflineCollecting => _collectTime >= 0f;

        /// <summary>T34: adaptive quality turned real shadows on or off: visible animals and the egg update their blob shadows.</summary>
        public void RefreshPresentationQuality()
        {
            for (int i = 0; i < _views.Length; i++)
                _views[i]?.RefreshQuality();
            _hatchingAnimal?.RefreshQuality();
            _waitingEgg?.RefreshQuality();
            _vfx?.SetTier(PresentationQuality.Tier);
        }

        public void Tick(float time, float deltaTime)
        {
            _time = time;
            // T34: while heat keeps the picture lighter, the HUD says so in one short line.
            bool notice = _context.Quality != null && _context.Quality.ThermalNotice;
            if (notice != _noticeShown)
            {
                _noticeShown = notice;
                _hud?.SetNotice(notice ? _context.Localization.Get(QualityService.ThermalNoticeKey) : null);
            }
            // A new egg waits until the newborn has walked off its spot (no-collision rule).
            if (_nextEggTime >= 0f && time >= _nextEggTime && _scheduler.IsEggSpotClear())
            {
                _nextEggTime = -1f;
                PresentEgg();
            }

            UpdateOfflineReturn(deltaTime);
            _codexView?.Tick(deltaTime);
            _sheetView?.Tick(deltaTime);
            _modalView?.Tick(deltaTime);
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
            {
                _scheduler.SetFocus(_rig.Focus);
                // T34B: animal voices fade with their distance to what the player looks at.
                if (_rig.Camera != null)
                    _context.Audio.SetView(_rig.Focus, _rig.Camera.transform.right, _rig.Distance);
            }
            _scheduler.Tick(time, deltaTime);
            _hatch.Tick(time, deltaTime);
            if (_vfx != null)
            {
                TickRareGlints();
                _vfx.Tick(time);
            }

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
                    // T34A unlock toast: only when the shop opens during play, never for a shop already open at entry.
                    int state = unlocked ? 1 : 0;
                    if (_shopUnlockState == 0 && state == 1)
                    {
                        _hud.ShowToast(ToastKind.Unlock, _context.Localization.Get(FeedbackText.ToastShopOpen));
                        _context.Audio.Play(AudioEventId.Claim, Vector3.zero, default);
                    }
                    _shopUnlockState = state;
                }
                UpdateTapHint(deltaTime);
                _hud.Tick(deltaTime);
            }
        }

        /// <summary>
        /// T34A tutorial gesture: on the very first egg (no animal yet), after a short pause, a hand taps on it. It
        /// follows the egg on screen and leaves for good with the first hatch. No text: the animation explains.
        /// </summary>
        private void UpdateTapHint(float deltaTime)
        {
            bool wanted = _context.State.Collection.Count == 0 && _waitingEgg != null && !_hatch.IsPlaying && !ModalOpen;
            _idleOnEgg = wanted ? _idleOnEgg + deltaTime : 0f;
            bool show = wanted && _idleOnEgg >= TapHintDelay;
            var camera = _rig != null ? _rig.Camera : Camera.main;
            if (show && camera != null)
            {
                var screen = camera.WorldToScreenPoint(_waitingEgg.VfxPoint.position + new Vector3(0f, 0.12f, 0f));
                _hud.SetTapHint(true, new Vector2(screen.x, screen.y));
                _tapHintShown = true;
            }
            else if (_tapHintShown)
            {
                _hud.SetTapHint(false, default);
                _tapHintShown = false;
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
                // Doc 05: a touch first brings a reaction; touching the same animal again opens its sheet.
                if (bestView == _tappedView && _time - _tappedTime <= SecondTapSeconds && _sheetView != null)
                {
                    _tappedView = -1;
                    OpenSheet(_views[bestView]);
                    return;
                }
                _tappedView = bestView;
                _tappedTime = _time;
                _scheduler.Poke(_agentByView[bestView], _time);
                _context.Audio.Play(AudioEventId.AnimalHappy, _scheduler.GetPosition(_agentByView[bestView]) + new Vector3(0f, 0.2f, 0f),
                    _views[bestView].Species);
                _context.Haptics.Play(HapticEventId.AnimalTouch);
            }
        }

        private void OpenSheet(AnimalView view)
        {
            if (view == null || ModalOpen || !TryBuildSheet(view.OwnerInstanceId, out var data))
                return;
            _sheetView.Open(data, _context.Localization, this);
            _context.Audio.Play(AudioEventId.PanelOpen, Vector3.zero, default);
            _context.Haptics.Play(HapticEventId.TapLight);
        }

        /// <summary>Sheet data of a saved animal: names localized once here, never per frame.</summary>
        public bool TryBuildSheet(uint instanceId, out AnimalSheetData data)
        {
            data = default;
            var collection = _context.State.Collection;
            if (!collection.TryGet(instanceId, out var animal))
                return false;
            var loc = _context.Localization;
            string species = _context.Content.Species.TryGet(animal.Species, out var speciesDef) ? loc.Get(speciesDef.DisplayKey) : string.Empty;
            string variant = _context.Content.Variants.TryGet(animal.Variant, out var variantDef) ? loc.Get(variantDef.DisplayKey) : string.Empty;
            string name = collection.CustomNames.TryGetValue(instanceId, out var custom) && !string.IsNullOrEmpty(custom) ? custom : species;
            int bond = Math.Min((int)animal.BondPoints, BondPointsForMax);
            float inLevel = bond >= BondPointsForMax ? 1f : (bond % BondPointsPerLevel) / (float)BondPointsPerLevel;
            // The second line names the variant (and the species when the animal has its own name).
            data = new AnimalSheetData(instanceId, name, name == species ? variant : species, name == species ? null : variant,
                animal.Species, animal.Variant, animal.Rarity, animal.IsFavorite, inLevel, bond / BondPointsPerLevel, animal.TraitBits);
            return true;
        }

        /// <summary>T34A: the heart on the sheet. Saved soon (normal urgency), shown at once with a few hearts.</summary>
        public void ToggleFavorite(uint instanceId)
        {
            var collection = _context.State.Collection;
            if (!collection.TryGet(instanceId, out var animal))
                return;
            bool favorite = !animal.IsFavorite;
            animal.Flags = favorite ? animal.Flags | AnimalFlags.Favorite : animal.Flags & ~AnimalFlags.Favorite;
            collection.Update(animal);
            _context.Save?.RequestSave(SaveUrgency.Normal);
            if (favorite)
            {
                for (int i = 0; i < _views.Length; i++)
                {
                    if (_views[i] == null || _views[i].OwnerInstanceId != instanceId || _agentByView[i] == AnimalScheduler.InvalidHandle)
                        continue;
                    float scale = _views[i].transform.localScale.x;
                    _vfx?.Play(VfxId.FavoriteHeart, _scheduler.GetPosition(_agentByView[i]) + new Vector3(0f, 0.2f * scale, 0f), _time);
                    break;
                }
                _context.Audio.Play(AudioEventId.ToggleOn, Vector3.zero, default);
                _context.Haptics.Play(HapticEventId.Confirm);
            }
            else
            {
                _context.Audio.Play(AudioEventId.ToggleOff, Vector3.zero, default);
                _context.Haptics.Play(HapticEventId.TapLight);
            }
            if (TryBuildSheet(instanceId, out var data))
                _sheetView?.Refresh(data);
        }

        public void OpenSpeciesDetails(ContentId species)
        {
            if (_codexView == null || _context.Codex == null || ModalOpen || _hatch.IsPlaying)
                return;
            _codexView.OpenSpecies(_context.Codex, _context.Localization, this, species);
            _context.Analytics.ScreenView(ScreenId.Codex);
            _context.Audio.Play(AudioEventId.PanelOpen, Vector3.zero, default);
            SetWorldVisible(false);
        }

        public void AnimalSheetClosed() => _context.Audio.Play(AudioEventId.UiBack, Vector3.zero, default);

        /// <summary>
        /// T34A egg odds (doc 05 Hatch UX: the info button always shows the chances): rarity odds of this egg and,
        /// while species are left to find, the new species guarantee.
        /// </summary>
        public void OpenEggInfo()
        {
            if (_modalView == null || ModalOpen || _hatch.IsPlaying)
                return;
            var loc = _context.Localization;
            string body = EggOddsText(_egg, loc, HasSpeciesToFind());
            _modalView.Show(ModalKind.Info, loc.Get(FeedbackText.EggOddsTitle), body, loc.Get(FeedbackText.ModalOkButton), null, this);
            _context.Audio.Play(AudioEventId.PanelOpen, Vector3.zero, default);
        }

        /// <summary>
        /// The odds text: one line per rarity (percent of the egg's rarity weights, French decimals in fr), then the new
        /// species guarantee while species are left to find. Built when the info opens, never per frame.
        /// </summary>
        public static string EggOddsText(EggDefinition egg, ILocalization loc, bool speciesLeft)
        {
            var weights = egg.RarityWeights;
            double total = 0.0;
            for (int i = 0; i < weights.Length; i++)
                total += weights[i];
            var culture = loc.Language == "fr" ? System.Globalization.CultureInfo.GetCultureInfo("fr-FR") : System.Globalization.CultureInfo.InvariantCulture;
            var body = new System.Text.StringBuilder(256);
            string line = loc.Get(FeedbackText.EggOddsLine);
            for (int i = 0; i < weights.Length && total > 0.0; i++)
            {
                if (i > 0)
                    body.Append('\n');
                double percent = weights[i] / total * 100.0;
                string number = percent.ToString(percent >= 10.0 ? "0.#" : percent >= 1.0 ? "0.##" : "0.###", culture);
                body.Append(string.Format(line, loc.Get(FeedbackText.RarityKey((RarityTier)i)), number));
            }
            if (speciesLeft)
                body.Append("\n\n").Append(string.Format(loc.Get(FeedbackText.EggOddsGuarantee), egg.PityHardLimit));
            return body.ToString();
        }

        private bool HasSpeciesToFind()
        {
            var discovered = _context.State.Collection.DiscoveredSpecies;
            foreach (var entry in _egg.Pool)
                if (!discovered.Contains(entry.Species))
                    return true;
            return false;
        }

        public void ModalClosed(bool confirmed) =>
            _context.Audio.Play(confirmed ? AudioEventId.UiConfirm : AudioEventId.UiBack, Vector3.zero, default);

        /// <summary>
        /// T34B: a near animal greets, rejoices or falls asleep on its own. Quiet and rare (the audio service spaces
        /// them for the whole refuge); never over a hatch or a screen, never right after the player touched it.
        /// </summary>
        void IAnimalVoiceListener.AnimalVoice(AnimalView view, AnimalBehaviorState state, Vector3 position)
        {
            if (view == null || ModalOpen || _hatch.IsPlaying)
                return;
            if (_tappedView >= 0 && _views[_tappedView] == view && _time - _tappedTime < AfterTouchQuietSeconds)
                return;
            var id = state == AnimalBehaviorState.Sleep ? AudioEventId.AnimalSleep
                : state == AnimalBehaviorState.Eat ? AudioEventId.AnimalEat
                : AudioEventId.AnimalChatter;
            _context.Audio.Play(id, position + new Vector3(0f, 0.2f, 0f), view.Species);
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
                _context.Audio.Play(AudioEventId.ErrorSoft, Vector3.zero, default);
                _context.Haptics.Play(HapticEventId.ErrorSoft);
                // One calm line the first time and then at most every few seconds: the shake says the rest.
                if (_time - _lastNotEnoughToast >= NotEnoughToastSeconds)
                {
                    _lastNotEnoughToast = _time;
                    _hud?.ShowToast(ToastKind.Warning, _context.Localization.Get(FeedbackText.ToastNotEnoughLight));
                }
                return;
            }

            bool paid = !_context.Hatch.IsNextHatchFree;
            var result = _context.Hatch.Hatch(_eggId);
            if (!result.Succeeded)
            {
                _hatch.Nudge();
                _hud?.PulseNotEnough();
                return;
            }
            RefreshEggCost();
            // T34B2: a paid egg is heard leaving the counter (soft, under the egg tap).
            if (paid)
                _context.Audio.Play(AudioEventId.SpendLight, Vector3.zero, default);

            var reveal = _waitingEgg.RevealPoint;
            _context.Visuals.TryResolve(result.Animal, reveal.position, reveal.eulerAngles.y, out var snapshot);
            if (!TryAcquire(snapshot, out _hatchingAnimal))
                _hatchingAnimal = null;
            _pendingMastery = result.Mastery;
            _masteryShown = false;
            _hatchingInstance = result.Animal.InstanceId;
            _hatchingSpecies = result.Animal.Species;
            _hatchingSeed = result.Animal.Seed;
            _hatchingRare = RareGlow.KindOf(result.Animal.Rarity, result.Animal.TraitBits);
            var news = result.NewSpecies ? HatchNews.NewSpecies : result.NewVariant ? HatchNews.NewVariant : HatchNews.None;
            PrepareReveal(result.Animal, news);

            bool repeat = _time - _lastHatchEnd < RepeatWindowSeconds;
            if (_hatchingAnimal != null && _hatch.Hatch(_hatchingAnimal, result.Animal.Species, result.Animal.Rarity, repeat, this, news))
            {
                _rig?.FocusOn(_waitingEgg.VfxPoint.position, RefugeCameraState.HatchReveal);
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

        public void CodexClosed()
        {
            _context.Audio.Play(AudioEventId.PanelClose, Vector3.zero, default);
            SetWorldVisible(true);
        }

        public void CodexNavigated(bool deeper) =>
            _context.Audio.Play(deeper ? AudioEventId.UiTab : AudioEventId.UiBack, Vector3.zero, default);

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
            // T34A: the Light flies from the card (screen center) into the chip.
            _hud?.PlayCollect(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
        }

        void IHatchListener.OnHatchRevealed()
        {
            ShowMastery();
            if (_hasPendingReveal)
            {
                _hasPendingReveal = false;
                _hud?.ShowReveal(_pendingReveal);
            }
        }

        /// <summary>
        /// T34A reveal banner: a new species or variant says so; otherwise a rarity from Rare up says its name. Common
        /// duplicates show nothing (the mastery toast already moves). Texts resolved once per hatch.
        /// </summary>
        private void PrepareReveal(in AnimalInstance animal, HatchNews news)
        {
            _hasPendingReveal = false;
            if (news == HatchNews.None && animal.Rarity < RarityTier.Rare)
                return;
            var loc = _context.Localization;
            string species = _context.Content.Species.TryGet(animal.Species, out var speciesDef) ? loc.Get(speciesDef.DisplayKey) : string.Empty;
            string title;
            string name = species;
            if (news == HatchNews.NewSpecies)
            {
                title = loc.Get(FeedbackText.RevealNewSpecies);
            }
            else if (news == HatchNews.NewVariant)
            {
                title = loc.Get(FeedbackText.RevealNewVariant);
                if (_context.Content.Variants.TryGet(animal.Variant, out var variantDef))
                    name = species + " \u00B7 " + loc.Get(variantDef.DisplayKey);
            }
            else
            {
                title = loc.Get(FeedbackText.RarityKey(animal.Rarity));
            }
            _pendingReveal = new RevealInfo(title, name, animal.Rarity, news);
            _hasPendingReveal = true;
        }

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
                // H-VFX-009: a short star and paw burst around the newborn.
                if (_hatchingAnimal != null)
                    _vfx?.Play(VfxId.MasteryLevelUp, _hatchingAnimal.transform.position + new Vector3(0f, 0.18f, 0f), _time);
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
                AddToMeadow(_hatchingAnimal, _hatchingSpecies, _hatchingInstance, _hatchingSeed, _hatchingRare, emerging: true);
                _hatchingAnimal = null;
            }
            _rig?.ReturnToWorld();
            _hud?.SetHatching(false);
            _nextEggTime = _time + NextEggDelay;
            // A hatch is a happy moment (doc 09): the only time a moment offer may appear.
            _momentOfferTime = -1f;
            _momentOfferArmed = true;
        }

        /// <summary>
        /// Fills the rest of the visible budget with pooled views for every species the egg can give, about
        /// two per frame in total right after entering: later hatches never instantiate during gameplay
        /// (Performance Guide: pooling, no runtime Instantiate). Species are warmed side by side.
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
            _vfx?.Dispose();
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
            if (_momentOfferArmed)
            {
                // Same pause as before the newborn walked off: 1.6 s after the hatch, the egg being back after 0.45 s.
                _momentOfferArmed = false;
                _momentOfferTime = _time + (MomentOfferDelay - NextEggDelay);
            }
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
                    AddToMeadow(view, animal.Species, animal.InstanceId, animal.Seed, RareGlow.KindOf(animal.Rarity, animal.TraitBits), emerging: false);
                }
            }
        }

        /// <summary>emerging: the newborn stands on the egg spot and first walks out of it.</summary>
        private void AddToMeadow(AnimalView view, ContentId species, uint instanceId, uint seed, RareGlowKind rare, bool emerging)
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
                Array.Copy(_rareByView, 1, _rareByView, 0, _rareByView.Length - 1);
                slot = _views.Length - 1;
            }
            var transform = view.transform;
            _views[slot] = view;
            _rareByView[slot] = rare;
            _agentByView[slot] = _scheduler.Register(view, instanceId, seed, transform.position, transform.eulerAngles.y, ProfileOf(species), _time,
                transform.localScale.x, emerging);
            VisibleAnimals++;
        }

        /// <summary>
        /// T34A rare glints: the next visible rare animal (round robin) gets a few soft glints in its glow color. One
        /// effect at a time, never one per animal per frame; nothing while a screen covers the world.
        /// </summary>
        private void TickRareGlints()
        {
            if (_time < _nextGlintTime)
                return;
            _nextGlintTime = _time + RareGlintSeconds * (PresentationQuality.Tier == QualityTier.Low ? 2f : 1f);
            if (ModalOpen || _hatch.IsPlaying)
                return;
            int count = _views.Length;
            for (int n = 0; n < count; n++)
            {
                int i = (_glintCursor + n) % count;
                if (_views[i] == null || _rareByView[i] == RareGlowKind.None || _agentByView[i] == AnimalScheduler.InvalidHandle)
                    continue;
                _glintCursor = i + 1;
                float scale = _views[i].transform.localScale.x;
                float pick = Mathf.Repeat(_time * 0.37f, 1f);
                var position = _scheduler.GetPosition(_agentByView[i]) + new Vector3((pick - 0.5f) * 0.2f, 0.32f * scale, 0f);
                _vfx.Play(VfxId.RareGlint, position, _time, RareGlow.GlintColor(_rareByView[i], pick));
                return;
            }
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
