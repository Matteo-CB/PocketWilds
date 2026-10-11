#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Events;
using PocketWilds.Simulation;

namespace PocketWilds.Editor.Economy
{
    /// <summary>What a simulated player does in a day of the event (sessions, rounds, wishes, hatches) and how it spends.</summary>
    public sealed class EventSimProfile
    {
        public string Name = "regular";
        public int SessionsMin = 3;
        public int SessionsMax = 5;
        public double FeaturedRoundsPerSession = 0.75;
        public double OtherRoundsPerSession = 0.25;
        public double ThreeStarChance = 0.7;
        public int WishesPerDay = 3;
        public int HatchesPerSession = 4;
        public double ExpeditionsPerDay = 1;
        public int BondTouchesPerSession = 4;
        public int TreatsPerDay = 3;
        public int GardenHarvestsPerDay = 2;
        public double ChallengeStarsPerDay = 2.2;

        // T48 quests: the other moments the daily and long quests count (per day unless named per session).
        public double FocusHatchesPerDay = 1;
        public int BiomeVisitsPerSession = 2;
        public double UpgradesPerSession = 1;
        public double AccessoriesPerDay = 0.5;
        public double JournalGoalsPerDay = 1;

        /// <summary>Eggs before the first shop item (int.MaxValue: until the 7 items are owned).</summary>
        public int EggsBeforeShop = int.MaxValue;

        /// <summary>Shop lines bought in this order once the eggs above are done (then eggs again).</summary>
        public string[] ShopOrder = { "shop.outfit_4", "shop.deco_5", "shop.deco_6", "shop.deco_7", "shop.deco_8" };
    }

    public struct EventSimDay
    {
        public int Day;
        public int Earned;
        public int Points;
        public int Tier;
        public int Currency;
        public int Eggs;
        public int Items;
    }

    public sealed class EventSimRun
    {
        public EventSimProfile Profile = new EventSimProfile();
        public readonly List<EventSimDay> Days = new List<EventSimDay>();
        public long CurrencyEarned;
        public int Points;
        public int Tier;
        public int EggsOpened;
        public int ItemsOwned;
        public bool SecretOwned;
        public int LongQuestsDone;
        public int ChallengeStars;
        public int CurrencyConverted;
        public bool GuaranteesHeld = true;
        public readonly List<string> Problems = new List<string>();
        public double Milliseconds;

        /// <summary>First day the tier was reached (0: never).</summary>
        public int DayOfTier(int tier)
        {
            foreach (var d in Days)
                if (d.Tier >= tier)
                    return d.Day;
            return 0;
        }
    }

    /// <summary>
    /// R-TOOL-EVENTSIM (T48): plays the test event's 14 days for a regular (3 to 5 sessions a day) and a casual (1 or 2) profile
    /// through the real <see cref="EventService"/> (track, egg and its guarantees, quests, challenges, shop, end conversion). Only the
    /// activity and expedition amounts come from their configs (<see cref="ActivityConfig"/>, <see cref="ExpeditionConfig"/>), as
    /// the reward services give them. Outputs currency and tier per day, eggs, items and the guarantee checks, in well under 10 s.
    /// </summary>
    public static class EventPacingSimulator
    {
        public const int Days = 14;

        public static EventSimProfile Regular => new EventSimProfile();

        public static EventSimProfile Casual => new EventSimProfile
        {
            Name = "casual", SessionsMin = 1, SessionsMax = 2, FeaturedRoundsPerSession = 1, OtherRoundsPerSession = 0, ThreeStarChance = 0.5,
            WishesPerDay = 1, HatchesPerSession = 3, ExpeditionsPerDay = 0.3, BondTouchesPerSession = 3, TreatsPerDay = 1, GardenHarvestsPerDay = 0,
            ChallengeStarsPerDay = 0.5, EggsBeforeShop = 4, ShopOrder = new[] { "shop.outfit_4" }, FocusHatchesPerDay = 0, BiomeVisitsPerSession = 0,
            UpgradesPerSession = 0.25, AccessoriesPerDay = 0, JournalGoalsPerDay = 0.5,
        };

