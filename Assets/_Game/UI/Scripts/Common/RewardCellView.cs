using System;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>
    /// T36: one reward as the rendez-vous screens show it (doc 06 screen 028 « reward cells revealed one by one », screens
    /// 031 and 032, the return gift of screen 004): a soft disc, the icon of its kind (or of its item for a treat, a seed
    /// or a decoration; a Discovery egg is its tinted shell and spots), and its amount. Light shows the value really given,
    /// formatted like every counter, else « {n} min » of production. Bound when the model changes, never per frame; the
    /// label is touched only when its characters change. The reveal is the doc 05 Pop (0 to 1.12 in 160 ms, 0.97, 1.00:
    /// 360 ms); with Reduce Motion it only fades.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RewardCellView : MonoBehaviour
    {
        private const float FadeSeconds = 0.2f;

        [SerializeField] private RectTransform _root;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Image _disc;
        [SerializeField] private Image _icon;
        [SerializeField] private Image _spots;
        [SerializeField] private TMP_Text _amount;

        private UiLabel _label;
        private RewardKind _kind = (RewardKind)255;
        private ContentId _item;
        private float _time = -1f;
        private float _delay;
        private bool _revealed = true;

        public bool ReduceMotion { get; set; }

        /// <summary>The pop or fade is running (or waiting for its delay).</summary>
        public bool Revealing => _time >= 0f;

        /// <summary>Shown, at full size and full opacity.</summary>
        public bool FullyVisible => gameObject.activeInHierarchy && _revealed && _time < 0f;

        public RewardKind Kind => _kind;
        public string AmountText => _amount != null ? _amount.text : string.Empty;
        public RectTransform Rect => _root != null ? _root : (RectTransform)transform;

        /// <summary>Icon and amount of <paramref name="cell"/>; sprites change only with the kind or the item.</summary>
        public void Bind(in RewardCell cell, RewardIconSet icons, RewardTexts texts)
        {
            if (_label == null)
                _label = new UiLabel(_amount, 64);
            if (cell.Kind != _kind || cell.Item != _item)
            {
                _kind = cell.Kind;
                _item = cell.Item;
                if (icons != null)
                {
                    bool egg = cell.Kind == RewardKind.DiscoveryEgg;
                    _icon.sprite = egg ? icons.eggShell : icons.Icon(cell.Kind, cell.Item);
                    _icon.color = egg ? icons.eggShellColor : Color.white;
                    if (_spots != null)
                    {
                        _spots.sprite = icons.eggSpots;
                        _spots.color = icons.eggSpotsColor;
                        if (_spots.gameObject.activeSelf != egg)
                            _spots.gameObject.SetActive(egg);
                    }
                    if (_disc != null)
                        _disc.color = icons.Disc(cell.Kind);
                }
            }
            _label.Commit(UiChars.Amount(_label.Buffer, 0, cell, texts));
        }

        public void SetShown(bool shown)
        {
            if (gameObject.activeSelf != shown)
                gameObject.SetActive(shown);
        }

        /// <summary>At full size at once (a screen opened after the reveal).</summary>
        public void ShowNow()
        {
            _time = -1f;
            _revealed = true;
            Apply(1f, 1f);
        }

        /// <summary>Hidden until <see cref="Reveal"/> (the gift's box is still closed).</summary>
        public void HideNow()
        {
            _time = -1f;
            _revealed = false;
            Apply(0f, 0f);
        }

        /// <summary>Pops in after <paramref name="delay"/> seconds (fades with Reduce Motion).</summary>
        public void Reveal(float delay)
        {
            _revealed = false;
            _delay = Mathf.Max(0f, delay);
            _time = 0f;
            Apply(0f, ReduceMotion ? 1f : 0f);
        }

        /// <summary>A short pop of a cell already shown (a claim taken).</summary>
        public void Bump()
        {
            _revealed = false;
            _delay = 0f;
            _time = 0.12f;
        }

        public void Tick(float deltaTime)
        {
            if (_time < 0f)
                return;
            _time += deltaTime;
            float t = _time - _delay;
            if (t < 0f)
                return;
            if (ReduceMotion)
            {
                float a = Mathf.Clamp01(t / FadeSeconds);
                Apply(a, 1f);
                if (a >= 1f)
                    Finish();
                return;
            }
            Apply(Mathf.Clamp01(t / 0.08f), UiCurves.Pop(t));
            if (t >= UiCurves.PopSeconds)
                Finish();
        }

        private void Finish()
        {
            _time = -1f;
            _revealed = true;
            Apply(1f, 1f);
        }

        private void Apply(float alpha, float scale)
        {
            if (_group != null)
                _group.alpha = alpha;
            var rect = Rect;
            rect.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>Prefab builder hook (Editor DailyVisitUiPipeline).</summary>
        public void SetReferences(RectTransform root, CanvasGroup group, Image disc, Image icon, Image spots, TMP_Text amount)
        {
            _root = root;
            _group = group;
            _disc = disc;
            _icon = icon;
            _spots = spots;
            _amount = amount;
        }
    }

    /// <summary>
    /// The sprites of the reward kinds (Art/UI/Icons, Atlas_CommonHUD) and the items that have their own (Trèfle croquant).
    /// Serialized on each screen that shows cells; the item ids are hashed once, on first use.
    /// </summary>
    [Serializable]
    public sealed class RewardIconSet
    {
        [Serializable]
        public struct ItemIcon
        {
            public string sourceId;
            public Sprite sprite;
        }

        public Sprite gems;
        public Sprite light;
        public Sprite ticket;
        public Sprite treat;
        public Sprite seed;
        public Sprite eggShell;
        public Sprite eggSpots;
        public Sprite decoration;
        public Sprite sticker;
        public Sprite flowerSugar;
        public Sprite bond;
        public Color eggShellColor = new Color(0.965f, 0.945f, 0.906f, 1f);
        public Color eggSpotsColor = new Color(0.914f, 0.769f, 0.416f, 1f);

        [Tooltip("The soft disc behind each kind, by RewardKind value.")]
        public Color[] discTints = Array.Empty<Color>();

        public ItemIcon[] items = Array.Empty<ItemIcon>();

        [NonSerialized] private ContentId[] _ids;

        public Sprite Icon(RewardKind kind, ContentId item)
        {
            if (item.IsValid)
            {
                var own = ItemSprite(item);
                if (own != null)
                    return own;
            }
            switch (kind)
            {
                case RewardKind.Gems: return gems;
                case RewardKind.Light: return light;
                case RewardKind.Ticket: return ticket;
                case RewardKind.Treat: return treat;
                case RewardKind.Seed: return seed;
                case RewardKind.DiscoveryEgg: return eggShell;
                case RewardKind.Decoration: return decoration;
                case RewardKind.Sticker: return sticker;
                case RewardKind.FlowerSugar: return flowerSugar != null ? flowerSugar : treat;
                case RewardKind.Bond: return bond;
                // T40: an ingredient shows its item's own icon (items); one without falls back to the treat's.
                case RewardKind.Ingredient: return treat;
                default: return null;
            }
        }

        public Color Disc(RewardKind kind) => (int)kind < discTints.Length ? discTints[(int)kind] : new Color(0.953f, 0.922f, 0.867f, 1f);

        private Sprite ItemSprite(ContentId id)
        {
            if (_ids == null || _ids.Length != items.Length)
            {
                _ids = new ContentId[items.Length];
                for (int i = 0; i < items.Length; i++)
                    _ids[i] = ContentId.FromSource(items[i].sourceId);
            }
            for (int i = 0; i < _ids.Length; i++)
            {
                if (_ids[i] == id)
                    return items[i].sprite;
            }
            return null;
        }
    }

    /// <summary>
    /// The two forms of a counted text (doc 01 Contenu section 0: singular and plural), read once with
    /// <see cref="ILocalization.GetPlural"/> and picked by <see cref="PluralForms.IsOne"/>: nothing allocated afterwards.
    /// </summary>
    public struct UiCounted
    {
        public string One;
        public string Other;
        public string Language;

        public static UiCounted From(ILocalization text, string key) => text == null
            ? new UiCounted { One = key, Other = key, Language = "en" }
            : new UiCounted { One = text.GetPlural(key, 1), Other = text.GetPlural(key, 2), Language = text.Language };

        public string For(long n) => PluralForms.IsOne(Language, n) ? One : Other;
    }

    /// <summary>
    /// The reward texts (DailyVisitText reward keys): the words of each kind with their plural (« 1 billet », « 2 billets »,
    /// « 30 min de Lumière ») for sentences such as « Demain : {reward} », and the short « {n} min » beside the Light icon.
    /// Read once when a screen binds its texts.
    /// </summary>
    public sealed class RewardTexts
    {
        private readonly UiCounted[] _words = new UiCounted[(int)RewardKind.Ingredient + 1];
        private UiCounted _lightWords;

        /// <summary>The cell's amount beside the Light icon, « {n} min ».</summary>
        public string LightMinutes { get; private set; }

        public void Bind(ILocalization text)
        {
            if (text == null)
                return;
            LightMinutes = text.Get(DailyVisitText.RewardLightMinutes);
            _lightWords = UiCounted.From(text, DailyVisitTextFormatter.LightMinutesWordsKey);
            _words[(int)RewardKind.Gems] = UiCounted.From(text, DailyVisitText.RewardGems);
            _words[(int)RewardKind.Ticket] = UiCounted.From(text, DailyVisitText.RewardTickets);
            _words[(int)RewardKind.Treat] = UiCounted.From(text, DailyVisitText.RewardTreats);
            _words[(int)RewardKind.Seed] = UiCounted.From(text, DailyVisitText.RewardSeeds);
            _words[(int)RewardKind.DiscoveryEgg] = UiCounted.From(text, DailyVisitText.RewardEgg);
            _words[(int)RewardKind.Decoration] = UiCounted.From(text, DailyVisitText.RewardDecoration);
            _words[(int)RewardKind.Sticker] = UiCounted.From(text, DailyVisitText.RewardSticker);
            _words[(int)RewardKind.FlowerSugar] = UiCounted.From(text, DailyVisitText.RewardFlowerSugar);
            _words[(int)RewardKind.Bond] = UiCounted.From(text, DailyVisitText.RewardBond);
            _words[(int)RewardKind.Ingredient] = UiCounted.From(text, DailyVisitText.RewardIngredients);
        }

        /// <summary>The words of <paramref name="n"/> of a kind (a template with {n}), or null before <see cref="Bind"/>.</summary>
        public string Words(RewardKind kind, long n)
        {
            if (kind == RewardKind.Light)
                return _lightWords.For(n);
            return (int)kind < _words.Length ? _words[(int)kind].For(n) : null;
        }
    }

    /// <summary>
    /// A TMP label filled from a reused char buffer: writers fill <see cref="Buffer"/>, <see cref="Commit"/> touches the
    /// label only when the characters differ from what it shows. Strings are compared before being assigned. No
    /// allocation in players (in the Editor TMP mirrors char arrays into a string for its inspector).
    /// </summary>
    public sealed class UiLabel
    {
        private readonly TMP_Text _label;
        private readonly char[] _buffer;
        private readonly char[] _shown;
        private int _shownLength = -1;
        private string _shownString;

        public UiLabel(TMP_Text label, int capacity = 128)
        {
            _label = label;
            _buffer = new char[Math.Max(capacity, BigValueFormatter.MaxLength + 8)];
            _shown = new char[_buffer.Length];
        }

        public TMP_Text Label => _label;
        public char[] Buffer => _buffer;
        public string Text => _label != null ? _label.text : string.Empty;

        public void SetString(string value)
        {
            value ??= string.Empty;
            if (_shownLength < 0 && _shownString != null && string.Equals(value, _shownString, StringComparison.Ordinal))
                return;
            _shownString = value;
            _shownLength = -1;
            if (_label != null)
                _label.text = value;
        }

        public void Commit(int length)
        {
            length = Mathf.Clamp(length, 0, _buffer.Length);
            if (_shownLength == length && Same(length))
                return;
            Array.Copy(_buffer, _shown, length);
            _shownLength = length;
            _shownString = null;
            if (_label != null)
                _label.SetText(_buffer, 0, length);
        }

        public void SetNumber(long value, char prefix = '\0')
        {
            int i = 0;
            if (prefix != '\0')
                _buffer[i++] = prefix;
            Commit(UiChars.Int(_buffer, i, value));
        }

        public void SetRatio(long value, long total)
        {
            int i = UiChars.Int(_buffer, 0, value);
            i = UiChars.Char(_buffer, i, '/');
            Commit(UiChars.Int(_buffer, i, total));
        }

        /// <summary>A sentence with {n} and {total} (and {reward} when <paramref name="texts"/> is given).</summary>
        public void SetTemplate(string template, long n, long total = 0) =>
            Commit(UiChars.Template(_buffer, 0, template, n, total, default, null));

        public void SetTemplate(string template, long n, in RewardCell reward, RewardTexts texts) =>
            Commit(UiChars.Template(_buffer, 0, template, n, 0, reward, texts));

        private bool Same(int length)
        {
            for (int i = 0; i < length; i++)
            {
                if (_buffer[i] != _shown[i])
                    return false;
            }
            return true;
        }
    }

    /// <summary>Allocation-free writers into char buffers (numbers, strings, the doc's {n} / {total} / {reward} sentences).</summary>
    public static class UiChars
    {
        public const char Times = '×';

        public static int Char(char[] buffer, int i, char c)
        {
            if (i < buffer.Length)
                buffer[i++] = c;
            return i;
        }

        public static int Int(char[] buffer, int i, long value)
        {
            if (value < 0)
            {
                i = Char(buffer, i, '-');
                value = -value;
            }
            int digits = 1;
            for (long v = value; v >= 10; v /= 10)
                digits++;
            if (i + digits > buffer.Length)
                return i;
            for (int d = i + digits - 1; d >= i; d--)
            {
                buffer[d] = (char)('0' + value % 10);
                value /= 10;
            }
            return i + digits;
        }

        public static int Str(char[] buffer, int i, string value)
        {
            if (value == null)
                return i;
            for (int c = 0; c < value.Length && i < buffer.Length; c++)
                buffer[i++] = value[c];
            return i;
        }

        public static int Big(char[] buffer, int i, BigValue value)
        {
            if (buffer.Length - i < BigValueFormatter.MaxLength)
                return i;
            return i + BigValueFormatter.Format(value, buffer, i, NumberFormatSettings.Invariant);
        }

        /// <summary>
        /// Writes <paramref name="template"/>, replacing {n} (and {days}) by <paramref name="n"/>, {total} by
        /// <paramref name="total"/> and {reward} by the reward's sentence. Other braces are copied as they are.
        /// </summary>
        public static int Template(char[] buffer, int i, string template, long n, long total, in RewardCell reward, RewardTexts texts)
        {
            if (template == null)
                return i;
            int length = template.Length;
            for (int c = 0; c < length && i < buffer.Length; c++)
            {
                char ch = template[c];
                if (ch == '{')
                {
                    int end = template.IndexOf('}', c + 1);
                    if (end > c && end - c <= 12)
                    {
                        if (Is(template, c + 1, end, "n") || Is(template, c + 1, end, "days"))
                        {
                            i = Int(buffer, i, n);
                            c = end;
                            continue;
                        }
                        if (Is(template, c + 1, end, "total"))
                        {
                            i = Int(buffer, i, total);
                            c = end;
                            continue;
                        }
                        if (Is(template, c + 1, end, "reward"))
                        {
                            i = Reward(buffer, i, reward, texts);
                            c = end;
                            continue;
                        }
                    }
                }
                buffer[i++] = ch;
            }
            return i;
        }

        /// <summary>
        /// Writes <paramref name="template"/> with {reward} replaced by <paramref name="rewardTemplate"/> and its {n}
        /// (« Demain : Décoration du cycle 2 »).
        /// </summary>
        public static int TemplateWithReward(char[] buffer, int i, string template, string rewardTemplate, long rewardN)
        {
            if (template == null)
                return i;
            for (int c = 0; c < template.Length && i < buffer.Length; c++)
            {
                char ch = template[c];
                if (ch == '{')
                {
                    int end = template.IndexOf('}', c + 1);
                    if (end > c && Is(template, c + 1, end, "reward"))
                    {
                        i = Template(buffer, i, rewardTemplate, rewardN, 0, default, null);
                        c = end;
                        continue;
                    }
                }
                buffer[i++] = ch;
            }
            return i;
        }

        /// <summary>The reward in words with its plural: « 20 Gemmes », « 30 min de Lumière », « 1 billet »; the granted Light when known.</summary>
        public static int Reward(char[] buffer, int i, in RewardCell cell, RewardTexts texts)
        {
            if (cell.IsEmpty)
                return i;
            if (cell.Kind == RewardKind.Light && !cell.Light.IsZero)
                return Big(buffer, i, cell.Light);
            long n = cell.Kind == RewardKind.Light ? Minutes(cell) : Math.Max(1L, cell.Amount);
            string template = texts?.Words(cell.Kind, n);
            if (string.IsNullOrEmpty(template))
                return Int(buffer, i, n);
            return Template(buffer, i, template, n, 0, default, null);
        }

        /// <summary>
        /// A cell's short amount, as the wish card writes it (DailyVisitTextFormatter.RewardLabel): « +3 » for Gems and
        /// bond, « +1.2K » for the Light given or « 30 min » before, « ×2 » for objects, nothing for a single decoration.
        /// </summary>
        public static int Amount(char[] buffer, int i, in RewardCell cell, RewardTexts texts)
        {
            switch (cell.Kind)
            {
                case RewardKind.None:
                    return i;
                case RewardKind.Gems:
                case RewardKind.Bond:
                    i = Char(buffer, i, '+');
                    return Int(buffer, i, cell.Amount);
                case RewardKind.Light:
                    if (!cell.Light.IsZero)
                        return Big(buffer, Char(buffer, i, '+'), cell.Light);
                    string template = texts?.LightMinutes;
                    if (string.IsNullOrEmpty(template))
                        return Int(buffer, i, Minutes(cell));
                    return Template(buffer, i, template, Minutes(cell), 0, default, null);
                case RewardKind.Decoration:
                    if (cell.Amount <= 1)
                        return i;
                    i = Char(buffer, i, Times);
                    return Int(buffer, i, cell.Amount);
                default:
                    i = Char(buffer, i, Times);
                    return Int(buffer, i, cell.Amount);
            }
        }

        public static long Minutes(in RewardCell cell) => (long)Math.Round(cell.LightMinutes);

        private static bool Is(string s, int start, int end, string token)
        {
            if (end - start != token.Length)
                return false;
            for (int k = 0; k < token.Length; k++)
            {
                if (s[start + k] != token[k])
                    return false;
            }
            return true;
        }
    }

    /// <summary>The doc 05 reference curves (section 0.1) used by the rendez-vous screens, in seconds.</summary>
    public static class UiCurves
    {
        public const float PopSeconds = 0.36f;
        public const float PressSeconds = 0.25f;
        public const float StampSeconds = 0.30f;

        /// <summary>Pop: 0 to 1.12 in 160 ms (strong ease out), 0.97 at 250 ms, 1.00 at 360 ms.</summary>
        public static float Pop(float t)
        {
            if (t <= 0f)
                return 0f;
            if (t < 0.16f)
            {
                float k = 1f - t / 0.16f;
                return 1.12f * (1f - k * k * k);
            }
            if (t < 0.25f)
                return Mathf.Lerp(1.12f, 0.97f, Smooth((t - 0.16f) / 0.09f));
            if (t < PopSeconds)
                return Mathf.Lerp(0.97f, 1f, Smooth((t - 0.25f) / 0.11f));
            return 1f;
        }

        /// <summary>Press: 1.00 to 0.94 in 50 ms, 1.04 at 140 ms, 1.00 at 250 ms.</summary>
        public static float Press(float t)
        {
            if (t <= 0f || t >= PressSeconds)
                return 1f;
            if (t < 0.05f)
                return Mathf.Lerp(1f, 0.94f, Smooth(t / 0.05f));
            if (t < 0.14f)
                return Mathf.Lerp(0.94f, 1.04f, Smooth((t - 0.05f) / 0.09f));
            return Mathf.Lerp(1.04f, 1f, Smooth((t - 0.14f) / 0.11f));
        }

        /// <summary>The paw stamp (doc 05 sheet 19): 1.4 down to 0.95 in 140 ms (impact), 1.00 in 160 ms.</summary>
        public static float Stamp(float t)
        {
            if (t <= 0f)
                return 1.4f;
            if (t < 0.14f)
            {
                float k = t / 0.14f;
                return Mathf.Lerp(1.4f, 0.95f, k * k);
            }
            if (t < StampSeconds)
                return Mathf.Lerp(0.95f, 1f, Smooth((t - 0.14f) / 0.16f));
            return 1f;
        }

        public static float Smooth(float k)
        {
            k = Mathf.Clamp01(k);
            return k * k * (3f - 2f * k);
        }

        public static float EaseOut(float k)
        {
            k = Mathf.Clamp01(k);
            float u = 1f - k;
            return 1f - u * u * u;
        }
    }
}
