#nullable enable
using System;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// T48B the refuge's four seasons (doc Événements originaux, section 5), numbered like the album's season pages
    /// (page.season.1 to 4) and the visitors' seasons. Stable values.
    /// </summary>
    public enum RefugeSeason : byte
    {
        None = 0,
        Awakening = 1,
        Sun = 2,
        Harvest = 3,
        Frost = 4,
    }

    /// <summary>T48B the special weekends (doc Événements originaux, section 6; events.weekends « kind »). Stable values (analytics).</summary>
    public enum WeekendKind : byte
    {
        None = 0,
        Activities = 1,
        Eggs = 2,
        Cuddles = 3,
        Garden = 4,
        Expeditions = 5,
        Albums = 6,
    }

    /// <summary>
    /// T48B what the presentation reads of the season: the current one (by the player's local month and the Remote Config month
    /// map), its change, and the « La saison {season} commence » card owed once per new season.
    /// </summary>
    public interface ISeasonView
    {
        RefugeSeason Current { get; }

        /// <summary>Raised when the season changes (local midnight on the first day of a season month, or a new month map).</summary>
        event Action<RefugeSeason>? Changed;

        /// <summary>True when the card of the current season was not shown yet (never on the very first launch).</summary>
        bool CardPending { get; }

        void MarkCardShown();
    }

    /// <summary>T48B what the presentation reads of the special weekend running now (None when none or during a big event).</summary>
    public interface IWeekendView
    {
        WeekendKind Active { get; }

        /// <summary>When the running weekend ends (Unix seconds, from its local Sunday 23:59); 0 when none.</summary>
        long EndsUnix { get; }

        event Action<WeekendKind>? Changed;

        /// <summary>The weekend was seen on screen (analytics weekend_seen once per weekend).</summary>
        void MarkSeen();
    }
}
