using System;
using System.IO;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Services.Save;
using PocketWilds.Simulation;
using UnityEditor;
using UnityEngine;

namespace PocketWilds.Editor.Saves
{
    /// <summary>
    /// ED-012 Save Fixture Tool. Golden saves are frozen once written: they prove that every future
    /// build still loads saves made by older builds. This tool only creates missing fixtures.
    /// A schema change adds new fixtures (vN) and keeps the old ones.
    /// </summary>
    public static class GoldenSaveTool
    {
        public const string Folder = "Assets/_Game/Tests/Fixtures/GoldenSaves";

        private static readonly DateTimeOffset BaseTime = new DateTimeOffset(2026, 10, 4, 0, 0, 0, TimeSpan.Zero);

        [MenuItem("Pocket Wilds/Saves/Create Missing Golden Saves", priority = 200)]
        public static void CreateMissing()
        {
            int created = 0;
            created += WriteIfMissing("GoldenSave_New_v1", BuildNew());
            created += WriteIfMissing("GoldenSave_Day7_v1", BuildDay7());
            created += WriteIfMissing("GoldenSave_PremiumEntitlements_v1", BuildPremium());
            created += WriteIfMissing("GoldenSave_Offline_v2", BuildOffline());
            created += WriteIfMissing("GoldenSave_Codex_v3", BuildCodex());
            created += WriteIfMissing("GoldenSave_Pity_v4", BuildPity());
            created += WriteIfMissing("GoldenSave_Shop_v5", BuildShop());
            created += WriteIfMissing("GoldenSave_Analytics_v6", BuildAnalytics());
            created += WriteIfMissing("GoldenSave_Progression_v7", BuildProgression());
            created += WriteIfMissing("GoldenSave_Eggs_v8", BuildEggs());
            created += WriteIfMissing("GoldenSave_Renewal_v9", BuildRenewal());
            created += WriteIfMissing("GoldenSave_DailyVisit_v10", BuildDailyVisit());
            created += WriteIfMissing("GoldenSave_Activities_v11", BuildActivities());
            created += WriteIfMissing("GoldenSave_Expeditions_v12", BuildExpeditions());
            created += WriteIfMissing("GoldenSave_Garden_v13", BuildGarden());
            created += WriteIfMissing("GoldenSave_Albums_v14", BuildAlbums());
            created += WriteIfMissing("GoldenSave_Achievements_v15", BuildAchievements());
            created += WriteIfMissing("GoldenSave_Journal_v16", BuildJournal());
            created += WriteIfMissing("GoldenSave_Cosmetics_v17", BuildCosmetics());
            AssetDatabase.Refresh();
            Debug.Log($"[GoldenSaves] {created} fixture(s) created in {Folder}.");
        }

        public static void CreateMissingBatch()
        {
            try
            {
                CreateMissing();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorApplication.Exit(1);
            }
        }

        private static int WriteIfMissing(string name, PlayerState state)
        {
            Directory.CreateDirectory(Folder);
            string path = Path.Combine(Folder, name + SaveFileStore.Extension);
            if (File.Exists(path))
                return 0;

            var writer = new ByteBufferWriter();
            new BinarySaveSerializer().Serialize(state, writer);
            var bytes = new byte[writer.Length];
            Array.Copy(writer.Buffer, bytes, writer.Length);
            File.WriteAllBytes(path, bytes);
            return 1;
        }

        private static PlayerState BuildNew()
        {
            var state = new PlayerState();
            state.Meta.ProfileId = new Guid("6f0c1a2e-0000-4000-8000-000000000001");
            state.Meta.CreatedUtc = BaseTime;
            state.Meta.LastSaveUtc = BaseTime;
            state.Meta.LastKnownSessionId = new Guid("5e55e55e-0000-4000-8000-000000000001");
            state.Meta.Revision = 1;
            state.Meta.AppVersion = "0.1.0";
            return state;
        }

