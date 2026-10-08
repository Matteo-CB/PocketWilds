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
