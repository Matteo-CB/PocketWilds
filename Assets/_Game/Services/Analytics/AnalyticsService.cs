#nullable enable
using System;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Services.Store;
using PocketWilds.Simulation;

namespace PocketWilds.Services.Analytics
{
    /// <summary>Batching and sampling rules. Defaults from doc 09; Remote Config can replace them later.</summary>
    public sealed class AnalyticsConfig
    {
        public int RingCapacity = 512;
        public int MaxEventsPerBatch = 200;

        /// <summary>A batch leaves at most once a minute during play, or earlier when this many events wait.</summary>
        public double FlushIntervalSeconds = 60.0;
        public int FlushThreshold = 100;

        /// <summary>One performance summary per minute of play, after a short warmup that skips scene entry.</summary>
        public double PerfSampleSeconds = 60.0;
        public double PerfWarmupSeconds = 3.0;

        /// <summary>Share of sessions that send performance summaries (decided once per session, stable).</summary>
        public double PerfSessionSampleRate = 1.0;
    }

    /// <summary>
    /// Analytics (doc 09, T29). Events are fixed structs copied into a ring buffer; gameplay facts come from the
    /// services' own events (hatch, economy, purchases), presentation reports screens and offers through
    /// <see cref="IAnalytics"/>. A batch is encoded as compact JSON and handed to the outbox (disk, background
    /// thread) once a minute, at 100 events and when the app leaves. Light and Essence changes are summed per reason
    /// and sent once per session; Gems changes and purchases are sent one by one. Nothing here allocates per frame.
    /// Consent: Unknown keeps batches on the phone, Granted lets them leave, Denied collects nothing and deletes
    /// what was kept.
    /// </summary>
    public sealed class AnalyticsService : IAnalytics, IDisposable
    {
        private const int CurrencySlots = 4;
        private const int ReasonSlots = 16;
        private const int MaxSeenOffers = 32;

        private readonly PlayerState _state;
        private readonly ContentRegistry _content;
        private readonly IGameClock _clock;
        private readonly ISaveScheduler? _save;
        private readonly IAnalyticsSink? _sink;
        private readonly AnalyticsConfig _config;
        private readonly AnalyticsBatchEncoder _encoder;
        private readonly AnalyticsEvent[] _ring;
        private readonly FrameTimeHistogram _frames = new FrameTimeHistogram();
        private readonly int[] _flowCounts = new int[CurrencySlots * ReasonSlots];
        private readonly BigValue[] _flowSums = new BigValue[CurrencySlots * ReasonSlots];
        private readonly ContentId[] _seenOffers = new ContentId[MaxSeenOffers];
        private readonly uint _perfSeed;
        private readonly Action<HatchResult> _onHatched;
        private readonly Action<MultiHatchSummary> _onMultiHatched;
        private readonly Action<int, DiscoverySource> _onDiscoveryEggs;
        private readonly Action<EconomyChange> _onEconomy;
        private readonly Action<PurchaseOutcome> _onPurchase;
        private readonly Action<int> _onRestore;

        private EconomyService? _economy;
        private HatchService? _hatch;
        private StoreService? _store;
        private Func<uint>? _memoryMb;
        private AnalyticsConsent _consent;
        private int _head;
        private int _count;
        private int _dropped;
        private int _batchIndex;
        private int _seenCount;
        private bool _sessionActive;
        private DateTimeOffset _sessionStartUtc;
        private double _sinceFlush;
        private double _sessionPlayed;
        private double _perfElapsed;
        private bool _perfEnabled;
        private int _qualityTier;
        private bool _batterySaver;

        public AnalyticsService(PlayerState state, ContentRegistry content, IGameClock clock, ISaveScheduler? save,
            AnalyticsDeviceInfo device, IAnalyticsSink? sink, AnalyticsConsent consent, AnalyticsConfig? config = null)
        {
            _state = state;
            _content = content;
            _clock = clock;
            _save = save;
            _sink = sink;
            _config = config ?? new AnalyticsConfig();
            _encoder = new AnalyticsBatchEncoder(device);
            _ring = new AnalyticsEvent[Math.Max(16, _config.RingCapacity)];
            _perfSeed = ContentIdHash.Compute(device.AnalyticsId ?? string.Empty);
            _onHatched = OnHatched;
            _onMultiHatched = OnMultiHatched;
            _onDiscoveryEggs = OnDiscoveryEggs;
            _onEconomy = OnEconomyChanged;
            _onPurchase = OnPurchaseFinished;
            _onRestore = OnRestoreFinished;
            SetConsent(consent);
        }

