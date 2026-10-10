using System;
using PocketWilds.Presentation;
using PocketWilds.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>The hub's colors, read once from the tokens (UiColorTokens) and the screen's warm tints.</summary>
    public struct ActivityHubColors
    {
        public Color Card;
        public Color CardLocked;
        public Color Disc;
        public Color DiscLocked;
        public Color IconLocked;
        public Color Selected;
        public Color Featured;
        public Color StarOn;
        public Color StarOff;
        public Color Text;
        public Color TextSoft;
        public Color TextOff;
        public Color Warm;
    }

    /// <summary>
    /// T37 one card of the hub's carousel (doc 06 screen 029: thumbnail, name, best record, stars, challenge progress,
    /// locked state with its unlock hint; the featured event activity highlighted): the activity's icon in a soft disc,
    /// its name on two lines at most, three stars and the best score, ten round pips for the challenges done at 3 stars
    /// (language free) with « n/10 », « Nouveau » and « Gratuit » tags. A locked card is grey and soft and shows what opens
    /// it. Recycled by the hub (8 views for 18 activities): bound when its index or its model changes, labels touched only
    /// when their characters change. The selected card lifts a little (instant with Reduce Motion).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ActivityCardView : MonoBehaviour
    {
        public const int Pips = 10;
        private const float LiftSeconds = 0.16f;
        private const float LiftScale = 1.045f;

        [SerializeField] private RectTransform _root;
        [SerializeField] private Button _button;
        [SerializeField] private Image _background;
        [SerializeField] private Image _outline;
        [SerializeField] private Image _disc;
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private GameObject _progress;
        [SerializeField] private RectTransform _starsRow;
        [SerializeField] private float _starsShift = 68f;
        [SerializeField] private Image[] _stars = Array.Empty<Image>();
        [SerializeField] private TMP_Text _best;
        [SerializeField] private Image[] _pips = Array.Empty<Image>();
        [SerializeField] private TMP_Text _pipCount;
        [SerializeField] private TMP_Text _hint;
        [SerializeField] private GameObject _newTag;
        [SerializeField] private TMP_Text _newLabel;
        [SerializeField] private GameObject _freeTag;
        [SerializeField] private TMP_Text _freeLabel;
        [SerializeField] private GameObject _featuredMark;
        [SerializeField] private GameObject _lockMark;

        private Action<int> _onTap;
        private UiLabel _nameText;
        private UiLabel _bestText;
        private UiLabel _pipText;
        private UiLabel _hintText;
        private int _index = -1;
        private int _bound = -2;
        private ActivityId _activity;
        private ActivityCardState _state = (ActivityCardState)255;
        private int _starsShown = -1;
        private int _doneShown = -1;
        private int _bestShown = -1;
        private int _style = -1;
        private Sprite _sprite;
        private float _lift = 1f;
        private float _liftTarget = 1f;
        private bool _initialized;

        public bool ReduceMotion { get; set; }
        public int Index => _index;
        public bool Selected => _liftTarget > 1f;
        public bool Locked => _state == ActivityCardState.Locked;
        public RectTransform Rect => _root != null ? _root : (RectTransform)transform;
        public Button Button => _button;
        public string NameText => _name != null ? _name.text : string.Empty;
        public string HintText => _hint != null && _hint.gameObject.activeSelf ? _hint.text : string.Empty;
        public string BestText => _best != null && _best.gameObject.activeSelf ? _best.text : string.Empty;
        public string PipText => _pipCount != null ? _pipCount.text : string.Empty;
        public bool NewShown => _newTag != null && _newTag.activeSelf;
        public bool FreeShown => _freeTag != null && _freeTag.activeSelf;
        public bool FeaturedShown => _featuredMark != null && _featuredMark.activeSelf;
        public bool LockShown => _lockMark != null && _lockMark.activeSelf;
        public Sprite IconShown => _icon != null ? _icon.sprite : null;
        public float Lift => _lift;

        /// <summary>Stars lit on an open card (the best game's stars).</summary>
        public int StarsLit => Locked || _starsShown < 0 ? 0 : _starsShown;

        /// <summary>Pips lit: challenges done at 3 stars.</summary>
        public int PipsLit => Locked || _doneShown < 0 ? 0 : Mathf.Min(_doneShown, _pips.Length);

        public void Initialize(Action<int> onTap)
        {
            _onTap = onTap;
            if (_initialized)
                return;
            _initialized = true;
            _nameText = new UiLabel(_name, 96);
            _bestText = new UiLabel(_best, 24);
            _pipText = new UiLabel(_pipCount, 24);
            _hintText = new UiLabel(_hint, 128);
            if (_button != null)
                _button.onClick.AddListener(OnTap);
        }

        public void SetTexts(string newWord, string freeWord)
        {
            if (_newLabel != null && _newLabel.text != newWord)
                _newLabel.text = newWord;
            if (_freeLabel != null && _freeLabel.text != freeWord)
                _freeLabel.text = freeWord;
        }

        public void SetShown(bool shown)
        {
            if (gameObject.activeSelf != shown)
                gameObject.SetActive(shown);
        }

        /// <summary>Forgets what is shown: the next bind writes everything (texts changed, a new opening).</summary>
        public void Invalidate()
        {
            _bound = -2;
            _state = (ActivityCardState)255;
            _starsShown = -1;
            _doneShown = -1;
            _bestShown = -1;
            _style = -1;
        }

        /// <summary>Shows <paramref name="card"/> as the <paramref name="index"/>-th card; nothing is touched when nothing changed.</summary>
        public void Bind(int index, in ActivityCardModel card, Sprite icon, bool selected, in ActivityHubColors colors)
        {
            _index = index;
            bool locked = card.State == ActivityCardState.Locked;
            if (index != _bound || card.State != _state || card.Activity != _activity)
            {
                _bound = index;
                _state = card.State;
                _activity = card.Activity;
                _style = -1;
                // Expeditions and the Garden are timers with their own screens: no score, no stars, no challenges.
                bool meta = ActivityCatalog.TryGet(card.Activity, out var definition) && definition.Meta;
                SetActive(_progress, !locked && !meta);
                SetActive(_hint.gameObject, locked);
                _background.color = locked ? colors.CardLocked : colors.Card;
                _disc.color = locked ? colors.DiscLocked : colors.Disc;
                _icon.color = locked ? colors.IconLocked : Color.white;
                _name.color = locked ? colors.TextSoft : colors.Text;
            }
            if (icon != _sprite)
            {
                _sprite = icon;
                _icon.sprite = icon;
            }
            _nameText.SetString(card.Name);
            if (locked)
            {
                _hintText.SetString(card.UnlockHint);
            }
            else if (card.Activity == ActivityId.Expeditions)
            {
                // T42: a timer activity's line in the hint's place (« De retour dans 3 h 12 », « 2 de retour »).
                bool status = !string.IsNullOrEmpty(card.Status);
                SetActive(_hint.gameObject, status);
                if (status)
                    _hintText.SetString(card.Status);
            }
            else
            {
                int stars = Mathf.Clamp(card.BestStars, 0, 3);
                if (stars != _starsShown)
                {
                    _starsShown = stars;
                    for (int i = 0; i < _stars.Length; i++)
                        _stars[i].color = i < stars ? colors.StarOn : colors.StarOff;
                }
                if (card.Best != _bestShown)
                {
                    _bestShown = card.Best;
                    SetActive(_best.gameObject, card.Best > 0);
                    if (card.Best > 0)
                        _bestText.SetNumber(card.Best);
                    // Never played: the stars alone, centered.
                    if (_starsRow != null)
                        _starsRow.anchoredPosition = new Vector2(card.Best > 0 ? 0f : _starsShift, _starsRow.anchoredPosition.y);
                }
                int done = Mathf.Clamp(card.ChallengesDone, 0, Pips);
                if (done != _doneShown)
                {
                    _doneShown = done;
                    for (int i = 0; i < _pips.Length; i++)
                        _pips[i].color = i < done ? colors.StarOn : colors.StarOff;
                    _pipText.SetRatio(done, Pips);
                }
            }
            // T42: a timer activity is always free: its card shows « Nouveau » (A18), never the « Gratuit » tag.
            bool timer = card.Activity == ActivityId.Expeditions;
            SetActive(_newTag, !locked && card.State == ActivityCardState.New && (!card.FreeGame || timer));
            SetActive(_freeTag, !locked && card.FreeGame && !timer);
            SetActive(_featuredMark, card.Featured);
            SetActive(_lockMark, locked);
            int style = (selected ? 1 : 0) | (card.Featured ? 2 : 0);
            if (style != _style)
            {
                _style = style;
                bool outline = selected || card.Featured;
                SetActive(_outline.gameObject, outline);
                _outline.color = selected ? colors.Selected : colors.Featured;
            }
            _liftTarget = selected && !locked ? LiftScale : selected ? 1.02f : 1f;
            if (ReduceMotion)
                ApplyLift(_liftTarget);
        }

        public void Tick(float deltaTime)
        {
            if (_lift == _liftTarget)
                return;
            if (ReduceMotion)
            {
                ApplyLift(_liftTarget);
                return;
            }
            ApplyLift(Mathf.MoveTowards(_lift, _liftTarget, deltaTime * (LiftScale - 1f) / LiftSeconds));
        }

        /// <summary>At rest at once (the hub closed).</summary>
        public void Settle() => ApplyLift(_liftTarget);

        private void ApplyLift(float scale)
        {
            _lift = scale;
            Rect.localScale = new Vector3(scale, scale, 1f);
        }

        private void OnTap()
        {
            if (_index >= 0)
                _onTap?.Invoke(_index);
        }

        private static void SetActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active)
                go.SetActive(active);
        }

        private void OnDestroy()
        {
            if (_initialized && _button != null)
                _button.onClick.RemoveListener(OnTap);
        }

        /// <summary>Prefab builder hook (Editor ActivityUiPipeline).</summary>
        public void SetReferences(RectTransform root, Button button, Image background, Image outline, Image disc, Image icon, TMP_Text name,
            GameObject progress, RectTransform starsRow, float starsShift, Image[] stars, TMP_Text best, Image[] pips, TMP_Text pipCount, TMP_Text hint, GameObject newTag, TMP_Text newLabel,
            GameObject freeTag, TMP_Text freeLabel, GameObject featuredMark, GameObject lockMark)
        {
            _root = root;
            _button = button;
            _background = background;
            _outline = outline;
            _disc = disc;
            _icon = icon;
            _name = name;
            _progress = progress;
            _starsRow = starsRow;
            _starsShift = starsShift;
            _stars = stars;
            _best = best;
            _pips = pips;
            _pipCount = pipCount;
            _hint = hint;
            _newTag = newTag;
            _newLabel = newLabel;
            _freeTag = freeTag;
            _freeLabel = freeLabel;
            _featuredMark = featuredMark;
            _lockMark = lockMark;
        }
    }
}
