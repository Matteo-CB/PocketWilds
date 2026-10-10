#nullable enable
using System;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>A seed was planted (analytics 150 garden_plant; WISH-031).</summary>
    public struct GardenPlantInfo
    {
        public int Plot;
        public ContentId Plant;

        /// <summary>The real growth in seconds (events and the Essence tree applied).</summary>
        public double GrowSeconds;

        /// <summary>Seeds of that plant left in the inventory.</summary>
        public int SeedsLeft;
    }

    /// <summary>A plant was harvested (analytics 151 garden_harvest; WISH-032, 034 to 036; JRN-027, 029, 030, 041, 012).</summary>
    public struct GardenHarvestInfo
    {
        public GardenHarvest Harvest;
        public GardenPlantKind Kind;

        /// <summary>The plant's catalogue growth in seconds (« Récolte longue », analytics).</summary>
        public double PlantSeconds;

        /// <summary>The challenge counted (0 to 9), −1 when none was played or the harvest did not count.</summary>
        public int Challenge;

        /// <summary>The challenge ended with this harvest: its stars and the best before it.</summary>
        public bool ChallengeFinished;

        public byte ChallengeStars;
        public byte ChallengeStarsBefore;
    }

    public delegate void GardenPlantedHandler(in GardenPlantInfo info);

    public delegate void GardenHarvestedHandler(in GardenHarvestInfo info);

    /// <summary>
    /// T43 « Jardin » (doc 01 Contenu, sections 8 and 9, S-ACT-006): the second meta activity. Seeds planted in the garden
    /// beds grow for 30 min to 8 h, are watered once and give flowers, fruits, seeds back, dyes and garden points.
    /// <list type="bullet">
    /// <item>Growth times are absolute UTC times in <see cref="GardenState"/>, read from <see cref="IGameClock.UtcNow"/>:
    /// plants grow while the game is closed; a ripe plant waits for ever; a clock set back never makes a growth longer than
    /// its duration (<see cref="SecondsLeft"/> is clamped and <see cref="Tick"/> moves such a sprout's times to now).</item>
    /// <item>Every random part of a harvest (the seed given back, « Pousse heureuse ») is drawn at planting with the
    /// sprout's seed and saved; a speed-up draws nothing. The stars are fixed rules: watered; a neighbour of another species
    /// grew beside it at least half of its growth (each sprout keeps the help of its neighbours' finished crops, the current
    /// crop beside it is added when read: exact for any number of crops, offline too).</item>
    /// <item>Products and seeds go to the inventory (<see cref="InventoryState"/>); dyes and palettes stay in the garden.</item>
    /// </list>
    /// Built by the bootstrap after <see cref="ActivityServices"/>; <see cref="Tick"/> each frame (cheap, no allocation).
    /// Reads never allocate. Building it marks the Garden as existing (<see cref="ActivityConfig.GardenAvailable"/>) and keeps
    /// <see cref="ActivityServices.GardenLevel"/> on the garden level (the Pâtisserie opens at 3).
    /// </summary>
    public sealed class GardenService : IGarden, IGardenDyes
    {
        private const uint SproutSalt = 0x47524457u;

        private readonly ActivityServices _activities;
        private readonly ActivityContext _ctx;
        private readonly GardenState _state;
        private readonly GardenPlotState[] _seenState = new GardenPlotState[GardenState.MaxPlots];
        private readonly byte[] _seenStage = new byte[GardenState.MaxPlots];
        private bool _seenReady;

        public GardenService(ActivityServices activities, GardenConfig? config = null)
        {
            _activities = activities;
            _ctx = activities.Context;
            _state = _ctx.State.Garden;
            Config = config ?? new GardenConfig();
            if (ActivityCatalog.Get(ActivityId.Garden).ShippedInThisBuild)
                activities.Config.GardenAvailable = true;
            SyncLevel();
        }

        public GardenConfig Config { get; }
        public GardenState State => _state;

        /// <summary>The Wild Club is active (the Club plot); the activities' flag is the one the shop sets.</summary>
        public bool ClubActive => _activities.Config.ClubActive;

        public event Action<int>? PlotChanged;

        /// <summary>A sprout passed to stage 2 or ripened (3): plot, stage (the « pop » sound, the green ring).</summary>
        public event Action<int, int>? StageChanged;

        public event GardenPlantedHandler? Planted;

        /// <summary>A sprout was watered (WISH-033, JRN-028).</summary>
        public event Action<int>? SproutWatered;

        public event GardenHarvestedHandler? Harvested;

        /// <summary>Finish now: plot, Gems spent, seconds that were left (analytics 152).</summary>
        public event Action<int, int, double>? SpedUp;

        /// <summary>The garden level rose: before, after (the level-up sound, the Pâtisserie at 3).</summary>
        public event Action<int, int>? LeveledUp;

        /// <summary>A challenge's best stars rose: challenge (0 to 9), stars, attempts (analytics 112).</summary>
        public event Action<int, int, int>? ChallengeImproved;

        /// <summary>The Gems plot was bought.</summary>
        public event Action? PlotBought;

        public event Action<ContentId>? PaletteBought;

        // ------------------------------------------------------------------------------------------- opening and plots

        public bool IsOpen => Config.Enabled && _activities.IsAvailable(ActivityId.Garden);

        public int PlotCount => Math.Min(GardenState.MaxPlots, Math.Max(1, Config.PlotsBase) + 1 + Math.Max(0, Config.PlotsClubBonus));

        /// <summary>The Gems plot's index (after the free ones).</summary>
        public int GemsPlot => Math.Min(GardenState.MaxPlots - 1, Math.Max(1, Config.PlotsBase));

        public long PlotPrice => Math.Max(0, Config.PlotPrice);

        /// <summary>The plot can take a seed: a free one, the Gems plot once bought, the Club plots while the Club runs.</summary>
        public bool IsOwned(int plot)
        {
            if ((uint)plot >= (uint)PlotCount)
                return false;
            int gems = GemsPlot;
            if (plot < gems)
                return true;
            if (plot == gems)
                return _state.PlotBought;
            return ClubActive;
        }

        public int OwnedPlots
        {
            get
            {
                int owned = 0;
                for (int p = 0; p < PlotCount; p++)
                    if (IsOwned(p))
                        owned++;
                return owned;
            }
        }

        public GardenPlotState StateOf(int plot) => StateAt(plot, NowMs);

        public GardenPlotUnlock UnlockOf(int plot)
        {
            if ((uint)plot >= (uint)PlotCount || IsOwned(plot) || _state.Plots[plot].Active)
                return GardenPlotUnlock.None;
            return plot == GemsPlot ? GardenPlotUnlock.Gems : GardenPlotUnlock.Club;
        }

        public bool TryBuyPlot()
        {
            if (!IsOpen || _state.PlotBought)
                return false;
            if (!_ctx.Economy.Spend(CurrencyId.Gems, BigValue.FromDouble(PlotPrice), EconomyReason.Slot))
                return false;
            _state.PlotBought = true;
            _ctx.RequestSave(SaveUrgency.Priority);
            PlotBought?.Invoke();
            Raise(GemsPlot);
            return true;
        }

        // ------------------------------------------------------------------------------------------- a plot's sprout

        public ContentId PlantOf(int plot) => Active(plot) ? _state.Plots[plot].Plant : ContentId.None;

        public int StageOf(int plot) => Active(plot) ? GardenRules.Stage(Growth(in _state.Plots[plot], NowMs)) : 0;

        public float GrowthOf(int plot) => Active(plot) ? (float)Growth(in _state.Plots[plot], NowMs) : 0f;

        public double SecondsLeft(int plot) => Active(plot) ? Left(in _state.Plots[plot], NowMs) : 0.0;

        public double TotalSecondsOf(int plot)
        {
            if (!Active(plot))
                return 0.0;
            ref readonly var record = ref _state.Plots[plot];
            return Math.Max(0L, record.RipeUnixMs - record.PlantedUnixMs) / 1000.0;
        }

        public bool Watered(int plot) => Active(plot) && _state.Plots[plot].Watered;

        public bool Helped(int plot)
        {
            if (!Active(plot))
                return false;
            long now = NowMs;
            ref readonly var record = ref _state.Plots[plot];
            return HelpedAt(plot, Math.Min(now, record.RipeUnixMs), now);
        }

        public int RipeCount
        {
            get
            {
                long now = NowMs;
                int ripe = 0;
                for (int p = 0; p < GardenState.MaxPlots; p++)
                    if (StateAt(p, now) == GardenPlotState.Ripe)
                        ripe++;
                return ripe;
            }
        }

        public double SoonestRipe
        {
            get
            {
                long now = NowMs;
                double soonest = -1.0;
                for (int p = 0; p < GardenState.MaxPlots; p++)
                {
                    if (StateAt(p, now) != GardenPlotState.Growing)
                        continue;
                    double left = Left(in _state.Plots[p], now);
                    if (soonest < 0.0 || left < soonest)
                        soonest = left;
                }
                return soonest;
            }
        }

        /// <summary>A sprout is growing (WISH-033's condition).</summary>
        public int GrowingCount
        {
            get
            {
                long now = NowMs;
                int growing = 0;
                for (int p = 0; p < GardenState.MaxPlots; p++)
                    if (StateAt(p, now) == GardenPlotState.Growing)
                        growing++;
                return growing;
            }
        }

        // ------------------------------------------------------------------------------------------- plants and seeds

        public int PlantCount => GardenCatalog.PlantCount;

        public GardenPlant PlantAt(int index) => (uint)index < (uint)GardenCatalog.PlantCount ? GardenCatalog.Plants[index] : default;

        public bool TryGetPlant(ContentId plant, out GardenPlant info)
        {
            int index = GardenCatalog.IndexOf(plant);
            info = index >= 0 ? GardenCatalog.Plants[index] : default;
            return index >= 0;
        }

        public bool IsPlantOpen(ContentId plant) => IsPlantOpenAt(GardenCatalog.IndexOf(plant));

        public int SeedsOf(ContentId plant) => plant.IsValid ? _ctx.State.Inventory.Count(plant) : 0;

        /// <summary>The first visit's tutorial is still to show (the day 3 gift gives 5 Sweet Clover seeds).</summary>
        public bool FirstVisit => !_state.FirstVisitDone;

        public void MarkVisited()
        {
            if (_state.FirstVisitDone)
                return;
            _state.FirstVisitDone = true;
            _ctx.RequestSave(SaveUrgency.Normal);
        }

        public GardenPlantResult Plant(int plot, ContentId plant)
        {
            if (!IsOpen)
                return GardenPlantResult.NotOpen;
            long now = NowMs;
            if (!IsOwned(plot) || StateAt(plot, now) != GardenPlotState.Empty)
                return GardenPlantResult.PlotNotEmpty;
            int index = GardenCatalog.IndexOf(plant);
            if (!IsPlantOpenAt(index))
                return GardenPlantResult.PlantLocked;
            var inventory = _ctx.State.Inventory;
            if (!inventory.TryRemove(plant, 1))
                return GardenPlantResult.NoSeed;

            var info = GardenCatalog.Plants[index];
            int level = Level;
            uint seed = SeedHash.Mix(_ctx.ProfileSeed ^ (uint)_state.Planted, SproutSalt);
            _state.Planted++;
            long grow = GardenRules.GrowMs(in info, Config, _ctx.Essence(EssenceEffect.GardenGrowth));
            _state.Plots[plot] = new GardenPlotRecord
            {
                Active = true,
                Plant = plant,
                PlantedUnixMs = now,
                RipeUnixMs = now + grow,
                GrowMs = grow,
                Seed = seed,
                // Every draw of the harvest happens now, with the sprout's seed (decision of 7 October).
                SeedsBack = GardenRules.DrawSeedsBack(seed, GardenRules.SeedBackPercent(level, Config)),
                Happy = GardenRules.DrawHappy(seed, level, Config),
            };
            _seenState[plot] = GardenPlotState.Growing;
            _seenStage[plot] = 1;
            _ctx.RequestSave(SaveUrgency.Priority);
            var planted = new GardenPlantInfo { Plot = plot, Plant = plant, GrowSeconds = grow / 1000.0, SeedsLeft = inventory.Count(plant) };
            Planted?.Invoke(in planted);
            PlotChanged?.Invoke(plot);
            return GardenPlantResult.Planted;
        }

        public bool Water(int plot)
        {
            long now = NowMs;
            if (!IsOpen || StateAt(plot, now) != GardenPlotState.Growing)
                return false;
            ref var record = ref _state.Plots[plot];
            if (record.Watered)
                return false;
            record.Watered = true;
            record.RipeUnixMs = GardenRules.WaterRipe(record.PlantedUnixMs, record.RipeUnixMs, record.GrowMs, Config.WaterReduction, now);
            _ctx.RequestSave(SaveUrgency.Normal);
            SproutWatered?.Invoke(plot);
            RaiseIfChanged(plot, now);
            return true;
        }

        // ------------------------------------------------------------------------------------------- finish now

        public int SpeedUpCost(int plot) => StateOf(plot) == GardenPlotState.Growing ? GardenRules.SpeedUpCost(SecondsLeft(plot), Config) : 0;

        public int SpeedUpsLeftToday
        {
            get
            {
                int used = _state.SpeedUpDayKey == _ctx.TodayKey ? _state.SpeedUpsToday : 0;
                return Math.Max(0, Config.SpeedUpsPerDay - used);
            }
        }

        public bool TrySpeedUp(int plot)
        {
            long now = NowMs;
            if (!IsOpen || StateAt(plot, now) != GardenPlotState.Growing || SpeedUpsLeftToday <= 0)
                return false;
            ref var record = ref _state.Plots[plot];
            double left = Left(in record, now);
            int cost = GardenRules.SpeedUpCost(left, Config);
            if (cost <= 0 || !_ctx.Economy.Spend(CurrencyId.Gems, BigValue.FromDouble(cost), EconomyReason.SpeedUp))
                return false;
            int today = _ctx.TodayKey;
            if (_state.SpeedUpDayKey != today)
            {
                _state.SpeedUpDayKey = today;
                _state.SpeedUpsToday = 0;
            }
            _state.SpeedUpsToday++;
            // Ripe now; the draws were made at planting and do not change.
            record.RipeUnixMs = Math.Max(record.PlantedUnixMs, now);
            _ctx.RequestSave(SaveUrgency.Priority);
            SpedUp?.Invoke(plot, cost, left);
            RaiseIfChanged(plot, now);
            return true;
        }

        // ------------------------------------------------------------------------------------------- the harvest

        public bool TryHarvest(int plot, out GardenHarvest harvest)
        {
            harvest = default;
            long now = NowMs;
            if (StateAt(plot, now) != GardenPlotState.Ripe)
                return false;
            var record = _state.Plots[plot];
            int index = GardenCatalog.IndexOf(record.Plant);
            var plant = index >= 0 ? GardenCatalog.Plants[index] : default;
            int levelBefore = Level;
            bool helped = HelpedAt(plot, record.RipeUnixMs, now);
            byte stars = GardenRules.Stars(record.Watered, helped);
            bool flowerBeside = record.FlowerBeside || FlowerNow(plot);

            harvest = new GardenHarvest
            {
                Plot = plot,
                Plant = record.Plant,
                Stars = stars,
                Watered = record.Watered,
                Helped = helped,
                Happy = record.Happy,
                LevelBefore = levelBefore,
            };
            var inventory = _ctx.State.Inventory;
            if (index >= 0)
            {
                harvest.Main = plant.Main;
                harvest.MainCount = GardenRules.MainCount(in plant, stars, record.Happy);
                harvest.Side = plant.Side;
                harvest.SideCount = GardenRules.SideCount(in plant, record.Happy);
                if (levelBefore >= GardenRules.DyesLevel && plant.Dye.IsValid && plant.DyeCount > 0)
                {
                    harvest.Dye = plant.Dye;
                    harvest.Dyes = plant.DyeCount;
                    int dye = GardenCatalog.DyeIndexOf(plant.Dye);
                    if (dye >= 0)
                        _state.Dyes[dye] = (int)Math.Min(int.MaxValue, (long)_state.Dyes[dye] + plant.DyeCount);
                }
                harvest.SeedsBack = GardenRules.SeedsBack(record.SeedsBack, stars, levelBefore);
                harvest.Points = GardenRules.Points(plant.GrowSeconds, stars);
                inventory.Add(harvest.Main, harvest.MainCount);
                inventory.Add(harvest.Side, harvest.SideCount);
                inventory.Add(record.Plant, harvest.SeedsBack);
            }

            // The finished crop helped its growing neighbours for the time it grew beside them.
            CreditNeighbours(plot, in record, plant.Kind == GardenPlantKind.Flower && index >= 0, now);

            _state.Plots[plot] = default;
            _state.Harvested++;
            _state.Points = (int)Math.Min(int.MaxValue, (long)_state.Points + harvest.Points);
            int levelAfter = Level;
            harvest.LevelAfter = levelAfter;
            _seenState[plot] = OwnedOrLocked(plot);
            _seenStage[plot] = 0;

            var info = new GardenHarvestInfo
            {
                Harvest = harvest,
                Kind = plant.Kind,
                PlantSeconds = plant.GrowSeconds,
                Challenge = -1,
            };
            if (index >= 0)
                CountChallenge(plot, index, in plant, stars, flowerBeside, now, ref info);
            if (levelAfter > levelBefore)
                OnLevelUp(levelBefore, levelAfter);
            _ctx.RequestSave(SaveUrgency.Priority);
            Harvested?.Invoke(in info);
            if (levelAfter > levelBefore)
                LeveledUp?.Invoke(levelBefore, levelAfter);
            PlotChanged?.Invoke(plot);
            return true;
        }

        // ------------------------------------------------------------------------------------------- garden level

        public int Points => _state.Points;

        public int Level => GardenRules.LevelOf(_state.Points);

        public int PointsInLevel => GardenRules.PointsInLevel(_state.Points);

        public int PointsForNext => GardenRules.PointsForNext(_state.Points);

        public bool DyesOpen => Level >= GardenRules.DyesLevel;

        // ------------------------------------------------------------------------------------------- challenges

        public int ChallengeCount => GardenCatalog.ChallengeCount;

        public int ActiveChallenge => _state.Challenge;

        public bool IsChallengeOpen(int challenge)
        {
            if (!IsOpen || (uint)challenge >= (uint)GardenCatalog.ChallengeCount)
                return false;
            ref readonly var rule = ref GardenCatalog.Challenges[challenge];
            if (rule.NeedsDyes && !DyesOpen)
                return false;
            switch (rule.Rule)
            {
                case GardenChallengeRule.FruitBesideFlower:
                    return AnyOpen(GardenPlantKind.Fruit) && AnyOpen(GardenPlantKind.Flower);
                case GardenChallengeRule.DifferentPlants:
                    return OpenPlants() >= rule.Harvests;
                case GardenChallengeRule.LongPlants:
                    for (int i = 0; i < GardenCatalog.PlantCount; i++)
                        if (GardenCatalog.Plants[i].GrowSeconds >= GardenCatalog.LongPlantSeconds && IsPlantOpenAt(i))
                            return true;
                    return false;
                case GardenChallengeRule.DifferentBiomes:
                    return OpenBiomePlants() >= 2;
                case GardenChallengeRule.CakeBasket:
                    return IsPlantOpen(GardenCatalog.GoldenWheat) && IsPlantOpen(GardenCatalog.WildStrawberry)
                           && IsPlantOpen(GardenCatalog.BlueberryBush);
                default:
                    return true;
            }
        }

        /// <summary>Starts a challenge (replacing the one being played): the next harvests count toward it.</summary>
        public bool StartChallenge(int challenge)
        {
            if (!IsChallengeOpen(challenge))
                return false;
            _state.ClearChallenge();
            _state.Challenge = (sbyte)challenge;
            _ctx.RequestSave(SaveUrgency.Normal);
            return true;
        }

        public void StopChallenge()
        {
            if (_state.Challenge < 0)
                return;
            _state.ClearChallenge();
            _ctx.RequestSave(SaveUrgency.Normal);
        }

        public int ChallengeHarvests => _state.Challenge >= 0 ? _state.ChallengeHarvests : 0;

        public int ChallengeHarvestsNeeded(int challenge)
        {
            if ((uint)challenge >= (uint)GardenCatalog.ChallengeCount)
                return 0;
            ref readonly var rule = ref GardenCatalog.Challenges[challenge];
            return rule.Rule == GardenChallengeRule.AllPlotsSameMinute ? Math.Max(rule.Harvests, OwnedPlots) : rule.Harvests;
        }

        public int ChallengeStarsSoFar => _state.Challenge >= 0 ? _state.ChallengeStars : 0;

        public int ChallengeThreshold(int challenge, int stars) =>
            (uint)challenge < (uint)GardenCatalog.ChallengeCount ? GardenCatalog.Challenges[challenge].Threshold(stars) : 0;

        public byte ChallengeStars(int challenge)
        {
            if ((uint)challenge >= (uint)GardenCatalog.ChallengeCount)
                return 0;
            int slot = ActivityCatalog.SlotOf(ActivityId.Garden);
            return _ctx.Activities.ChallengeStars[ActivityState.ChallengeIndex(slot, challenge)];
        }

        // ------------------------------------------------------------------------------------------- dyes and palettes

        public int DyeCount => GardenCatalog.DyeCount;

        public ContentId DyeAt(int index) => (uint)index < (uint)GardenCatalog.DyeCount ? GardenCatalog.Dyes[index].Id : ContentId.None;

        public int Owned(ContentId dye)
        {
            int index = GardenCatalog.DyeIndexOf(dye);
            return index >= 0 ? _state.Dyes[index] : 0;
        }

        public int PaletteCount => GardenCatalog.PaletteCount;

        public ContentId PaletteAt(int index) => (uint)index < (uint)GardenCatalog.PaletteCount ? GardenCatalog.Palettes[index].Id : ContentId.None;

        public bool OwnsPalette(ContentId palette)
        {
            int index = GardenCatalog.PaletteIndexOf(palette);
            return index >= 0 && (_state.Palettes & (1u << index)) != 0;
        }

        public bool CanBuyPalette(ContentId palette)
        {
            int index = GardenCatalog.PaletteIndexOf(palette);
            if (index < 0 || !DyesOpen || (_state.Palettes & (1u << index)) != 0)
                return false;
            ref readonly var p = ref GardenCatalog.Palettes[index];
            if (_state.Dyes[p.DyeA] < p.CostEach)
                return false;
            return !p.IsDuo || _state.Dyes[p.DyeB] >= p.CostEach;
        }

        public bool TryBuyPalette(ContentId palette)
        {
            if (!CanBuyPalette(palette))
                return false;
            int index = GardenCatalog.PaletteIndexOf(palette);
            ref readonly var p = ref GardenCatalog.Palettes[index];
            _state.Dyes[p.DyeA] -= p.CostEach;
            if (p.IsDuo)
                _state.Dyes[p.DyeB] -= p.CostEach;
            _state.Palettes |= 1u << index;
            _ctx.RequestSave(SaveUrgency.Priority);
            PaletteBought?.Invoke(palette);
            return true;
        }

        // ------------------------------------------------------------------------------------------- the frame

        /// <summary>
        /// Each frame (cheap, no allocation): a plot whose state or stage changed (a stage passed, ripe, a Club plot opened)
        /// raises <see cref="PlotChanged"/> once; a sprout whose growth grew past its duration (a clock set back) gets its times
        /// moved to now, so it never grows longer than its duration.
        /// </summary>
        public void Tick()
        {
            long now = NowMs;
            for (int p = 0; p < GardenState.MaxPlots; p++)
            {
                ref var record = ref _state.Plots[p];
                if (record.Active)
                {
                    long total = record.RipeUnixMs - record.PlantedUnixMs;
                    if (total < 0L)
                    {
                        record.RipeUnixMs = record.PlantedUnixMs;
                        total = 0L;
                    }
                    if (now < record.PlantedUnixMs)
                    {
                        // The clock went back before the planting: the growth starts again from now, never longer.
                        record.PlantedUnixMs = now;
                        record.RipeUnixMs = now + total;
                        _ctx.RequestSave(SaveUrgency.Normal);
                    }
                }
                if (!_seenReady)
                {
                    _seenState[p] = StateAt(p, now);
                    _seenStage[p] = StageAt(p, now);
                    continue;
                }
                RaiseIfChanged(p, now);
            }
            _seenReady = true;
        }

        // ------------------------------------------------------------------------------------------- helpers

        private long NowMs => _ctx.Clock.UtcNow.ToUnixTimeMilliseconds();

        private bool Active(int plot) => (uint)plot < GardenState.MaxPlots && _state.Plots[plot].Active;

        private GardenPlotState StateAt(int plot, long now)
        {
            if ((uint)plot >= GardenState.MaxPlots)
                return GardenPlotState.Locked;
            ref readonly var record = ref _state.Plots[plot];
            if (record.Active)
                return now < record.RipeUnixMs && Left(in record, now) > 0.0 ? GardenPlotState.Growing : GardenPlotState.Ripe;
            return OwnedOrLocked(plot);
        }

        private byte StageAt(int plot, long now) => Active(plot) ? (byte)GardenRules.Stage(Growth(in _state.Plots[plot], now)) : (byte)0;

        private GardenPlotState OwnedOrLocked(int plot) => IsOwned(plot) ? GardenPlotState.Empty : GardenPlotState.Locked;

        /// <summary>Seconds left, clamped to [0, the sprout's total]: a clock set back never shows a longer growth.</summary>
        private static double Left(in GardenPlotRecord record, long now)
        {
            long total = Math.Max(0L, record.RipeUnixMs - record.PlantedUnixMs);
            long left = record.RipeUnixMs - now;
            if (left <= 0L)
                return 0.0;
            return Math.Min(left, total) / 1000.0;
        }

        /// <summary>The growth from 0 to 1: elapsed over the total (watering shortens the total, so the sprout leaps ahead).</summary>
        private static double Growth(in GardenPlotRecord record, long now)
        {
            long total = record.RipeUnixMs - record.PlantedUnixMs;
            if (total <= 0L || now >= record.RipeUnixMs)
                return 1.0;
            long elapsed = Math.Max(0L, now - record.PlantedUnixMs);
            return Math.Min(1.0, (double)elapsed / total);
        }

        /// <summary>
        /// The neighbour condition of the third star over the growth window [planted, <paramref name="windowEnd"/>]: on one
        /// side, a crop of another species grew at least half of the sprout's total growth (the help already credited by the
        /// finished crops, plus the crop there now).
        /// </summary>
        private bool HelpedAt(int plot, long windowEnd, long now)
        {
            ref readonly var record = ref _state.Plots[plot];
            long total = Math.Max(0L, record.RipeUnixMs - record.PlantedUnixMs);
            long left = record.HelpLeftMs + LiveHelp(plot - 1, in record, windowEnd, now, out bool besideLeft);
            long right = record.HelpRightMs + LiveHelp(plot + 1, in record, windowEnd, now, out bool besideRight);
            if (total <= 0L)
                return besideLeft || besideRight || left > 0L || right > 0L;
            return GardenRules.HelpEnough(left, total) || GardenRules.HelpEnough(right, total);
        }

        /// <summary>The time the crop in <paramref name="neighbour"/> now has grown beside the sprout (another species only).</summary>
        private long LiveHelp(int neighbour, in GardenPlotRecord record, long windowEnd, long now, out bool beside)
        {
            beside = false;
            if ((uint)neighbour >= GardenState.MaxPlots)
                return 0L;
            ref readonly var other = ref _state.Plots[neighbour];
            if (!other.Active || other.Plant == record.Plant)
                return 0L;
            beside = true;
            return GardenRules.Overlap(other.PlantedUnixMs, now, record.PlantedUnixMs, windowEnd);
        }

        /// <summary>A neighbour bears a flower now (challenge 02).</summary>
        private bool FlowerNow(int plot)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                int n = plot + side;
                if ((uint)n >= GardenState.MaxPlots || !_state.Plots[n].Active)
                    continue;
                int index = GardenCatalog.IndexOf(_state.Plots[n].Plant);
                if (index >= 0 && GardenCatalog.Plants[index].Kind == GardenPlantKind.Flower)
                    return true;
            }
            return false;
        }

        /// <summary>A crop just harvested gives its neighbours the time it grew beside their growth (another species only).</summary>
        private void CreditNeighbours(int plot, in GardenPlotRecord finished, bool flower, long now)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                int n = plot + side;
                if ((uint)n >= GardenState.MaxPlots)
                    continue;
                ref var other = ref _state.Plots[n];
                if (!other.Active || other.Plant == finished.Plant)
                    continue;
                long shared = GardenRules.Overlap(finished.PlantedUnixMs, now, other.PlantedUnixMs, other.RipeUnixMs);
                if (shared <= 0L)
                    continue;
                // The harvested plot is on the right of plot − 1 and on the left of plot + 1.
                if (side < 0)
                    other.HelpRightMs += shared;
                else
                    other.HelpLeftMs += shared;
                if (flower)
                    other.FlowerBeside = true;
            }
        }

        private bool IsPlantOpenAt(int index)
        {
            if ((uint)index >= (uint)GardenCatalog.PlantCount || !GardenCatalog.Built[index])
                return false;
            ref readonly var plant = ref GardenCatalog.Plants[index];
            if (plant.Level > 0)
                return Level >= plant.Level;
            var progression = _ctx.Progression;
            return progression != null ? progression.Store.IsUnlocked(plant.Biome) : plant.Biome == ActivityCatalog.Meadow;
        }

        private bool AnyOpen(GardenPlantKind kind)
        {
            for (int i = 0; i < GardenCatalog.PlantCount; i++)
                if (GardenCatalog.Plants[i].Kind == kind && IsPlantOpenAt(i))
                    return true;
            return false;
        }

        private int OpenPlants()
        {
            int open = 0;
            for (int i = 0; i < GardenCatalog.PlantCount; i++)
                if (IsPlantOpenAt(i))
                    open++;
            return open;
        }

        private int OpenBiomePlants()
        {
            int open = 0;
            for (int i = GardenCatalog.StarterPlants; i < GardenCatalog.PlantCount; i++)
                if (IsPlantOpenAt(i))
                    open++;
            return open;
        }

        /// <summary>The harvest counts toward the challenge being played; the last one needed records the challenge stars.</summary>
        private void CountChallenge(int plot, int index, in GardenPlant plant, byte stars, bool flowerBeside, long now, ref GardenHarvestInfo info)
        {
            int challenge = _state.Challenge;
            if ((uint)challenge >= (uint)GardenCatalog.ChallengeCount)
                return;
            ref readonly var rule = ref GardenCatalog.Challenges[challenge];
            // A run that outlived its window (60 s, 3 days, 2 days) starts again from this harvest.
            if (rule.WindowSeconds > 0.0 && _state.ChallengeRunStartUnixMs > 0
                && now - _state.ChallengeRunStartUnixMs > (long)(rule.WindowSeconds * 1000.0))
                _state.ResetChallengeRun();
            uint bit = 0;
            bool counts;
            switch (rule.Rule)
            {
                case GardenChallengeRule.FruitBesideFlower:
                    counts = plant.Kind == GardenPlantKind.Fruit && flowerBeside;
                    break;
                case GardenChallengeRule.DifferentPlants:
                    bit = 1u << index;
                    counts = (_state.ChallengeMask & bit) == 0;
                    break;
                case GardenChallengeRule.LongPlants:
                    counts = plant.GrowSeconds >= GardenCatalog.LongPlantSeconds;
                    break;
                case GardenChallengeRule.AllPlotsSameMinute:
                    bit = 1u << plot;
                    counts = (_state.ChallengeMask & bit) == 0;
                    break;
                case GardenChallengeRule.DifferentBiomes:
                    bit = 1u << index;
                    counts = plant.Level == 0 && plant.Biome.IsValid && (_state.ChallengeMask & bit) == 0;
                    break;
                case GardenChallengeRule.CakeBasket:
                    bit = 1u << index;
                    counts = GardenCatalog.InCakeBasket(plant.Id) && (_state.ChallengeMask & bit) == 0;
                    break;
                default:
                    counts = true;
                    break;
            }
            if (!counts)
                return;
            if (_state.ChallengeRunStartUnixMs <= 0)
                _state.ChallengeRunStartUnixMs = Math.Max(1L, now);
            _state.ChallengeMask |= bit;
            _state.ChallengeHarvests++;
            _state.ChallengeStars += stars;
            info.Challenge = challenge;
            if (_state.ChallengeHarvests < ChallengeHarvestsNeeded(challenge))
                return;
            byte earned = rule.StarsFor(_state.ChallengeStars);
            RecordChallenge(challenge, earned, out byte before);
            info.ChallengeFinished = true;
            info.ChallengeStars = earned;
            info.ChallengeStarsBefore = before;
            _state.ClearChallenge();
        }

        /// <summary>
        /// A challenge's stars (best kept, attempts counted); its first 3 stars give the challenge Gems once and count toward
        /// the challenge decorations, as every activity's (doc 01, section 12.0 « Défis »).
        /// </summary>
        private void RecordChallenge(int challenge, byte stars, out byte before)
        {
            var activities = _ctx.Activities;
            int cell = ActivityState.ChallengeIndex(ActivityCatalog.SlotOf(ActivityId.Garden), challenge);
            before = activities.ChallengeStars[cell];
            if (activities.ChallengeAttempts[cell] < ushort.MaxValue)
                activities.ChallengeAttempts[cell]++;
            if (stars <= before)
                return;
            activities.ChallengeStars[cell] = stars;
            if (stars >= 3 && before < 3)
            {
                var config = _activities.Config;
                activities.ChallengesThreeStars++;
                var bundle = new RewardBundle { Gems = Math.Max(0, config.GemsChallenge) };
                int every = Math.Max(1, config.ChallengeDecorationEvery);
                if (activities.ChallengeDecorations < activities.ChallengesThreeStars / every)
                {
                    bundle.Decoration = ActivityRewardService.ChallengeDecoration(activities.ChallengeDecorations);
                    if (bundle.Decoration.IsValid)
                        activities.ChallengeDecorations++;
                }
                if (!bundle.IsEmpty)
                    _ctx.Granter.Grant(in bundle, EconomyReason.ActivityChallenge, DiscoverySource.Debug);
            }
            ChallengeImproved?.Invoke(challenge, stars, activities.ChallengeAttempts[cell]);
        }

        /// <summary>The levels crossed give their decorations (table 8.3); the Pâtisserie's opening reads the new level.</summary>
        private void OnLevelUp(int before, int after)
        {
            for (int level = before + 1; level <= after && level <= GardenRules.MaxLevel; level++)
            {
                var decoration = GardenCatalog.Levels[level - 1].Decoration;
                if (!decoration.IsValid)
                    continue;
                var bundle = new RewardBundle { Decoration = decoration };
                _ctx.Granter.Grant(in bundle, EconomyReason.Garden, DiscoverySource.Debug);
            }
            SyncLevel();
        }

        private void SyncLevel()
        {
            int level = Level;
            if (_activities.GardenLevel == level)
                return;
            _activities.GardenLevel = level;
            _activities.MarkUnlocksDirty();
        }

        private void RaiseIfChanged(int plot, long now)
        {
            var state = StateAt(plot, now);
            byte stage = StageAt(plot, now);
            bool stateChanged = _seenState[plot] != state;
            bool stageChanged = _seenStage[plot] != stage;
            if (!stateChanged && !stageChanged)
                return;
            _seenState[plot] = state;
            _seenStage[plot] = stage;
            if (stageChanged && stage >= 2)
                StageChanged?.Invoke(plot, stage);
            PlotChanged?.Invoke(plot);
        }

        private void Raise(int plot)
        {
            if ((uint)plot < GardenState.MaxPlots)
            {
                long now = NowMs;
                _seenState[plot] = StateAt(plot, now);
                _seenStage[plot] = StageAt(plot, now);
            }
            PlotChanged?.Invoke(plot);
        }
    }
}