        public AnalyticsConsent Consent => _consent;
        public bool SessionActive => _sessionActive;
        public int Session => _state.Analytics.Sessions;

        /// <summary>Events waiting for the next batch.</summary>
        public int Pending => _count;

        /// <summary>Events lost since the last batch because the ring was full (sent in the next batch header).</summary>
        public int Dropped => _dropped;

        public int BatchesWritten => _batchIndex;
        public bool PerfSampling => _perfEnabled;

        /// <summary>Gameplay sources. Any of them may be null (headless tests).</summary>
        public void Attach(EconomyService? economy, HatchService? hatch, StoreService? store)
        {
            Detach();
            _economy = economy;
            _hatch = hatch;
            _store = store;
            if (_economy != null)
                _economy.Changed += _onEconomy;
            if (_hatch != null)
            {
                _hatch.Hatched += _onHatched;
                _hatch.MultiHatched += _onMultiHatched;
                _hatch.DiscoveryEggsGranted += _onDiscoveryEggs;
            }
            if (_store != null)
            {
                _store.PurchaseFinished += _onPurchase;
                _store.RestoreFinished += _onRestore;
            }
        }

        /// <summary>Device facts sent with session starts and performance summaries. Memory is read once per summary.</summary>
        public void SetDeviceState(int qualityTier, bool batterySaver, Func<uint>? memoryMb)
        {
            _qualityTier = qualityTier;
            _batterySaver = batterySaver;
            _memoryMb = memoryMb;
        }

        public void SetConsent(AnalyticsConsent consent)
        {
            _consent = consent;
            if (consent == AnalyticsConsent.Denied)
            {
                _head = 0;
                _count = 0;
                _dropped = 0;
                ClearFlows();
                _frames.Reset();
                _sink?.Purge();
            }
            if (_sink != null)
                _sink.SendAllowed = consent == AnalyticsConsent.Granted;
        }

        // ---------------- Sessions ----------------

        /// <summary>App opened or back from the background. offlineSeconds: away time just paid (0 if none).</summary>
        public void StartSession(double offlineSeconds)
        {
            if (_sessionActive)
                return;
            var analytics = _state.Analytics;
            analytics.Sessions++;
            _save?.RequestSave(SaveUrgency.Normal);
            _sessionActive = true;
            _sessionStartUtc = _clock.UtcNow;
            _sinceFlush = 0.0;
            _sessionPlayed = 0.0;
            _perfElapsed = 0.0;
            _seenCount = 0;
            _frames.Reset();
            _perfEnabled = PickPerfSession(analytics.Sessions);

            var e = new AnalyticsEvent(AnalyticsEventId.SessionStart)
            {
                D0 = offlineSeconds,
                I0 = _state.Collection.Count,
                I1 = _state.Collection.DiscoveredSpecies.Count,
                I2 = _qualityTier,
                I3 = _batterySaver ? 1 : 0,
            };
            Record(ref e);
            if (_state.Collection.Count == 0)
                Milestone(FunnelMilestone.TutorialStart);
        }

        /// <summary>App going to the background or closing: summaries, then every pending event to the disk.</summary>
        public void EndSession()
        {
            if (!_sessionActive)
                return;
            if (_perfEnabled && _frames.Frames >= 60)
                EmitPerfSample();
            EmitFlows();
            EmitSnapshot();
            var e = new AnalyticsEvent(AnalyticsEventId.SessionEnd)
            {
                D0 = Math.Max(0.0, (_clock.UtcNow - _sessionStartUtc).TotalSeconds),
                I0 = _state.Collection.Count,
            };
            Record(ref e);
            _sessionActive = false;
            FlushAll();
        }