        public static List<EventSimRun> RunAll(int seed = 1) => new List<EventSimRun> { Run(Regular, seed), Run(Casual, seed) };

        public static EventSimRun Run(EventSimProfile profile, int seed = 1)
        {
            var watch = Stopwatch.StartNew();
            var run = new EventSimRun { Profile = profile };
            var content = EventTestFixture.BuildContent();
            var entry = EventTestFixture.Entry();
            var start = entry.StartUtc;
            var clock = new FakeGameClock(start.AddDays(-3));
            var state = PlayerState.CreateNew(clock.UtcNow);
            state.Meta.ProfileId = new Guid(seed, 0x48, 0x48, 1, 2, 3, 4, 5, 6, 7, 8);
            var events = new EventService(state, content, clock, null, new EventConfig(), EventTestFixture.Calendar(entry))
            {
                FeatureOpen = gate => gate != EventFeatureGate.Albums && gate != EventFeatureGate.Garden && gate != EventFeatureGate.AccessoryOwned
                                      || profile.GardenHarvestsPerDay > 0 && gate == EventFeatureGate.Garden
                                      || profile.AccessoriesPerDay > 0 && gate == EventFeatureGate.AccessoryOwned,
                // The two activities the profiles play: the featured one and grooming (the activity of the day is one of them).
                ActivityShipped = a => a == ActivityId.HideAndSeek || a == ActivityId.Grooming,
            };
            var activity = new ActivityConfig();
            var expedition = new ExpeditionConfig();
            var rng = new Random(seed * 7919 + profile.Name.Length);
            int shopLines = 0;
            int missesInRow = 0, maxMisses = 0, completeAt = 0;
            int eggsTotal = 0;
            events.EggOpened += r =>
            {
                eggsTotal++;
                // A duplicate counts as a miss only while an item was still missing before it.
                missesInRow = r.IsNew || EggsComplete(events) ? 0 : missesInRow + 1;
                maxMisses = Math.Max(maxMisses, missesInRow);
                if (completeAt == 0 && events.EggItemsOwned == EventDefinition.EggItemCount)
                    completeAt = r.Opened;
            };
            double featuredCarry = 0, otherCarry = 0, expeditionCarry = 0, starsCarry = 0, focusCarry = 0, upgradeCarry = 0, accessoryCarry = 0, journalCarry = 0;
            int eggsBeforeShop = profile.EggsBeforeShop;
            long earnedBefore = 0;
            for (int day = 1; day <= Days; day++)
            {
                int sessions = rng.Next(profile.SessionsMin, profile.SessionsMax + 1);
                for (int s = 0; s < sessions; s++)
                {
                    clock.Set(start.AddDays(day - 1).AddHours(1 + s * (13.0 / sessions)));
                    events.Tick();
                    events.Join();
                    shopLines = Math.Max(shopLines, events.ShopCount);
                    featuredCarry += profile.FeaturedRoundsPerSession;
                    while (featuredCarry >= 1)
                    {
                        featuredCarry -= 1;
                        PlayRound(events, activity, ActivityId.HideAndSeek, rng.NextDouble() < profile.ThreeStarChance ? 3 : 2);
                    }
                    otherCarry += profile.OtherRoundsPerSession;
                    while (otherCarry >= 1)
                    {
                        otherCarry -= 1;
                        PlayRound(events, activity, ActivityId.Grooming, rng.NextDouble() < profile.ThreeStarChance ? 3 : 2);
                    }
                    for (int h = 0; h < profile.HatchesPerSession; h++)
                        events.OnEggHatched();
                    events.Report(EventGoalKind.NewestBiomeHatches, profile.HatchesPerSession);
                    events.Report(EventGoalKind.BondTouches, profile.BondTouchesPerSession);
                    events.Report(EventGoalKind.LightCollections, 1);
                    events.Report(EventGoalKind.BiomeVisits, profile.BiomeVisitsPerSession);
                    upgradeCarry += profile.UpgradesPerSession;
                    for (; upgradeCarry >= 1; upgradeCarry -= 1)
                        events.Report(EventGoalKind.UpgradesBought, 1);
                    if (s == 0)
                    {
                        events.Report(EventGoalKind.DailyGift, 1);
                        for (int w = 0; w < profile.WishesPerDay; w++)
                            events.OnWishClaimed();
                        events.Report(EventGoalKind.TreatsGiven, profile.TreatsPerDay);
                        events.Report(EventGoalKind.GardenHarvests, profile.GardenHarvestsPerDay);
                        events.Report(EventGoalKind.GardenPlants, profile.GardenHarvestsPerDay + (profile.GardenHarvestsPerDay > 0 ? 1 : 0));
                        events.Report(EventGoalKind.GardenWaterings, profile.GardenHarvestsPerDay);
                        expeditionCarry += profile.ExpeditionsPerDay;
                        while (expeditionCarry >= 1)
                        {
                            expeditionCarry -= 1;
                            events.Report(EventGoalKind.ExpeditionStarts, 1);
                            events.Report(EventGoalKind.LongExpeditionStarts, 1);
                            events.GrantEventCurrency(expedition.EventCurrency[1], ActivityId.Expeditions);
                            events.Report(EventGoalKind.Expeditions, 1);
                        }
                        focusCarry += profile.FocusHatchesPerDay;
                        for (; focusCarry >= 1; focusCarry -= 1)
                            events.Report(EventGoalKind.FocusHatches, 1);
                        accessoryCarry += profile.AccessoriesPerDay;
                        for (; accessoryCarry >= 1; accessoryCarry -= 1)
                            events.Report(EventGoalKind.AccessoriesEquipped, 1);
                        journalCarry += profile.JournalGoalsPerDay;
                        for (; journalCarry >= 1; journalCarry -= 1)
                            events.Report(EventGoalKind.WeeklyJournalGoals, 1);
                        starsCarry += profile.ChallengeStarsPerDay;
                        while (starsCarry >= 1 && events.TotalChallengeStars < 30)
                        {
                            starsCarry -= 1;
                            int c = LowestChallenge(events);
                            events.ReportChallenge(c, events.ChallengeStars(c) + 1);
                        }
                    }
                    events.ClaimAll();
                    Spend(events, profile, ref eggsBeforeShop);
                }
                var inst = events.Instance!;
                run.Days.Add(new EventSimDay
                {
                    Day = day, Earned = (int)(inst.CurrencyEarned - earnedBefore), Points = inst.Points, Tier = inst.TierReached, Currency = inst.Currency,
                    Eggs = inst.EggsOpened, Items = events.EggItemsOwned,
                });
                earnedBefore = inst.CurrencyEarned;
            }
            var last = events.Instance!;
            run.CurrencyEarned = last.CurrencyEarned;
            run.Points = last.Points;
            run.Tier = last.TierReached;
            run.EggsOpened = last.EggsOpened;
            run.ItemsOwned = events.EggItemsOwned;
            run.SecretOwned = last.SecretOwned;
            run.LongQuestsDone = last.LongDone;
            run.ChallengeStars = events.TotalChallengeStars;
            clock.Set(entry.EndUtc.AddHours(1));
            events.Tick();
            run.CurrencyConverted = last.ConvertedCurrency;

            var p = last.Frozen;
            if (maxMisses >= p.Int(EventParam.EggNewEvery))
                Fail(run, $"{maxMisses} eggs in a row without a new item (guarantee {p.Int(EventParam.EggNewEvery)}).");
            if (eggsTotal >= p.Int(EventParam.EggCompleteBy) && (completeAt == 0 || completeAt > p.Int(EventParam.EggCompleteBy)))
                Fail(run, $"All items only at egg {completeAt} (guarantee {p.Int(EventParam.EggCompleteBy)}).");
            if (!last.Converted || last.Currency != 0)
                Fail(run, "The leftover currency was not converted once.");
            if (shopLines == 0)
                Fail(run, "The test event has no shop.");
            run.Milliseconds = watch.Elapsed.TotalMilliseconds;
            return run;
        }

