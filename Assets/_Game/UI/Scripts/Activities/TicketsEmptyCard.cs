using PocketWilds.Core;
using PocketWilds.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>
    /// The « no ticket » card (doc « Mini-jeux & activités » section 2: « l'écran montre d'abord le temps avant le prochain
    /// billet gratuit, puis les autres options ; jamais de fenêtre bloquante »): the ticket, « Billets en route » and
    /// at once the real time to the next free ticket; then « Jouer en entraînement » (free, said under it),
    /// then the pack « 3 billets » with its price chip (Gems, 30), dimmed softly with a calm word when today's packs are used
    /// or Gems are short (never red, never a timer). The rewarded ad (+2 tickets) comes with T55: nothing is shown for it yet. A touch beside, Back
    /// or the round close dismisses it. Closed: canvas disabled, content inactive. Prewarmed; nothing is instantiated after.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TicketsEmptyCard : MonoBehaviour, ITicketsEmptyView, IMotionSettings
    {
        private const float DimmedAlpha = 0.5f;

        [SerializeField] private UiColorTokens _tokens;
        [SerializeField] private Canvas _canvas;
        [SerializeField] private GameObject _content;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private RectTransform _card;
        [SerializeField] private Button _outside;
        [SerializeField] private Button _close;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _time;
        [SerializeField] private Button _practice;
        [SerializeField] private TMP_Text _practiceLabel;
        [SerializeField] private TMP_Text _practiceLine;
        [SerializeField] private Button _pack;
        [SerializeField] private CanvasGroup _packGroup;
        [SerializeField] private TMP_Text _packLabel;
        [SerializeField] private TMP_Text _packPrice;
        [SerializeField] private TMP_Text _packNote;

        private readonly WindowMotion _motion = new WindowMotion(0.16f);
        private readonly TicketTimeText _ticketTime = new TicketTimeText();
        private ITicketsEmptyActions _actions;
        private ILocalization _text;
        private UiLabel _titleText;
        private UiLabel _timeText;
        private UiLabel _practiceText;
        private UiLabel _practiceLineText;
        private UiLabel _packText;
        private UiLabel _priceText;
        private UiLabel _noteText;
        private UiCounted _packWords;
        private string _packsUsed;
        private string _needGems;
        private int _packKey = int.MinValue;
        private bool _packEnabled;
        private bool _reduceMotion;
        private bool _initialized;

        /// <summary>A « no ticket » card is open (the hub leaves Back to it).</summary>
        public static bool AnyOpen { get; private set; }

        public bool IsOpen => _motion.IsOpen && !_motion.Closing;
        public bool Visible => _motion.IsOpen;

        public bool ReduceMotion
        {
            get => _reduceMotion;
            set
            {
                _reduceMotion = value;
                _motion.ReduceMotion = value;
            }
        }

        // ---------------- read by tests and captures ----------------

        public Button OutsideButton => _outside;
        public Button CloseButton => _close;
        public Button PracticeButton => _practice;
        public Button PackButton => _pack;
        public string TitleText => _title.text;
        public string TimeText => _time.text;
        public string PracticeText => _practiceLabel.text;
        public string PackText => _packLabel.text;
        public string PriceText => _packPrice.text;
        public string PackNote => _packNote.gameObject.activeSelf ? _packNote.text : string.Empty;
        public bool PackEnabled => _packEnabled;
        public float PackAlpha => _packGroup != null ? _packGroup.alpha : 1f;

        /// <summary>The time line sits above both options (the time comes first).</summary>
        public bool TimeAboveOptions =>
            _time.rectTransform.position.y > _practice.transform.position.y && _practice.transform.position.y > _pack.transform.position.y;

        // ---------------- ITicketsEmptyView ----------------

        public void Open(in TicketsModel tickets, ILocalization text, ITicketsEmptyActions actions)
        {
            _actions = actions;
            BindTexts(text);
            Refresh(tickets);
            if (!_motion.IsOpen || _motion.Closing)
                _motion.Show();
            AnyOpen = true;
        }

        public void Prewarm(ILocalization text)
        {
            if (_motion.IsOpen)
                return;
            BindTexts(text);
            Refresh(new TicketsModel { Count = 0, Max = 5, MinutesToNext = 72, PackPrice = 30, PackSize = 3, PacksLeftToday = 2, Gems = 100 });
            _motion.Show();
            Canvas.ForceUpdateCanvases();
            _motion.HideNow();
            AnyOpen = false;
        }

        public void Refresh(in TicketsModel tickets)
        {
            Initialize();
            _ticketTime.Write(_timeText, tickets.Count >= tickets.Max ? 0 : Mathf.Max(1, tickets.MinutesToNext));
            bool packsLeft = tickets.PacksLeftToday > 0;
            bool gems = tickets.Gems >= tickets.PackPrice;
            bool enabled = packsLeft && gems;
            int key = tickets.PackSize * 100000 + tickets.PackPrice * 10 + (packsLeft ? 1 : 0) + (gems ? 2 : 0) + (_text != null ? 4 : 0);
            if (key == _packKey)
                return;
            _packKey = key;
            _packEnabled = enabled;
            if (_text != null)
            {
                string template = _packWords.For(tickets.PackSize);
                _packText.Commit(ActivityChars.Fill(_packText.Buffer, 0, template, tickets.PackSize));
            }
            _priceText.SetNumber(tickets.PackPrice);
            _pack.interactable = enabled;
            if (_packGroup != null)
                _packGroup.alpha = enabled ? 1f : DimmedAlpha;
            bool note = !enabled;
            if (_packNote.gameObject.activeSelf != note)
                _packNote.gameObject.SetActive(note);
            if (note)
                _noteText.SetString(!packsLeft ? _packsUsed : _needGems);
        }

        public void Close()
        {
            _motion.Hide();
            AnyOpen = false;
        }

        /// <summary>Android back, or Escape: the same as a touch beside the card.</summary>
        public void PressBack()
        {
            if (IsOpen)
                _actions?.TicketsEmptyClosed();
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!_motion.IsOpen)
                return;
            if (_motion.BackPressed)
                PressBack();
            _motion.Tick(unscaledDeltaTime);
        }

        // ---------------- binding ----------------

        private void BindTexts(ILocalization text)
        {
            Initialize();
            if (text == null)
                return;
            _text = text;
            _ticketTime.Bind(text);
            _titleText.SetString(text.Get(ActivityText.EmptyTitle));
            _practiceText.SetString(text.Get(ActivityText.EmptyPractice));
            _practiceLineText.SetString(text.Get(ActivityText.PracticeLine));
            _packWords = UiCounted.From(text, ActivityText.EmptyPack);
            _packsUsed = text.Get(ActivityText.EmptyPacksUsed);
            _needGems = text.Get(ActivityText.EmptyNeedGems);
            _packKey = int.MinValue;
        }

        private void Initialize()
        {
            if (_initialized)
                return;
            _initialized = true;
            _motion.Bind(_canvas, _content, _group, _card);
            _motion.ReduceMotion = _reduceMotion;
            _titleText = new UiLabel(_title, 96);
            _timeText = new UiLabel(_time, 96);
            _practiceText = new UiLabel(_practiceLabel, 64);
            _practiceLineText = new UiLabel(_practiceLine, 64);
            _packText = new UiLabel(_packLabel, 64);
            _priceText = new UiLabel(_packPrice, 16);
            _noteText = new UiLabel(_packNote, 96);
            _outside.onClick.AddListener(OnClosed);
            _close.onClick.AddListener(OnClosed);
            _practice.onClick.AddListener(OnPractice);
            _pack.onClick.AddListener(OnPack);
        }

        private void OnClosed() => _actions?.TicketsEmptyClosed();
        private void OnPractice() => _actions?.PlayPractice();

        private void OnPack()
        {
            if (_packEnabled)
                _actions?.BuyTickets();
        }

        private void OnDisable()
        {
            if (!_motion.IsOpen)
                AnyOpen = false;
        }

        private void OnDestroy()
        {
            AnyOpen = false;
            if (!_initialized)
                return;
            _outside.onClick.RemoveListener(OnClosed);
            _close.onClick.RemoveListener(OnClosed);
            _practice.onClick.RemoveListener(OnPractice);
            _pack.onClick.RemoveListener(OnPack);
        }

        /// <summary>Prefab builder hook (Editor ActivityUiPipeline).</summary>
        public void SetReferences(UiColorTokens tokens, Canvas canvas, GameObject content, CanvasGroup group, RectTransform card, Button outside,
            Button close, TMP_Text title, TMP_Text time, Button practice, TMP_Text practiceLabel, TMP_Text practiceLine, Button pack,
            CanvasGroup packGroup, TMP_Text packLabel, TMP_Text packPrice, TMP_Text packNote)
        {
            _tokens = tokens;
            _canvas = canvas;
            _content = content;
            _group = group;
            _card = card;
            _outside = outside;
            _close = close;
            _title = title;
            _time = time;
            _practice = practice;
            _practiceLabel = practiceLabel;
            _practiceLine = practiceLine;
            _pack = pack;
            _packGroup = packGroup;
            _packLabel = packLabel;
            _packPrice = packPrice;
            _packNote = packNote;
        }
    }
}