        /// <summary>Called once per frame by the game loop (unscaled seconds).</summary>
        public void Tick(float unscaledDeltaTime)
        {
            if (!_sessionActive)
                return;
            _sinceFlush += unscaledDeltaTime;
            if (_perfEnabled)
            {
                _sessionPlayed += unscaledDeltaTime;
                if (_sessionPlayed > _config.PerfWarmupSeconds)
                {
                    _frames.Add(unscaledDeltaTime * 1000f);
                    _perfElapsed += unscaledDeltaTime;
                    if (_perfElapsed >= _config.PerfSampleSeconds)
                        EmitPerfSample();
                }
            }
            if (_count >= _config.FlushThreshold || (_count > 0 && _sinceFlush >= _config.FlushIntervalSeconds))
                Flush();
        }

        // ---------------- Reports from the composition root ----------------

        public void LoadTimes(double bootMs, double sceneMs)
        {
            var e = new AnalyticsEvent(AnalyticsEventId.LoadTime) { D0 = bootMs, D1 = sceneMs };
            Record(ref e);
        }

        public void OfflineReturn(in OfflineReport report)
        {
            // Only real returns (something paid, at least a second away): a relaunch a moment later is not one.
            if (!report.HasGain || report.CreditedSeconds < 1.0)
                return;
            var e = new AnalyticsEvent(AnalyticsEventId.OfflineReturn) { D0 = report.AwaySeconds };
            AnalyticsSchema.Split(report.Light, out e.D1, out e.I0);
            Record(ref e);
        }

        // ---------------- IAnalytics ----------------

        public void ScreenView(ScreenId screen)
        {
            var e = new AnalyticsEvent(AnalyticsEventId.ScreenView) { I0 = (int)screen };
            Record(ref e);
            if (screen == ScreenId.Codex)
                Milestone(FunnelMilestone.CodexOpen);
            else if (screen == ScreenId.Shop)
                Milestone(FunnelMilestone.FirstShopExposure);
        }

        public void OfferImpression(ContentId offer, OfferPlacement placement)
        {
            for (int i = 0; i < _seenCount; i++)
            {
                if (_seenOffers[i] == offer)
                    return;
            }
            if (_seenCount < MaxSeenOffers)
                _seenOffers[_seenCount++] = offer;
            var e = new AnalyticsEvent(AnalyticsEventId.OfferImpression) { U0 = offer.Value, I0 = (int)placement };
            Record(ref e);
            Milestone(FunnelMilestone.FirstOfferImpression);
        }

        public void OfferClick(ContentId offer, OfferPlacement placement)
        {
            var e = new AnalyticsEvent(AnalyticsEventId.OfferClick) { U0 = offer.Value, I0 = (int)placement };
            Record(ref e);
        }

        public void Milestone(FunnelMilestone milestone)
        {
            if (_consent == AnalyticsConsent.Denied || !_state.Analytics.TrySet(milestone))
                return;
            _save?.RequestSave(SaveUrgency.Normal);
            var e = new AnalyticsEvent(AnalyticsEventId.Milestone)
            {
                I0 = (int)milestone,
                D0 = Math.Max(0.0, (_clock.UtcNow - _state.Meta.CreatedUtc).TotalSeconds),
            };
            Record(ref e);
        }

        // ---------------- Batches ----------------

        /// <summary>Encodes up to one batch of pending events and hands it to the outbox.</summary>
        public void Flush()
        {
            _sinceFlush = 0.0;
            if (_count == 0 || _consent == AnalyticsConsent.Denied)
                return;
            int take = Math.Min(_count, _config.MaxEventsPerBatch);
            _encoder.Encode(_batchIndex, _dropped, _ring, _head, take);
            _sink?.Write(_encoder.Buffer, _encoder.Length);
            _batchIndex++;
            _dropped = 0;
            _head = (_head + take) % _ring.Length;
            _count -= take;
        }

        public void FlushAll()
        {
            while (_count > 0 && _consent != AnalyticsConsent.Denied)
                Flush();
            _sinceFlush = 0.0;
        }

        /// <summary>The last encoded batch (tests and the debug overlay). Allocates.</summary>
        public string LastBatchText() => System.Text.Encoding.UTF8.GetString(_encoder.Buffer, 0, _encoder.Length);