        private static void PlayRound(EventService events, ActivityConfig config, ActivityId activity, int stars)
        {
            double featured = events.IsFeatured(activity) ? Math.Max(1.0, config.FeaturedMultiplier) : 1.0;
            events.GrantEventCurrency((int)Math.Round(config.EventCurrency(stars) * featured, MidpointRounding.AwayFromZero), activity);
            events.OnActivityEnded(activity, stars);
        }

        private static bool EggsComplete(EventService events) => events.EggItemsOwned >= EventDefinition.EggItemCount;

        private static int LowestChallenge(EventService events)
        {
            int best = 0;
            for (int i = 1; i < EventDefinition.ChallengeCount; i++)
                if (events.ChallengeStars(i) < events.ChallengeStars(best))
                    best = i;
            return best;
        }

        /// <summary>Eggs first (until the profile's count or the 7 items), then its shop lines in order, then eggs again.</summary>
        private static void Spend(EventService events, EventSimProfile profile, ref int eggsBeforeShop)
        {
            for (int guard = 0; guard < 64; guard++)
            {
                var inst = events.Instance;
                if (inst == null)
                    return;
                bool eggPhase = !EggsComplete(events) && inst.EggsOpened < eggsBeforeShop;
                if (!eggPhase)
                {
                    int line = NextShopLine(events, profile);
                    if (line >= 0)
                    {
                        if (events.Buy(line) != EventBuyStatus.Bought)
                            return;
                        continue;
                    }
                    eggsBeforeShop = int.MaxValue;
                }
                if (inst.Currency < events.EggCost || events.OpenEgg().Status != EventEggStatus.Opened)
                    return;
            }
        }

