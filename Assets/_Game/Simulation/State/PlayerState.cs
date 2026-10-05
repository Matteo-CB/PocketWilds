#nullable enable
using System;
using System.Collections.Generic;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>Root of everything persisted for one profile. Plain data, no Unity objects.</summary>
    public sealed class PlayerState
    {
        public SaveMeta Meta { get; } = new SaveMeta();
        public WalletState Wallet { get; } = new WalletState();
        public WorldProgressState World { get; } = new WorldProgressState();
        public CollectionState Collection { get; } = new CollectionState();
        public MasteryState Mastery { get; } = new MasteryState();
        public SettingsState Settings { get; } = new SettingsState();
        public EntitlementState Entitlements { get; } = new EntitlementState();
        public OfflineState Offline { get; } = new OfflineState();

        public static PlayerState CreateNew(DateTimeOffset utcNow)
        {
            var state = new PlayerState();
            state.Meta.ProfileId = Guid.NewGuid();
            state.Meta.CreatedUtc = utcNow;
            state.Meta.LastSaveUtc = utcNow;
            state.Offline.HighWaterUtc = utcNow;
            return state;
        }
    }

    /// <summary>
    /// Economic and time snapshot written with every save (doc 08 Offline). The offline gap starts at
    /// Meta.LastSaveUtc, the moment the saved Light was true.
    /// </summary>
    public sealed class OfflineState
    {
        /// <summary>Light per second when the last save was written (doc 02 AggregateProductionAtExit).</summary>
        public BigValue RateAtSave;

        /// <summary>False for saves made before schema 2: the rate is then rebuilt from the collection.</summary>
        public bool HasRate;

        /// <summary>
        /// Latest wall time this profile has seen. After a clock moved backwards, offline time only counts
        /// again once real time passes this mark (tolerant, never punitive).
        /// </summary>
        public DateTimeOffset HighWaterUtc;
    }

    public sealed class SaveMeta
    {
        public Guid ProfileId;
        public DateTimeOffset CreatedUtc;
        public DateTimeOffset LastSaveUtc;
        public Guid LastKnownSessionId;

        /// <summary>Incremented on every successful save. Picks the newest valid file after a crash.</summary>
        public long Revision;

        public string AppVersion = string.Empty;
        public double TotalPlaySeconds;
    }

    public sealed class WalletState
    {
        /// <summary>Soft currency. Only EconomyService writes it during gameplay.</summary>
        public BigValue Light;

        /// <summary>Total Light ever produced. Drives the Renewal formula.</summary>
        public BigValue LifetimeLight;

        public long Essence;

        /// <summary>Local cache. Purchased Gems are granted through idempotent entitlements.</summary>
        public long Gems;
    }

    public struct BiomeProgress
    {
        public ContentId Biome;
        public bool Unlocked;
        public int Expansions;
    }

    public sealed class WorldProgressState
    {
        public List<BiomeProgress> Biomes { get; } = new List<BiomeProgress>();

        /// <summary>Eggs bought per egg type in the current economic cycle (cost curve input).</summary>
        public Dictionary<ContentId, int> EggPurchasesInCycle { get; } = new Dictionary<ContentId, int>();

        public int RenewalCount;
    }

    public struct SpeciesMastery
    {
        public int Level;
        public int Xp;
    }

    public sealed class MasteryState
    {
        public Dictionary<ContentId, SpeciesMastery> Species { get; } = new Dictionary<ContentId, SpeciesMastery>();

        public int GetLevel(ContentId species) =>
            Species.TryGetValue(species, out var mastery) ? mastery.Level : 0;
    }

    /// <summary>Player preferences that follow the profile. Device-only settings (quality, battery) live outside the save.</summary>
    public sealed class SettingsState
    {
        public float MusicVolume = 0.8f;
        public float SfxVolume = 1f;
        public bool HapticsEnabled = true;
        public bool ReduceMotion;

        /// <summary>Empty means "follow the device language".</summary>
        public string LanguageCode = string.Empty;
    }

    public sealed class EntitlementState
    {
        public HashSet<ContentId> Owned { get; } = new HashSet<ContentId>();

        /// <summary>Store transaction ids already granted. Makes purchase callbacks idempotent.</summary>
        public HashSet<string> ProcessedTransactions { get; } = new HashSet<string>();
    }
}
