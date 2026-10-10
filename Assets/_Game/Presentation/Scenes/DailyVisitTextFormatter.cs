using System;
using System.Collections.Generic;
using System.Text;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Simulation;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T36 sentences in the player's language (doc 01 Contenu: section 0 « Textes », wishes 2.3, journal 5.2, calendar
    /// 4.4, return gifts 6.2 and 6.3; Writing Guide: short, warm, no em dash). Fills the placeholders of the shipped
    /// texts (Editor/UI/DailyVisitTexts.cs): {n} and {total} with grouped digits, {species} and {biome} bare names,
    /// {species_the} and {biome_the} names with their article, {animal} the animal's own name (else its species with the
    /// article), {upgrade} and {reward}. French grammar where the doc's sentences need it: « de le » becomes « du »,
    /// « de les » « des », « à le » « au », « à les » « aux », and « de » is elided before a vowel (« d'Îles des nuages »).
    /// <para>Every result is cached by what it depends on (template, subject, target, animal name, language): the same
    /// wish asked twice returns the same string and allocates nothing; a text is built (one reusable StringBuilder) only
    /// when it really changes. The caches drop when <see cref="ILocalization.Language"/> changes (Settings). Not for
    /// per-frame use: the presenter asks once per change.</para>
    /// </summary>
    public sealed class DailyVisitTextFormatter
    {
        /// <summary>The placeholders this formatter fills (the text tests check every shipped sentence against it).</summary>
        public static readonly string[] Placeholders = { "n", "total", "species", "species_the", "biome", "biome_the", "animal", "upgrade", "reward" };

        /// <summary>WISH-026 « Upgrade {upgrade} in {biome} »: one sentence per upgrade kind (key « wish.026.&lt;kind&gt; »).</summary>
        public static readonly string[] UpgradeWishKeys =
        {
            WishCatalog.TextKey(WishTemplateId.UpgradeInBiome) + ".production",
            WishCatalog.TextKey(WishTemplateId.UpgradeInBiome) + ".nests",
            WishCatalog.TextKey(WishTemplateId.UpgradeInBiome) + ".curiosity",
        };

        // Keys this formatter adds beside DailyVisitText (doc 4.4 « Jour {n} », the big box today, Light in words).
        public const string CalendarDayKey = "daily.calendar.day";
        public const string CalendarBigTodayKey = "daily.calendar.big_today";
        public const string LightMinutesWordsKey = "reward.light_minutes.words";
        public const string LightAmountKey = "reward.light";

        private const int WishSlots = 24;
        private const int GoalSlots = 16;
        private const int LineSlots = 48;
        private const int CellSlots = 32;

        private const char NarrowNoBreakSpace = ' ';
        private const string Vowels = "aeiouyàâäéèêëîïôöùûüÿœæAEIOUYÀÂÄÉÈÊËÎÏÔÖÙÛÜŸŒÆ";

        private const byte OpPlain = 0;
        private const byte OpCounted = 1;
        private const byte OpPair = 2;
        private const byte OpLabel = 3;
        private const byte OpName = 4;
        private const byte OpTomorrow = 5;

        private readonly ContentRegistry _content;
        private readonly ILocalization _text;
        private readonly StringBuilder _builder = new StringBuilder(192);
        private readonly char[] _digits = new char[BigValueFormatter.MaxLength];

        /// <summary>Localization keys of the names this formatter writes: species, biomes, and the items the rendez-vous gives.</summary>
        private readonly Dictionary<ContentId, NameKeys> _names = new Dictionary<ContentId, NameKeys>(128);

        private readonly WishEntry[] _wishes = new WishEntry[WishSlots];
        private readonly GoalEntry[] _goals = new GoalEntry[GoalSlots];
        private readonly LineEntry[] _lines = new LineEntry[LineSlots];
        private readonly CellEntry[] _cells = new CellEntry[CellSlots];
        private int _wishCount, _wishNext, _goalCount, _goalNext, _lineCount, _lineNext, _cellCount, _cellNext;

        private string _language;
        private bool _french;

        /// <param name="state">The player's state, for the animals' names ({animal}, <see cref="AnimalName"/>); the presenter
        /// passes <c>context.State</c>. Without it, animals are nameless.</param>
        public DailyVisitTextFormatter(ContentRegistry content, ILocalization text, PlayerState state = null)
        {
            _content = content ?? ContentRegistry.Empty;
            _text = text ?? throw new ArgumentNullException(nameof(text));
            State = state;
            BuildNames();
        }

        /// <summary>The state whose animals are named (replace it when a save is restored).</summary>
        public PlayerState State { get; set; }

        // ------------------------------------------------------------------------------------------- sentences

        /// <summary>The wish's sentence (section 2.3, the tutorial WISH-T01 too) with its parameters and plural.</summary>
        public string Wish(in WishRecord wish)
        {
            EnsureLanguage();
            var id = (WishTemplateId)wish.Template;
            if (!WishCatalog.TryGet(id, out var template))
                return string.Empty;
            bool named = template.ParamKind == WishParamKind.Animal;
            uint animal = named ? wish.Animal : 0u;
            string animalName = named ? SentenceAnimal(wish.Animal) : null;
            for (int i = 0; i < _wishCount; i++)
            {
                ref var e = ref _wishes[i];
                if (e.Template == wish.Template && e.Param == wish.Param && e.Extra == wish.ParamExtra && e.Target == wish.Target
                    && e.Animal == animal && string.Equals(e.AnimalName, animalName, StringComparison.Ordinal))
                    return e.Text;
            }
            string text = BuildWish(in wish, in template, animalName);
            int slot = Claim(ref _wishCount, ref _wishNext, WishSlots);
            _wishes[slot] = new WishEntry
            {
                Template = wish.Template, Param = wish.Param, Extra = wish.ParamExtra, Target = wish.Target, Animal = animal,
                AnimalName = animalName, Text = text,
            };
            return text;
        }

        /// <summary>The journal goal's sentence (section 5.2) with its target and plural.</summary>
        public string Goal(in JournalGoalRecord goal)
        {
            EnsureLanguage();
            for (int i = 0; i < _goalCount; i++)
            {
                ref var e = ref _goals[i];
                if (e.Goal == goal.Goal && e.Target == goal.Target)
                    return e.Text;
            }
            string key = WeeklyJournalCatalog.TextKey((JournalGoalId)goal.Goal);
            string text = key.Length == 0 ? string.Empty : Expand(_text.GetPlural(key, goal.Target), new Args { N = goal.Target });
            int slot = Claim(ref _goalCount, ref _goalNext, GoalSlots);
            _goals[slot] = new GoalEntry { Goal = goal.Goal, Target = goal.Target, Text = text };
            return text;
        }

        /// <summary>A « Pendant ton absence » line with its count and plural (« 3 souhaits t'attendent »); lines without
        /// a count come back as they are.</summary>
        public string ReturnLine(string key, int n) => Counted(key, n);

        /// <summary>Any counted text: the plural form for <paramref name="n"/>, {n} filled (« Encore 3 jours »).</summary>
        public string Counted(string key, long n)
        {
            EnsureLanguage();
            if (TryLine(OpCounted, key, n, 0, out string cached))
                return cached;
            return StoreLine(OpCounted, key, n, 0, Expand(_text.GetPlural(key, n), new Args { N = n }));
        }

        /// <summary>A wish or journal progress, « {n}/{total} ».</summary>
        public string Progress(int progress, int target)
        {
            EnsureLanguage();
            if (TryLine(OpPair, DailyVisitText.WishProgress, progress, target, out string cached))
                return cached;
            return StoreLine(OpPair, DailyVisitText.WishProgress, progress, target,
                Expand(_text.Get(DailyVisitText.WishProgress), new Args { N = progress, M = target }));
        }

        /// <summary>The journal's days left in real days, counting today: « Encore 3 jours », « Dernier jour » on the last.</summary>
        public string DaysLeft(int days) => days <= 1 ? Plain(DailyVisitText.JournalLastDay) : Counted(DailyVisitText.JournalDaysLeft, days);

        /// <summary>
        /// The calendar's next big box (doc 05 sheet 19): <paramref name="days"/> real days until it can be stamped
        /// (0 today, 1 tomorrow): « Grande case aujourd'hui », « Grande case demain », « Encore 3 jours ».
        /// </summary>
        public string BigBoxIn(int days)
        {
            if (days <= 0)
                return Plain(CalendarBigTodayKey);
            return days == 1 ? Plain(DailyVisitText.CalendarBigTomorrow) : Counted(DailyVisitText.CalendarBigIn, days);
        }

        /// <summary>The welcome card's day, « Jour 2 sur 3 » (<paramref name="index"/> 0 to 2).</summary>
        public string WelcomeDay(int index) => Counted(DailyVisitText.WelcomeDay, index + 1);

        /// <summary>A calendar box's number, « Jour 7 » (<paramref name="box"/> 0 to 27).</summary>
        public string CalendarDay(int box) => Counted(CalendarDayKey, box + 1);

        /// <summary>The opened gift's size in words (the band's color says it too).</summary>
        public string GiftSizeName(GiftSize size) =>
            Plain(size == GiftSize.Big ? DailyVisitText.GiftBig : size == GiftSize.Nice ? DailyVisitText.GiftNice : DailyVisitText.GiftSmall);

        // ------------------------------------------------------------------------------------------- names

        /// <summary>The animal's own name, else its species' name (Today rows). Empty without a state or for an unknown animal.</summary>
        public string AnimalName(uint instanceId)
        {
            var collection = State?.Collection;
            if (collection == null)
                return string.Empty;
            if (collection.CustomNames.TryGetValue(instanceId, out string custom) && !string.IsNullOrEmpty(custom))
                return custom;
            return collection.TryGet(instanceId, out var animal) ? SpeciesName(animal.Species) : string.Empty;
        }

        /// <summary>The species' display name (« Lapin »).</summary>
        public string SpeciesName(ContentId species) => NameOf(species, false);

        /// <summary>The biome's display name (« Prairie »).</summary>
        public string BiomeName(ContentId biome) => NameOf(biome, false);

        /// <summary>The name of a treat, a cycle decoration, a seed's plant or an ingredient; empty when unknown.</summary>
        public string ItemName(ContentId item) => NameOf(item, false);

        // ------------------------------------------------------------------------------------------- reward cells

        /// <summary>A reward cell's amount: « +15 » (Gems, bond), « 30 min » or the Light given (« +1.2K »), « ×2 »; empty
        /// for a single decoration (its icon says it).</summary>
        public string RewardLabel(in RewardCell cell)
        {
            EnsureLanguage();
            if (TryCell(OpLabel, in cell, out string cached))
                return cached;
            string text;
            switch (cell.Kind)
            {
                case RewardKind.None:
                    text = string.Empty;
                    break;
                case RewardKind.Gems:
                case RewardKind.Bond:
                    text = Signed('+', cell.Amount);
                    break;
                case RewardKind.Light:
                    if (!cell.Light.IsZero)
                    {
                        _builder.Clear();
                        _builder.Append('+');
                        AppendLight(cell.Light);
                        text = _builder.ToString();
                    }
                    else
                    {
                        text = Expand(_text.Get(DailyVisitText.RewardLightMinutes), new Args { N = Minutes(cell.LightMinutes) });
                    }
                    break;
                case RewardKind.Decoration:
                    text = cell.Amount <= 1 ? string.Empty : Signed('×', cell.Amount);
                    break;
                default:
                    text = Signed('×', cell.Amount);
                    break;
            }
            return StoreCell(OpLabel, in cell, text);
        }

        /// <summary>A reward in words (« 20 Gemmes », « 30 min de Lumière », « Fauteuil de mousse »): accessibility labels,
        /// the calendar's « Demain : … ».</summary>
        public string RewardName(in RewardCell cell)
        {
            EnsureLanguage();
            if (TryCell(OpName, in cell, out string cached))
                return cached;
            string text;
            switch (cell.Kind)
            {
                case RewardKind.Gems: text = Counted(DailyVisitText.RewardGems, cell.Amount); break;
                case RewardKind.Light:
                    if (cell.LightMinutes > 0.0 || cell.Light.IsZero)
                    {
                        text = Counted(LightMinutesWordsKey, Minutes(cell.LightMinutes));
                    }
                    else
                    {
                        _builder.Clear();
                        AppendLight(cell.Light);
                        string amount = _builder.ToString();
                        text = Expand(_text.Get(LightAmountKey), new Args { NText = amount });
                    }
                    break;
                case RewardKind.Ticket: text = Counted(DailyVisitText.RewardTickets, cell.Amount); break;
                case RewardKind.Treat: text = Counted(DailyVisitText.RewardTreats, cell.Amount); break;
                case RewardKind.Seed: text = Counted(DailyVisitText.RewardSeeds, cell.Amount); break;
                case RewardKind.DiscoveryEgg: text = Counted(DailyVisitText.RewardEgg, cell.Amount); break;
                case RewardKind.Sticker: text = Counted(DailyVisitText.RewardSticker, cell.Amount); break;
                case RewardKind.FlowerSugar: text = Counted(DailyVisitText.RewardFlowerSugar, cell.Amount); break;
                case RewardKind.Bond: text = Counted(DailyVisitText.RewardBond, cell.Amount); break;
                case RewardKind.Ingredient: text = Counted(DailyVisitText.RewardIngredients, cell.Amount); break;
                case RewardKind.Decoration:
                    string name = cell.Amount <= 1 ? ItemName(cell.Item) : string.Empty;
                    text = name.Length > 0 ? name : Counted(DailyVisitText.RewardDecoration, Math.Max(1L, cell.Amount));
                    break;
                default: text = string.Empty; break;
            }
            return StoreCell(OpName, in cell, text);
        }

        /// <summary>The calendar's next box in words, « Demain : 20 Gemmes » (section 4.4).</summary>
        public string Tomorrow(in RewardCell cell)
        {
            EnsureLanguage();
            if (TryCell(OpTomorrow, in cell, out string cached))
                return cached;
            string reward = RewardName(in cell);
            return StoreCell(OpTomorrow, in cell, Expand(_text.Get(DailyVisitText.CalendarTomorrow), new Args { Reward = reward }));
        }

        /// <summary>Forgets every cached text (the presenter calls it after a content or save swap).</summary>
        public void Clear()
        {
            Array.Clear(_wishes, 0, _wishes.Length);
            Array.Clear(_goals, 0, _goals.Length);
            Array.Clear(_lines, 0, _lines.Length);
            Array.Clear(_cells, 0, _cells.Length);
            _wishCount = _wishNext = _goalCount = _goalNext = _lineCount = _lineNext = _cellCount = _cellNext = 0;
        }

        // ------------------------------------------------------------------------------------------- building

        private string BuildWish(in WishRecord wish, in WishTemplate template, string animalName)
        {
            string key = WishCatalog.TextKey(template.Id);
            var args = new Args { N = wish.Target, Animal = animalName };
            switch (template.ParamKind)
            {
                case WishParamKind.Species:
                    args.Species = NameOf(wish.Param, false);
                    args.SpeciesThe = NameOf(wish.Param, true);
                    break;
                case WishParamKind.Biome:
                    args.Biome = NameOf(wish.Param, false);
                    args.BiomeThe = NameOf(wish.Param, true);
                    break;
                case WishParamKind.Upgrade:
                    args.Biome = NameOf(wish.Param, false);
                    args.BiomeThe = NameOf(wish.Param, true);
                    if (wish.ParamExtra < UpgradeWishKeys.Length)
                        key = UpgradeWishKeys[wish.ParamExtra];
                    else
                        args.Upgrade = string.Empty;
                    break;
            }
            return Expand(_text.GetPlural(key, wish.Target), in args);
        }

        /// <summary>The template with its placeholders filled; the template itself when it has none (no allocation).</summary>
        private string Expand(string template, in Args args)
        {
            if (string.IsNullOrEmpty(template) || template.IndexOf('{') < 0)
                return template ?? string.Empty;
            _builder.Clear();
            int i = 0;
            while (i < template.Length)
            {
                char c = template[i];
                if (c == '{')
                {
                    int close = template.IndexOf('}', i + 1);
                    if (close > i && AppendPlaceholder(template, i + 1, close - i - 1, in args))
                    {
                        i = close + 1;
                        continue;
                    }
                }
                _builder.Append(c);
                i++;
            }
            return _builder.ToString();
        }

        /// <summary>Appends the value of a known placeholder; false leaves an unknown one in the text as it is.</summary>
        private bool AppendPlaceholder(string template, int start, int length, in Args args)
        {
            if (Is(template, start, length, "n"))
            {
                if (args.NText != null)
                    _builder.Append(args.NText);
                else
                    AppendNumber(args.N);
            }
            else if (Is(template, start, length, "total"))
                AppendNumber(args.M);
            else if (Is(template, start, length, "species"))
                AppendValue(args.Species);
            else if (Is(template, start, length, "species_the"))
                AppendValue(args.SpeciesThe ?? args.Species);
            else if (Is(template, start, length, "biome"))
                AppendValue(args.Biome);
            else if (Is(template, start, length, "biome_the"))
                AppendValue(args.BiomeThe ?? args.Biome);
            else if (Is(template, start, length, "animal"))
                AppendValue(args.Animal);
            else if (Is(template, start, length, "upgrade"))
                AppendValue(args.Upgrade);
            else if (Is(template, start, length, "reward"))
                AppendValue(args.Reward);
            else
                return false;
            return true;
        }

        private static bool Is(string template, int start, int length, string name) =>
            length == name.Length && string.CompareOrdinal(template, start, name, 0, length) == 0;

        /// <summary>A name into the sentence, with the French contractions and elision; capitalized at the sentence start.</summary>
        private void AppendValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return;
            int length = _builder.Length;
            if (_french)
            {
                if (EndsWithWord("de"))
                {
                    char d = _builder[length - 3];
                    if (StartsWithWord(value, "le"))
                    {
                        _builder.Length = length - 3;
                        _builder.Append(d).Append("u ").Append(value, 3, value.Length - 3);
                        return;
                    }
                    if (StartsWithWord(value, "les"))
                    {
                        _builder.Length = length - 3;
                        _builder.Append(d).Append("es ").Append(value, 4, value.Length - 4);
                        return;
                    }
                    if (Vowels.IndexOf(value[0]) >= 0)
                    {
                        _builder.Length = length - 2;
                        _builder.Append('\'').Append(value);
                        return;
                    }
                }
                else if (EndsWithWord("à"))
                {
                    if (StartsWithWord(value, "le"))
                    {
                        _builder.Length = length - 2;
                        _builder.Append("au ").Append(value, 3, value.Length - 3);
                        return;
                    }
                    if (StartsWithWord(value, "les"))
                    {
                        _builder.Length = length - 2;
                        _builder.Append("aux ").Append(value, 4, value.Length - 4);
                        return;
                    }
                }
            }
            if (length == 0 && char.IsLower(value[0]))
            {
                _builder.Append(char.ToUpperInvariant(value[0])).Append(value, 1, value.Length - 1);
                return;
            }
            _builder.Append(value);
        }

        /// <summary>The text so far ends with the word and a space (« … œufs de »), the word standing on its own.</summary>
        private bool EndsWithWord(string word)
        {
            int length = _builder.Length;
            int start = length - word.Length - 1;
            if (start < 0 || _builder[length - 1] != ' ')
                return false;
            for (int i = 0; i < word.Length; i++)
            {
                char c = _builder[start + i];
                if (char.ToLowerInvariant(c) != word[i])
                    return false;
            }
            if (start == 0)
                return true;
            char before = _builder[start - 1];
            return before == ' ' || before == ' ' || before == '\'';
        }

        private static bool StartsWithWord(string value, string word) =>
            value.Length > word.Length + 1 && value[word.Length] == ' ' && string.CompareOrdinal(value, 0, word, 0, word.Length) == 0;

        /// <summary>Digits with the language's thousands separator: « 5,000 », « 5 000 » (narrow no-break space).</summary>
        private void AppendNumber(long value)
        {
            if (value < 0)
            {
                _builder.Append('-');
                value = value == long.MinValue ? long.MaxValue : -value;
            }
            char group = _french ? NarrowNoBreakSpace : ',';
            int n = 0;
            int digits = 0;
            do
            {
                if (digits > 0 && digits % 3 == 0)
                    _digits[n++] = group;
                _digits[n++] = (char)('0' + (int)(value % 10));
                value /= 10;
                digits++;
            }
            while (value > 0 && n < _digits.Length - 1);
            for (int i = n - 1; i >= 0; i--)
                _builder.Append(_digits[i]);
        }

        /// <summary>Light as the HUD writes it (« 1.2K »), same digits as the Light chip.</summary>
        private void AppendLight(BigValue light)
        {
            int length = BigValueFormatter.Format(light, _digits, 0, NumberFormatSettings.Invariant);
            _builder.Append(_digits, 0, length);
        }

        private string Signed(char sign, long amount)
        {
            _builder.Clear();
            _builder.Append(sign);
            AppendNumber(amount);
            return _builder.ToString();
        }

        private static long Minutes(double minutes) => minutes > 0.0 ? (long)Math.Round(minutes) : 0L;

        private string Plain(string key)
        {
            EnsureLanguage();
            if (TryLine(OpPlain, key, 0, 0, out string cached))
                return cached;
            return StoreLine(OpPlain, key, 0, 0, Expand(_text.Get(key), default));
        }

        // ------------------------------------------------------------------------------------------- names

        /// <summary>
        /// Keys of the names: species and biomes of the registry, the 13 biomes of the progression (when the registry has
        /// no biome table), the treats, the cycle decorations (and their dusk tint), the welcome seeds' plant and the
        /// flower sugar. « X.name » and its article form « X.name_the ».
        /// </summary>
        private void BuildNames()
        {
            var species = _content.Species;
            for (int i = 0; i < species.Count; i++)
                AddName(species[i].Id, species[i].DisplayKey);
            var biomes = _content.Biomes;
            for (int i = 0; i < biomes.Count; i++)
                AddName(biomes[i].Id, biomes[i].DisplayKey);
            var config = ProgressionConfig.Default;
            foreach (var biome in config.Biomes)
                AddName(biome.Id, biome.SourceId + ".name");
            foreach (var treat in config.Treats)
                AddName(treat.Id, treat.SourceId + ".name");
            AddSource("treat.seed_cookie");
            AddSource("treat.treat_basket");
            AddSource("treat.golden_treat");
            AddSource("plant.sweet_clover");
            AddSource("ingredient.flower_sugar");
            for (int cycle = 1; cycle < DailyRewardTables.FirstCycleWithoutDecoration; cycle++)
            {
                string source = DailyRewardTables.CycleDecorationSource(cycle);
                if (source != null)
                    AddSource(source);
            }
        }

        private void AddSource(string source) => AddName(ContentId.FromSource(source), source + ".name");

        private void AddName(ContentId id, string displayKey)
        {
            if (id == ContentId.None || string.IsNullOrEmpty(displayKey) || _names.ContainsKey(id))
                return;
            _names.Add(id, new NameKeys(displayKey, displayKey + "_the"));
        }

        /// <summary>The name of a content id; with the article when asked and the table has that form, else the bare name.</summary>
        private string NameOf(ContentId id, bool withArticle)
        {
            if (!_names.TryGetValue(id, out var keys))
                return string.Empty;
            if (withArticle && _text.Has(keys.The))
                return _text.Get(keys.The);
            return _text.Has(keys.Name) ? _text.Get(keys.Name) : string.Empty;
        }

        /// <summary>{animal} in a sentence: its own name, else its species with the article (« Donne une friandise au Lapin »).</summary>
        private string SentenceAnimal(uint instanceId)
        {
            var collection = State?.Collection;
            if (collection == null)
                return string.Empty;
            if (collection.CustomNames.TryGetValue(instanceId, out string custom) && !string.IsNullOrEmpty(custom))
                return custom;
            return collection.TryGet(instanceId, out var animal) ? NameOf(animal.Species, true) : string.Empty;
        }

        // ------------------------------------------------------------------------------------------- caches

        private void EnsureLanguage()
        {
            string language = _text.Language;
            if (string.Equals(language, _language, StringComparison.Ordinal))
                return;
            _language = language;
            _french = language == "fr";
            Clear();
        }

        private static int Claim(ref int count, ref int next, int capacity)
        {
            int slot;
            if (count < capacity)
            {
                slot = count++;
            }
            else
            {
                slot = next;
                next = (next + 1) % capacity;
            }
            return slot;
        }

        private bool TryLine(byte op, string key, long a, long b, out string text)
        {
            for (int i = 0; i < _lineCount; i++)
            {
                ref var e = ref _lines[i];
                if (e.Op == op && e.A == a && e.B == b && (ReferenceEquals(e.Key, key) || string.Equals(e.Key, key, StringComparison.Ordinal)))
                {
                    text = e.Text;
                    return true;
                }
            }
            text = null;
            return false;
        }

        private string StoreLine(byte op, string key, long a, long b, string text)
        {
            int slot = Claim(ref _lineCount, ref _lineNext, LineSlots);
            _lines[slot] = new LineEntry { Op = op, Key = key, A = a, B = b, Text = text };
            return text;
        }

        private bool TryCell(byte op, in RewardCell cell, out string text)
        {
            for (int i = 0; i < _cellCount; i++)
            {
                ref var e = ref _cells[i];
                if (e.Op == op && e.Kind == cell.Kind && e.Amount == cell.Amount && e.Minutes.Equals(cell.LightMinutes) && e.Light == cell.Light
                    && e.Item == cell.Item)
                {
                    text = e.Text;
                    return true;
                }
            }
            text = null;
            return false;
        }

        private string StoreCell(byte op, in RewardCell cell, string text)
        {
            int slot = Claim(ref _cellCount, ref _cellNext, CellSlots);
            _cells[slot] = new CellEntry
            {
                Op = op, Kind = cell.Kind, Amount = cell.Amount, Minutes = cell.LightMinutes, Light = cell.Light, Item = cell.Item, Text = text,
            };
            return text;
        }

        private readonly struct NameKeys
        {
            public readonly string Name;
            public readonly string The;

            public NameKeys(string name, string the)
            {
                Name = name;
                The = the;
            }
        }

        private struct Args
        {
            public long N;

            /// <summary>{total}.</summary>
            public long M;

            /// <summary>Replaces {n} when the number is already written (a Light amount).</summary>
            public string NText;

            public string Species;
            public string SpeciesThe;
            public string Biome;
            public string BiomeThe;
            public string Animal;
            public string Upgrade;
            public string Reward;
        }

        private struct WishEntry
        {
            public ushort Template;
            public ContentId Param;
            public byte Extra;
            public int Target;
            public uint Animal;
            public string AnimalName;
            public string Text;
        }

        private struct GoalEntry
        {
            public ushort Goal;
            public int Target;
            public string Text;
        }

        private struct LineEntry
        {
            public byte Op;
            public string Key;
            public long A;
            public long B;
            public string Text;
        }

        private struct CellEntry
        {
            public byte Op;
            public RewardKind Kind;
            public long Amount;
            public double Minutes;
            public BigValue Light;
            public ContentId Item;
            public string Text;
        }
    }
}
