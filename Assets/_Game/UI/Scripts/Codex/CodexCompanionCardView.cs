using System;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Presentation;
using PocketWilds.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>
    /// T47: one companion in the Codex's Companions tab. Owned: its picture in the look it wears, its name, its bond bar and level,
    /// the reward ladder 5/10/15/20/25 (locked or reached), the biome bonus line, the looks it has unlocked to choose from and
    /// « Tenue ». Not owned: its silhouette with « Pack de la Prairie » under it; a tap opens the pack's offer. Bound on open and
    /// after a change, never per frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CodexCompanionCardView : MonoBehaviour
    {
        public const int Looks = 3;

        [SerializeField] private RectTransform _rect;
        [SerializeField] private GameObject _owned;
        [SerializeField] private GameObject _locked;
        [SerializeField] private Image _thumb;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _bondLabel;
        [SerializeField] private RectTransform _bondBar;
        [SerializeField] private RectTransform _bondFill;
        [SerializeField] private TMP_Text _bondLevel;
        [SerializeField] private TMP_Text _bonus;
        [SerializeField] private CanvasGroup[] _rungGroups = Array.Empty<CanvasGroup>();
        [SerializeField] private TMP_Text[] _rungLevels = Array.Empty<TMP_Text>();
        [SerializeField] private TMP_Text[] _rungKinds = Array.Empty<TMP_Text>();
        [SerializeField] private TMP_Text[] _rungNames = Array.Empty<TMP_Text>();
        [SerializeField] private GameObject[] _rungLocks = Array.Empty<GameObject>();
        [SerializeField] private GameObject[] _rungDone = Array.Empty<GameObject>();
        [SerializeField] private TMP_Text _looksTitle;
        [SerializeField] private Button[] _lookButtons = Array.Empty<Button>();
        [SerializeField] private Image[] _lookBacks = Array.Empty<Image>();
        [SerializeField] private Image[] _lookThumbs = Array.Empty<Image>();
        [SerializeField] private GameObject[] _lookLocks = Array.Empty<GameObject>();
        [SerializeField] private Button _outfit;
        [SerializeField] private TMP_Text _outfitLabel;
        [SerializeField] private Image _silhouette;
        [SerializeField] private Button _pack;
        [SerializeField] private TMP_Text _packLabel;
        [SerializeField] private string _companionName = "fieldhamster";
        [SerializeField] private string _packTitleKey = "shop.pack.grassland.title";
        [SerializeField] private Sprite[] _lookSprites = Array.Empty<Sprite>();
        [SerializeField] private Sprite _silhouetteSprite;
        [SerializeField] private float _ownedHeight = 1560f;
        [SerializeField] private float _lockedHeight = 720f;
        [SerializeField] private UiColorTokens _tokens;

        private readonly CompanionRung[] _rungs = new CompanionRung[CompanionLadder.Rungs];
        private readonly UiNumberText _level = new UiNumberText();
        private CompanionRow _row;
        private CompanionService _companions;
        private ContentRegistry _content;
        private PlayerState _state;
        private ILocalization _text;
        private ICodexCompanionActions _actions;
        private Action _closeCodex;
        private bool _listening;

        public bool OwnedShown { get; private set; }
        public bool SilhouetteShown => _locked != null && _locked.activeSelf;
        public float Height => OwnedShown ? _ownedHeight : _lockedHeight;
        public string PackText => _packLabel != null ? _packLabel.text : string.Empty;
        public string NameText => _name != null ? _name.text : string.Empty;
        public string BonusText => _bonus != null ? _bonus.text : string.Empty;
        public Button PackButton => _pack;
        public Button OutfitButton => _outfit;
        public Button LookButton(int index) => _lookButtons[index];
        public bool RungUnlocked(int index) => _rungs[index].Unlocked;
        public bool RungLockShown(int index) => _rungLocks[index].activeSelf;
        public int BondLevelShown { get; private set; }

        public void Bind(CompanionRow row, CompanionService companions, ContentRegistry content, PlayerState state, ILocalization text,
            ICodexCompanionActions actions, Action closeCodex)
        {
            _row = row;
            _companions = companions;
            _content = content;
            _state = state;
            _text = text;
            _actions = actions;
            _closeCodex = closeCodex;
            Listen();
            Rebind();
        }

        /// <summary>Owned state changed (the pack bought over the Codex): binds again. True when the card changed height.</summary>
        public bool NeedsRebind => _row != null && OwnedShown != Owned();

        public void Rebind()
        {
            if (_row == null)
                return;
            OwnedShown = Owned();
            _owned.SetActive(OwnedShown);
            _locked.SetActive(!OwnedShown);
            _rect.sizeDelta = new Vector2(_rect.sizeDelta.x, Height);
            if (!OwnedShown)
            {
                _silhouette.sprite = _silhouetteSprite;
                _packLabel.text = Text(_packTitleKey);
                return;
            }

            var species = _row.Species;
            _name.text = _content != null && _content.Species.TryGet(species, out var definition) ? Text(definition.DisplayKey) : string.Empty;
            _bondLabel.text = Text(FeedbackText.SheetBond);
            var bond = _companions.GetBond(species);
            BondLevelShown = bond.Level;
            _level.SetCount(_bondLevel, bond.Level);
            float fill = bond.XpForNext > 0 ? Mathf.Clamp01(bond.XpInLevel / (float)bond.XpForNext) : 1f;
            float width = _bondBar.rect.width, height = _bondBar.rect.height;
            _bondFill.gameObject.SetActive(fill > 0.001f);
            _bondFill.sizeDelta = new Vector2(Mathf.Max(height, width * fill), _bondFill.sizeDelta.y);
            _bonus.text = Text(OutfitText.BonusKey(_companionName));

            CompanionLadder.Fill(_row, bond.Level, _content, _rungs);
            for (int i = 0; i < _rungs.Length && i < _rungNames.Length; i++)
            {
                var rung = _rungs[i];
                _rungLevels[i].text = rung.Level.ToString(System.Globalization.CultureInfo.InvariantCulture);
                _rungKinds[i].text = Text(rung.KindKey);
                _rungNames[i].text = string.IsNullOrEmpty(rung.NameKey) ? string.Empty : Text(rung.NameKey);
                _rungLocks[i].SetActive(!rung.Unlocked);
                _rungDone[i].SetActive(rung.Unlocked);
                _rungGroups[i].alpha = rung.Unlocked ? 1f : 0.55f;
            }

            _looksTitle.text = Text(OutfitText.CodexLooks);
            ContentId wearing = ContentId.None;
            uint animal = _companions.AnimalOf(species);
            if (animal != 0 && _companions.TryGet(species, out _))
                wearing = WornLook(animal);
            for (int i = 0; i < _lookButtons.Length && i < Looks; i++)
            {
                var look = CompanionLadder.Look(_row, i);
                bool unlocked = _companions.IsLookUnlocked(species, look);
                _lookThumbs[i].sprite = i < _lookSprites.Length ? _lookSprites[i] : null;
                _lookThumbs[i].color = unlocked ? Color.white : new Color(1f, 1f, 1f, 0.35f);
                _lookLocks[i].SetActive(!unlocked);
                _lookButtons[i].interactable = unlocked;
                if (_tokens != null)
                    _lookBacks[i].color = look == wearing ? _tokens.accentPrimary : _tokens.panelSecondary;
                if (look == wearing && i < _lookSprites.Length)
                    _thumb.sprite = _lookSprites[i];
            }
            if (_thumb.sprite == null && _lookSprites.Length > 0)
                _thumb.sprite = _lookSprites[0];
            _outfitLabel.text = Text(OutfitText.SheetButton);
            _outfit.gameObject.SetActive(animal != 0);
        }

        private ContentId WornLook(uint animal)
        {
            return _state != null && _state.Collection.TryGet(animal, out var instance) ? instance.Variant : _row.BaseLook;
        }

        private bool Owned() => _companions != null && _companions.Owns(_row.Species);

        private void Listen()
        {
            if (_listening)
                return;
            _listening = true;
            _pack.onClick.AddListener(OnPack);
            _outfit.onClick.AddListener(OnOutfit);
            for (int i = 0; i < _lookButtons.Length; i++)
            {
                int index = i;
                _lookButtons[i].onClick.AddListener(() => OnLook(index));
            }
        }

        private void OnPack()
        {
            if (_row != null && !OwnedShown)
                _actions?.OpenCompanionOffer(_row.Entitlement);
        }

        private void OnOutfit()
        {
            if (_row == null || !OwnedShown)
                return;
            _actions?.OpenCompanionOutfit(_row.Species);
            _closeCodex?.Invoke();
        }

        private void OnLook(int index)
        {
            if (_row == null || !OwnedShown || _actions == null)
                return;
            if (_actions.SelectCompanionLook(_row.Species, CompanionLadder.Look(_row, index)))
                Rebind();
        }

        private string Text(string key) => _text != null ? _text.Get(key) : key;

        private void OnDestroy()
        {
            if (!_listening)
                return;
            _pack.onClick.RemoveListener(OnPack);
            _outfit.onClick.RemoveListener(OnOutfit);
            foreach (var button in _lookButtons)
                button.onClick.RemoveAllListeners();
        }

        /// <summary>Prefab builder hook (Editor OutfitUiPipeline).</summary>
        public void SetReferences(RectTransform rect, GameObject owned, GameObject locked, Image thumb, TMP_Text name, TMP_Text bondLabel,
            RectTransform bondBar, RectTransform bondFill, TMP_Text bondLevel, TMP_Text bonus, CanvasGroup[] rungGroups, TMP_Text[] rungLevels,
            TMP_Text[] rungKinds, TMP_Text[] rungNames, GameObject[] rungLocks, GameObject[] rungDone, TMP_Text looksTitle, Button[] lookButtons,
            Image[] lookBacks, Image[] lookThumbs, GameObject[] lookLocks, Button outfit, TMP_Text outfitLabel, Image silhouette, Button pack,
            TMP_Text packLabel, string companionName, string packTitleKey, Sprite[] lookSprites, Sprite silhouetteSprite, float ownedHeight,
            float lockedHeight, UiColorTokens tokens)
        {
            _rect = rect;
            _owned = owned;
            _locked = locked;
            _thumb = thumb;
            _name = name;
            _bondLabel = bondLabel;
            _bondBar = bondBar;
            _bondFill = bondFill;
            _bondLevel = bondLevel;
            _bonus = bonus;
            _rungGroups = rungGroups;
            _rungLevels = rungLevels;
            _rungKinds = rungKinds;
            _rungNames = rungNames;
            _rungLocks = rungLocks;
            _rungDone = rungDone;
            _looksTitle = looksTitle;
            _lookButtons = lookButtons;
            _lookBacks = lookBacks;
            _lookThumbs = lookThumbs;
            _lookLocks = lookLocks;
            _outfit = outfit;
            _outfitLabel = outfitLabel;
            _silhouette = silhouette;
            _pack = pack;
            _packLabel = packLabel;
            _companionName = companionName;
            _packTitleKey = packTitleKey;
            _lookSprites = lookSprites;
            _silhouetteSprite = silhouetteSprite;
            _ownedHeight = ownedHeight;
            _lockedHeight = lockedHeight;
            _tokens = tokens;
        }
    }
}
