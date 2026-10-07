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
