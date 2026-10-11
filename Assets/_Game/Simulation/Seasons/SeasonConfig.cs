#nullable enable
using System;
using System.Collections.Generic;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// T48B the refuge seasons' Remote Config (doc 08): the month map « events.refuge_seasons » (a JSON object, default
    /// {"awakening":[3,4,5],"sun":[6,7,8],"harvest":[9,10,11],"frost":[12,1,2]}) and the flag « flags.refuge_seasons » (false:
    /// always Éveil). A map is valid only when each of the 12 months is in exactly one season (no gap, no duplicate, no other key);
    /// an invalid map is ignored whole. Every season lookup of the game (album pages, journal, visitors, achievements, the look)
    /// goes through <see cref="SeasonOfMonth"/>, so a new map changes the season without a new build.
    /// </summary>
    public sealed class SeasonConfig
    {
        public const string MonthsKey = "events.refuge_seasons";
        public const string EnabledFlag = "flags.refuge_seasons";

        /// <summary>The Remote Config names of the seasons, indexed by <see cref="RefugeSeason"/> (0 unused).</summary>
        public static readonly string[] SeasonKeys = { string.Empty, "awakening", "sun", "harvest", "frost" };

        private readonly RefugeSeason[] _months = new RefugeSeason[12];
        private readonly RefugeSeason[] _parsed = new RefugeSeason[12];

        public SeasonConfig()
        {
            for (int m = 1; m <= 12; m++)
                _months[m - 1] = DefaultSeasonOfMonth(m);
        }

        /// <summary>flags.refuge_seasons: false keeps the refuge in Éveil all year (the doc's fallback) and owes no card.</summary>
        public bool Enabled { get; private set; } = true;

        /// <summary>Raised when the map or the flag really changed (the season service re-reads the season at once).</summary>
        public event Action? Changed;

        /// <summary>The default map (March to May Éveil, June to August Soleil, September to November Récolte, December to February Givre).</summary>
        public static RefugeSeason DefaultSeasonOfMonth(int month) => (RefugeSeason)AlbumHooks.SeasonOfMonth(month);

        /// <summary>The season of a local month (1 to 12) under the active map; Éveil while the flag is off.</summary>
        public RefugeSeason SeasonOfMonth(int month)
        {
            if (!Enabled)
                return RefugeSeason.Awakening;
            return month >= 1 && month <= 12 ? _months[month - 1] : DefaultSeasonOfMonth(month);
        }

        /// <summary>The season number (1 to 4) the albums, journal, visitors and achievements use; the default map when null.</summary>
        public static int SeasonNumber(SeasonConfig? config, int month) => config != null ? (int)config.SeasonOfMonth(month) : AlbumHooks.SeasonOfMonth(month);

        /// <summary>
        /// The month (yyyymm) the season run holding this month began: the months before it of the same season are walked back
        /// (a winter spans two years: January 2027 belongs to the Givre that began in December 2026). A map with one season all
        /// year gives one run per calendar year.
        /// </summary>
        public int RunStart(int year, int month)
        {
            var season = SeasonOfMonth(month);
            int y = year, m = month;
            for (int i = 0; i < 11; i++)
            {
                int py = m == 1 ? y - 1 : y;
                int pm = m == 1 ? 12 : m - 1;
                if (SeasonOfMonth(pm) != season)
                    return y * 100 + m;
                y = py;
                m = pm;
            }
            return year * 100 + 1;
        }

        /// <summary>The year a season run began (achievements count each season once per run).</summary>
        public static int RunYear(SeasonConfig? config, int year, int month)
        {
            if (config == null)
                return month <= 2 ? year - 1 : year;
            return config.RunStart(year, month) / 100;
        }

        /// <summary>The month map as Remote Config writes it (tests and logs).</summary>
        public string MonthsJson()
        {
            var text = new System.Text.StringBuilder("{");
            for (int s = 1; s <= 4; s++)
            {
                if (s > 1)
                    text.Append(',');
                text.Append('"').Append(SeasonKeys[s]).Append("\":[");
                bool first = true;
                for (int m = 1; m <= 12; m++)
                {
                    if (_months[m - 1] != (RefugeSeason)s)
                        continue;
                    if (!first)
                        text.Append(',');
                    text.Append(m.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    first = false;
                }
                text.Append(']');
            }
            return text.Append('}').ToString();
        }

        /// <summary>
        /// Remote Config « events.refuge_seasons »: null or empty restores the default map; a valid map replaces the active one; an
        /// invalid one (bad JSON, unknown key, a month outside 1 to 12, a gap, a duplicate) is ignored whole. True when applied.
        /// </summary>
        public bool ApplyRemoteMonths(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                for (int m = 1; m <= 12; m++)
                    _parsed[m - 1] = DefaultSeasonOfMonth(m);
            }
            else if (!TryParseMonths(json!, _parsed))
                return false;
            bool changed = false;
            for (int i = 0; i < 12; i++)
            {
                if (_months[i] != _parsed[i])
                    changed = true;
                _months[i] = _parsed[i];
            }
            if (changed)
                Changed?.Invoke();
            return true;
        }

        public bool TryOverrideFlag(string key, bool value)
        {
            if (key != EnabledFlag)
                return false;
            if (Enabled != value)
            {
                Enabled = value;
                Changed?.Invoke();
            }
            return true;
        }

        /// <summary>
        /// The strict month map parser: an object whose keys are season names (each at most once) and whose values are lists of
        /// whole months 1 to 12; the 12 months must each appear exactly once. Writes <paramref name="into"/> (12 seasons) only
        /// when valid.
        /// </summary>
        public static bool TryParseMonths(string json, RefugeSeason[] into)
        {
            if (json == null || into == null || into.Length < 12)
                return false;
            var months = new RefugeSeason[12];
            var reader = new SeasonJson(json);
            bool ok = reader.TryReadObject(key =>
            {
                int season = Array.IndexOf(SeasonKeys, key);
                if (season < 1)
                    return false;
                return reader.TryReadList(() =>
                {
                    if (!reader.TryReadInt(out int month) || month < 1 || month > 12 || months[month - 1] != RefugeSeason.None)
                        return false;
                    months[month - 1] = (RefugeSeason)season;
                    return true;
                });
            });
            if (!ok || !reader.AtEnd)
                return false;
            for (int i = 0; i < 12; i++)
                if (months[i] == RefugeSeason.None)
                    return false;
            Array.Copy(months, into, 12);
            return true;
        }

        public static IEnumerable<string> RemoteKeys
        {
            get { yield return MonthsKey; }
        }
    }
}