        public int LastBatchBytes => _encoder.Length;

        // ---------------- Sources ----------------

        private void OnHatched(HatchResult result)
        {
            if (!result.Succeeded)
                return;
            var animal = result.Animal;
            bool discovery = result.Discovery != DiscoveryRule.None;
            int flags = (result.NewSpecies ? 1 : 0) | (result.NewVariant ? 2 : 0) | (result.WasFree ? 4 : 0)
                        | (result.Pity != PityOutcome.None ? 8 : 0)
                        | (result.Pity == PityOutcome.SpeciesBonus || result.Pity == PityOutcome.SpeciesGuarantee ? 16 : 0)
                        | (result.Pity == PityOutcome.VariantBonus ? 32 : 0) | (discovery ? 64 : 0);
            var e = new AnalyticsEvent(AnalyticsEventId.Hatch)
            {
                U0 = result.Egg.Value,
                U1 = animal.Species.Value,
                U2 = animal.Variant.Value,
                I0 = (int)animal.Rarity,
                I1 = flags,
                I3 = result.Mastery.After.Level,
                D1 = result.MultiIndex,
                D2 = (int)result.Discovery,
            };
            AnalyticsSchema.Split(result.Cost, out e.D0, out e.I2);
            Record(ref e);
            // A single egg is a purchase of one (the free tutorial egg included); a multi hatch sends one for all.
            if (result.MultiIndex == 0)
                EggBuy(result.Egg, 1, result.Cost, discovery);
            Milestone(FunnelMilestone.FirstHatch);
            if (result.NewSpecies && _state.Collection.DiscoveredSpecies.Count >= 2)
                Milestone(FunnelMilestone.FirstNewSpecies);
            if (animal.Rarity >= RarityTier.Rare)
                Milestone(FunnelMilestone.FirstRare);
        }

        private void OnEconomyChanged(EconomyChange change)
        {
            if (change.Reason == EconomyReason.Upgrade && change.Delta.IsNegative)
                Milestone(FunnelMilestone.FirstUpgrade);
            if (change.Currency == CurrencyId.Gems)
            {
                var e = new AnalyticsEvent(AnalyticsEventId.Economy) { I0 = (int)change.Currency, I1 = (int)change.Reason };
                AnalyticsSchema.Split(change.Delta, out e.D0, out e.I2);
                AnalyticsSchema.Split(change.NewBalance, out e.D1, out e.I3);
                Record(ref e);
                return;
            }
            if (_consent == AnalyticsConsent.Denied)
                return;
            int currency = (int)change.Currency;
            int reason = (int)change.Reason;
            if (currency >= CurrencySlots || reason >= ReasonSlots)
                return;
            int slot = currency * ReasonSlots + reason;
            _flowCounts[slot]++;
            _flowSums[slot] = BigValue.Add(_flowSums[slot], change.Delta);
        }

        private void OnPurchaseFinished(PurchaseOutcome outcome)
        {
            if (outcome.Status == PurchaseFlowStatus.Busy)
                return;
            OfferDefinition? offer = null;
            if (outcome.Offer.IsValid)
                _content.Shop.Offers.TryGet(outcome.Offer, out offer);
            bool gemPriced = offer != null && offer.IsGemPriced;
            var product = outcome.Grant.Product.IsValid ? outcome.Grant.Product : offer != null ? offer.Product : ContentId.None;
            int cents = 0;
            if (!gemPriced && product.IsValid && _content.Shop.Products.TryGet(product, out var definition))
                cents = definition.ReferencePriceCents;
            // A default GrantResult reads as "granted": only a granted outcome carries a real grant.
            bool granted = outcome.Status == PurchaseFlowStatus.Granted;
            bool newGrant = granted && outcome.Grant.IsNewGrant;
            double gems = !granted ? 0.0 : gemPriced ? -offer!.GemCost : outcome.Grant.Gems;
            var e = new AnalyticsEvent(AnalyticsEventId.PurchaseResult)
            {
                U0 = outcome.Offer.Value,
                U1 = product.Value,
                U2 = (outcome.Restored ? 1u : 0u) | (newGrant ? 2u : 0u),
                I0 = (int)outcome.Status,
                I1 = cents,
                D0 = gems,
                I2 = outcome.Offer.IsValid ? _state.Shop.Get(outcome.Offer).Purchases : 0,
                I3 = _state.Shop.PaidPurchases,
            };
            Record(ref e);
            if (newGrant && !gemPriced && !outcome.Restored)
            {
                if (_state.Shop.PaidPurchases >= 1)
                    Milestone(FunnelMilestone.FirstPurchase);
                if (_state.Shop.PaidPurchases >= 2)
                    Milestone(FunnelMilestone.SecondPurchase);
            }
        }