        /// <summary>
        /// Schema 7 (T35A): two Prairie zones opened, Prairie mastery 4, Light upgrades, refuge level 5 with rewards
        /// claimed up to 3, two Cute Benches bought, a treat stand filled with one treat bought.
        /// </summary>
        /// <summary>
        /// Schema 10 (T36): the welcome over, a gift opened today, 5 calendar boxes stamped, two pending wishes (one done),
        /// a journal with three goals, a pending return gift, today's touches and an inventory.
        /// </summary>
        private static PlayerState BuildDailyVisit()
        {
            var state = BuildRenewal();
            state.Meta.ProfileId = new Guid("6f0c1a2e-0000-4000-8000-000000000081");
            state.Meta.Revision = 280;
            var d = state.DailyVisit;
            d.LastDayKey = 20261012;
            d.LoginDays = 9;
            d.OnboardingDone = true;
            d.TutorialStep = 3;
            d.WelcomeClaimed = 3;
            d.WelcomeDayKey = 20261006;
            d.GiftDayKey = 20261012;
            d.GiftsOpened = 6;
            d.GiftsSinceBig = 4;
            d.LastGiftLot = 7;
            d.CalendarNext = 5;
            d.CalendarCycle = 1;
            d.CalendarDayKey = 20261012;
            d.CalendarStamps = 5;
            d.Wishes[0] = new WishRecord
            {
                Template = 3, Animal = 2, Param = ContentId.FromSource("species.rabbit"), ParamKind = 1, Target = 2, Progress = 1,
                DrawnDay = 20261011,
            };
            d.Wishes[1] = new WishRecord
            {
                Template = 11, Animal = 1, Param = ContentId.FromSource("biome.grassland"), ParamKind = 2, ParamExtra = 1, Target = 3,
                Progress = 3, DrawnDay = 20261012, Flags = WishRecord.FlagDone,
            };
            d.WishCount = 2;
            d.WishDrawDayKey = 20261012;
            d.SwapDayKey = 20261012;
            d.SwapsUsed = 1;
            d.WishGemsTotal = 54;
            d.WishesCompleted = 12;
            d.History[0] = new WishHistoryEntry { Template = 3, DayKey = 20261011 };
            d.History[1] = new WishHistoryEntry { Template = 11, DayKey = 20261012 };
            d.HistoryCount = 2;
            d.HistoryNext = 2;
            d.SystemUnlockDay[1] = 2;
            d.SystemUnlockDay[4] = 3;
            d.TouchedForWish[0] = 1;
            d.TouchedCount = 1;
            d.JournalStartKey = 20261012;
            d.JournalEndKey = 20261019;
            d.FirstJournalDone = true;
            d.Goals[0] = new JournalGoalRecord { Goal = 1, Slot = 0, Target = 20, Progress = 7 };
            d.Goals[1] = new JournalGoalRecord { Goal = 14, Slot = 1, Target = 5, Progress = 5, Claimed = true };
            d.Goals[2] = new JournalGoalRecord { Goal = 30, Slot = 2, Target = 3, Progress = 0 };
            d.GoalCount = 3;
            d.PreviousGoals[0] = 2;
            d.PreviousGoalCount = 1;
            d.CarryGems = 10;
            d.LastReturnGiftDay = 4;
            d.PendingReturnGift = 1;
            d.PendingReturnDays = 4;
            d.TouchDayKey = 20261012;
            d.TouchesToday = 6;
            d.TouchesByAnimal[1] = 4;
            d.TouchesByAnimal[2] = 2;
            state.Inventory.Tickets = 2;
            state.Inventory.PendingStickers = 1;
            state.Inventory.Add(ContentId.FromSource("ingredient.flower_sugar"), 3);
            return state;
        }

