#nullable enable

namespace PocketWilds.Simulation
{
    /// <summary>
    /// T48B what the save keeps of the seasons and the special weekends (schema 19): the season run whose card was shown (or
    /// stored silently on the very first launch) and the weekend already seen on screen (analytics weekend_seen once).
    /// </summary>
    public sealed class SeasonState
    {
        /// <summary>
        /// The season run whose « La saison {season} commence » card is done: run start yyyymm × 10 + season (202612 4 → 2026124);
        /// 0 before the first launch with seasons.
        /// </summary>
        public int CardKey;

        /// <summary>The weekend seen on screen: its first local day (yyyymmdd) × 10 + kind; 0 for none yet.</summary>
        public int WeekendSeenKey;
    }
}