        private void OnRestoreFinished(int restored)
        {
            var e = new AnalyticsEvent(AnalyticsEventId.Restore) { I0 = restored };
            Record(ref e);
        }

        // ---------------- Summaries ----------------

        private void EmitPerfSample()
        {
            var e = new AnalyticsEvent(AnalyticsEventId.PerfSample)
            {
                I0 = _frames.Frames,
                D0 = _frames.AverageMs,
                D1 = _frames.Percentile(0.95),
                I1 = _frames.SlowFrames,
                D2 = _frames.MaxMs,
                U0 = _memoryMb != null ? _memoryMb() : 0u,
                I2 = _qualityTier,
                I3 = _batterySaver ? 1 : 0,
            };
            Record(ref e);
            _frames.Reset();
            _perfElapsed = 0.0;
        }

        private void EmitFlows()
        {
            for (int slot = 0; slot < _flowCounts.Length; slot++)
            {
                if (_flowCounts[slot] == 0)
                    continue;
                var e = new AnalyticsEvent(AnalyticsEventId.EconomyFlow)
                {
                    I0 = slot / ReasonSlots,
                    I1 = slot % ReasonSlots,
                    I2 = _flowCounts[slot],
                };
                AnalyticsSchema.Split(_flowSums[slot], out e.D0, out e.I3);
                Record(ref e);
            }
            ClearFlows();
        }

        private void EmitSnapshot()
        {
            var e = new AnalyticsEvent(AnalyticsEventId.EconomySnapshot)
            {
                D2 = _state.Wallet.Gems,
                I2 = _state.Collection.Count,
                I3 = _state.Collection.DiscoveredSpecies.Count,
            };
            AnalyticsSchema.Split(_state.Wallet.Light, out e.D0, out e.I0);
            AnalyticsSchema.Split(_economy != null ? _economy.GetLightPerSecond() : BigValue.Zero, out e.D1, out e.I1);
            Record(ref e);
        }

        private void ClearFlows()
        {
            Array.Clear(_flowCounts, 0, _flowCounts.Length);
            Array.Clear(_flowSums, 0, _flowSums.Length);
        }

        private bool PickPerfSession(int session)
        {
            double rate = _config.PerfSessionSampleRate;
            if (rate >= 1.0)
                return true;
            if (rate <= 0.0)
                return false;
            uint hash = unchecked((_perfSeed ^ (uint)session) * 2654435761u);
            return hash % 10000u < (uint)(rate * 10000.0);
        }

        private void Record(ref AnalyticsEvent e)
        {
            if (_consent == AnalyticsConsent.Denied)
                return;
            e.UnixMs = _clock.UtcNow.ToUnixTimeMilliseconds();
            e.Session = _state.Analytics.Sessions;
            if (_count == _ring.Length)
            {
                // Full (no flush for a long time): the oldest event goes, the batch header counts it.
                _head = (_head + 1) % _ring.Length;
                _count--;
                _dropped++;
            }
            _ring[(_head + _count) % _ring.Length] = e;
            _count++;
        }

        // ---------------- Progression (T35A) ----------------

        private ProgressionServices? _progression;
        private Action<UpgradePurchase>? _onUpgrade;
        private Action<ZoneOpening>? _onZone;
        private Action<BiomeOpening>? _onBiome;
        private Action<RefugeLevelUp>? _onRefugeLevel;
        private Action<BiomeMasteryGain>? _onBiomeMastery;
        private Action<DecorationPurchase>? _onDecoration;
        private Action<TreatPurchase>? _onTreat;
        private Action<RenewalDone>? _onRenewal;
        private Action<EssencePurchase>? _onEssence;

