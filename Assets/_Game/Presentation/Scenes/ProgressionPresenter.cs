using System;
using System.Text;
using PocketWilds.Core;
using PocketWilds.Services.Feedback;
using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T35A: the progression screens and HUD parts of the refuge (doc 02 Améliorations, section 11). It reads the
    /// progression services, builds what each view shows (texts localized here, once per change), and turns the
    /// player's touches into purchases. Views never see a service. A plain class ticked by the scene entry: the cards
    /// are rebuilt only when what they show changes (a purchase, the Light crossing a price), never per frame.
    /// </summary>
    public sealed class ProgressionPresenter : IUpgradesModel, IUpgradesActions, IZoneOpenActions, IRefugeLevelActions,
        ILevelRewardActions, IBiomeGateActions, IProgressionHudActions, IDisposable
    {
        public const int Cards = 3;

        /// <summary>Doc 02, section 3.6: the dot shows for an upgrade worth at least 5% of the production.</summary>
        public const double DotGain = 0.05;

        /// <summary>Cozy Nests' estimated gain: the saving on the biome's next 10 eggs over 600 seconds (section 3.6).</summary>
        public const int NestsLookahead = 10;
        public const double NestsHorizonSeconds = 600.0;

        private const float CheckInterval = 0.25f;
        private const float HintDelay = 1.5f;
        /// <summary>The level card waits for a calm moment: this long without a hatch or a screen (never cuts a hatching run).</summary>
        public const float RewardIdleSeconds = 4f;
        private const float EffectSpacing = 0.15f;
        private const string QtyPreference = "pw.upgrades.qty";

        private readonly GameContext _context;
        private readonly ProgressionServices _progression;
        private readonly RefugeController _refuge;
        private readonly RefugeZones _zones;
        private readonly RefugeCameraRig _rig;
        private readonly IProgressionHud _hud;
        private readonly IRefugeHud _refugeHud;
        private readonly IUpgradesView _upgrades;
        private readonly IZoneOpenView _zoneView;
        private readonly IRefugeLevelView _levelView;
        private readonly ILevelRewardView _rewardView;
        private readonly IBiomeGateView _gateView;
        private readonly ContentId _biome;
        private readonly string _biomeSourceId;
        private readonly UpgradeCardModel[] _cards = new UpgradeCardModel[Cards];
        private readonly char[] _number = new char[BigValueFormatter.MaxLength];
        private readonly StringBuilder _builder = new StringBuilder(96);
        private readonly Action<UpgradePurchase> _onPurchase;
        private readonly Action<RefugeLevelUp> _onLevelUp;
        private readonly Action<BiomeMasteryGain> _onBiomeMastery;
        private readonly Action _onSignTapped;

        private PurchaseQty _qty;
        private UpgradesFooter _footer;
        private int _signature = int.MinValue;
        private float _nextCheck;
        private bool _dot;
        private bool _upgradeShown;
        private int _upgradeState = -1;
        private float _hintIdle;
        private bool _hintShown;
        private bool _openedThisSession;
        private bool _rewardPending;
        private bool _rewardNow;
        private float _lastBusy = -1f;
        private float _lastEffect = -10f;
        private float _time;
        private int _levelShown = -1;
        private float _fractionShown = -1f;
        private ContentId _gateBiome;

        public ProgressionPresenter(GameContext context, RefugeController refuge, RefugeZones zones, RefugeCameraRig rig,
            IProgressionHud hud, IUpgradesView upgrades, IZoneOpenView zoneView, IRefugeLevelView levelView, ILevelRewardView rewardView,
            IBiomeGateView gateView)
        {
            _context = context;
            _progression = context.Progression ?? throw new ArgumentException("No progression services.", nameof(context));
            _refuge = refuge;
            _zones = zones;
            _rig = rig;
            _hud = hud;
            _refugeHud = hud as IRefugeHud;
            _upgrades = upgrades;
            _zoneView = zoneView;
            _levelView = levelView;
            _rewardView = rewardView;
            _gateView = gateView;
            _biome = _progression.FirstBiome;
            _biomeSourceId = _progression.Config.Biomes[0].SourceId;
            _qty = (PurchaseQty)Mathf.Clamp(PlayerPrefs.GetInt(QtyPreference, 0), 0, 2);
            _onPurchase = OnPurchased;
            _onLevelUp = OnLevelUp;
            _onBiomeMastery = OnBiomeMasteryUp;
            _onSignTapped = OnBoundaryTapped;
            _progression.Upgrades.Purchased += _onPurchase;
            _progression.Refuge.LeveledUp += _onLevelUp;
            _progression.Mastery.LeveledUp += _onBiomeMastery;
            if (_zones != null)
                _zones.SignTapped += _onSignTapped;

            _hud?.BindProgression(this);
            // Built once now, behind the loading screen (T28 lesson: a first opening never hitches).
            Rebuild();
            var loc = context.Localization;
            _upgrades?.Prewarm(this, loc, this);
            _zoneView?.Prewarm(loc);
            _levelView?.Prewarm(loc);
            _rewardView?.Prewarm(loc);
            _gateView?.Prewarm(loc);
            // Rewards of levels reached before (an older save, a level reached offline) wait on their card.
            _rewardPending = _progression.Refuge.PendingRewards().Any;
        }

        /// <summary>One of the progression screens covers the refuge: world touches and hatching wait.</summary>
        public bool IsOpen =>
            (_upgrades != null && _upgrades.IsOpen) || (_zoneView != null && _zoneView.IsOpen) || (_levelView != null && _levelView.IsOpen)
            || (_rewardView != null && _rewardView.IsOpen) || (_gateView != null && _gateView.IsOpen);

        public bool UpgradesUnlocked => _progression.Refuge.IsFeatureUnlocked(RefugeFeature.UpgradeButton);
        public bool DotShown => _dot;
        public bool HintShown => _hintShown;
        public bool RewardWaiting => _rewardPending;
        public ContentId Biome => _biome;

        // ---------------- IUpgradesModel ----------------

        public PurchaseQty Qty => _qty;
        public BigValue Rate => _context.Economy.GetLightPerSecond();
        public string BiomeName => Text(_biomeSourceId + ".name");
        public int CardCount => Cards;
        public UpgradeCardModel GetCard(int index) => _cards[index];
        public UpgradesFooter Footer => _footer;

        // ---------------- Tick ----------------

        public void Tick(float time, float deltaTime)
        {
            _time = time;
            _upgrades?.Tick(deltaTime);
            _zoneView?.Tick(deltaTime);
            _levelView?.Tick(deltaTime);
            _rewardView?.Tick(deltaTime);
            _gateView?.Tick(deltaTime);
            if (_hud != null)
            {
                var refuge = _progression.Refuge;
                float fraction = refuge.LevelFraction;
                if (refuge.Level != _levelShown || Mathf.Abs(fraction - _fractionShown) > 0.002f)
                {
                    _levelShown = refuge.Level;
                    _fractionShown = fraction;
                    _hud.SetRefugeLevel(_levelShown, fraction);
                }
            }
            if (time >= _nextCheck)
            {
                _nextCheck = time + CheckInterval;
                Check();
            }
            UpdateHint(deltaTime);
            bool busy = !Calm();
            if (busy || _lastBusy < 0f)
                _lastBusy = time;
            if (_rewardPending && !busy && (_rewardNow || time - _lastBusy >= RewardIdleSeconds))
                ShowRewards();
            _hud?.SetLevelReward(_rewardPending);
        }

        /// <summary>Four times a second: the button, its dot, and the open screen when a price was crossed.</summary>
        private void Check()
        {
            bool unlocked = UpgradesUnlocked;
            int signature = Signature();
            if (signature != _signature)
            {
                _signature = signature;
                Rebuild();
                if (_upgrades != null && _upgrades.IsOpen)
                    _upgrades.Refresh();
            }
            if (_zoneView != null && _zoneView.IsOpen && _progression.Zones.TryGetNext(_biome, out var zone))
                _zoneView.Refresh(BuildZoneData(zone));
            _dot = unlocked && ComputeDot();
            if (_hud == null)
                return;
            int state = unlocked ? 1 : 0;
            // T34A pattern: the unlock line only when the button arrives during play, never for one already there.
            if (_upgradeState == 0 && state == 1)
            {
                _hud.SetUpgrade(true, _dot);
                _context.Audio.Play(AudioEventId.Claim, Vector3.zero, default);
                _refugeHud?.ShowToast(ToastKind.Unlock, Text(ProgressionText.UpgradesTutorial));
            }
            _upgradeState = state;
            _hud.SetUpgrade(unlocked, _dot);
            _upgradeShown = unlocked;
        }


        /// <summary>What the cards show, packed: a change means a rebuild (Light crossed a price, a level, a mastery).</summary>
        private int Signature()
        {
            unchecked
            {
                int hash = (int)_qty * 31 + _progression.Mastery.GetLevel(_biome);
                for (int i = 0; i < Cards; i++)
                {
                    var quote = _progression.Upgrades.Quote(_biome, (UpgradeKind)i, _qty);
                    hash = hash * 397 ^ (quote.LevelFrom * 7 + quote.Levels * 131 + (quote.Affordable ? 1 : 0) + (quote.Maxed ? 2 : 0)
                                         + (quote.Locked ? 4 : 0));
                }
                if (_progression.Zones.TryGetNext(_biome, out var zone))
                    hash = hash * 397 ^ ((int)_progression.Zones.GetState(zone) + zone.Index * 16);
                return hash;
            }
        }

        // ---------------- Cards ----------------

        private void Rebuild()
        {
            var upgrades = _progression.Upgrades;
            var biomeRate = _context.Economy.GetBiomeLightPerSecond(_biome);
            double bestPayback = double.MaxValue;
            int best = -1;
            for (int i = 0; i < Cards; i++)
            {
                var kind = (UpgradeKind)i;
                var quote = upgrades.Quote(_biome, kind, _qty);
                var card = new UpgradeCardModel
                {
                    Kind = kind,
                    Name = UpgradeName(kind),
                    Level = upgrades.GetLevel(_biome, kind),
                    Price = quote.Cost,
                    Levels = quote.Levels,
                };
                if (quote.Maxed)
                {
                    card.State = UpgradeCardState.Maxed;
                    card.Gain = TotalEffect(kind, card.Level);
                    card.Detail = string.Empty;
                }
                else if (quote.Locked)
                {
                    card.State = UpgradeCardState.Locked;
                    card.Gain = Gain(kind, quote.LevelFrom, quote.LevelFrom + 1);
                    card.Detail = string.Empty;
                    card.LockText = MasteryLine(quote.LockedUntilMastery);
                }
                else
                {
                    card.State = quote.Affordable ? UpgradeCardState.Affordable : UpgradeCardState.NotEnough;
                    card.Gain = Gain(kind, quote.LevelFrom, quote.LevelTo);
                    card.Detail = Detail(kind, quote, biomeRate);
                    if (quote.Affordable && kind != UpgradeKind.Curiosity)
                    {
                        var perSecond = GainPerSecond(kind, quote.LevelFrom, quote.LevelTo, biomeRate);
                        if (!perSecond.IsZero)
                        {
                            double payback = BigValue.Divide(quote.Cost, perSecond).ToDoubleClamped();
                            if (payback < bestPayback)
                            {
                                bestPayback = payback;
                                best = i;
                            }
                        }
                    }
                }
                _cards[i] = card;
            }
            if (best >= 0)
                _cards[best].Best = true;
            _footer = BuildFooter();
        }

        /// <summary>Light per second a purchase adds (Production) or saves on the next eggs (Cozy Nests, section 3.6).</summary>
        public BigValue GainPerSecond(UpgradeKind kind, int from, int to, BigValue biomeRate)
        {
            var upgrades = _progression.Upgrades;
            if (kind == UpgradeKind.Production)
                return BigValue.Multiply(biomeRate, upgrades.ProductionMultiplierAt(to) / upgrades.ProductionMultiplierAt(from) - 1.0);
            if (kind == UpgradeKind.Nests && _refuge != null)
            {
                double saved = 1.0 - Math.Pow(_progression.Config.NestsStep, to - from);
                return BigValue.Multiply(_refuge.EggCost, saved * NestsLookahead / NestsHorizonSeconds);
            }
            return BigValue.Zero;
        }

        /// <summary>
        /// Section 3.6: one affordable purchase worth at least 5% of the whole production (one level, or the biome's
        /// next ×2 milestone when it is affordable).
        /// </summary>
        private bool ComputeDot()
        {
            var upgrades = _progression.Upgrades;
            var total = _context.Economy.GetLightPerSecond();
            if (total.IsZero)
                return false;
            var biomeRate = _context.Economy.GetBiomeLightPerSecond(_biome);
            for (int i = 0; i < 2; i++)
            {
                var kind = (UpgradeKind)i;
                var one = upgrades.Quote(_biome, kind, PurchaseQty.One);
                if (one.Maxed || one.Locked || !one.Affordable)
                    continue;
                var gain = GainPerSecond(kind, one.LevelFrom, one.LevelTo, biomeRate);
                if (BigValue.Divide(gain, total).ToDoubleClamped() >= DotGain)
                    return true;
                if (kind == UpgradeKind.Production)
                {
                    int every = Math.Max(1, _progression.Config.ProductionMilestoneEvery);
                    int toMilestone = every - one.LevelFrom % every;
                    var price = ProgressionMath.GeometricTotal(one.Cost, upgrades.Growth(kind), toMilestone);
                    if (_context.Economy.CanSpend(CurrencyId.Light, price))
                        return true;
                }
            }
            return false;
        }

        private UpgradesFooter BuildFooter()
        {
            var footer = new UpgradesFooter();
            var mastery = _progression.Mastery.Get(_biome);
            footer.MasteryTitle = Text(ProgressionText.BiomeMastery(_biomeSourceId));
            footer.MasteryLevel = mastery.Level;
            footer.MasteryFraction = mastery.Fraction;
            footer.MasteryNext = Format(Format(Text(ProgressionText.UpgradesMasteryNext), "{n}", (mastery.Level + 1).ToString()),
                "{reward}", MasteryReward(mastery.Level + 1));
            if (_progression.Zones.TryGetNext(_biome, out var zone))
            {
                footer.HasZone = true;
                footer.ZoneLabel = Text(ProgressionText.UpgradesNextZone);
                footer.ZoneTitle = Capitalized(Text(zone.DisplayKey));
                footer.ZonePrice = _progression.Zones.Price(zone);
                var state = _progression.Zones.GetState(zone);
                footer.ZoneAffordable = state == ZoneState.Available;
                footer.ZoneCondition = state == ZoneState.NeedsMastery ? MasteryLine(_progression.Zones.RequiredMastery(zone)) : string.Empty;
            }
            else if (_progression.Gates.TryGetNextLocked(out var biome))
            {
                // Every zone is open: the block shows the next biome and its gate (See glides to it).
                var gate = BuildGateData(biome);
                string source = _progression.Config.Biomes[Math.Max(0, _progression.Config.BiomeOrder(biome))].SourceId;
                footer.HasZone = true;
                footer.ZoneLabel = Text(ProgressionText.UpgradesNextBiome);
                footer.ZoneTitle = Capitalized(BiomeWithArticle(source));
                footer.ZonePrice = gate.Price;
                footer.ZoneAffordable = gate.CanOpen;
                footer.ZoneCondition = gate.Soon ? Text(ProgressionText.BiomeGateSoon) : FirstUnmet(gate);
            }
            return footer;
        }

        /// <summary>What the next biome mastery level brings: an upgrade, a zone, or its +2% Light.</summary>
        private string MasteryReward(int level)
        {
            var config = _progression.Config;
            if (level == config.NestsUnlockMastery)
                return UpgradeName(UpgradeKind.Nests);
            if (level == config.CuriosityUnlockMastery)
                return UpgradeName(UpgradeKind.Curiosity);
            foreach (var zone in config.Zones)
                if (zone.Biome == _biome && zone.Role == ZoneRole.Expansion && _progression.Zones.RequiredMastery(zone) == level
                    && !_progression.Zones.IsOpen(zone))
                    return Format(Text(ProgressionText.ZoneOpenTitle), "{zone}", Text(zone.DisplayKey));
            return Format(Text(ProgressionText.ProductionEffect), "{pct}", Percent(config.MasteryProductionStep * 100.0));
        }

        private string UpgradeName(UpgradeKind kind)
        {
            switch (kind)
            {
                case UpgradeKind.Production: return Format(Text(ProgressionText.ProductionName), "{biome}", BiomeName);
                case UpgradeKind.Nests: return Text(ProgressionText.NestsName);
                default: return Text(ProgressionText.CuriosityName);
            }
        }

        /// <summary>What buying from level <paramref name="from"/> to <paramref name="to"/> brings ("+12% Light").</summary>
        private string Gain(UpgradeKind kind, int from, int to)
        {
            var upgrades = _progression.Upgrades;
            var config = _progression.Config;
            switch (kind)
            {
                case UpgradeKind.Production:
                    double ratio = upgrades.ProductionMultiplierAt(to) / upgrades.ProductionMultiplierAt(from) - 1.0;
                    return Format(Text(ProgressionText.ProductionEffect), "{pct}", Percent(ratio * 100.0));
                case UpgradeKind.Nests:
                    double saved = 1.0 - Math.Pow(config.NestsStep, to - from);
                    return Format(Text(ProgressionText.NestsEffect), "{pct}", Percent(saved * 100.0));
                default:
                    return Format(Text(ProgressionText.CuriosityEffect), "{pct}", Percent((to - from) * config.CuriosityPerLevel * 100.0));
            }
        }

        private string TotalEffect(UpgradeKind kind, int level)
        {
            var config = _progression.Config;
            string effect;
            switch (kind)
            {
                case UpgradeKind.Production:
                    effect = Format(Text(ProgressionText.ProductionEffect), "{pct}",
                        Percent((_progression.Upgrades.ProductionMultiplierAt(level) - 1.0) * 100.0));
                    break;
                case UpgradeKind.Nests:
                    effect = Format(Text(ProgressionText.NestsEffect), "{pct}", Percent((1.0 - Math.Pow(config.NestsStep, level)) * 100.0));
                    break;
                default:
                    effect = Format(Text(ProgressionText.CuriosityEffect), "{pct}", Percent(level * config.CuriosityPerLevel * 100.0));
                    break;
            }
            return Format(Text(ProgressionText.UpgradesTotal), "{effect}", effect);
        }

        private string Detail(UpgradeKind kind, in UpgradeQuote quote, BigValue biomeRate)
        {
            if (kind != UpgradeKind.Production)
                return quote.LevelFrom > 0 ? TotalEffect(kind, quote.LevelFrom) : string.Empty;
            int every = Math.Max(1, _progression.Config.ProductionMilestoneEvery);
            int left = every - quote.LevelFrom % every;
            if (left > quote.Levels && left <= 5)
                return Text(ProgressionText.ProductionMilestone);
            var upgrades = _progression.Upgrades;
            var after = BigValue.Multiply(biomeRate, upgrades.ProductionMultiplierAt(quote.LevelTo) / upgrades.ProductionMultiplierAt(quote.LevelFrom));
            return Format(Text(ProgressionText.UpgradesRateAfter), "{rate}", FormatLight(after));
        }

        // ---------------- IUpgradesActions ----------------

        public void OpenUpgrades()
        {
            if (_upgrades == null || !UpgradesUnlocked || IsOpen || (_refuge != null && (_refuge.ModalOpen || _refuge.Hatching)))
                return;
            _openedThisSession = true;
            Rebuild();
            _signature = Signature();
            _upgrades.Open(this, _context.Localization, this);
            _context.Audio.Play(AudioEventId.PanelOpen, Vector3.zero, default);
            _context.Haptics.Play(HapticEventId.TapLight);
        }

        public void SetQty(PurchaseQty qty)
        {
            if (qty == _qty)
                return;
            _qty = qty;
            PlayerPrefs.SetInt(QtyPreference, (int)qty);
            _context.Audio.Play(AudioEventId.UiTab, Vector3.zero, default);
            Rebuild();
            _signature = Signature();
            _upgrades?.Refresh();
        }

        public void BuyUpgrade(int card)
        {
            if (card < 0 || card >= Cards)
                return;
            var kind = (UpgradeKind)card;
            if (!_progression.Upgrades.TryBuy(_biome, kind, _qty, out var purchase))
            {
                var quote = _progression.Upgrades.Quote(_biome, kind, _qty);
                if (!quote.Maxed && !quote.Locked)
                {
                    _context.Audio.Play(AudioEventId.ErrorSoft, Vector3.zero, default);
                    _context.Haptics.Play(HapticEventId.ErrorSoft);
                    _upgrades?.PlayNotEnough(card);
                    _refugeHud?.PulseNotEnough();
                }
                return;
            }
            // Purchased raised OnPurchased: the cards are rebuilt there.
            if (_upgrades != null && _upgrades.IsOpen)
            {
                _upgrades.Refresh();
                // Repeated ×1 purchases play at most one effect every 0.15 s (section 11.6).
                if (purchase.MilestoneCrossed || _time - _lastEffect >= EffectSpacing)
                {
                    _lastEffect = _time;
                    _upgrades.PlayPurchase(card, purchase.MilestoneCrossed);
                }
            }
        }

        public void ShowNextZone()
        {
            _upgrades?.Close();
            _context.Audio.Play(AudioEventId.PanelClose, Vector3.zero, default);
            if (_zones != null && _rig != null && _zones.TryGetSignPosition(out var sign))
                _rig.GlideTo(sign);
        }

        public void CloseUpgrades()
        {
            if (_upgrades == null || !_upgrades.IsOpen)
                return;
            _upgrades.Close();
            _context.Audio.Play(AudioEventId.PanelClose, Vector3.zero, default);
        }

        private void OnPurchased(UpgradePurchase purchase)
        {
            _context.Audio.Play(purchase.MilestoneCrossed ? AudioEventId.UpgradeMilestone : AudioEventId.UpgradeBuy, Vector3.zero, default);
            _context.Haptics.Play(purchase.MilestoneCrossed ? HapticEventId.Rare : HapticEventId.TapLight);
            _context.Save?.RequestSave(SaveUrgency.Normal);
            _refuge?.OnUpgradePurchased(purchase);
            Rebuild();
            _signature = Signature();
            _dot = ComputeDot();
            _hud?.SetUpgrade(_upgradeShown, _dot);
        }

        // ---------------- Tutorial ----------------

        /// <summary>
        /// The tutorial paw taps on the Upgrades button (section 11.1) until the player opens the screen, and never
        /// again once a first upgrade was bought. It waits for a calm moment and an affordable first level.
        /// </summary>
        private void UpdateHint(float deltaTime)
        {
            if (_hud == null)
                return;
            bool wanted = _upgradeShown && !_openedThisSession && !_context.State.Analytics.Has(FunnelMilestone.FirstUpgrade)
                          && !IsOpen && (_refuge == null || (!_refuge.ModalOpen && !_refuge.Hatching))
                          && _cards[0].State == UpgradeCardState.Affordable;
            _hintIdle = wanted ? _hintIdle + deltaTime : 0f;
            bool show = wanted && _hintIdle >= HintDelay;
            if (show == _hintShown)
                return;
            _hintShown = show;
            _hud.SetUpgradeHint(show);
        }

        // ---------------- Zone window ----------------

        /// <summary>The hedge, the sign or the gate was touched: the next zone's window, or the next biome's gate.</summary>
        private void OnBoundaryTapped()
        {
            if (_zones != null && _zones.NextIsPassage && _progression.Gates.TryGetNextLocked(out var biome))
                ShowBiomeGate(biome);
            else
                ShowZoneWindow();
        }

        /// <summary>The next zone's window (a touch on its hedge or sign).</summary>
        public void ShowZoneWindow()
        {
            if (_zoneView == null || IsOpen || (_refuge != null && _refuge.ModalOpen) || !_progression.Zones.TryGetNext(_biome, out var zone))
                return;
            _zoneView.Open(BuildZoneData(zone), _context.Localization, this);
            _context.Audio.Play(AudioEventId.PanelOpen, Vector3.zero, default);
            _context.Haptics.Play(HapticEventId.TapLight);
        }

        public ZoneOpenData BuildZoneData(in ZoneDefinition zone)
        {
            var zones = _progression.Zones;
            var state = zones.GetState(zone);
            var data = new ZoneOpenData
            {
                Title = Format(Text(ProgressionText.ZoneOpenTitle), "{zone}", Text(zone.DisplayKey)),
                GainProduction = Text(ProgressionText.ZoneGainProduction),
                GainAnimals = Text(ProgressionText.ZoneGainAnimals),
                // "6 decoration spots" waits for the decoration mode: never promise what the build does not have.
                GainDecor = string.Empty,
                Price = zones.Price(zone),
                CanOpen = state == ZoneState.Available,
                Condition = string.Empty,
                Capture = _zones != null ? _zones.CaptureFor(zone) : null,
            };
            if (state == ZoneState.NeedsMastery)
            {
                var mastery = _progression.Mastery.Get(_biome);
                int needed = zones.RequiredMastery(zone);
                data.Condition = Format(Format(Text(ProgressionText.ZoneLockedMastery), "{mastery}", Text(ProgressionText.BiomeMastery(_biomeSourceId))),
                    "{level}", needed.ToString());
                data.ConditionFraction = needed > 0 ? Mathf.Clamp01((mastery.Level + mastery.Fraction) / needed) : 0f;
            }
            else if (state == ZoneState.TooExpensive)
            {
                data.Condition = Text(ProgressionText.ZoneNeedLight);
                data.ConditionFraction = (float)Math.Min(1.0, BigValue.Divide(_context.Economy.Light, data.Price).ToDoubleClamped());
            }
            return data;
        }

        public void OpenZone()
        {
            if (!_progression.Zones.TryGetNext(_biome, out var zone) || !_progression.Zones.TryOpen(zone.Id, out _))
            {
                _context.Audio.Play(AudioEventId.ErrorSoft, Vector3.zero, default);
                _context.Haptics.Play(HapticEventId.ErrorSoft);
                return;
            }
            // The world plays the opening (RefugeZones listens to Zones.Opened); the window steps aside.
            _zoneView?.Close();
            _context.Save?.RequestSave(SaveUrgency.Priority);
        }

        public void ZoneWindowClosed() => _context.Audio.Play(AudioEventId.UiBack, Vector3.zero, default);

        // ---------------- Refuge level ----------------

        public void OpenRefugeLevel()
        {
            if (IsOpen || (_refuge != null && _refuge.ModalOpen))
                return;
            // A waiting reward card comes first: the badge's dot said so.
            if (_rewardPending)
            {
                ShowRewards();
                return;
            }
            if (_levelView == null)
                return;
            _levelView.Open(BuildLevelData(), _context.Localization, this);
            _context.Audio.Play(AudioEventId.PanelOpen, Vector3.zero, default);
            _context.Haptics.Play(HapticEventId.TapLight);
        }

        public void CloseRefugeLevel()
        {
            _levelView?.Close();
            _context.Audio.Play(AudioEventId.PanelClose, Vector3.zero, default);
        }

        public RefugeLevelData BuildLevelData()
        {
            var refuge = _progression.Refuge;
            var breakdown = refuge.Breakdown();
            var data = new RefugeLevelData
            {
                Level = refuge.Level,
                Fraction = refuge.LevelFraction,
                Score = refuge.Score.ToString() + " / " + refuge.ScoreForLevel(refuge.Level + 1).ToString(),
                SourceNames = new string[RefugeLevelData.MaxSources],
                SourceValues = new long[RefugeLevelData.MaxSources],
                Track = new RefugeTrackEntry[RefugeLevelData.MaxTrack],
            };
            // Without jargon, only what the player already has (Species always, the first thing they find).
            AddSource(ref data, ProgressionText.SourceSpecies, breakdown.Species, true);
            AddSource(ref data, ProgressionText.SourceVariants, breakdown.Variants, false);
            AddSource(ref data, ProgressionText.SourceTraits, breakdown.Traits, false);
            AddSource(ref data, ProgressionText.SourceSpeciesMastery, breakdown.SpeciesMastery, false);
            AddSource(ref data, ProgressionText.SourceZones, breakdown.Zones, false);
            AddSource(ref data, ProgressionText.SourceBiomeMastery, breakdown.BiomeMastery, false);
            AddSource(ref data, ProgressionText.SourceBiomes, breakdown.Biomes, false);
            // The track: the last level reached, then what the next ones bring.
            int first = Math.Max(2, refuge.Level - 1);
            for (int level = first; data.TrackCount < RefugeLevelData.MaxTrack; level++)
            {
                data.Track[data.TrackCount++] = new RefugeTrackEntry
                {
                    Level = level,
                    Reached = level <= refuge.Level,
                    Reward = LevelReward(level),
                };
            }
            return data;
        }

        private void AddSource(ref RefugeLevelData data, string key, long value, bool always)
        {
            if (value <= 0 && !always)
                return;
            data.SourceNames[data.SourceCount] = Text(key);
            data.SourceValues[data.SourceCount] = value;
            data.SourceCount++;
        }

        /// <summary>
        /// What reaching <paramref name="level"/> gives in this build: its Gems, and the functions that open at that
        /// level among those the game has (never a promise of something not there yet).
        /// </summary>
        public string LevelReward(int level)
        {
            var config = _progression.Config;
            _builder.Clear();
            long gems = level <= 50 ? config.LevelGemsUpTo50 : config.LevelGemsAfter50;
            _builder.Append(Format(Text(ProgressionText.RewardGems), "{n}", gems.ToString()));
            var feature = FeatureAt(level);
            if (feature != null)
                _builder.Append(" · ").Append(feature);
            return _builder.ToString();
        }

        /// <summary>Functions of section 6.4 present in this build, by the level that opens them.</summary>
        private string FeatureAt(int level)
        {
            switch (level)
            {
                case 2: return Text(ProgressionText.FeatureShop);
                default: return null;
            }
        }

        private void OnLevelUp(RefugeLevelUp up)
        {
            _hud?.PlayRefugeLevelUp();
            _context.Audio.Play(AudioEventId.RefugeLevelUp, Vector3.zero, default);
            _context.Haptics.Play(HapticEventId.Confirm);
            // The card waits for a calm moment after the badge's celebration (the dot on the badge says it is there).
            _rewardPending = true;
            _lastBusy = _time;
        }

        /// <summary>
        /// A biome's mastery grew (section 11.6): two wooden notes and leaves, and one calm line saying the new level
        /// (the Upgrades screen shows the rest).
        /// </summary>
        private void OnBiomeMasteryUp(BiomeMasteryGain gain)
        {
            _context.Audio.Play(AudioEventId.BiomeMasteryUp, Vector3.zero, default);
            _context.Haptics.Play(HapticEventId.TapLight);
            string source = _progression.Config.Biomes[Math.Max(0, _progression.Config.BiomeOrder(gain.Biome))].SourceId;
            _refugeHud?.ShowToast(ToastKind.Unlock, Format(Format(Text(ProgressionText.BiomeMasteryToast), "{mastery}",
                Text(ProgressionText.BiomeMastery(source))), "{level}", gain.LevelAfter.ToString()));
        }

        /// <summary>Nothing else on screen and no hatch playing: a card may come.</summary>
        private bool CanInterrupt() => !IsOpen && (_refuge == null || (!_refuge.ModalOpen && !_refuge.Hatching));

        /// <summary>
        /// A real pause: nothing on screen, no hatch, and the next egg is there but left alone (between two hatches the
        /// player is only waiting for it).
        /// </summary>
        private bool Calm() => CanInterrupt() && (_refuge == null || _refuge.WaitingEgg != null) && (_zones == null || !_zones.Opening);

        private void ShowRewards()
        {
            var rewards = _progression.Refuge.PendingRewards();
            _rewardNow = false;
            if (!rewards.Any || _rewardView == null)
            {
                _rewardPending = false;
                return;
            }
            _builder.Clear();
            for (int level = rewards.FromLevel + 1; level <= rewards.ToLevel; level++)
            {
                var feature = FeatureAt(level);
                if (feature == null)
                    continue;
                if (_builder.Length > 0)
                    _builder.Append('\n');
                _builder.Append(Format(Text(ProgressionText.RewardUnlock), "{feature}", feature));
            }
            var data = new LevelRewardData
            {
                Level = rewards.ToLevel,
                Title = Format(Text(ProgressionText.RefugeLevelUp), "{n}", rewards.ToLevel.ToString()),
                Gems = rewards.Gems,
                Unlocks = _builder.ToString(),
            };
            _rewardView.Open(data, _context.Localization, this);
            _rewardPending = false;
            _context.Audio.Play(AudioEventId.PanelOpen, Vector3.zero, default);
        }

        public void ClaimLevelRewards()
        {
            if (_progression.Refuge.TryClaimRewards(out var rewards))
            {
                _context.Audio.Play(AudioEventId.CollectLight, Vector3.zero, default);
                _context.Haptics.Play(HapticEventId.Confirm);
                _context.Save?.RequestSave(SaveUrgency.Priority);
            }
            _rewardView?.Close();
        }

        /// <summary>Tests and debug: show the pending level card as soon as nothing else is on screen.</summary>
        public void ShowPendingRewards() => _rewardNow = _rewardPending;

        // ---------------- Biome gate ----------------

        /// <summary>Shows the gate of a biome (the gate at the end of the passage calls it).</summary>
        public void ShowBiomeGate(ContentId biome)
        {
            if (_gateView == null || IsOpen || (_refuge != null && _refuge.ModalOpen))
                return;
            _gateBiome = biome;
            _gateView.Open(BuildGateData(biome), _context.Localization, this);
            _context.Analytics.BiomeGateSeen(biome, _progression.Gates.GetStatus(biome).Met);
            _context.Audio.Play(AudioEventId.PanelOpen, Vector3.zero, default);
        }

        public BiomeGateData BuildGateData(ContentId biome)
        {
            var status = _progression.Gates.GetStatus(biome);
            string source = _progression.Config.Biomes[Math.Max(0, _progression.Config.BiomeOrder(biome))].SourceId;
            string previous = _progression.Config.Biomes[Math.Max(0, _progression.Config.BiomeOrder(biome) - 1)].SourceId;
            var data = new BiomeGateData
            {
                Title = Format(Text(ProgressionText.BiomeGateTitle), "{biome}", BiomeWithArticle(source)),
                Soon = !status.ContentAvailable,
                Conditions = new GateConditionLine[BiomeGateData.MaxConditions],
                Price = status.Price,
                CanOpen = status.CanOpen,
            };
            AddCondition(ref data, Format(Text(ProgressionText.BiomeGateLevel), "{n}", status.RefugeLevelNeeded.ToString()),
                (status.Met & GateConditions.RefugeLevel) != 0);
            AddCondition(ref data, Format(Format(Format(Text(ProgressionText.BiomeGateSpecies), "{biome}", Text(previous + ".name")),
                "{found}", Math.Min(status.SpeciesFound, status.SpeciesNeeded).ToString()), "{n}", status.SpeciesNeeded.ToString()),
                (status.Met & GateConditions.Species) != 0);
            AddCondition(ref data, Format(Format(Text(ProgressionText.UpgradesLockedMastery), "{mastery}", Text(ProgressionText.BiomeMastery(previous))),
                "{level}", status.MasteryNeeded.ToString()), (status.Met & GateConditions.Mastery) != 0);
            if (status.FloorKind != GateFloorKind.None)
                AddCondition(ref data, FloorText(status), (status.Met & GateConditions.Floor) != 0);
            return data;
        }

        /// <summary>The first condition of a gate still to meet (empty when all are met).</summary>
        private static string FirstUnmet(in BiomeGateData gate)
        {
            for (int i = 0; i < gate.ConditionCount; i++)
                if (!gate.Conditions[i].Met)
                    return gate.Conditions[i].Text;
            return string.Empty;
        }

        private static void AddCondition(ref BiomeGateData data, string text, bool met)
        {
            data.Conditions[data.ConditionCount++] = new GateConditionLine { Text = text, Met = met };
        }

        /// <summary>The day floor in plain words ("Tomorrow", "In 2 days"), or the minutes of play left. No countdown.</summary>
        private string FloorText(in GateStatus status)
        {
            if (status.FloorKind == GateFloorKind.PlayMinutes)
                return Format(Text(ProgressionText.BiomeGatePlay), "{n}", status.FloorValue.ToString());
            if (status.FloorLeft <= 0)
                return Format(Text(ProgressionText.BiomeGateDay), "{n}", status.FloorValue.ToString());
            if (status.FloorLeft == 1)
                return Text(ProgressionText.BiomeGateTomorrow);
            return Format(Text(ProgressionText.BiomeGateDaysIn), "{n}", status.FloorLeft.ToString());
        }

        private string BiomeWithArticle(string sourceId)
        {
            string key = sourceId + ".name_the";
            string text = Text(key);
            return text == key ? Text(sourceId + ".name") : text;
        }

        public void OpenBiome()
        {
            if (!_progression.Gates.TryOpen(_gateBiome, out _))
            {
                _context.Audio.Play(AudioEventId.ErrorSoft, Vector3.zero, default);
                _context.Haptics.Play(HapticEventId.ErrorSoft);
                return;
            }
            _context.Audio.Play(AudioEventId.Claim, Vector3.zero, default);
            _context.Haptics.Play(HapticEventId.Legendary);
            _context.Save?.RequestSave(SaveUrgency.Priority);
            _gateView?.Close();
        }

        public void BiomeGateClosed() => _context.Audio.Play(AudioEventId.UiBack, Vector3.zero, default);

        // ---------------- Helpers ----------------

        public void Dispose()
        {
            _progression.Upgrades.Purchased -= _onPurchase;
            _progression.Refuge.LeveledUp -= _onLevelUp;
            _progression.Mastery.LeveledUp -= _onBiomeMastery;
            if (_zones != null)
                _zones.SignTapped -= _onSignTapped;
        }

        private string MasteryLine(int level) =>
            Format(Format(Text(ProgressionText.UpgradesLockedMastery), "{mastery}", Text(ProgressionText.BiomeMastery(_biomeSourceId))),
                "{level}", level.ToString());

        private string Text(string key) => _context.Localization != null ? _context.Localization.Get(key) : key;

        private static string Format(string template, string token, string value) => template.Replace(token, value);

        /// <summary>A name that carries its article, shown on its own line: "la Prairie fleurie" reads "La Prairie fleurie".</summary>
        private string Capitalized(string name)
        {
            if (string.IsNullOrEmpty(name) || char.IsUpper(name[0]))
                return name;
            var culture = System.Globalization.CultureInfo.GetCultureInfo(_context.Localization != null ? _context.Localization.Language : "en");
            return char.ToUpper(name[0], culture) + name.Substring(1);
        }

        /// <summary>A percentage as the language writes it: whole above 10, one decimal below ("1,5" in French).</summary>
        private string Percent(double value)
        {
            double rounded = value >= 10.0 ? Math.Round(value) : Math.Round(value, 1);
            string text = rounded.ToString(rounded >= 10.0 || rounded == Math.Floor(rounded) ? "0" : "0.0",
                System.Globalization.CultureInfo.InvariantCulture);
            return _context.Localization != null && _context.Localization.Language == "fr" ? text.Replace('.', ',') : text;
        }

        private string FormatLight(BigValue value)
        {
            // Same digits as the HUD's Light counter.
            int length = BigValueFormatter.Format(value, _number, 0, NumberFormatSettings.Invariant);
            return new string(_number, 0, length);
        }
    }
}
