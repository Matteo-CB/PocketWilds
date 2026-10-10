#nullable enable
using System;
using System.Collections.Generic;
using PocketWilds.Content;
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
        public CodexState Codex { get; } = new CodexState();
        public PityState Pity { get; } = new PityState();
        public ShopState Shop { get; } = new ShopState();
        public AnalyticsState Analytics { get; } = new AnalyticsState();
        public ProgressionState Progression { get; } = new ProgressionState();

        /// <summary>T36: the daily rendez-vous (welcome, gift, calendar, wishes, journal, return gifts).</summary>
        public DailyVisitState DailyVisit { get; } = new DailyVisitState();

        /// <summary>T36: tickets, seeds, ingredients, decorations and stickers won before their systems open.</summary>
        public InventoryState Inventory { get; } = new InventoryState();

        /// <summary>T37: the activities (tickets, records, challenges, adaptive difficulty, daily Gems).</summary>
        public ActivityState Activities { get; } = new ActivityState();

        /// <summary>T42: the expeditions (slots, teams, timers, loot drawn at departure, the guarantee, the postcards).</summary>
        public ExpeditionState Expeditions { get; } = new ExpeditionState();

        /// <summary>T43: the garden (plots, sprouts, timers, draws made at planting, points, challenge, dyes and palettes).</summary>
        public GardenState Garden { get; } = new GardenState();

        /// <summary>The welcome is over: the tutorial wish was done, or the save already holds 5 animals (doc 01, section 1.1).</summary>
        public bool IsOnboardingDone => DailyVisit.OnboardingDone || Collection.Count >= 5;

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
    /// Bad-luck protection counters (doc 02 Pity). Per egg: eggs in a row without a new species.
    /// Per species: known variants in a row while that species still hides some.
    /// </summary>
    public sealed class PityState
    {
        public Dictionary<ContentId, int> NewSpeciesMisses { get; } = new Dictionary<ContentId, int>();
        public Dictionary<ContentId, int> VariantMisses { get; } = new Dictionary<ContentId, int>();
    }

    /// <summary>
    /// What the player has already looked at in the Codex. Discovered minus seen = "New" badges.
    /// Only discovered ids are ever added, so seen is always a subset of discovered.
    /// </summary>
    public sealed class CodexState
    {
        public HashSet<ContentId> SeenSpecies { get; } = new HashSet<ContentId>();
        public HashSet<ContentId> SeenVariants { get; } = new HashSet<ContentId>();
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

    /// <summary>
    /// One biome's progression (doc 02 Améliorations, section 13.2). Zones are bits in their order: bit 0 the
    /// arrival zone, bits 1 to n the zones to open, the last bit the passage toward the next biome.
    /// </summary>
    public struct BiomeProgress
    {
        public ContentId Biome;
        public bool Unlocked;

        /// <summary>Zones opened by purchase (the arrival and the passage never count).</summary>
        public int Expansions;

        public uint ZonesOpened;

        /// <summary>Biome mastery XP gathered in total (never reset) and the level it gives.</summary>
        public long MasteryXp;
        public int MasteryLevel;

        public int ProductionLevel;
        public int NestsLevel;
        public int CuriosityLevel;

        public bool IsZoneOpen(int zoneIndex) => zoneIndex >= 0 && zoneIndex < 32 && (ZonesOpened & (1u << zoneIndex)) != 0;
    }

    public sealed class WorldProgressState
    {
        public List<BiomeProgress> Biomes { get; } = new List<BiomeProgress>();

        /// <summary>Eggs bought per egg type in the current economic cycle (cost curve input).</summary>
        public Dictionary<ContentId, int> EggPurchasesInCycle { get; } = new Dictionary<ContentId, int>();

        public int RenewalCount;

        /// <summary>Discovery eggs won and not hatched yet (doc 02 Œufs, section 2.3: a stock with no limit).</summary>
        public int DiscoveryEggs;
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

    /// <summary>Per-offer history: purchases (limits), last time shown (moment cooldown), impressions.</summary>
    public struct OfferRecord
    {
        public int Purchases;
        public int Impressions;
        public long LastShownUnix;
    }

    /// <summary>Shop history (T28). Grants themselves live in Wallet.Gems and EntitlementState.</summary>
    public sealed class ShopState
    {
        public Dictionary<ContentId, OfferRecord> Offers { get; } = new Dictionary<ContentId, OfferRecord>();

        /// <summary>Real-money purchases granted (doc 09 segments: payer or not).</summary>
        public int PaidPurchases;

        public OfferRecord Get(ContentId offer) => Offers.TryGetValue(offer, out var record) ? record : default;
    }

    /// <summary>
    /// Funnel milestones (doc 09 Analytics: launch to second purchase). Each fires once per profile. Values are bit
    /// indices and analytics ids: stable, append only, never renumbered.
    /// </summary>
    public enum FunnelMilestone : byte
    {
        TutorialStart = 0,
        FirstHatch = 1,
        FirstCollect = 2,
        FirstUpgrade = 3,
        FirstNewSpecies = 4,
        FirstRare = 5,
        BiomeUnlock = 6,
        CodexOpen = 7,
        FirstShopExposure = 8,
        FirstRewardedAd = 9,
        FirstOfferImpression = 10,
        FirstPurchase = 11,
        SecondPurchase = 12,

        // Doc 08 section 9.5 (T37).
        FirstWish = 13,
        FirstActivity = 14,
    }

    /// <summary>Analytics bookkeeping that follows the profile (T29): reached milestones, session count.</summary>
    public sealed class AnalyticsState
    {
        public ulong Milestones;
        public int Sessions;

        public bool Has(FunnelMilestone milestone) => (Milestones & (1UL << (int)milestone)) != 0;

        /// <summary>Marks a milestone; false when it was already reached (the event must not fire again).</summary>
        public bool TrySet(FunnelMilestone milestone)
        {
            ulong bit = 1UL << (int)milestone;
            if ((Milestones & bit) != 0)
                return false;
            Milestones |= bit;
            return true;
        }

        /// <summary>
        /// Saves older than schema 6: what the profile already did counts as reached, so a veteran never sends
        /// "first hatch" on update.
        /// </summary>
        public static void InferFrom(PlayerState state)
        {
            var analytics = state.Analytics;
            var collection = state.Collection;
            if (collection.Count > 0)
            {
                analytics.TrySet(FunnelMilestone.TutorialStart);
                analytics.TrySet(FunnelMilestone.FirstHatch);
            }
            if (collection.DiscoveredSpecies.Count >= 2)
                analytics.TrySet(FunnelMilestone.FirstNewSpecies);
            for (int i = 0; i < collection.Count; i++)
            {
                if (collection[i].Rarity >= RarityTier.Rare)
                {
                    analytics.TrySet(FunnelMilestone.FirstRare);
                    break;
                }
            }
            if (state.Codex.SeenSpecies.Count > 0)
                analytics.TrySet(FunnelMilestone.CodexOpen);
            if (state.Shop.Offers.Count > 0 || state.Shop.PaidPurchases > 0)
            {
                analytics.TrySet(FunnelMilestone.FirstShopExposure);
                analytics.TrySet(FunnelMilestone.FirstOfferImpression);
            }
            if (state.Shop.PaidPurchases >= 1)
                analytics.TrySet(FunnelMilestone.FirstPurchase);
            if (state.Shop.PaidPurchases >= 2)
                analytics.TrySet(FunnelMilestone.SecondPurchase);
        }
    }

    public sealed class EntitlementState
    {
        public HashSet<ContentId> Owned { get; } = new HashSet<ContentId>();

        /// <summary>Store transaction ids already granted. Makes purchase callbacks idempotent.</summary>
        public HashSet<string> ProcessedTransactions { get; } = new HashSet<string>();
    }
}