        /// <summary>Upgrades, zones, biome gates, refuge level, biome mastery, Light decorations, treats (doc 02, section 14).</summary>
        public void AttachProgression(ProgressionServices? progression)
        {
            DetachProgression();
            _progression = progression;
            if (progression == null)
                return;
            _onUpgrade ??= OnUpgrade;
            _onZone ??= OnZoneOpened;
            _onBiome ??= OnBiomeOpened;
            _onRefugeLevel ??= OnRefugeLevelUp;
            _onBiomeMastery ??= OnBiomeMasteryUp;
            _onDecoration ??= OnDecorationBought;
            _onTreat ??= OnTreatBought;
            _onRenewal ??= OnRenewal;
            _onEssence ??= OnEssenceBought;
            progression.Upgrades.Purchased += _onUpgrade;
            progression.Zones.Opened += _onZone;
            progression.Gates.Opened += _onBiome;
            progression.Refuge.LeveledUp += _onRefugeLevel;
            progression.Mastery.LeveledUp += _onBiomeMastery;
            progression.Decorations.Purchased += _onDecoration;
            progression.Treats.Purchased += _onTreat;
            progression.Renewal.Renewed += _onRenewal;
            progression.Essence.Purchased += _onEssence;
        }

        /// <summary>The first time the player sees a biome gate, with the conditions it shows met.</summary>
        public void BiomeGateSeen(ContentId biome, GateConditions met)
        {
            var e = new AnalyticsEvent(AnalyticsEventId.BiomeGateSeen) { U0 = biome.Value, I0 = (int)met };
            Record(ref e);
        }

        private void DetachProgression()
        {
            var p = _progression;
            if (p == null)
                return;
            p.Upgrades.Purchased -= _onUpgrade;
            p.Zones.Opened -= _onZone;
            p.Gates.Opened -= _onBiome;
            p.Refuge.LeveledUp -= _onRefugeLevel;
            p.Mastery.LeveledUp -= _onBiomeMastery;
            p.Decorations.Purchased -= _onDecoration;
            p.Treats.Purchased -= _onTreat;
            p.Renewal.Renewed -= _onRenewal;
            p.Essence.Purchased -= _onEssence;
            _progression = null;
        }

        private static double CostLog10(BigValue cost) => cost.IsZero ? 0.0 : BigValue.Log10(cost);

        private void EggBuy(ContentId egg, int quantity, BigValue cost, bool discovery)
        {
            int bought = _hatch?.BoughtInCycle(egg) ?? 0;
            var e = new AnalyticsEvent(AnalyticsEventId.EggBuy)
            {
                U0 = egg.Value,
                I0 = quantity,
                I1 = discovery ? 0 : Math.Max(0, bought - quantity),
                D0 = CostLog10(cost),
                I2 = discovery ? 1 : 0,
            };
            Record(ref e);
        }

        private void OnMultiHatched(MultiHatchSummary summary)
        {
            bool discovery = _hatch != null && summary.Egg == _hatch.DiscoveryEgg;
            EggBuy(summary.Egg, summary.Count, summary.Cost, discovery);
            var e = new AnalyticsEvent(AnalyticsEventId.MultiHatch)
            {
                U0 = summary.Egg.Value, I0 = summary.Count, I1 = summary.RareOrBetter, I2 = summary.New,
            };
            Record(ref e);
        }

        private void OnDiscoveryEggs(int count, DiscoverySource source)
        {
            var e = new AnalyticsEvent(AnalyticsEventId.DiscoveryEggGain)
            {
                I0 = (int)source, I1 = count, I2 = _state.World.DiscoveryEggs,
            };
            Record(ref e);
        }

        public void EggOddsOpened(ContentId egg)
        {
            var e = new AnalyticsEvent(AnalyticsEventId.EggOddsOpen) { U0 = egg.Value };
            Record(ref e);
        }

        private int DaysSinceInstall => Math.Max(0, (int)(_clock.UtcNow - _state.Meta.CreatedUtc).TotalDays);

