using System;
using PocketWilds.Core;
using PocketWilds.Presentation;
using PocketWilds.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>
    /// PF_UI_ActivityResult, screen 030 (doc 05 Sensations sheet 20; doc 06: stars, record badge, rewards revealed one by
    /// one, Replay, Back; doc 01 Contenu section 12.0). The card slides in (200 ms); the three stars pop one by one to 1.20
    /// (350 ms each, 4 round bursts and a ring each, H-VFX-073) while the score rolls up (600 ms); « Nouveau record » pops
    /// softly with its golden ring of round stars; then each reward card turns over (250 ms, then 50 ms: 300 ms a card) and
    /// shows its face with a ring and 6 round bursts. Under the sticker of a game paid with a bought ticket: « Prochain
    /// autocollant de la page » (no paid randomness). Practice shows stars and score only. Reduce Motion: the card fades,
    /// stars and cards fade in 150 ms each. From the second game of the same activity one tap reveals everything at once.
    /// « Rejouer » and « Retour » stay readable from the start. The presenter plays the sounds and vibrations on the beats
    /// (<see cref="IActivityResultBeats"/>). Closed: canvas disabled, content inactive; nothing is instantiated after the
    /// prewarm.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ActivityResultScreen : MonoBehaviour, IActivityResultView, IActivityResultBeatSource, IMotionSettings
    {
        public const int MaxRewards = 6;
        public const float SlideSeconds = 0.2f;
        public const float StarSlot = 0.35f;
        public const float ScoreSeconds = 0.6f;
        public const float RewardSlot = 0.3f;
        public const float FadeSeconds = 0.15f;

        /// <summary>The rewards start once the three star slots are done: 200 + 1 050 ms.</summary>
        public const float RewardsStart = SlideSeconds + 3f * StarSlot;

        [SerializeField] private UiColorTokens _tokens;
        [SerializeField] private Canvas _canvas;
        [SerializeField] private GameObject _content;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private RectTransform _card;
        [SerializeField] private Button _scrim;
        [SerializeField] private Button _cardTap;
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _subtitle;

        [Header("Stars and score")]
        [SerializeField] private RectTransform[] _starSlots = Array.Empty<RectTransform>();
        [SerializeField] private RectTransform[] _starsOn = Array.Empty<RectTransform>();
        [SerializeField] private CanvasGroup[] _starGroups = Array.Empty<CanvasGroup>();
        [SerializeField] private TMP_Text _score;
        [SerializeField] private RectTransform _ribbon;
        [SerializeField] private CanvasGroup _ribbonGroup;
        [SerializeField] private TMP_Text _ribbonLabel;
        [SerializeField] private TMP_Text _bestLine;

        [Header("Challenge")]
        [SerializeField] private RectTransform _challengeRow;
        [SerializeField] private TMP_Text _challengeLabel;
        [SerializeField] private Image[] _challengeStars = Array.Empty<Image>();
        [SerializeField] private RectTransform _newStarTag;
        [SerializeField] private TMP_Text _newStarLabel;

        [Header("Rewards")]
        [SerializeField] private RectTransform _rewardsRow;
        [SerializeField] private RectTransform[] _flips = Array.Empty<RectTransform>();
        [SerializeField] private CanvasGroup[] _flipGroups = Array.Empty<CanvasGroup>();
        [SerializeField] private GameObject[] _backs = Array.Empty<GameObject>();
        [SerializeField] private RewardCellView[] _cells = Array.Empty<RewardCellView>();
        [SerializeField] private TMP_Text _stickerNote;
        [SerializeField] private TMP_Text _practiceNote;
        [SerializeField] private float _rewardPitch = 140f;

        [Tooltip("Height of the challenge rows (« Défi n » and its stars, « Nouvelle étoile »): the card is that much shorter without them.")]
        [SerializeField] private float _challengeSpace = 140f;

        [Header("Buttons")]
        [SerializeField] private Button _replay;
        [SerializeField] private TMP_Text _replayLabel;
        [SerializeField] private Button _back;
        [SerializeField] private TMP_Text _backLabel;

        [Header("Feel and art")]
        [SerializeField] private UiBurst _burst;
        [SerializeField] private RewardIconSet _icons = new RewardIconSet();
        [SerializeField] private Sprite[] _activityIcons = Array.Empty<Sprite>();
        [SerializeField] private Sprite _fallbackIcon;
        [SerializeField] private Color _gold = new Color(0.949f, 0.710f, 0.267f, 1f);
        [SerializeField] private Color _starOff = new Color(0.89f, 0.86f, 0.81f, 1f);

        private readonly WindowMotion _motion = new WindowMotion(SlideSeconds, true);
        private readonly RewardTexts _rewardTexts = new RewardTexts();
        private readonly RewardCell[] _rewards = new RewardCell[MaxRewards];
        private readonly bool[] _rewardShown = new bool[MaxRewards];
        private readonly bool[] _starShown = new bool[3];
        private ActivityResultModel _model;
        private IActivityResultActions _actions;
        private IActivityResultBeats _beats;
        private ILocalization _text;
        private UiLabel _titleText;
        private UiLabel _subtitleText;
        private UiLabel _scoreText;
        private UiLabel _ribbonText;
        private UiLabel _bestText;
        private UiLabel _challengeText;
        private UiLabel _newStarText;
        private UiLabel _stickerText;
        private UiLabel _practiceText;
        private UiLabel _replayText;
        private UiLabel _backText;
        private string _bestTemplate;
        private string _challengeTemplate;
        private int _count;
        private float _cardScale = 1f;
        private float _cardHeight = -1f;
        private float _rewardsY;
        private float _stickerY;
        private float _practiceY;
        private int _stickerCell = -1;
        private float _time = -1f;
        private int _scoreShown = -1;
        private bool _recordShown;
        private bool _finished = true;
        private bool _reduceMotion;
        private bool _initialized;

        public bool IsOpen => _motion.IsOpen && !_motion.Closing;
        public bool Visible => _motion.IsOpen;

        public bool ReduceMotion
        {
            get => _reduceMotion;
            set
            {
                _reduceMotion = value;
                _motion.ReduceMotion = value;
                if (_burst != null)
                    _burst.ReduceMotion = value;
                foreach (var cell in _cells)
                    if (cell != null)
                        cell.ReduceMotion = value;
            }
        }

        // ---------------- read by tests and captures ----------------

        public Button ReplayButton => _replay;
        public Button BackButton => _back;
        public Button CardTap => _cardTap;
        public Button ScrimButton => _scrim;
        public UiBurst Burst => _burst;
        public bool Finished => _finished;
        public float Elapsed => _time;
        public string TitleText => _title.text;
        public string SubtitleText => _subtitle.text;
        public string ScoreText => _score.text;
        public bool RibbonShown => _ribbon.gameObject.activeSelf && _ribbonGroup.alpha > 0.99f && Mathf.Abs(_ribbon.localScale.x - 1f) < 1e-3f;
        public bool BestShown => _bestLine.gameObject.activeSelf;
        public string BestText => _bestLine.text;
        public bool ChallengeShown => _challengeRow.gameObject.activeSelf;
        public bool NewStarShown => _newStarTag.gameObject.activeSelf;
        public bool StickerNoteShown => _stickerNote.gameObject.activeSelf && _stickerNote.alpha > 0.99f;
        public bool PracticeNoteShown => _practiceNote.gameObject.activeSelf;
        public int RewardsShown => _count;
        public RewardCellView RewardCell(int i) => _cells[i];

        /// <summary>The star is earned and fully shown (full opacity, at rest).</summary>
        public bool StarVisible(int star) =>
            _starShown[star] && _starsOn[star].gameObject.activeSelf && _starGroups[star].alpha > 0.99f && Mathf.Abs(_starsOn[star].localScale.x - 1f) < 1e-3f;

        public float StarScale(int star) => _starsOn[star].localScale.x;

        /// <summary>The reward card shows its face, at full size and opacity.</summary>
        public bool RewardVisible(int i) =>
            i < _count && _rewardShown[i] && !_backs[i].activeSelf && _cells[i].gameObject.activeInHierarchy && _flipGroups[i].alpha > 0.99f
            && Mathf.Abs(_flips[i].localScale.x - _cardScale) < 1e-3f;

        public void SetBeats(IActivityResultBeats beats) => _beats = beats;

        // ---------------- IActivityResultView ----------------

        public void Open(in ActivityResultModel model, RewardCell[] rewards, int rewardCount, ILocalization text, IActivityResultActions actions)
        {
            Initialize();
            _model = model;
            _actions = actions;
            BindTexts(text);
            _count = model.Rewarded && rewards != null ? Mathf.Clamp(rewardCount, 0, Mathf.Min(MaxRewards, Mathf.Min(rewards.Length, _cells.Length))) : 0;
            _stickerCell = -1;
            for (int i = 0; i < _count; i++)
            {
                _rewards[i] = rewards[i];
                if (_stickerCell < 0 && rewards[i].Kind == RewardKind.Sticker)
                    _stickerCell = i;
            }

            // T42: an expedition's loot: « Repartir » instead of « Rejouer », no score (its stars are the team rating).
            bool expedition = model.Activity == ActivityId.Expeditions;
            if (_text != null)
                _replayText.SetString(_text.Get(expedition ? ExpeditionText.SendAgain : ActivityText.ResultReplay));
            SetActive(_score.gameObject, !expedition);
            _icon.sprite = Icon(model.Activity);
            int stars = Mathf.Clamp(model.Stars, 0, 3);
            _titleText.SetString(_text != null ? _text.Get(ActivityText.ResultTitle(stars)) : string.Empty);
            WriteSubtitle();

            for (int i = 0; i < 3 && i < _starsOn.Length; i++)
            {
                _starShown[i] = false;
                SetActive(_starsOn[i].gameObject, false);
                _starsOn[i].localScale = Vector3.one;
                _starGroups[i].alpha = 0f;
            }
            _scoreShown = -1;
            SetScore(0);
            _recordShown = false;
            SetActive(_ribbon.gameObject, false);
            bool best = !model.NewRecord && model.Best > 0;
            SetActive(_bestLine.gameObject, best);
            if (best)
                _bestText.SetTemplate(_bestTemplate, model.Best);

            bool challenge = model.Mode == ActivityMode.Challenge;
            LayoutCard(challenge);
            SetActive(_challengeRow.gameObject, challenge);
            SetActive(_newStarTag.gameObject, false);
            if (challenge)
            {
                _challengeText.Commit(ActivityChars.Fill(_challengeText.Buffer, 0, _challengeTemplate, model.ChallengeIndex + 1));
                for (int i = 0; i < _challengeStars.Length; i++)
                    _challengeStars[i].color = i < stars ? _gold : _starOff;
            }

            LayoutRewards();
            SetActive(_practiceNote.gameObject, !model.Rewarded);
            SetActive(_stickerNote.gameObject, model.Rewarded && model.FixedStickerRule && _stickerCell >= 0);
            _stickerNote.alpha = 0f;

            _burst.Clear();
            _time = 0f;
            _finished = false;
            if (!_motion.IsOpen || _motion.Closing)
                _motion.Show();
        }

        /// <summary>Everything at once (one tap from the second game, or the presenter): stars, score, record, every reward.</summary>
        public void RevealAll()
        {
            if (_finished || !_motion.IsOpen)
                return;
            int stars = Mathf.Clamp(_model.Stars, 0, 3);
            for (int i = 0; i < 3 && i < _starsOn.Length; i++)
            {
                if (i < stars)
                    ShowStar(i, 1f, 1f);
            }
            SetScore(_model.Score);
            if (_model.NewRecord)
                ShowRibbon(1f, 1f);
            if (_model.ChallengeImproved && _model.Mode == ActivityMode.Challenge)
                SetActive(_newStarTag.gameObject, true);
            for (int i = 0; i < _count; i++)
                ShowFace(i, 1f, 1f);
            if (_stickerNote.gameObject.activeSelf)
                _stickerNote.alpha = 1f;
            _finished = true;
            _beats?.ResultRevealedAll();
        }

        public void Close()
        {
            _motion.Hide();
        }

        public void Prewarm(ILocalization text)
        {
            if (_motion.IsOpen)
                return;
            var cells = new RewardCell[MaxRewards];
            cells[0] = new RewardCell(RewardKind.Light, 0) { LightMinutes = 20 };
            cells[1] = new RewardCell(RewardKind.Gems, 2);
            cells[2] = new RewardCell(RewardKind.Bond, 6);
            cells[3] = new RewardCell(RewardKind.Sticker, 1);
            var model = new ActivityResultModel
            {
                Activity = ActivityId.Grooming, Mode = ActivityMode.Challenge, Score = 520, Best = 400, Stars = 3, NewRecord = true, Rewarded = true,
                ChallengeImproved = true, FixedStickerRule = true,
            };
            Open(model, cells, 4, text, null);
            _burst.PlayLocal(UiBurstKind.StampBig, Vector2.zero, Color.clear, Color.clear);
            _burst.PlayLocal(UiBurstKind.Claim, Vector2.zero, Color.clear, Color.clear);
            var beats = _beats;
            _beats = null;
            RevealAll();
            _beats = beats;
            Canvas.ForceUpdateCanvases();
            _burst.Clear();
            _motion.HideNow();
            _finished = true;
            _time = -1f;
        }

        /// <summary>Android back, or Escape: « Retour ».</summary>
        public void PressBack()
        {
            if (IsOpen)
                _actions?.BackToHub();
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!_motion.IsOpen)
                return;
            if (_motion.BackPressed)
                PressBack();
            bool closed = _motion.Tick(unscaledDeltaTime);
            if (closed)
            {
                _burst.Clear();
                _finished = true;
                return;
            }
            if (!_finished)
            {
                _time += unscaledDeltaTime;
                Advance();
            }
            for (int i = 0; i < _count; i++)
                _cells[i].Tick(unscaledDeltaTime);
            _burst.Tick(unscaledDeltaTime);
        }

        // ---------------- the sequence (doc 05 sheet 20) ----------------

        private void Advance()
        {
            float t = _time;
            int stars = Mathf.Clamp(_model.Stars, 0, 3);
            for (int i = 0; i < stars && i < _starsOn.Length; i++)
            {
                float local = t - (SlideSeconds + i * StarSlot);
                if (local < 0f)
                    break;
                if (!_starShown[i])
                {
                    _starShown[i] = true;
                    SetActive(_starsOn[i].gameObject, true);
                    _burst.Play(UiBurstKind.Stamp, _starSlots[i], _gold, Color.white);
                    _beats?.ResultStarLanded(i);
                }
                if (_reduceMotion)
                    ShowStar(i, Mathf.Clamp01(local / FadeSeconds), 1f);
                else
                    ShowStar(i, Mathf.Clamp01(local / 0.06f), local >= StarSlot ? 1f : ActivityCurves.StarPop(local));
            }

            float roll = Mathf.Clamp01((t - SlideSeconds) / ScoreSeconds);
            SetScore(roll >= 1f ? _model.Score : Mathf.RoundToInt(_model.Score * UiCurves.EaseOut(roll)));

            float after = t - RewardsStart;
            if (after >= 0f)
            {
                if (_model.NewRecord)
                {
                    if (!_recordShown)
                    {
                        _recordShown = true;
                        _burst.Play(UiBurstKind.StampBig, _ribbon, _gold, Color.white);
                        _beats?.ResultRecordShown();
                    }
                    if (_reduceMotion)
                        ShowRibbon(Mathf.Clamp01(after / FadeSeconds), 1f);
                    else
                        ShowRibbon(Mathf.Clamp01(after / 0.08f), ActivityCurves.SoftPop(after));
                }
                if (_model.ChallengeImproved && _model.Mode == ActivityMode.Challenge && !_newStarTag.gameObject.activeSelf)
                    SetActive(_newStarTag.gameObject, true);
            }

            for (int i = 0; i < _count; i++)
            {
                float local = after - i * RewardSlot;
                if (local < 0f)
                    break;
                if (_reduceMotion)
                {
                    if (!_rewardShown[i])
                        Turned(i);
                    ShowFace(i, Mathf.Clamp01(local / FadeSeconds), 1f);
                    continue;
                }
                float half = ActivityCurves.FlipSeconds * 0.5f;
                if (local < half)
                {
                    // The back turns away.
                    _flips[i].localScale = new Vector3(Mathf.Max(0.001f, 1f - local / half) * _cardScale, _cardScale, 1f);
                    continue;
                }
                if (!_rewardShown[i])
                    Turned(i);
                ShowFace(i, 1f, Mathf.Min(1f, (local - half) / half));
            }

            float end = RewardsStart + Mathf.Max(0, _count - 1) * RewardSlot + (_count > 0 ? ActivityCurves.FlipSeconds : 0f);
            float starsEnd = SlideSeconds + Mathf.Max(0, stars - 1) * StarSlot + (stars > 0 ? StarSlot : 0f);
            float recordEnd = _model.NewRecord ? RewardsStart + 0.32f : 0f;
            if (_reduceMotion)
                end = Mathf.Max(end, RewardsStart + Mathf.Max(0, _count - 1) * RewardSlot + FadeSeconds);
            if (t >= end && t >= starsEnd && t >= recordEnd && t >= SlideSeconds + ScoreSeconds)
            {
                for (int i = 0; i < stars; i++)
                    ShowStar(i, 1f, 1f);
                for (int i = 0; i < _count; i++)
                    ShowFace(i, 1f, 1f);
                if (_model.NewRecord)
                    ShowRibbon(1f, 1f);
                if (_stickerNote.gameObject.activeSelf)
                    _stickerNote.alpha = 1f;
                SetScore(_model.Score);
                _finished = true;
            }
        }

        private void Turned(int i)
        {
            _rewardShown[i] = true;
            SetActive(_backs[i], false);
            _cells[i].SetShown(true);
            _cells[i].ShowNow();
            _burst.Play(UiBurstKind.Claim, _flips[i], _tokens != null ? _tokens.accentPrimary : _gold, Color.white);
            if (i == _stickerCell && _stickerNote.gameObject.activeSelf)
                _stickerNote.alpha = 1f;
            _beats?.ResultRewardShown(i);
        }

        private void ShowStar(int i, float alpha, float scale)
        {
            _starShown[i] = true;
            SetActive(_starsOn[i].gameObject, true);
            _starGroups[i].alpha = alpha;
            _starsOn[i].localScale = new Vector3(scale, scale, 1f);
        }

        private void ShowRibbon(float alpha, float scale)
        {
            _recordShown = true;
            SetActive(_ribbon.gameObject, true);
            _ribbonGroup.alpha = alpha;
            _ribbon.localScale = new Vector3(scale, scale, 1f);
        }

        private void ShowFace(int i, float alpha, float scaleX)
        {
            if (!_rewardShown[i])
            {
                _rewardShown[i] = true;
                SetActive(_backs[i], false);
                _cells[i].SetShown(true);
                _cells[i].ShowNow();
                if (i == _stickerCell && _stickerNote.gameObject.activeSelf)
                    _stickerNote.alpha = 1f;
            }
            _flipGroups[i].alpha = alpha;
            _flips[i].localScale = new Vector3(Mathf.Max(0.001f, scaleX) * _cardScale, _cardScale, 1f);
        }

        private void SetScore(int score)
        {
            if (score == _scoreShown)
                return;
            _scoreShown = score;
            _scoreText.SetNumber(score);
        }

        /// <summary>The cards centered on their row, face down; a long row (7 or more) gets slightly smaller cards.</summary>
        private void LayoutRewards()
        {
            SetActive(_rewardsRow.gameObject, _count > 0);
            float pitch = _rewardPitch;
            float width = _rewardsRow.rect.width > 1f ? _rewardsRow.rect.width : 840f;
            if (_count * pitch > width)
                pitch = width / _count;
            _cardScale = Mathf.Min(1f, pitch / _rewardPitch);
            for (int i = 0; i < _flips.Length; i++)
            {
                bool shown = i < _count;
                SetActive(_flips[i].gameObject, shown);
                _rewardShown[i] = false;
                if (!shown)
                    continue;
                _flips[i].anchoredPosition = new Vector2((i - (_count - 1) * 0.5f) * pitch, _flips[i].anchoredPosition.y);
                _flips[i].localScale = new Vector3(_cardScale, _cardScale, 1f);
                _cells[i].Bind(_rewards[i], _icons, _rewardTexts);
                _cells[i].ShowNow();
                bool back = !_reduceMotion;
                SetActive(_backs[i], back);
                _cells[i].SetShown(!back);
                _flipGroups[i].alpha = back ? 1f : 0f;
            }
        }

        /// <summary>The activity's name (the mode has its own line: « Défi n » with its stars, or « Partie d'entraînement »).</summary>
        private void WriteSubtitle() =>
            _subtitleText.SetString(!string.IsNullOrEmpty(_model.Subtitle) ? _model.Subtitle : _text != null ? NameOf(_model.Activity) : string.Empty);

        /// <summary>Without the challenge rows the card is shorter and the rewards move up; the buttons stay at the thumb.</summary>
        private void LayoutCard(bool challenge)
        {
            if (_cardHeight < 0f)
            {
                _cardHeight = _card.sizeDelta.y;
                _rewardsY = _rewardsRow.anchoredPosition.y;
                _stickerY = _stickerNote.rectTransform.anchoredPosition.y;
                _practiceY = _practiceNote.rectTransform.anchoredPosition.y;
            }
            float lift = challenge ? 0f : _challengeSpace;
            _card.sizeDelta = new Vector2(_card.sizeDelta.x, _cardHeight - lift);
            _rewardsRow.anchoredPosition = new Vector2(_rewardsRow.anchoredPosition.x, _rewardsY + lift);
            var sticker = _stickerNote.rectTransform;
            sticker.anchoredPosition = new Vector2(sticker.anchoredPosition.x, _stickerY + lift);
            var practice = _practiceNote.rectTransform;
            practice.anchoredPosition = new Vector2(practice.anchoredPosition.x, _practiceY + lift);
        }

        private string NameOf(ActivityId activity)
        {
            string key = ActivityText.NameKey(activity);
            return key != null ? _text.Get(key) : string.Empty;
        }

        private Sprite Icon(ActivityId activity)
        {
            int i = (int)activity;
            if (i >= 0 && i < _activityIcons.Length && _activityIcons[i] != null)
                return _activityIcons[i];
            return _fallbackIcon;
        }

        private void BindTexts(ILocalization text)
        {
            if (text == null)
                return;
            _text = text;
            _rewardTexts.Bind(text);
            _bestTemplate = text.Get(ActivityText.ResultBest);
            _challengeTemplate = text.Get(ActivityText.Challenge);
            _ribbonText.SetString(text.Get(ActivityText.ResultRecord));
            _newStarText.SetString(text.Get(ActivityText.ResultNewStar));
            _stickerText.SetString(text.Get(ActivityText.ResultNextSticker));
            _practiceText.SetString(text.Get(ActivityText.ResultPractice));
            _replayText.SetString(text.Get(ActivityText.ResultReplay));
            _backText.SetString(text.Get(ActivityText.ResultBack));
        }

        private void Initialize()
        {
            if (_initialized)
                return;
            _initialized = true;
            _motion.Bind(_canvas, _content, _group, _card);
            _motion.ReduceMotion = _reduceMotion;
            _titleText = new UiLabel(_title, 64);
            _subtitleText = new UiLabel(_subtitle, 128);
            _scoreText = new UiLabel(_score, 24);
            _ribbonText = new UiLabel(_ribbonLabel, 64);
            _bestText = new UiLabel(_bestLine, 64);
            _challengeText = new UiLabel(_challengeLabel, 48);
            _newStarText = new UiLabel(_newStarLabel, 48);
            _stickerText = new UiLabel(_stickerNote, 96);
            _practiceText = new UiLabel(_practiceNote, 64);
            _replayText = new UiLabel(_replayLabel, 48);
            _backText = new UiLabel(_backLabel, 48);
            _replay.onClick.AddListener(OnReplay);
            _back.onClick.AddListener(OnBack);
            _scrim.onClick.AddListener(OnTap);
            _cardTap.onClick.AddListener(OnTap);
            ReduceMotion = _reduceMotion;
        }

        private void OnReplay() => _actions?.PlayAgain();
        private void OnBack() => _actions?.BackToHub();

        /// <summary>A tap on the card or beside it: from the second game of the activity it reveals everything at once.</summary>
        private void OnTap()
        {
            if (_model.QuickReveal && !_finished)
                RevealAll();
        }

        private static void SetActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active)
                go.SetActive(active);
        }

        private void OnDestroy()
        {
            if (!_initialized)
                return;
            _replay.onClick.RemoveListener(OnReplay);
            _back.onClick.RemoveListener(OnBack);
            _scrim.onClick.RemoveListener(OnTap);
            _cardTap.onClick.RemoveListener(OnTap);
        }

        // ---------------- prefab builder hooks (Editor ActivityUiPipeline) ----------------

        public void SetReferences(UiColorTokens tokens, Canvas canvas, GameObject content, CanvasGroup group, RectTransform card, Button scrim,
            Button cardTap, Image icon, TMP_Text title, TMP_Text subtitle)
        {
            _tokens = tokens;
            _canvas = canvas;
            _content = content;
            _group = group;
            _card = card;
            _scrim = scrim;
            _cardTap = cardTap;
            _icon = icon;
            _title = title;
            _subtitle = subtitle;
        }

        public void SetScoreReferences(RectTransform[] starSlots, RectTransform[] starsOn, CanvasGroup[] starGroups, TMP_Text score, RectTransform ribbon,
            CanvasGroup ribbonGroup, TMP_Text ribbonLabel, TMP_Text bestLine, RectTransform challengeRow, TMP_Text challengeLabel, Image[] challengeStars,
            RectTransform newStarTag, TMP_Text newStarLabel)
        {
            _starSlots = starSlots;
            _starsOn = starsOn;
            _starGroups = starGroups;
            _score = score;
            _ribbon = ribbon;
            _ribbonGroup = ribbonGroup;
            _ribbonLabel = ribbonLabel;
            _bestLine = bestLine;
            _challengeRow = challengeRow;
            _challengeLabel = challengeLabel;
            _challengeStars = challengeStars;
            _newStarTag = newStarTag;
            _newStarLabel = newStarLabel;
        }

        public void SetRewardReferences(RectTransform rewardsRow, RectTransform[] flips, CanvasGroup[] flipGroups, GameObject[] backs, RewardCellView[] cells,
            TMP_Text stickerNote, TMP_Text practiceNote, float rewardPitch, float challengeSpace, Button replay, TMP_Text replayLabel, Button back,
            TMP_Text backLabel)
        {
            _rewardsRow = rewardsRow;
            _flips = flips;
            _flipGroups = flipGroups;
            _backs = backs;
            _cells = cells;
            _stickerNote = stickerNote;
            _practiceNote = practiceNote;
            _rewardPitch = rewardPitch;
            _challengeSpace = challengeSpace;
            _replay = replay;
            _replayLabel = replayLabel;
            _back = back;
            _backLabel = backLabel;
        }

        public void SetArtReferences(UiBurst burst, RewardIconSet icons, Sprite[] activityIcons, Sprite fallbackIcon, Color gold, Color starOff)
        {
            _burst = burst;
            _icons = icons;
            _activityIcons = activityIcons;
            _fallbackIcon = fallbackIcon;
            _gold = gold;
            _starOff = starOff;
        }
    }
}