        /// <summary>
        /// Schema 11 (T37): the activities: tickets bought and free, a refill anchor, packs and Gems of the day, records,
        /// adaptive difficulty, challenges with stars and attempts, the free first game used, a theme, the paid sticker counter.
        /// </summary>
        private static PlayerState BuildActivities()
        {
            var state = BuildDailyVisit();
            state.Meta.ProfileId = new Guid("6f0c1a2e-0000-4000-8000-000000000091");
            state.Meta.Revision = 300;
            var a = state.Activities;
            a.TicketsStarted = true;
            a.FreeTickets = 3;
            a.PaidTickets = 2;
            a.RefillAnchorUnixMs = new DateTimeOffset(2026, 10, 12, 9, 30, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
            a.PackDayKey = 20261012;
            a.PacksToday = 1;
            a.GemsDayKey = 20261012;
            a.GemsToday = 4;
            a.ChallengesThreeStars = 6;
            a.ChallengeDecorations = 1;
            a.GamesStarted = 23;
            int grooming = (int)ActivityId.Grooming - 1;
            a.Records[grooming] = new ActivityRecord
            {
                Unlocked = true, Best = 1840, BestStars = 3, Difficulty = 5, ThreeStarStreak = 1, GamesPlayed = 17, RewardedGames = 12,
                FreeGameUsed = true, ThreeStarGemsDayKey = 20261012, LastTheme = ContentId.FromSource("biome.grassland"),
                PaidStickerPoints = 45,
            };
            int nuts = (int)ActivityId.NutRain - 1;
            a.Records[nuts] = new ActivityRecord { Unlocked = true, Best = 37, BestStars = 2, Difficulty = 2, ZeroStarStreak = 1, GamesPlayed = 6, RewardedGames = 4 };
            for (int i = 0; i < 6; i++)
            {
                a.ChallengeStars[ActivityState.ChallengeIndex(grooming, i)] = 3;
                a.ChallengeAttempts[ActivityState.ChallengeIndex(grooming, i)] = (ushort)(i + 1);
            }
            a.ChallengeStars[ActivityState.ChallengeIndex(grooming, 6)] = 1;
            a.ChallengeAttempts[ActivityState.ChallengeIndex(grooming, 6)] = 4;
            state.Inventory.Tickets = 0;
            return state;
        }

        /// <summary>
        /// Schema 12 (T42): the expeditions: the Gems slot bought, the first stroll done, the guarantee counter, speed-ups of
        /// the day, a team of two away to the Flower Meadows for 8 h on a challenge with its loot drawn at departure, a team
        /// of one back in the Gems slot, three postcards (the first of its destination kept, one rendered).
        /// </summary>
        private static PlayerState BuildExpeditions()
        {
            var state = BuildActivities();
            state.Meta.ProfileId = new Guid("6f0c1a2e-0000-4000-8000-0000000000a1");
            state.Meta.Revision = 320;
            var e = state.Expeditions;
            var meadows = ContentId.FromSource("expedition.flower_meadows");
            e.SlotBought = true;
            e.FirstStrollDone = true;
            e.LongWithoutEgg = 7;
            e.Started = 15;
            e.Completed = 13;
            e.SpeedUpDayKey = 20261012;
            e.SpeedUpsToday = 2;
            e.NextPostcardId = 14;
            e.JournalWeekKey = 20261012;
            e.JournalPlacesMask = 1;
            var away = new ExpeditionSlotRecord
            {
                Active = true,
                Destination = meadows,
                Duration = ExpeditionDuration.EightHours,
                TeamSize = 2,
                StartUnixMs = new DateTimeOffset(2026, 10, 12, 8, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds(),
                ReturnUnixMs = new DateTimeOffset(2026, 10, 12, 16, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds(),
                Stars = 2,
                Challenge = 3,
                Seed = 123457u,
                Loot = new ExpeditionLoot
                {
                    LightMinutes = 99.0, Treat = ContentId.FromSource("treat.clover_crunch"), Treats = 4, SeedKinds = 2,
                    Seed0 = ContentId.FromSource("plant.sweet_pea"), Seeds0 = 2, Seed1 = ContentId.FromSource("plant.daisy"), Seeds1 = 2,
                    Stickers = 1, Tickets = 1, Ingredient0 = ContentId.FromSource("ingredient.gentle_honey"), Ingredients0 = 1,
                    BondPoints = 5, MasteryXp = 15,
                },
            };
            away.SetMember(0, 1, ContentId.FromSource("variant.rabbit.brown"), ContentId.FromSource("species.rabbit"));
            away.SetMember(1, 2, ContentId.FromSource("variant.rabbit.white"), ContentId.FromSource("species.rabbit"));
            e.Slots[0] = away;
            var back = new ExpeditionSlotRecord
            {
                Active = true,
                Destination = meadows,
                Duration = ExpeditionDuration.OneHour,
                TeamSize = 1,
                StartUnixMs = new DateTimeOffset(2026, 10, 11, 18, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds(),
                ReturnUnixMs = new DateTimeOffset(2026, 10, 11, 19, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds(),
                Stars = 1,
                Challenge = -1,
                Seed = 98765u,
                Loot = new ExpeditionLoot { LightMinutes = 15.0, Treat = ContentId.FromSource("treat.clover_crunch"), Treats = 1, BondPoints = 5, MasteryXp = 15 },
            };
            back.SetMember(0, 3, ContentId.FromSource("variant.rabbit.white"), ContentId.FromSource("species.rabbit"));
            e.Slots[2] = back;
            e.Postcards[0] = new PostcardRecord { Id = 1, Destination = meadows, UnixDay = 20372, Rendered = true, FirstOfDestination = true };
            e.Postcards[1] = new PostcardRecord { Id = 12, Destination = meadows, UnixDay = 20372, Rendered = true };
            e.Postcards[2] = new PostcardRecord { Id = 13, Destination = meadows, UnixDay = 20373 };
            e.PostcardCount = 3;
            return state;
        }

        /// <summary>
        /// Schema 13 (T43): the garden: the Gems plot bought, the tutorial seen, 230 points (level 4), speed-ups of the day, a
        /// challenge half played; a watered Sweet Clover growing in plot 0 with a seed back drawn and help from its left
        /// neighbour, a ripe Wild Strawberry in plot 1 (« Pousse heureuse »), plot 2 bare; 7 Clover Green and 3 Meadow Indigo
        /// dyes, the Clover palette and the Meadow duo bought; seeds and crops in the inventory.
        /// </summary>
        private static PlayerState BuildGarden()
        {
            var state = BuildExpeditions();
            state.Meta.ProfileId = new Guid("6f0c1a2e-0000-4000-8000-0000000000b3");
            state.Meta.Revision = 340;
            var g = state.Garden;
            g.PlotBought = true;
            g.FirstVisitDone = true;
            g.Points = 230;
            g.Planted = 21;
            g.Harvested = 19;
            g.SpeedUpDayKey = 20261013;
            g.SpeedUpsToday = 1;
            g.Challenge = 3;
            g.ChallengeHarvests = 2;
            g.ChallengeStars = 5;
            g.ChallengeRunStartUnixMs = new DateTimeOffset(2026, 10, 13, 9, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
            g.ChallengeMask = 0b11;
            long planted = new DateTimeOffset(2026, 10, 13, 10, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
            g.Plots[0] = new GardenPlotRecord
            {
                Active = true,
                Plant = ContentId.FromSource("plant.sweet_clover"),
                PlantedUnixMs = planted,
                RipeUnixMs = planted + 27 * 60_000L,
                GrowMs = 30 * 60_000L,
                Watered = true,
                Seed = 424242u,
                SeedsBack = 1,
                HelpLeftMs = 0,
                HelpRightMs = 600_000L,
                FlowerBeside = true,
            };
            g.Plots[1] = new GardenPlotRecord
            {
                Active = true,
                Plant = ContentId.FromSource("plant.wild_strawberry"),
                PlantedUnixMs = planted - 3_600_000L,
                RipeUnixMs = planted,
                GrowMs = 3_600_000L,
                Seed = 77u,
                Happy = true,
            };
            g.Dyes[0] = 7;
            g.Dyes[8] = 3;
            g.Palettes = (1u << 0) | (1u << 25);
            state.Inventory.Add(ContentId.FromSource("plant.sweet_clover"), 4);
            state.Inventory.Add(ContentId.FromSource("crop.clover_flower"), 9);
            state.Inventory.Add(ContentId.FromSource("ingredient.strawberry"), 4);
            return state;
        }

        /// <summary>
        /// Schema 14 (T44A): the albums open: STK-GL-01, the Hamster companion, the Start and Meadow zones owned, the Prairie
        /// pages, the Grooming page and the Éveil page open, 13 album stars, a duplicate streak of 2, Shiny counters, a paid
        /// counter for the Garden, the Prairie's signature moment seen, page 1's half reward claimed, the seed, the day's
        /// wishes, two new records in Grooming, a 12 h expedition, plants and dyes, a Garden plot finished with Gems.
        /// </summary>
        private static PlayerState BuildAlbums()
        {
            var state = BuildGarden();
            state.Meta.ProfileId = new Guid("6f0c1a2e-0000-4000-8000-0000000000c4");
            state.Meta.Revision = 360;
            var a = state.Albums;
            a.Opened = true;
            foreach (int sticker in new[] { 0, 6, 12, 13, 1, 2 })
            {
                AlbumState.SetBit(a.Owned, sticker, true);
                AlbumState.SetBit(a.Unseen, sticker, sticker != 0);
            }
            a.Stars = 13;
            a.DuplicateStreak = 2;
            a.ShinyDraws[0] = 7;
            a.ShinyDraws[1] = 3;
            a.PaidPoints[5] = 45;
            a.SignatureSeen = 1;
            AlbumState.SetBit(a.PageRewards, 0, true);
            a.AlbumRewards = 0;
            a.Seed = 0x0123456789ABCDEFUL;
            a.PagesOpen = (1UL << 0) | (1UL << 1) | (1UL << 42) | (1UL << 38);
            a.WishDayKey = 20261014;
            a.WishesToday = 2;
            a.Records[0] = 2;
            a.LongExpeditions = 1;
            a.ExpeditionBiomes = 1;
            a.PlantsHarvested = 0b101;
            a.DyesObtained = 3;
            a.GardenSpedUp = 0b10;
            return state;
        }

        /// <summary>
        /// Schema 15 (T44B): 250 eggs hatched (ACH-083 Silver reached, Bronze claimed), 40 eggs in the run, a day of 12 eggs
        /// and 2 wishes, biomes and rarities hatched, two species and trait pairs, two animals petted, the season counted, the
        /// starting title and frame owned and shown, title.lucky_star owned and unseen, the base pose « Sit » owned.
        /// </summary>
        private static PlayerState BuildAchievements()
        {
            var state = BuildAlbums();
            state.Meta.ProfileId = new Guid("6f0c1a2e-0000-4000-8000-0000000000c5");
            state.Meta.Revision = 380;
            var a = state.Achievements;
            a.Progress[82] = 250;
            a.Reached[82] = (byte)AchievementTier.Silver;
            a.Claimed[82] = (byte)AchievementTier.Bronze;
            a.Progress[189] = 40;
            a.RunHatches = 40;
            a.RunLightLevels = 7;
            a.BestRenewalEssence = 120;
            a.DayKey = 20261015;
            a.DayHatches = 12;
            a.DayWishes = 2;
            a.SeasonKey = 2026 * 4 + 3;
            a.JournalWeek = 20261012;
            a.JournalGoalsCounted = 3;
            a.HatchBiomes = 0b11;
            a.HatchRarities = 0b111;
            a.ActivitiesStarred = 1;
            a.Destinations = 1;
            a.PlantsHarvested = 0b101;
            a.PosesUsed = 0;
            a.SpeciesTraits[0] = 0b101;
            a.AnimalsPetted[0] = 7;
            a.AnimalsPetted[1] = 9;
            a.AnimalsPettedCount = 2;
            a.StartGiven = true;
            AchievementState.SetBit(a.TitlesOwned, 0, true);
            AchievementState.SetBit(a.TitlesOwned, 4, true);
            AchievementState.SetBit(a.TitlesUnseen, 4, true);
            a.FramesOwned = 1;
            a.PosesOwned = 1;
            a.PosesUnseen = 1;
            a.SelectedTitle = 0;
            a.SelectedFrame = 0;
            return state;
        }

        /// <summary>
        /// Schema 16 (T44C): the journal set up with three pages (the first egg, captured; the first hatch, waiting for its
        /// portrait, « {name} » the rabbit's species key; a new species, its thumbnail standing in), 2 unseen, a surprise seen,
        /// two garden plants, a best Renewal; the visitors open on day 9800, the meadow rabbit touched twice, the snow rabbit
        /// kept, today's first visit greeted, the guaranteed first visit, 3 greetings.
        /// </summary>
        /// <summary>T47: a crown worn on HeadTop, the Meadow Pack bought, the Field Hamster placed at bond 9 (its arrival seen).</summary>
        private static PlayerState BuildCosmetics()
        {
            var state = BuildJournal();
            state.Meta.ProfileId = new Guid("6f0c1a2e-0000-4000-8000-0000000000c7");
            state.Meta.Revision = 430;
            state.Entitlements.Owned.Add(ContentId.FromSource("companion.fieldhamster"));
            state.Entitlements.Owned.Add(ContentId.FromSource("decor.cozy_burrow"));
            state.Entitlements.Owned.Add(ContentId.FromSource("cosmetic.flower_crown"));
            state.Entitlements.ProcessedTransactions.Add("golden-meadow-pack-1");
            uint first = state.Collection.Count > 0 ? state.Collection[0].InstanceId : 0u;
            if (first != 0)
                state.Cosmetics.Set(first, AccessorySocket.HeadTop, new EquippedAccessory { Accessory = ContentId.FromSource("cosmetic.flower_crown"), Palette = 1 });
            uint hamster = state.Collection.Add(new AnimalInstance
            {
                Species = ContentId.FromSource("species.fieldhamster"),
                Variant = ContentId.FromSource("variant.fieldhamster.fieldgold"),
                Seed = 0x5EED0047u,
                CreatedUtcSeconds = (uint)BaseTime.AddDays(9).ToUnixTimeSeconds(),
                Rarity = RarityTier.Rare,
            });
            state.Companions.Companions.Add(new CompanionRecord
            {
                Species = ContentId.FromSource("species.fieldhamster"), AnimalId = hamster, BondXp = 1300, RewardedLevel = 9, PhotoDay = 20372,
            });
            return state;
        }

        private static PlayerState BuildJournal()
        {
            var state = BuildAchievements();
            state.Meta.ProfileId = new Guid("6f0c1a2e-0000-4000-8000-0000000000c6");
            state.Meta.Revision = 420;
            var j = state.Journal;
            j.Initialized = true;
            ref var egg = ref j.Append();
            egg.Id = 1;
            egg.Moment = JournalMoment.FirstEgg;
            egg.Day = 9773;
            egg.Flags = JournalEntry.FlagCaptured;
            ref var hatch = ref j.Append();
            hatch.Id = 2;
            hatch.Moment = JournalMoment.FirstHatch;
            hatch.Day = 9773;
            hatch.ImageRef = 1;
            hatch.Flags = JournalEntry.FlagPending;
            hatch.Value0 = "#species.rabbit.name";
            ref var species = ref j.Append();
            species.Id = 3;
            species.Moment = JournalMoment.NewSpecies;
            species.Day = 9774;
            species.ImageRef = 1;
            species.Subject = 77;
            species.Flags = JournalEntry.FlagThumbnail;
            species.Value0 = "#species.rabbit.name";
            j.NextId = 4;
            j.SetFirstDone(JournalMoment.FirstEgg);
            j.SetFirstDone(JournalMoment.FirstHatch);
            j.AddSubject(JournalMoment.NewSpecies, 77);
            j.AddSubject(JournalMoment.Season, 3);
            j.Unseen = 2;
            j.GardenPlants = 0b11;
            j.BestRenewalEssence = 120;
            j.AddSurprise("surprise.visitor_arrival");
            var v = state.Visitors;
            v.Contacts[1] = 2;
            v.Contacts[10] = 4;
            v.JoinedMask = 1 << 10;
            v.GreetedDay = 9800;
            v.GreetedMask = 1;
            v.OpenedDay = 9800;
            v.FirstVisitUnixMs = 1_791_000_000_000L;
            v.FirstVisitor = 1;
            v.Greetings = 3;
            return state;
        }

        /// <summary>Schema 9 (T35C): one Renewal done, its memory, Essence in the wallet and three Essence nodes bought.</summary>
        private static PlayerState BuildRenewal()
        {
            var state = BuildEggs();
            state.Meta.ProfileId = new Guid("6f0c1a2e-0000-4000-8000-000000000071");
            state.Meta.Revision = 260;
            state.World.RenewalCount = 1;
            state.Wallet.Essence = 7;
            var renewal = state.Progression.Renewal;
            renewal.EssenceFromLight = 18;
            renewal.EssenceEarned = 41;
            renewal.SpeciesAtLast = 3;
            renewal.BiomesAtLast = 1;
            renewal.MasteredAtLast = 0;
            renewal.AlbumPagesAtLast = 0;
            state.Progression.EssenceRanks[ContentId.FromSource("essence.economy.soft_light")] = 5;
            state.Progression.EssenceRanks[ContentId.FromSource("essence.economy.morning_eggs")] = 2;
            state.Progression.EssenceRanks[ContentId.FromSource("essence.offline.long_nap")] = 1;
            return state;
        }

        /// <summary>Schema 8: four Discovery eggs in stock and Focus eggs bought this cycle.</summary>
        private static PlayerState BuildEggs()
        {
            var state = BuildProgression();
            state.Meta.ProfileId = new Guid("6f0c1a2e-0000-4000-8000-000000000061");
            state.Meta.Revision = 240;
            state.World.DiscoveryEggs = 4;
            state.World.EggPurchasesInCycle[ContentId.FromSource("egg.grassland.focus")] = 6;
            return state;
        }

        private static PlayerState BuildProgression()
        {
            var state = BuildAnalytics();
            state.Meta.ProfileId = new Guid("6f0c1a2e-0000-4000-8000-000000000051");
            state.Meta.Revision = 210;
            state.World.Biomes.Clear();
            state.World.Biomes.Add(new BiomeProgress
            {
                Biome = ContentId.FromSource("biome.grassland"), Unlocked = true, Expansions = 2, ZonesOpened = 0b111u,
                MasteryXp = 600, MasteryLevel = 4, ProductionLevel = 12, NestsLevel = 3, CuriosityLevel = 0,
            });
            state.Progression.Refuge.Score = 150;
            state.Progression.Refuge.Level = 5;
            state.Progression.Refuge.LastRewardedLevel = 3;
            state.Progression.DecorationCopies[ContentId.FromSource("decor.bench_cute")] = 2;
            state.Progression.TreatStand.DayKey = 20261012;
            state.Progression.TreatStand.Slot0 = ContentId.FromSource("treat.clover_crunch");
            state.Progression.TreatStand.Slot1 = ContentId.FromSource("treat.clover_crunch");
            state.Progression.TreatStand.Slot2 = ContentId.FromSource("treat.clover_crunch");
            state.Progression.TreatStand.BoughtMask = 0b001;
            state.Progression.Treats[ContentId.FromSource("treat.clover_crunch")] = 1;
            return state;
        }

        /// <summary>Schema 6: funnel milestones reached and 9 sessions played.</summary>
        private static PlayerState BuildAnalytics()
        {
            var state = BuildShop();
            state.Meta.ProfileId = new Guid("6f0c1a2e-0000-4000-8000-000000000041");
            state.Meta.Revision = 180;
            state.Analytics.Milestones = (1UL << (int)FunnelMilestone.TutorialStart) | (1UL << (int)FunnelMilestone.FirstHatch)
                                         | (1UL << (int)FunnelMilestone.CodexOpen) | (1UL << (int)FunnelMilestone.FirstPurchase);
            state.Analytics.Sessions = 9;
            return state;
        }

        /// <summary>Schema 5: Starter Pack bought once in the sandbox store, shown twice.</summary>
        private static PlayerState BuildShop()
        {
            var state = BuildPity();
            state.Meta.ProfileId = new Guid("6f0c1a2e-0000-4000-8000-000000000031");
            state.Meta.Revision = 150;
            state.Wallet.Gems = 620;
            state.Entitlements.Owned.Add(ContentId.FromSource("cosmetic.flower_crown"));
            state.Entitlements.ProcessedTransactions.Add("sandbox-0001");
            state.Shop.PaidPurchases = 1;
            state.Shop.Offers[ContentId.FromSource("offer.starter.001")] = new OfferRecord { Purchases = 1, Impressions = 2, LastShownUnix = 1791000000 };
            return state;
        }

        private static PlayerState BuildPity()
        {
            var state = BuildCodex();
            state.Meta.ProfileId = new Guid("6f0c1a2e-0000-4000-8000-000000000021");
            state.Meta.Revision = 120;
            state.Pity.NewSpeciesMisses[ContentId.FromSource("egg.grassland.basic")] = 13;
            state.Pity.VariantMisses[ContentId.FromSource("species.rabbit")] = 7;
            return state;
        }

        /// <summary>Schema 3: one species seen, one variant still new.</summary>
        private static PlayerState BuildCodex()
        {
            var state = BuildOffline();
            state.Meta.ProfileId = new Guid("6f0c1a2e-0000-4000-8000-000000000019");
            state.Meta.Revision = 90;
            var rabbit = ContentId.FromSource("species.rabbit");
            state.Collection.Add(new AnimalInstance
            {
                Species = rabbit,
                Variant = ContentId.FromSource("variant.rabbit.white"),
                Seed = 19u,
                CreatedUtcSeconds = (uint)BaseTime.ToUnixTimeSeconds(),
            });
            state.Collection.DiscoveredSpecies.Add(rabbit);
            state.Collection.DiscoveredVariants.Add(ContentId.FromSource("variant.rabbit.brown"));
            state.Collection.DiscoveredVariants.Add(ContentId.FromSource("variant.rabbit.white"));
            state.Codex.SeenSpecies.Add(rabbit);
            state.Codex.SeenVariants.Add(ContentId.FromSource("variant.rabbit.brown"));
            return state;
        }

        /// <summary>Schema 2: exit snapshot with a known rate and a time high-water mark.</summary>
        private static PlayerState BuildOffline()
        {
            var state = new PlayerState();
            state.Meta.ProfileId = new Guid("6f0c1a2e-0000-4000-8000-000000000018");
            state.Meta.CreatedUtc = BaseTime;
            state.Meta.LastSaveUtc = BaseTime.AddDays(10);
            state.Meta.LastKnownSessionId = new Guid("5e55e55e-0000-4000-8000-000000000018");
            state.Meta.Revision = 77;
            state.Meta.AppVersion = "0.2.0";
            state.Wallet.Light = BigValue.FromDouble(5000);
            state.Wallet.LifetimeLight = BigValue.FromDouble(90000);
            state.Collection.Add(new AnimalInstance
            {
                Species = ContentId.FromSource("species.rabbit"),
                Variant = ContentId.FromSource("variant.rabbit.brown"),
                Seed = 18u,
                CreatedUtcSeconds = (uint)BaseTime.ToUnixTimeSeconds(),
            });
            state.Offline.RateAtSave = BigValue.FromDouble(12.5);
            state.Offline.HasRate = true;
            state.Offline.HighWaterUtc = BaseTime.AddDays(10).AddMinutes(3);
            return state;
        }

        private static PlayerState BuildDay7()
        {
            var state = new PlayerState();
            state.Meta.ProfileId = new Guid("6f0c1a2e-0000-4000-8000-000000000007");
            state.Meta.CreatedUtc = BaseTime;
            state.Meta.LastSaveUtc = BaseTime.AddDays(7);
            state.Meta.LastKnownSessionId = new Guid("5e55e55e-0000-4000-8000-000000000007");
            state.Meta.Revision = 42;
            state.Meta.AppVersion = "0.1.0";
            state.Meta.TotalPlaySeconds = 18000.0;

            state.Wallet.Light = BigValue.FromMantissaExponent(1.2345, 6);
            state.Wallet.LifetimeLight = BigValue.FromMantissaExponent(9.87, 7);
            state.Wallet.Gems = 50;

            state.World.Biomes.Add(new BiomeProgress { Biome = ContentId.FromSource("biome.grassland"), Unlocked = true, Expansions = 3 });
            state.World.Biomes.Add(new BiomeProgress { Biome = ContentId.FromSource("biome.forest"), Unlocked = true, Expansions = 0 });
            state.World.EggPurchasesInCycle[ContentId.FromSource("egg.grassland.basic")] = 37;

            string[] species = { "rabbit", "squirrel", "hedgehog" };
            string[][] variants =
            {
                new[] { "brown", "white", "grey", "black", "spotted", "sakura" },
                new[] { "red", "brown", "grey", "cream", "black", "autumn" },
                new[] { "brown", "cream", "grey", "black", "white", "mushroom" },
            };

            uint baseUnix = (uint)BaseTime.ToUnixTimeSeconds();
            for (int i = 0; i < 120; i++)
            {
                int s = i % 3;
                var speciesId = ContentId.FromSource("species." + species[s]);
                var variantId = ContentId.FromSource("variant." + species[s] + "." + variants[s][i % 6]);
                var animal = new AnimalInstance
                {
                    Species = speciesId,
                    Variant = variantId,
                    Seed = unchecked(1000003u * (uint)(i + 1)),
                    CreatedUtcSeconds = baseUnix + (uint)(i * 3600),
                    TraitBits = i % 17 == 0 ? 1u : 0u,
                    BondPoints = (ushort)(i * 7 % 1000),
                    Rarity = i % 6 == 5 ? RarityTier.Rare : (i % 4 == 3 ? RarityTier.Uncommon : RarityTier.Common),
                    Flags = i == 0 ? AnimalFlags.Favorite | AnimalFlags.HasCustomName : AnimalFlags.None,
                };
                state.Collection.Add(animal);
                state.Collection.DiscoveredSpecies.Add(speciesId);
                state.Collection.DiscoveredVariants.Add(variantId);
            }

            state.Collection.CustomNames[1] = "Mochi";
            state.Collection.CustomNames[2] = "Pip";

            state.Mastery.Species[ContentId.FromSource("species.rabbit")] = new SpeciesMastery { Level = 5, Xp = 12 };
            state.Mastery.Species[ContentId.FromSource("species.squirrel")] = new SpeciesMastery { Level = 3, Xp = 4 };
            state.Mastery.Species[ContentId.FromSource("species.hedgehog")] = new SpeciesMastery { Level = 2, Xp = 0 };

            state.Settings.MusicVolume = 0.5f;
            state.Settings.SfxVolume = 0.9f;
            state.Settings.HapticsEnabled = false;
            state.Settings.ReduceMotion = true;
            state.Settings.LanguageCode = "fr";
            return state;
        }

        private static PlayerState BuildPremium()
        {
            var state = BuildNew();
            state.Meta.ProfileId = new Guid("6f0c1a2e-0000-4000-8000-0000000000aa");
            state.Meta.Revision = 9;
            state.Wallet.Gems = 1250;
            state.Entitlements.Owned.Add(ContentId.FromSource("offer.starter.001"));
            state.Entitlements.Owned.Add(ContentId.FromSource("cosmetic.flower_crown"));
            state.Entitlements.ProcessedTransactions.Add("GPA.0000-0000-0000-00001");
            state.Entitlements.ProcessedTransactions.Add("ios-1000000000000001");
            return state;
        }
    }
}
