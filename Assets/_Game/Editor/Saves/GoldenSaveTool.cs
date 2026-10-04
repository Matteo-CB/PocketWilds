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