        private static int NextShopLine(EventService events, EventSimProfile profile)
        {
            foreach (var key in profile.ShopOrder)
            {
                for (int i = 0; i < events.ShopCount; i++)
                    if (events.ShopItem(i)!.Key == key && events.ShopRemaining(i) > 0)
                        return i;
            }
            return -1;
        }

        private static void Fail(EventSimRun run, string problem)
        {
            run.GuaranteesHeld = false;
            run.Problems.Add(problem);
        }

        public static string Report(List<EventSimRun> runs)
        {
            var sb = new StringBuilder();
            foreach (var run in runs)
            {
                sb.Append($"[{run.Profile.Name}] earned {run.CurrencyEarned}, points {run.Points}, tier {run.Tier} (T10 day {run.DayOfTier(10)}, T30 day {run.DayOfTier(30)}), ");
                sb.Append($"eggs {run.EggsOpened}, items {run.ItemsOwned}/7{(run.SecretOwned ? " + secret" : "")}, long quests {run.LongQuestsDone}, stars {run.ChallengeStars}, ");
                sb.AppendLine($"to Light {run.CurrencyConverted}, guarantees {(run.GuaranteesHeld ? "held" : "BROKEN")}, {run.Milliseconds:F0} ms");
                foreach (var d in run.Days)
                    sb.AppendLine($"  day {d.Day,2}: +{d.Earned,4} points {d.Points,5} tier {d.Tier,2} hand {d.Currency,4} eggs {d.Eggs,2} items {d.Items}");
                foreach (var problem in run.Problems)
                    sb.AppendLine("  ! " + problem);
            }
            return sb.ToString();
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Pocket Wilds/Economy/Event Pacing Simulator", priority = 310)]
        public static void RunMenu() => UnityEngine.Debug.Log("[EventPacingSimulator]\n" + Report(RunAll()));
#endif
    }
}