        private void OnUpgrade(UpgradePurchase purchase)
        {
            var e = new AnalyticsEvent(AnalyticsEventId.UpgradeBuy)
            {
                U0 = purchase.Biome.Value,
                I0 = (int)purchase.Kind,
                I1 = purchase.LevelFrom,
                I2 = purchase.LevelTo,
                D0 = CostLog10(purchase.Cost),
                I3 = (int)purchase.Qty,
            };
            Record(ref e);
            if (purchase.MilestoneCrossed)
            {
                var m = new AnalyticsEvent(AnalyticsEventId.UpgradeMilestone)
                {
                    U0 = purchase.Biome.Value, I0 = (int)purchase.Kind, I1 = purchase.LevelTo,
                };
                Record(ref m);
            }
        }

        private void OnRenewal(RenewalDone done)
        {
            var e = new AnalyticsEvent(AnalyticsEventId.Renewal)
            {
                I0 = done.Count,
                I1 = (int)Math.Min(int.MaxValue, done.Gain.Total),
                D0 = CostLog10(_state.Wallet.LifetimeLight),
                I2 = DaysSinceInstall,
                I3 = _state.Progression.Refuge.Level,
            };
            Record(ref e);
        }

        private void OnEssenceBought(EssencePurchase purchase)
        {
            var e = new AnalyticsEvent(AnalyticsEventId.EssenceNodeBuy) { U0 = purchase.Node.Value, I0 = purchase.Rank };
            Record(ref e);
        }

        private void OnZoneOpened(ZoneOpening opening)
        {
            var e = new AnalyticsEvent(AnalyticsEventId.ZoneOpen)
            {
                U0 = opening.Zone.Id.Value,
                U1 = opening.Zone.Biome.Value,
                I0 = opening.Zone.ExpansionIndex,
                D0 = CostLog10(opening.Cost),
                D1 = Math.Max(0.0, (_clock.UtcNow - _state.Meta.CreatedUtc).TotalMinutes),
            };
            Record(ref e);
        }

        private void OnBiomeOpened(BiomeOpening opening)
        {
            var e = new AnalyticsEvent(AnalyticsEventId.BiomeUnlock)
            {
                U0 = opening.Biome.Value,
                I0 = _state.Progression.Refuge.Level,
                I1 = DaysSinceInstall,
                D0 = _state.Meta.TotalPlaySeconds / 60.0,
            };
            Record(ref e);
            Milestone(FunnelMilestone.BiomeUnlock);
        }

        private void OnRefugeLevelUp(RefugeLevelUp levelUp)
        {
            var e = new AnalyticsEvent(AnalyticsEventId.RefugeLevelUp) { I0 = levelUp.To, D0 = levelUp.Score, I1 = DaysSinceInstall };
            Record(ref e);
        }

        private void OnBiomeMasteryUp(BiomeMasteryGain gain)
        {
            var e = new AnalyticsEvent(AnalyticsEventId.BiomeMasteryUp) { U0 = gain.Biome.Value, I0 = gain.LevelAfter };
            Record(ref e);
        }

        private void OnDecorationBought(DecorationPurchase purchase)
        {
            var e = new AnalyticsEvent(AnalyticsEventId.DecorationBuyLight)
            {
                U0 = purchase.Decoration.Value, I0 = purchase.Copy, D0 = CostLog10(purchase.Cost),
            };
            Record(ref e);
        }

        private void OnTreatBought(TreatPurchase purchase)
        {
            var e = new AnalyticsEvent(AnalyticsEventId.TreatBuy) { U0 = purchase.Treat.Value, D0 = CostLog10(purchase.Cost) };
            Record(ref e);
        }

        private void Detach()
        {
            if (_economy != null)
                _economy.Changed -= _onEconomy;
            if (_hatch != null)
            {
                _hatch.Hatched -= _onHatched;
                _hatch.MultiHatched -= _onMultiHatched;
                _hatch.DiscoveryEggsGranted -= _onDiscoveryEggs;
            }
            if (_store != null)
            {
                _store.PurchaseFinished -= _onPurchase;
                _store.RestoreFinished -= _onRestore;
            }
            _economy = null;
            _hatch = null;
            _store = null;
        }

        public void Dispose()
        {
            DetachProgression();
            Detach();
            _sink?.Dispose();
        }
    }
}
