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
        /// <summary>Doc 08 currencies: 1 Light, 2 Essence, 3 Gems, 4 event currency, 5 tickets, 6 album stars.</summary>
        private const int CurrencySlots = 7;

        /// <summary>Doc 08: the tickets currency and the automatic refill reason (summed in economy_flow).</summary>
        private const int TicketCurrency = 5;
        private const int TicketRefillReason = 52;
        private const int ReasonSlots = 64;
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

        // ------------------------------------------------------------------ T36: the rendez-vous of the day

        private DailyVisitServices? _daily;
        private Action<GiftOpening>? _onGift;
        private Action<CalendarClaim>? _onCalendar;
        private Action<int, GrantedRewards>? _onWelcome;
        private Action<ReturnGift>? _onReturnGift;
        private Action<int, GrantedRewards>? _onJournal;
        private Action<uint, ContentId, int>? _onTreatGiven;
        private Action<WishDraw>? _onWishDrawn;
        private Action<WishClaim>? _onWishClaimed;
        private Action<WishSwapInfo>? _onWishSwapped;

        /// <summary>Gift, calendar, welcome and return gifts, wishes, journal goals, treats given (doc 01 Contenu; T36).</summary>
        public void AttachDaily(DailyVisitServices? daily)
        {
            DetachDaily();
            _daily = daily;
            if (daily == null)
                return;
            _onGift ??= OnGiftOpened;
            _onCalendar ??= OnCalendarClaimed;
            _onWelcome ??= OnWelcomeClaimed;
            _onReturnGift ??= OnReturnGift;
            _onJournal ??= OnJournalClaimed;
            _onTreatGiven ??= OnTreatGiven;
            _onWishDrawn ??= OnWishDrawn;
            _onWishClaimed ??= OnWishClaimed;
            _onWishSwapped ??= OnWishSwapped;
            daily.Gift.Opened += _onGift;
            daily.Calendar.Claimed += _onCalendar;
            daily.Welcome.Claimed += _onWelcome;
            daily.Returns.Granted += _onReturnGift;
            daily.Journal.Claimed += _onJournal;
            daily.Bond.TreatGiven += _onTreatGiven;
            daily.Wishes.Drawn += _onWishDrawn;
            daily.Wishes.Claimed += _onWishClaimed;
            daily.Wishes.Swapped += _onWishSwapped;
        }

        /// <summary>
        /// Everything claimable in Today was claimed (doc 01, section 0: the visit under two minutes): seconds from Today
        /// opened to the last claim, taps on the way, items claimed.
        /// </summary>
        public void DailyVisitDone(double seconds, int taps, int items)
        {
            var e = new AnalyticsEvent(AnalyticsEventId.DailyVisitDone) { D0 = seconds, I0 = taps, I1 = items };
            Record(ref e);
        }

        private void DetachDaily()
        {
            var d = _daily;
            if (d == null)
                return;
            d.Gift.Opened -= _onGift;
            d.Calendar.Claimed -= _onCalendar;
            d.Welcome.Claimed -= _onWelcome;
            d.Returns.Granted -= _onReturnGift;
            d.Journal.Claimed -= _onJournal;
            d.Bond.TreatGiven -= _onTreatGiven;
            d.Wishes.Drawn -= _onWishDrawn;
            d.Wishes.Claimed -= _onWishClaimed;
            d.Wishes.Swapped -= _onWishSwapped;
            _daily = null;
        }

        private void OnGiftOpened(GiftOpening opening)
        {
            var e = new AnalyticsEvent(AnalyticsEventId.DailyGift)
            {
                I0 = (int)opening.Lot, I1 = (int)opening.Size, I2 = opening.Flags, I3 = opening.SinceBigBefore, D0 = opening.Granted.Bundle.Gems,
            };
            Record(ref e);
        }

        private void OnCalendarClaimed(CalendarClaim claim)
        {
            var e = new AnalyticsEvent(AnalyticsEventId.CalendarDay) { I0 = claim.Number, I1 = claim.Cycle, I2 = claim.Substituted ? 1 : 0 };
            Record(ref e);
        }

        private void OnWelcomeClaimed(int index, GrantedRewards granted)
        {
            var e = new AnalyticsEvent(AnalyticsEventId.WelcomeGift) { I0 = index + 1 };
            Record(ref e);
        }

        private void OnReturnGift(ReturnGift gift)
        {
            var e = new AnalyticsEvent(AnalyticsEventId.ReturnGift) { I0 = gift.Kind, I1 = gift.Warm ? 1 : 0, D0 = gift.DaysAway };
            Record(ref e);
        }

        private void OnJournalClaimed(int goal, GrantedRewards granted)
        {
            if (goal == 0)
            {
                // The completed journal's chest (doc 08: journal_complete, week and day of the week).
                var done = new AnalyticsEvent(AnalyticsEventId.JournalComplete) { I0 = WeekIndex(), I1 = DayOfWeek() };
                Record(ref done);
                return;
            }
            int slot = WeeklyJournalCatalog.TryGet((JournalGoalId)Math.Max(0, goal), out var definition) ? (int)definition.Slot : -1;
            var e = new AnalyticsEvent(AnalyticsEventId.JournalGoal)
            {
                I0 = Math.Max(0, goal),
                I1 = goal > 0 ? slot : -1,
                I2 = WeekIndex(),
                I3 = goal == WeeklyJournalService.CarryClaimId ? 1 : 0,
            };
            Record(ref e);
        }

        /// <summary>Day of the week in the player's time zone, 1 Monday to 7 Sunday.</summary>
        private int DayOfWeek()
        {
            var zone = _progression?.Config.TimeZone ?? TimeZoneInfo.Local;
            var local = TimeZoneInfo.ConvertTime(_clock.UtcNow, zone);
            return local.DayOfWeek == System.DayOfWeek.Sunday ? 7 : (int)local.DayOfWeek;
        }

        /// <summary>The « no ticket » card closed (doc 08, tickets_empty): the choice made, seconds to the next free ticket.</summary>
        public void TicketsEmpty(int choice, double refillSeconds)
        {
            var e = new AnalyticsEvent(AnalyticsEventId.TicketsEmpty) { I0 = choice, D0 = Math.Max(0.0, refillSeconds) };
            Record(ref e);
        }

        /// <summary>Today opened (doc 08, today_open): wishes pending, the gift ready, the calendar's day.</summary>
        public void TodayOpen(int wishesPending, bool giftReady, int calendarDay)
        {
            var e = new AnalyticsEvent(AnalyticsEventId.TodayOpen) { I0 = wishesPending, I1 = giftReady ? 1 : 0, I2 = calendarDay };
            Record(ref e);
        }

        /// <summary>Weeks since install, from 1 (a time-zone conversion, once per claim).</summary>
        private int WeekIndex()
        {
            var zone = _progression?.Config.TimeZone ?? TimeZoneInfo.Local;
            return (ProgressionMath.DayNumber(_state, _clock.UtcNow, zone) - 1) / 7 + 1;
        }

        private void OnTreatGiven(uint animal, ContentId treat, int points)
        {
            var species = _state.Collection.TryGet(animal, out var instance) ? instance.Species : ContentId.None;
            bool preferred = _daily != null && _daily.Bond.IsPreferred(animal, treat);
            var e = new AnalyticsEvent(AnalyticsEventId.TreatGive) { U0 = treat.Value, U1 = species.Value, I0 = points, I1 = preferred ? 1 : 0 };
            Record(ref e);
        }

        private void OnWishDrawn(WishDraw draw)
        {
            // A swap has its own event (WishReroll).
            if (draw.IsSwap)
                return;
            var e = new AnalyticsEvent(AnalyticsEventId.WishDrawn)
            {
                I0 = draw.Template, I1 = (int)draw.System, I2 = draw.Tier, I3 = draw.PendingAfter,
            };
            Record(ref e);
        }

        private void OnWishClaimed(WishClaim claim)
        {
            Milestone(FunnelMilestone.FirstWish);
            var e = new AnalyticsEvent(AnalyticsEventId.WishDone)
            {
                I0 = claim.Template, I1 = (int)claim.Path, I2 = (int)claim.Gems, I3 = claim.PendingAfter, D0 = claim.MinutesFromDraw,
            };
            Record(ref e);
        }

        private void OnWishSwapped(WishSwapInfo swap)
        {
            var e = new AnalyticsEvent(AnalyticsEventId.WishReroll) { I0 = swap.OldTemplate, I1 = swap.NewTemplate };
            Record(ref e);
        }

        // ------------------------------------------------------------------ T37: the activities (doc 08, block 110)

        private ActivityServices? _activities;
        private ActivityStartedHandler? _onActivityStarted;
        private ActivityEndedHandler? _onActivityEnded;
        private Action<ActivityId, int, int, int>? _onChallengeImproved;
        private Action<TicketSource, int, int>? _onTicketsGained;
        private Action<int, int>? _onTicketsRefilled;

        /// <summary>Games started and ended, challenges improved, tickets received and refilled (doc 08 events 110 to 114).</summary>
        public void AttachActivities(ActivityServices? activities)
        {
            DetachActivities();
            _activities = activities;
            if (activities == null)
                return;
            _onActivityStarted ??= OnActivityStarted;
            _onActivityEnded ??= OnActivityEnded;
            _onChallengeImproved ??= OnChallengeImproved;
            _onTicketsGained ??= OnTicketsGained;
            _onTicketsRefilled ??= OnTicketsRefilled;
            activities.Runner.Started += _onActivityStarted;
            activities.Runner.Ended += _onActivityEnded;
            activities.Runner.ChallengeImproved += _onChallengeImproved;
            activities.Tickets.TicketsGained += _onTicketsGained;
            activities.Tickets.Refilled += _onTicketsRefilled;
        }

        private void DetachActivities()
        {
            var a = _activities;
            if (a == null)
                return;
            a.Runner.Started -= _onActivityStarted;
            a.Runner.Ended -= _onActivityEnded;
            a.Runner.ChallengeImproved -= _onChallengeImproved;
            a.Tickets.TicketsGained -= _onTicketsGained;
            a.Tickets.Refilled -= _onTicketsRefilled;
            _activities = null;
        }

        private static uint ActivityContentId(ActivityId activity) =>
            ActivityCatalog.TryGet(activity, out var definition) ? definition.ContentId.Value : (uint)activity;

        private void OnActivityStarted(in ActivityStartInfo info)
        {
            var e = new AnalyticsEvent(AnalyticsEventId.ActivityStart)
            {
                U0 = ActivityContentId(info.Activity), I0 = info.AnalyticsMode, I1 = info.Difficulty, I2 = info.Tickets, I3 = info.Featured ? 1 : 0,
            };
            Record(ref e);
        }

        private void OnActivityEnded(in ActivityGameResult result)
        {
            var e = new AnalyticsEvent(AnalyticsEventId.ActivityEnd)
            {
                U0 = ActivityContentId(result.Activity),
                I0 = result.AnalyticsMode,
                I1 = result.Stars,
                I2 = result.AnalyticsFlags,
                I3 = result.Challenge >= 0 ? result.Challenge + 1 : 0,
                D0 = result.Score,
                D1 = result.Seconds,
            };
            Record(ref e);
            if (!result.Abandoned && result.Activity != ActivityId.Test)
                Milestone(FunnelMilestone.FirstActivity);
        }

        private void OnChallengeImproved(ActivityId activity, int challenge, int stars, int attempts)
        {
            var e = new AnalyticsEvent(AnalyticsEventId.ActivityChallenge)
            {
                U0 = ActivityContentId(activity), I0 = challenge + 1, I1 = stars, I2 = attempts,
            };
            Record(ref e);
        }

        private void OnTicketsGained(TicketSource source, int amount, int balance)
        {
            var e = new AnalyticsEvent(AnalyticsEventId.TicketsGain) { I0 = ActivityAnalytics.TicketSourceCode(source), I1 = amount, I2 = balance };
            Record(ref e);
        }

        /// <summary>The automatic refill is not an event of its own: it is summed in economy_flow (doc 08, event 114).</summary>
        private void OnTicketsRefilled(int count, int balance)
        {
            if (_consent == AnalyticsConsent.Denied || count <= 0)
                return;
            int slot = TicketCurrency * ReasonSlots + TicketRefillReason;
            _flowCounts[slot]++;
            _flowSums[slot] = BigValue.Add(_flowSums[slot], BigValue.FromDouble(count));
        }

        // ------------------------------------------------------------------ T42: the expeditions (doc 08, block 140)

        private ExpeditionService? _expeditions;
        private ExpeditionStartedHandler? _onExpeditionStarted;
        private ExpeditionWelcomedHandler? _onExpeditionWelcomed;
        private Action<int, int, double>? _onExpeditionSpedUp;
        private Action<int, int, int>? _onExpeditionChallenge;

        /// <summary>Teams leaving and welcomed, speed-ups (doc 08 events 140 to 142), challenges improved (112).</summary>
        public void AttachExpeditions(ExpeditionService? expeditions)
        {
            DetachExpeditions();
            _expeditions = expeditions;
            if (expeditions == null)
                return;
            _onExpeditionStarted ??= OnExpeditionStarted;
            _onExpeditionWelcomed ??= OnExpeditionWelcomed;
            _onExpeditionSpedUp ??= OnExpeditionSpedUp;
            _onExpeditionChallenge ??= OnExpeditionChallenge;
            expeditions.Started += _onExpeditionStarted;
            expeditions.Welcomed += _onExpeditionWelcomed;
            expeditions.SpedUp += _onExpeditionSpedUp;
            expeditions.ChallengeImproved += _onExpeditionChallenge;
        }

        private void DetachExpeditions()
        {
            var e = _expeditions;
            if (e == null)
                return;
            e.Started -= _onExpeditionStarted;
            e.Welcomed -= _onExpeditionWelcomed;
            e.SpedUp -= _onExpeditionSpedUp;
            e.ChallengeImproved -= _onExpeditionChallenge;
            _expeditions = null;
        }

        private int ExpeditionHours(ExpeditionDuration duration, bool firstStroll) =>
            firstStroll ? 0 : (int)Math.Round(_expeditions?.Config.Hours(duration) ?? ExpeditionConfig.Default.Hours(duration));

        private void OnExpeditionStarted(in ExpeditionStartInfo info)
        {
            var e = new AnalyticsEvent(AnalyticsEventId.ExpeditionStart)
            {
                U0 = info.Destination.Value, I0 = ExpeditionHours(info.Duration, info.FirstStroll), I1 = info.TeamSize, I2 = info.Slot,
            };
            Record(ref e);
        }

        private void OnExpeditionWelcomed(in ExpeditionWelcomeInfo info)
        {
            var e = new AnalyticsEvent(AnalyticsEventId.ExpeditionReturn)
            {
                U0 = info.Return.Destination.Value,
                I0 = ExpeditionHours(info.Return.Duration, info.Return.FirstStroll),
                I1 = info.RewardMask,
                I2 = info.Return.Loot.DiscoveryEggs,
                I3 = info.Return.Loot.EventCurrency,
            };
            Record(ref e);
        }

        private void OnExpeditionSpedUp(int slot, int gems, double seconds)
        {
            var e = new AnalyticsEvent(AnalyticsEventId.ExpeditionSpeedup) { I0 = 1, I1 = gems, D0 = seconds };
            Record(ref e);
        }

        private void OnExpeditionChallenge(int challenge, int stars, int attempts)
        {
            var e = new AnalyticsEvent(AnalyticsEventId.ActivityChallenge)
            {
                U0 = ActivityContentId(ActivityId.Expeditions), I0 = challenge + 1, I1 = stars, I2 = attempts,
            };
            Record(ref e);
        }

        // ------------------------------------------------------------------ T43: the garden (doc 08, block 150)

        private GardenService? _garden;
        private GardenPlantedHandler? _onGardenPlanted;
        private GardenHarvestedHandler? _onGardenHarvested;
        private Action<int, int, double>? _onGardenSpedUp;
        private Action<int, int, int>? _onGardenChallenge;

        /// <summary>Seeds planted, harvests, speed-ups (doc 08 events 150 to 152), challenges improved (112).</summary>
        public void AttachGarden(GardenService? garden)
        {
            DetachGarden();
            _garden = garden;
            if (garden == null)
                return;
            _onGardenPlanted ??= OnGardenPlanted;
            _onGardenHarvested ??= OnGardenHarvested;
            _onGardenSpedUp ??= OnGardenSpedUp;
            _onGardenChallenge ??= OnGardenChallenge;
            garden.Planted += _onGardenPlanted;
            garden.Harvested += _onGardenHarvested;
            garden.SpedUp += _onGardenSpedUp;
            garden.ChallengeImproved += _onGardenChallenge;
        }

        private void DetachGarden()
        {
            var g = _garden;
            if (g == null)
                return;
            g.Planted -= _onGardenPlanted;
            g.Harvested -= _onGardenHarvested;
            g.SpedUp -= _onGardenSpedUp;
            g.ChallengeImproved -= _onGardenChallenge;
            _garden = null;
        }

        private void OnGardenPlanted(in GardenPlantInfo info)
        {
            var e = new AnalyticsEvent(AnalyticsEventId.GardenPlant) { U0 = info.Plant.Value, I0 = info.Plot, D0 = info.GrowSeconds };
            Record(ref e);
        }

        /// <summary>One event for the products (yield_kind 1 for a flower, 2 for a fruit, vegetable or grain), one more for the dyes (3).</summary>
        private void OnGardenHarvested(in GardenHarvestInfo info)
        {
            var e = new AnalyticsEvent(AnalyticsEventId.GardenHarvest)
            {
                U0 = info.Harvest.Plant.Value,
                I0 = info.Kind == GardenPlantKind.Flower ? 1 : 2,
                I1 = info.Harvest.MainCount,
                I2 = info.Harvest.Watered ? 1 : 0,
            };
            Record(ref e);
            if (info.Harvest.Dyes <= 0)
                return;
            var dye = new AnalyticsEvent(AnalyticsEventId.GardenHarvest)
            {
                U0 = info.Harvest.Plant.Value, I0 = 3, I1 = info.Harvest.Dyes, I2 = info.Harvest.Watered ? 1 : 0,
            };
            Record(ref dye);
        }

        private void OnGardenSpedUp(int plot, int gems, double seconds)
        {
            var e = new AnalyticsEvent(AnalyticsEventId.GardenSpeedup) { I0 = 1, I1 = gems, D0 = seconds };
            Record(ref e);
        }

        private void OnGardenChallenge(int challenge, int stars, int attempts)
        {
            var e = new AnalyticsEvent(AnalyticsEventId.ActivityChallenge)
            {
                U0 = ActivityContentId(ActivityId.Garden), I0 = challenge + 1, I1 = stars, I2 = attempts,
            };
            Record(ref e);
        }

        // ------------------------------------------------------------------ T44A: the albums (block 160)

        private AlbumService? _albums;
        private AlbumGainHandler? _onAlbumGain;
        private Action<int>? _onAlbumStarSpent;
        private AlbumRewardHandler? _onAlbumReward;

        /// <summary>Stickers gained, stars spent, pages half and complete, albums complete (events 160 to 164).</summary>
        public void AttachAlbums(AlbumService? albums)
        {
            DetachAlbums();
            _albums = albums;
            if (albums == null)
                return;
            _onAlbumGain ??= OnAlbumGain;
            _onAlbumStarSpent ??= OnAlbumStarSpent;
            _onAlbumReward ??= OnAlbumReward;
            albums.StickerGained += _onAlbumGain;
            albums.StarsSpent += _onAlbumStarSpent;
            albums.RewardEarned += _onAlbumReward;
        }

        private void DetachAlbums()
        {
            var a = _albums;
            if (a == null)
                return;
            a.StickerGained -= _onAlbumGain;
            a.StarsSpent -= _onAlbumStarSpent;
            a.RewardEarned -= _onAlbumReward;
            _albums = null;
        }

        /// <summary>The odds screen of the albums was opened (event 165; the albums screen calls it).</summary>
        public void AlbumInfoOpened()
        {
            int owned = 0;
            var albums = _albums;
            if (albums != null)
                for (int p = 0; p < albums.PageCount; p++)
                    owned += albums.OwnedOnPage(p);
            var e = new AnalyticsEvent(AnalyticsEventId.AlbumInfoOpened) { I0 = owned };
            Record(ref e);
        }

        private void OnAlbumGain(in AlbumGainInfo info)
        {
            var e = new AnalyticsEvent(AnalyticsEventId.AlbumStickerGained)
            {
                I0 = info.Sticker, I1 = (int)info.Source, I2 = info.New ? 1 : 0, I3 = (int)info.Rarity, D0 = info.Page,
            };
            Record(ref e);
        }

        private void OnAlbumStarSpent(int sticker)
        {
            var e = new AnalyticsEvent(AnalyticsEventId.AlbumStarSpent) { I0 = sticker, I1 = _albums?.AlbumStars ?? 0 };
            Record(ref e);
        }

        private void OnAlbumReward(in AlbumReward reward)
        {
            var id = reward.Kind == AlbumRewardKind.PageHalf ? AnalyticsEventId.AlbumPageHalf
                : reward.Kind == AlbumRewardKind.PageComplete ? AnalyticsEventId.AlbumPageComplete
                : AnalyticsEventId.AlbumComplete;
            var e = new AnalyticsEvent(id) { I0 = reward.Kind == AlbumRewardKind.AlbumComplete ? reward.Album : reward.Page, I1 = reward.Album };
            Record(ref e);
        }

        // ------------------------------------------------------------------ T44B: achievements and profile items (block 170)

        private AchievementService? _achievements;
        private ProfileItemService? _profileItems;
        private Action<int, AchievementTier>? _onAchievementTier;
        private ProfileItemHandler? _onProfileSelection;

        /// <summary>Tiers reached (event 170: id, tier), the title and the frame shown (events 171 and 172).</summary>
        public void AttachAchievements(AchievementService? achievements, ProfileItemService? items)
        {
            DetachAchievements();
            _achievements = achievements;
            _profileItems = items;
            _onAchievementTier ??= OnAchievementTier;
            _onProfileSelection ??= OnProfileSelection;
            if (achievements != null)
                achievements.TierReached += _onAchievementTier;
            if (items != null)
                items.SelectionChanged += _onProfileSelection;
        }

        private void DetachAchievements()
        {
            if (_achievements != null)
                _achievements.TierReached -= _onAchievementTier;
            if (_profileItems != null)
                _profileItems.SelectionChanged -= _onProfileSelection;
            _achievements = null;
            _profileItems = null;
        }

        private void OnAchievementTier(int index, AchievementTier tier)
        {
            var e = new AnalyticsEvent(AnalyticsEventId.AchievementTier) { I0 = index + 1, I1 = (int)tier };
            Record(ref e);
        }

        private void OnProfileSelection(ProfileItemKind kind, int index)
        {
            var id = kind == ProfileItemKind.Title ? AnalyticsEventId.ProfileTitleSelected : AnalyticsEventId.ProfileFrameSelected;
            var e = new AnalyticsEvent(id) { I0 = index };
            Record(ref e);
        }

        public void Dispose()
        {
            DetachAchievements();
            DetachAlbums();
            DetachGarden();
            DetachExpeditions();
            DetachActivities();
            DetachDaily();
            DetachProgression();
            Detach();
            _sink?.Dispose();
        }
    }
}
