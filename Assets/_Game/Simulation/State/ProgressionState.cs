#nullable enable
using System.Collections.Generic;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// Refuge level, Light decorations and the treat stand (doc 02 Améliorations, section 13.2). Biome upgrades,
    /// zones and mastery live with each biome in <see cref="WorldProgressState.Biomes"/>.
    /// </summary>
    public sealed class ProgressionState
    {
        public RefugeState Refuge { get; } = new RefugeState();

        /// <summary>Copies bought in Light per common decoration.</summary>
        public Dictionary<ContentId, int> DecorationCopies { get; } = new Dictionary<ContentId, int>();

        public TreatStandState TreatStand { get; } = new TreatStandState();

        /// <summary>Treats owned, per treat (fed to animals, wishes).</summary>
        public Dictionary<ContentId, int> Treats { get; } = new Dictionary<ContentId, int>();

        /// <summary>T35C, doc 02 Renouveau, section 7: what the Renewal formula and its completion bonus remember.</summary>
        public RenewalState Renewal { get; } = new RenewalState();

        /// <summary>Ranks bought per Essence node (doc 02 Renouveau, section 2). Kept by every Renewal.</summary>
        public Dictionary<ContentId, int> EssenceRanks { get; } = new Dictionary<ContentId, int>();
    }

    /// <summary>
    /// The Renewal's memory (the count itself is <see cref="WorldProgressState.RenewalCount"/>): the Essence the
    /// lifetime Light already gave, all Essence ever earned (the 10 % rule of the button) and the counts the completion
    /// bonus is measured from.
    /// </summary>
    public sealed class RenewalState
    {
        /// <summary>Essence already given for the lifetime Light, E_light at the last Renewal.</summary>
        public long EssenceFromLight;

        /// <summary>Every Essence ever earned, from Renewals and other sources.</summary>
        public long EssenceEarned;

        /// <summary>Species discovered at the last Renewal (completion bonus: +1 per new species since).</summary>
        public int SpeciesAtLast;

        /// <summary>Biomes open at the last Renewal (+5 per biome since).</summary>
        public int BiomesAtLast;

        /// <summary>Species at mastery 30 or more at the last Renewal (+2 per species since).</summary>
        public int MasteredAtLast;

        /// <summary>Album pages complete at the last Renewal (+1 per page since; albums come with T44).</summary>
        public int AlbumPagesAtLast;
    }

    public sealed class RefugeState
    {
        /// <summary>Refuge score. Recalculable from the profile, and it never goes down.</summary>
        public long Score;

        public int Level = 1;

        /// <summary>Highest level whose rewards were claimed.</summary>
        public int LastRewardedLevel = 1;

        /// <summary>Essence earned by levels before the first Renewal: given at the first Renewal.</summary>
        public int PendingEssenceFromLevels;
    }

    /// <summary>The day's treat stand: which local day it was filled for, its three treats and what was bought.</summary>
    public sealed class TreatStandState
    {
        /// <summary>Local calendar day as yyyymmdd; 0 = never filled.</summary>
        public int DayKey;

        public ContentId Slot0;
        public ContentId Slot1;
        public ContentId Slot2;

        /// <summary>Bit i: slot i bought today.</summary>
        public int BoughtMask;
    }
}
