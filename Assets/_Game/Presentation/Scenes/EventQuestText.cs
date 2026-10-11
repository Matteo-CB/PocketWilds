using System.Collections.Generic;
using System.Text;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Simulation;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T48: an event quest's line in the player's language (texts: Editor/UI/EventQuestTexts.cs). Fills {activity} with the quest
    /// activity (<see cref="EventService.QuestActivityOf"/>: the featured activity or its fallback, the activity of the day of
    /// EQ-D-22), {currency} with the event currency's name and {n} with the record's target (EQ-D-23, « .one » for 1). French
    /// elides « de » before a vowel (« d'Énigmes »). Each line is cached by what it depends on (key, activity, currency, target,
    /// language): asking again allocates nothing. Not for per-frame use: the screens ask when their list changes.
    /// </summary>
    public sealed class EventQuestText
    {
        /// <summary>The placeholders this formatter fills (the text tests check every quest line against it).</summary>
        public static readonly string[] Placeholders = { "n", "activity", "currency" };

        private const string Vowels = "aeiouyàâäéèêëîïôöùûüÿœæAEIOUYÀÂÄÉÈÊËÎÏÔÖÙÛÜŸŒÆ";

        private readonly ILocalization _text;
        private readonly StringBuilder _builder = new StringBuilder(96);
        private readonly Dictionary<(string Key, ActivityId Activity, string Currency, int Target), string> _cache =
            new Dictionary<(string, ActivityId, string, int), string>(64);
        private string _language = string.Empty;

        public EventQuestText(ILocalization text)
        {
            _text = text;
        }

        /// <summary>The text key of a quest line (EQ-D-23 with a target of 1: « .one »).</summary>
        public static string KeyOf(EventQuestTemplate template, in EventQuestRecord quest) =>
            template.Goal == EventGoalKind.BiomeVisits && quest.Target == 1 ? template.TextKey + ".one" : template.TextKey;

        /// <summary>The line of a daily (<paramref name="daily"/>) or long quest of the running event; empty when its template is unknown.</summary>
        public string Line(EventService events, in EventQuestRecord quest, bool daily)
        {
            var template = events.TemplateOf(in quest, daily);
            var definition = events.Definition;
            if (template == null || definition == null)
                return string.Empty;
            return Line(KeyOf(template, in quest), events.QuestActivityOf(in quest), definition.CurrencyKey, quest.Target);
        }

        /// <summary>A line from its parts (the hub's preview and the tests).</summary>
        public string Line(string key, ActivityId activity, string currencyKey, int target)
        {
            if (_text.Language != _language)
            {
                _language = _text.Language;
                _cache.Clear();
            }
            var cacheKey = (key, activity, currencyKey ?? string.Empty, target);
            if (_cache.TryGetValue(cacheKey, out var cached))
                return cached;
            string line = Format(_text.Get(key), activity != ActivityId.None ? _text.Get(ActivityText.NameKey(activity)) : string.Empty,
                string.IsNullOrEmpty(currencyKey) ? string.Empty : _text.Get(currencyKey), target, _language.StartsWith("fr"));
            _cache[cacheKey] = line;
            return line;
        }

        /// <summary>Fills the placeholders of one line (pure: no localization needed).</summary>
        public string Format(string line, string activity, string currency, int target, bool french)
        {
            var sb = _builder;
            sb.Clear();
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '{')
                {
                    int close = line.IndexOf('}', i + 1);
                    if (close > i)
                    {
                        string name = line.Substring(i + 1, close - i - 1);
                        string value = name == "activity" ? activity : name == "currency" ? currency : name == "n" ? target.ToString() : null;
                        if (value != null)
                        {
                            // « de Cache-cache » but « d'Énigmes ».
                            if (french && value.Length > 0 && Vowels.IndexOf(value[0]) >= 0 && sb.Length >= 3 && sb[sb.Length - 1] == ' '
                                && sb[sb.Length - 2] == 'e' && sb[sb.Length - 3] == 'd' && (sb.Length == 3 || sb[sb.Length - 4] == ' '))
                            {
                                sb.Length -= 2;
                                sb.Append('\'');
                            }
                            sb.Append(value);
                            i = close;
                            continue;
                        }
                    }
                }
                sb.Append(line[i]);
            }
            return sb.ToString();
        }
    }
}
