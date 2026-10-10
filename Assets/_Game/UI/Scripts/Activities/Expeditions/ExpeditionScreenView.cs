using System;
using System.Collections.Generic;
using PocketWilds.Core;
using PocketWilds.Presentation;
using PocketWilds.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>
    /// PF_UI_Expeditions (T42 « Expéditions », doc 01 Contenu section 7 and A18/A19, doc 05 sheet 23; built by
    /// ExpeditionPipeline, Addressables group Activity_Expeditions). A screen-space canvas over the hidden refuge: the title
    /// and close with the Gems; the map card (agent EA's paper) with the destination cards (each destination's own art,
    /// rendered in the editor from its postcard set; the next biomes as soft locked silhouettes); the 4 slot cards (empty
    /// « Choisis ton équipe », away with the team's portraits, a soft ring and « De retour dans 3 h 12 » with « Finir
    /// maintenant » and its Gems, back glowing with « Voir la carte postale », locked for 800 Gems or the Wild Club); « Tout
    /// réclamer »; the « Défis » row; the planning sheet (the free animals' portraits, the live stars and their reasons,
    /// « Équipe conseillée », the duration chips, the loot preview, « Partir »); the welcome's postcards (a deckled card
    /// that pivots in over a dimmed world in 400 ms with a soft ring and 6 round petals); a short note pill; the tutorial
    /// paw. Reduce Motion: fades instead of slides, pivots, petals and pulses. Portraits (the codex thumbnails), the UI kit's
    /// sprites and the fonts are borrowed from what the scene already loaded (<see cref="Lend"/>), never referenced by the
    /// prefab. Timers write their labels from reused char buffers only when the shown text changes; nothing allocates per
    /// frame after <see cref="Bind"/>.
    /// </summary>
    public sealed class ExpeditionScreenView : MonoBehaviour, IExpeditionScreenView
    {
        /// <summary>The loot preview's icons, in display order.</summary>
        public enum LootIcon
        {
            Light = 0,
            Treat = 1,
            Seed = 2,
            Sticker = 3,
            Egg = 4,
            Ticket = 5,
            Ingredient = 6,
            Star = 7,
            Gems = 8,
            Lock = 9,
            Leaf = 10,
            Paw = 11,
            Dots = 12,
            Check = 13,
            Postcard = 14,
            StarOff = 15,
            Bell = 16,
        }

        private const float NoteIn = 0.15f;
        private const float NoteHold = 2f;
        private const float NoteOut = 0.25f;
        private const float GlowPeriod = 1.4f;
        private const float DustSeconds = 0.7f;
        private const float PetalSeconds = 0.7f;
        private const float RingSeconds = 0.6f;

        /// <summary>The rows' pitches (ExpeditionPipeline places the pooled views at these steps from the content's left).</summary>
        public const float DestinationPitch = 420f;

        public const float AnimalPitch = 176f;
        public const float ChallengePitch = 440f;

        /// <summary>The loot preview's items (an icon with its value under it).</summary>
        public const float LootPitch = 148f;

        /// <summary>The first stroll's single chip (« Première balade : 3 min »).</summary>
        public const float FirstStrollChip = 700f;
        public const float RowPadding = 20f;

        [SerializeField] private ExpeditionScreenParts _parts = new ExpeditionScreenParts();

        [Header("Agent EA's sprites (in the bundle)")]
        [SerializeField] private Sprite _slotEmpty;
        [SerializeField] private Sprite _slotAway;
        [SerializeField] private Sprite _slotBack;
        [SerializeField] private Sprite _slotLocked;
        [SerializeField] private Sprite _lockedDestination;
        [SerializeField] private Sprite _chip;
        [SerializeField] private Sprite _chipSelected;
        [SerializeField] private Sprite[] _durationIcons = Array.Empty<Sprite>();

        [Header("Destination cards' art (EXP_{suffix}, agent EP)")]
        [SerializeField] private string[] _artNames = Array.Empty<string>();
        [SerializeField] private Sprite[] _arts = Array.Empty<Sprite>();

        [Header("Borrowed at run time from the scene (never in the bundle)")]
        [SerializeField] private Image[] _kitImages = Array.Empty<Image>();
        [SerializeField] private string[] _kitNames = Array.Empty<string>();
        [SerializeField] private TMP_Text[] _labels = Array.Empty<TMP_Text>();
        [SerializeField] private string[] _labelFonts = Array.Empty<string>();
        [SerializeField] private string[] _labelMaterials = Array.Empty<string>();

        /// <summary>One name per <see cref="LootIcon"/>, a second choice after « | » (an own sprite of the bundle wins).</summary>
        [SerializeField] private string[] _iconNames = Array.Empty<string>();

        [SerializeField] private Sprite[] _ownIcons = Array.Empty<Sprite>();

        [Header("Colours")]
        [SerializeField] private Color _cream = new Color(0.99f, 0.96f, 0.90f, 1f);
        [SerializeField] private Color _card = new Color(1f, 0.99f, 0.96f, 1f);
        [SerializeField] private Color _cardLocked = new Color(0.93f, 0.90f, 0.85f, 1f);
        [SerializeField] private Color _text = new Color(0.29f, 0.23f, 0.17f, 1f);
        [SerializeField] private Color _textSoft = new Color(0.54f, 0.46f, 0.38f, 1f);
        [SerializeField] private Color _accent = new Color(1f, 0.69f, 0.37f, 1f);
        [SerializeField] private Color _gold = new Color(0.949f, 0.710f, 0.267f, 1f);
        [SerializeField] private Color _starOff = new Color(0.89f, 0.86f, 0.81f, 1f);
        [SerializeField] private Color _leaf = new Color(0.47f, 0.72f, 0.38f, 1f);
        [SerializeField] private Color _disabled = new Color(0.80f, 0.77f, 0.72f, 1f);
        [SerializeField] private Color _eggTint = new Color(0.98f, 0.93f, 0.80f, 1f);

        private readonly SafeAreaFitter _fitter = new SafeAreaFitter();
        private ExpeditionTimeText _time = new ExpeditionTimeText();
        private IExpeditionScreenActions _actions;
        private CanvasScaler _scaler;
        private CodexVisualCatalog _catalog;
        private bool _catalogSearched;
        private Sprite[] _icons;
        private bool _lent;
        private bool _initialized;
        private bool _shown;
        private bool _reduceMotion;

        // Templates read at Bind.
        private UiCounted _speedUpsLeft;
        private UiCounted _reasonBiome;
        private string _speedUpsNone;
        private string _lightMinutes = "{n} min";
        private string _percent = "{n}%";

        // Labels from char buffers.
        private UiLabel _gemsLabel;
        private UiLabel[] _timerLabels;
        private UiLabel[] _costLabels;
        private UiLabel[] _leftLabels;
        private UiLabel[] _priceLabels;
        private UiLabel[] _lootLabels;
        private UiLabel _reasonBiomeLabel;
        private long[] _timeKey;
        private int[] _leftShown;

        // Slots.
        private ExpeditionSlotState[] _slotState;
        private float[] _dustT;

        // Sheet and content fades.
        private bool _sheetShown;
        private float _sheetT;
        private float _sheetHeight = 1200f;
        private Vector2 _sheetHome;
        private bool _contentShown = true;
        private float _contentT = 1f;
        private bool _dimmed;
        private float _dimT;
        private float _glowT;

        // Postcards.
        private bool[] _cardShown;
        private float[] _cardT;
        private int _cardCount;
        private bool _docked;
        private float _dockT;

        // The note.
        private float _noteT = -1f;

        // ---------------- IExpeditionScreenView ----------------

        public bool Valid
        {
            get
            {
                var p = _parts;
                if (p == null || p.canvas == null || p.content == null || p.contentGroup == null || p.sheet == null || p.sheetGroup == null
                    || p.postcardLayer == null || p.dim == null || p.notePill == null || p.noteGroup == null || p.noteLabel == null)
                    return false;
                if (p.slots.Length < ExpeditionLayout.SlotCapacity || p.destinations.Length < ExpeditionLayout.DestinationCapacity
                    || p.animals.Length < ExpeditionLayout.AnimalCapacity || p.durations.Length < ExpeditionLayout.DurationCount
                    || p.loot.Length < ExpeditionLayout.LootCapacity || p.challenges.Length < ExpeditionLayout.ChallengeCapacity
                    || p.postcards.Length < ExpeditionLayout.PostcardCapacity || p.ratingStars.Length < 3 || p.reasons.Length < 3)
                    return false;
                foreach (var s in p.slots)
                    if (s == null || s.root == null || s.button == null || s.frame == null || s.glow == null || s.empty == null || s.team == null
                        || s.away == null || s.back == null || s.locked == null || s.timer == null || s.ringFill == null || s.speedUp == null
                        || s.portraits.Length < ExpeditionLayout.MaxTeam || s.stars.Length < 3 || s.dust.Length < ExpeditionLayout.DustDots)
                        return false;
                foreach (var d in p.destinations)
                    if (d == null || d.root == null || d.button == null || d.art == null || d.name == null || d.outline == null || d.lockIcon == null)
                        return false;
                foreach (var a in p.animals)
                    if (a == null || a.root == null || a.button == null || a.portrait == null || a.ring == null || a.check == null || a.leaf == null)
                        return false;
                foreach (var c in p.postcards)
                    if (c == null || c.root == null || c.group == null || c.card == null || c.picture == null || c.ring == null
                        || c.petals.Length < ExpeditionLayout.Petals)
                        return false;
                return true;
            }
        }

        public bool Shown => _shown;

        public bool ReduceMotion
        {
            get => _reduceMotion;
            set
            {
                _reduceMotion = value;
                if (_parts?.paw != null)
                    _parts.paw.ReduceMotion = value;
            }
        }

        public Canvas Canvas => _parts.canvas;

        public ExpeditionScreenParts Parts => _parts;

        public CodexVisualCatalog Portraits => _catalog;

        public bool SheetShown => _sheetShown;

        public bool ContentShown => _contentShown;

        public bool Dimmed => _dimmed;

        public bool PawShown => _parts.paw != null && _parts.paw.Visible;

        public string SlotTimerText(int slot) => _parts.slots[slot].timer.text;

        public ExpeditionSlotState SlotStateShown(int slot) => _slotState != null ? _slotState[slot] : ExpeditionSlotState.Locked;

        public bool SlotGlowing(int slot) => _parts.slots[slot].glow.enabled;

        public bool NoteShown => _noteT >= 0f;

        public string NoteText => _parts.noteLabel.text;

        public Sprite IconOf(LootIcon icon) => _icons != null && (int)icon < _icons.Length ? _icons[(int)icon] : null;

        public void Bind(ILocalization text, IExpeditionScreenActions actions)
        {
            Initialize();
            _actions = actions;
            if (!_lent)
                Lend(Resources.FindObjectsOfTypeAll<Sprite>(), Resources.FindObjectsOfTypeAll<TMP_FontAsset>(), Resources.FindObjectsOfTypeAll<Material>());
            if (_catalog == null)
                _catalogSearched = false;
            EnsureCatalog();
            BindTexts(text);
        }

        /// <summary>
        /// Borrows the scene's sprites (the UI kit, the icons), fonts and font materials by name: the bundle never holds a
        /// copy. Captures and tests lend what they loaded. Allocates (one lookup), once per load.
        /// </summary>
        public void Lend(IReadOnlyList<Sprite> sprites, IReadOnlyList<TMP_FontAsset> fonts, IReadOnlyList<Material> materials)
        {
            Initialize();
            _lent = true;
            var wanted = new HashSet<string>(StringComparer.Ordinal);
            foreach (var name in _kitNames)
                if (!string.IsNullOrEmpty(name))
                    wanted.Add(name);
            foreach (var names in _iconNames)
                if (!string.IsNullOrEmpty(names))
                    foreach (var name in names.Split('|'))
                        wanted.Add(name);
            var found = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            if (sprites != null)
                for (int i = 0; i < sprites.Count; i++)
                {
                    var sprite = sprites[i];
                    if (sprite != null && wanted.Contains(sprite.name) && !found.ContainsKey(sprite.name))
                        found.Add(sprite.name, sprite);
                }
            List<string> missing = null;
            for (int i = 0; i < _kitImages.Length && i < _kitNames.Length; i++)
            {
                var image = _kitImages[i];
                if (image == null)
                    continue;
                if (found.TryGetValue(_kitNames[i] ?? string.Empty, out var sprite))
                    image.sprite = sprite;
                else if (!string.IsNullOrEmpty(_kitNames[i]) && (missing ??= new List<string>()).IndexOf(_kitNames[i]) < 0)
                    missing.Add(_kitNames[i]);
            }
            if (missing != null)
                GameLog.Warning(LogCategory.Presentation, "Expeditions: kit sprites not loaded by the scene (plain shapes shown): " + string.Join(", ", missing));
            int iconCount = Enum.GetValues(typeof(LootIcon)).Length;
            _icons = new Sprite[iconCount];
            for (int i = 0; i < iconCount; i++)
            {
                if (i < _ownIcons.Length && _ownIcons[i] != null)
                {
                    _icons[i] = _ownIcons[i];
                    continue;
                }
                if (i >= _iconNames.Length || string.IsNullOrEmpty(_iconNames[i]))
                    continue;
                foreach (var name in _iconNames[i].Split('|'))
                    if (found.TryGetValue(name, out var sprite))
                    {
                        _icons[i] = sprite;
                        break;
                    }
            }
            // Fonts: the label's own font by name, its material preset by name (else the font's own material).
            var fontByName = new Dictionary<string, TMP_FontAsset>(StringComparer.Ordinal);
            if (fonts != null)
                for (int i = 0; i < fonts.Count; i++)
                    if (fonts[i] != null && !fontByName.ContainsKey(fonts[i].name))
                        fontByName.Add(fonts[i].name, fonts[i]);
            var materialWanted = new HashSet<string>(_labelMaterials, StringComparer.Ordinal);
            var materialByName = new Dictionary<string, Material>(StringComparer.Ordinal);
            if (materials != null)
                for (int i = 0; i < materials.Count; i++)
                    if (materials[i] != null && materialWanted.Contains(materials[i].name) && !materialByName.ContainsKey(materials[i].name))
                        materialByName.Add(materials[i].name, materials[i]);
            for (int i = 0; i < _labels.Length && i < _labelFonts.Length; i++)
            {
                var label = _labels[i];
                if (label == null || !fontByName.TryGetValue(_labelFonts[i] ?? string.Empty, out var font))
                    continue;
                label.font = font;
                if (i < _labelMaterials.Length && materialByName.TryGetValue(_labelMaterials[i] ?? string.Empty, out var material))
                    label.fontSharedMaterial = material;
                else
                    label.fontSharedMaterial = font.material;
            }
            ApplyIcons();
        }

        /// <summary>Tests and captures: the portraits' catalog (else the one the scene loaded).</summary>
        public void SetPortraits(CodexVisualCatalog catalog)
        {
            _catalog = catalog;
            _catalogSearched = catalog != null;
        }

        public void Show()
        {
            Initialize();
            _parts.content.SetActive(true);
            _parts.canvas.enabled = true;
            _shown = true;
            if (_scaler != null)
            {
                _scaler.enabled = false;
                _scaler.enabled = true;
            }
            _fitter.Fit(_parts.safeArea);
            Canvas.ForceUpdateCanvases();
            if (_parts.sheet.rect.height > 1f)
                _sheetHeight = _parts.sheet.rect.height;
            ApplySheet(_sheetShown ? 1f : 0f);
            ApplyContent(_contentShown ? 1f : 0f);
            _parts.mapScroll?.StopMovement();
        }

        public void Hide()
        {
            Initialize();
            _shown = false;
            _noteT = -1f;
            if (_parts.notePill != null)
                _parts.notePill.gameObject.SetActive(false);
            _parts.paw?.Set(false, default);
            if (_parts.canvas != null)
                _parts.canvas.enabled = false;
            if (_parts.content != null)
                _parts.content.SetActive(false);
        }

        public void SetGems(long gems)
        {
            Initialize();
            _gemsLabel.SetNumber(gems);
        }

        public void SetSlot(int slot, in ExpeditionSlotModel model)
        {
            Initialize();
            if ((uint)slot >= (uint)_parts.slots.Length)
                return;
            var s = _parts.slots[slot];
            var state = model.State;
            bool hidden = state == ExpeditionSlotState.Locked && model.Unlock == ExpeditionSlotUnlock.None;
            SetActive(s.root.gameObject, !hidden);
            _slotState[slot] = state;
            _timeKey[slot] = long.MinValue;
            SetActive(s.empty, state == ExpeditionSlotState.Empty);
            SetActive(s.team, state == ExpeditionSlotState.Away || state == ExpeditionSlotState.Back);
            SetActive(s.away, state == ExpeditionSlotState.Away);
            SetActive(s.back, state == ExpeditionSlotState.Back);
            SetActive(s.locked, state == ExpeditionSlotState.Locked);
            var frame = state switch
            {
                ExpeditionSlotState.Empty => _slotEmpty,
                ExpeditionSlotState.Away => _slotAway,
                ExpeditionSlotState.Back => _slotBack,
                _ => _slotLocked,
            };
            if (frame != null)
            {
                s.frame.sprite = frame;
                s.frame.color = Color.white;
            }
            else
            {
                s.frame.color = state == ExpeditionSlotState.Locked ? _cardLocked : _card;
            }
            s.glow.enabled = state == ExpeditionSlotState.Back;
            if (state == ExpeditionSlotState.Away || state == ExpeditionSlotState.Back)
            {
                for (int k = 0; k < s.portraits.Length; k++)
                {
                    bool shown = k < model.TeamSize;
                    SetActive(s.portraits[k].gameObject, shown);
                    if (shown)
                        SetPortrait(s.portraits[k], model.VariantAt(k), model.SpeciesAt(k));
                }
                SetText(s.destination, model.Destination);
                for (int k = 0; k < s.stars.Length; k++)
                    Star(s.stars[k], k < model.Stars);
            }
            if (state == ExpeditionSlotState.Away)
            {
                _costLabels[slot].SetNumber(model.SpeedUpCost);
                bool can = model.SpeedUpsLeft > 0;
                s.speedUp.interactable = can;
                if (s.speedUpBack != null)
                    s.speedUpBack.color = can ? _accent : _disabled;
                if (_leftShown[slot] != model.SpeedUpsLeft)
                {
                    _leftShown[slot] = model.SpeedUpsLeft;
                    if (can)
                        _leftLabels[slot].SetTemplate(_speedUpsLeft.For(model.SpeedUpsLeft), model.SpeedUpsLeft);
                    else
                        _leftLabels[slot].SetString(_speedUpsNone);
                }
            }
            if (state == ExpeditionSlotState.Locked)
            {
                bool gems = model.Unlock == ExpeditionSlotUnlock.Gems;
                SetActive(s.price.gameObject, gems);
                if (s.priceGem != null)
                    SetActive(s.priceGem.gameObject, gems);
                if (gems)
                    _priceLabels[slot].SetNumber(model.Price);
                SetText(s.lockedLabel, gems ? _slotLockedText : _slotClubText);
            }
        }

        public void SetSlotTime(int slot, double secondsLeft, double totalSeconds)
        {
            if ((uint)slot >= (uint)_parts.slots.Length || _slotState[slot] != ExpeditionSlotState.Away)
                return;
            long key = ExpeditionTimeText.Key(secondsLeft);
            if (key == _timeKey[slot])
                return;
            _timeKey[slot] = key;
            var label = _timerLabels[slot];
            label.Commit(_time.WriteBackIn(label.Buffer, secondsLeft, true));
            var s = _parts.slots[slot];
            float done = totalSeconds > 0.0 ? 1f - (float)(secondsLeft / totalSeconds) : 1f;
            s.ringFill.fillAmount = Mathf.Clamp01(done);
        }

        public void PlayDeparture(int slot)
        {
            if ((uint)slot >= (uint)_parts.slots.Length || _reduceMotion)
                return;
            _dustT[slot] = 0f;
            foreach (var dot in _parts.slots[slot].dust)
                dot.enabled = true;
            ApplyDust(slot, 0f);
        }

        public void SetDestination(int card, in ExpeditionDestinationModel model)
        {
            Initialize();
            if ((uint)card >= (uint)_parts.destinations.Length)
                return;
            var d = _parts.destinations[card];
            SetActive(d.root.gameObject, model.Shown);
            if (!model.Shown)
            {
                LayoutRow(_parts.mapContent, ShownDestinations(), DestinationPitch);
                return;
            }
            var art = ArtOf(model.Key);
            // A biome not open yet: its own art dimmed with a lock, else EA's soft silhouette (its padlock drawn in).
            bool silhouette = art == null && !model.Open && _lockedDestination != null;
            if (silhouette)
                art = _lockedDestination;
            d.art.sprite = art;
            d.art.enabled = art != null;
            d.art.color = model.Open || silhouette ? Color.white : new Color(0.62f, 0.60f, 0.58f, 0.55f);
            if (d.frame != null)
                d.frame.color = model.Open ? _card : _cardLocked;
            SetActive(d.lockIcon.gameObject, !model.Open && !silhouette);
            SetText(d.name, model.Name);
            d.name.color = model.Open ? _text : _textSoft;
            SetActive(d.outline.gameObject, model.Selected);
            LayoutRow(_parts.mapContent, ShownDestinations(), DestinationPitch);
        }

        public void SetChallenge(int index, in ExpeditionChallengeModel model)
        {
            Initialize();
            if ((uint)index >= (uint)_parts.challenges.Length)
                return;
            var c = _parts.challenges[index];
            SetActive(c.root.gameObject, model.Shown);
            if (!model.Shown)
            {
                LayoutRow(_parts.challengeContent, ShownChallenges(), ChallengePitch);
                return;
            }
            SetText(c.name, model.Name);
            SetText(c.goal, model.Goal);
            for (int k = 0; k < c.stars.Length; k++)
                Star(c.stars[k], k < model.Stars);
            if (c.back != null)
                c.back.color = model.Selected ? _accent : _card;
            LayoutRow(_parts.challengeContent, ShownChallenges(), ChallengePitch);
        }

        public void SetClaimAll(bool shown)
        {
            Initialize();
            if (_parts.claimAll != null)
                SetActive(_parts.claimAll.gameObject, shown);
        }

        public void ShowNote(string text)
        {
            Initialize();
            if (string.IsNullOrEmpty(text))
                return;
            SetText(_parts.noteLabel, text);
            _noteT = 0f;
            _parts.noteGroup.alpha = 0f;
            _parts.notePill.gameObject.SetActive(true);
        }

        public void ShowSheet(bool shown)
        {
            Initialize();
            if (_sheetShown == shown)
                return;
            _sheetShown = shown;
            if (shown)
                _parts.sheet.gameObject.SetActive(true);
            if (!_shown)
                ApplySheet(shown ? 1f : 0f);
        }

        public void SetSheet(in ExpeditionSheetModel model)
        {
            Initialize();
            SetText(_parts.sheetTitle, model.Title);
            SetText(_parts.sheetDestination, model.Destination);
            bool note = !string.IsNullOrEmpty(model.Note);
            SetActive(_parts.note.gameObject, note);
            if (note)
                SetText(_parts.note, model.Note);
            _parts.go.interactable = model.CanGo;
            if (_parts.goBack != null)
                _parts.goBack.color = model.CanGo ? _accent : _disabled;
            if (_parts.suggested != null)
                SetActive(_parts.suggested.gameObject, true);
        }

        public void SetAnimal(int cell, in ExpeditionAnimalModel model)
        {
            Initialize();
            if ((uint)cell >= (uint)_parts.animals.Length)
                return;
            var a = _parts.animals[cell];
            SetActive(a.root.gameObject, model.Shown);
            if (!model.Shown)
            {
                LayoutRow(_parts.animalContent, ShownAnimals(), AnimalPitch);
                return;
            }
            SetPortrait(a.portrait, model.Variant, model.Species);
            a.ring.enabled = model.Picked;
            a.ring.color = _accent;
            a.check.enabled = model.Picked;
            a.leaf.enabled = model.FromBiome;
            a.leaf.color = _leaf;
            if (a.disc != null)
                a.disc.color = model.Picked ? new Color(1f, 0.93f, 0.82f, 1f) : _card;
            if (cell == _parts.animals.Length - 1 || !model.Shown)
                LayoutRow(_parts.animalContent, ShownAnimals(), AnimalPitch);
        }

        public void SetRating(in ExpeditionRatingModel model)
        {
            Initialize();
            for (int k = 0; k < _parts.ratingStars.Length; k++)
                Star(_parts.ratingStars[k], k < model.Stars);
            var r = _parts.reasons;
            bool biome = model.FromBiome > 0;
            SetActive(r[0].root.gameObject, biome);
            if (biome)
                _reasonBiomeLabel.SetTemplate(_reasonBiome.For(model.FromBiome), model.FromBiome);
            SetActive(r[1].root.gameObject, model.FavoriteFamily);
            SetActive(r[2].root.gameObject, model.ThreeSpecies);
            LayoutReasons();
        }

        public void SetDuration(int index, string label, bool shown, bool selected)
        {
            Initialize();
            if ((uint)index >= (uint)_parts.durations.Length)
                return;
            var chip = _parts.durations[index];
            SetActive(chip.root.gameObject, shown);
            if (!shown)
                return;
            SetText(chip.label, label);
            var back = selected && _chipSelected != null ? _chipSelected : _chip;
            if (back != null)
            {
                chip.back.sprite = back;
                chip.back.color = Color.white;
            }
            else
            {
                chip.back.color = selected ? _accent : _card;
            }
            if (chip.icon != null)
            {
                var icon = index < _durationIcons.Length ? _durationIcons[index] : null;
                chip.icon.sprite = icon;
                chip.icon.enabled = icon != null;
            }
            chip.label.color = selected ? _text : _textSoft;
            LayoutDurations();
        }

        public void SetLoot(in ExpeditionLoot preview)
        {
            Initialize();
            int n = 0;
            if (preview.LightMinutes > 0.0)
                LootLight(n++, preview.LightMinutes);
            if (preview.Treats > 0)
                Loot(n++, LootIcon.Treat, null, preview.Treats, true);
            int seeds = 0;
            for (int k = 0; k < preview.SeedKinds && k < 4; k++)
                seeds += preview.SeedsAt(k);
            if (seeds > 0)
                Loot(n++, LootIcon.Seed, null, seeds, true);
            if (preview.StickerChance > 0)
                Loot(n++, LootIcon.Sticker, _percent, preview.StickerChance, false);
            if (preview.DiscoveryChance > 0)
                Loot(n++, LootIcon.Egg, _percent, preview.DiscoveryChance, false);
            if (preview.Tickets > 0 && n < _parts.loot.Length)
                Loot(n++, LootIcon.Ticket, null, preview.Tickets, true);
            int ingredients = preview.Ingredients0 + preview.Ingredients1;
            if (ingredients > 0 && n < _parts.loot.Length)
                Loot(n++, LootIcon.Ingredient, null, ingredients, true);
            for (int i = 0; i < _parts.loot.Length; i++)
                SetActive(_parts.loot[i].root.gameObject, i < n);
            LayoutLoot(n);
        }

        /// <summary>Light as time of production: « 50 min », then « 2 h 24 » from an hour (« 144 min » would not fit its cell).</summary>
        private void LootLight(int index, double minutes)
        {
            long whole = (long)Math.Round(minutes);
            if (whole < 60)
            {
                Loot(index, LootIcon.Light, _lightMinutes, whole, false);
                return;
            }
            if ((uint)index >= (uint)_parts.loot.Length)
                return;
            Loot(index, LootIcon.Light, _lightMinutes, whole, false);
            var label = _lootLabels[index];
            label.Commit(_time.WriteTime(label.Buffer, 0, whole * 60.0));
        }

        private void Loot(int index, LootIcon icon, string template, long value, bool times)
        {
            if ((uint)index >= (uint)_parts.loot.Length)
                return;
            var item = _parts.loot[index];
            var sprite = IconOf(icon);
            item.icon.sprite = sprite;
            item.icon.enabled = sprite != null;
            item.icon.color = icon == LootIcon.Egg ? _eggTint : Color.white;
            var label = _lootLabels[index];
            if (times)
            {
                int i = UiChars.Char(label.Buffer, 0, UiChars.Times);
                label.Commit(UiChars.Int(label.Buffer, i, value));
            }
            else
            {
                label.Commit(ActivityChars.Fill(label.Buffer, 0, template, value));
            }
        }

        // ---------------- the welcome ----------------

        public void SetContentShown(bool shown)
        {
            Initialize();
            _contentShown = shown;
            _parts.contentGroup.blocksRaycasts = shown;
            _parts.contentGroup.interactable = shown;
            if (!_shown)
                ApplyContent(shown ? 1f : 0f);
        }

        public void SetDim(bool dimmed)
        {
            Initialize();
            _dimmed = dimmed;
            if (dimmed)
                _parts.dim.enabled = true;
        }

        public void ShowPostcard(int card, int count, string destination, float delay)
        {
            Initialize();
            if ((uint)card >= (uint)_parts.postcards.Length)
                return;
            var p = _parts.postcards[card];
            _cardShown[card] = true;
            _cardT[card] = -Mathf.Max(0f, delay);
            _cardCount = Mathf.Clamp(count, 1, _parts.postcards.Length);
            SetText(p.destination, destination);
            // The picture may have arrived during the walk (the renderer starts at once): it stays; HidePostcards clears it.
            bool picture = p.picture.texture != null;
            p.picture.enabled = picture;
            if (p.placeholder != null)
                p.placeholder.enabled = !picture;
            p.root.gameObject.SetActive(true);
            PlaceCard(card);
            ApplyCard(card, 0f);
        }

        public void SetPostcardPicture(int card, Texture picture)
        {
            if ((uint)card >= (uint)_parts.postcards.Length)
                return;
            var p = _parts.postcards[card];
            p.picture.texture = picture;
            p.picture.enabled = picture != null;
            if (p.placeholder != null)
                p.placeholder.enabled = picture == null;
        }

        public Texture PostcardPicture(int card) => _parts.postcards[card].picture.texture;

        public void DockPostcards(bool docked)
        {
            _docked = docked;
            if (_reduceMotion)
                _dockT = docked ? 1f : 0f;
        }

        public void HidePostcards()
        {
            Initialize();
            for (int i = 0; i < _parts.postcards.Length; i++)
            {
                _cardShown[i] = false;
                _parts.postcards[i].root.gameObject.SetActive(false);
                _parts.postcards[i].picture.texture = null;
                _parts.postcards[i].picture.enabled = false;
            }
            _docked = false;
            _dockT = 0f;
        }

        public bool PostcardShown(int card) =>
            (uint)card < (uint)_parts.postcards.Length && _cardShown != null && _cardShown[card] && _cardT[card] >= 0f;

        /// <summary>The card has pivoted all the way in (its 400 ms done).</summary>
        public bool PostcardSettled(int card) => PostcardShown(card) && _cardT[card] >= ExpeditionLayout.PostcardPivotSeconds;

        // ---------------- the paw ----------------

        public void SetPaw(bool visible, Vector2 screenPoint)
        {
            var paw = _parts.paw;
            if (paw == null)
                return;
            if (!visible)
            {
                paw.Set(false, default);
                return;
            }
            var parent = paw.transform.parent as RectTransform;
            if (parent != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, EventCamera, out var local))
                paw.SetLocal(local);
        }

        public Vector2 SlotPoint(int slot) => ScreenOf(_parts.slots[Mathf.Clamp(slot, 0, _parts.slots.Length - 1)].root);

        public Vector2 DestinationPoint(int card) => ScreenOf(_parts.destinations[Mathf.Clamp(card, 0, _parts.destinations.Length - 1)].root);

        public Vector2 AnimalPoint(int cell) => ScreenOf(_parts.animals[Mathf.Clamp(cell, 0, _parts.animals.Length - 1)].root);

        public Vector2 GoPoint => ScreenOf((RectTransform)_parts.go.transform);

        private Camera EventCamera => _parts.canvas != null && _parts.canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _parts.canvas.worldCamera : null;

        private Vector2 ScreenOf(RectTransform rect)
        {
            if (rect == null)
                return Vector2.zero;
            var world = rect.TransformPoint(rect.rect.center);
            return RectTransformUtility.WorldToScreenPoint(EventCamera, world);
        }

        // ---------------- Tick ----------------

        public void Tick(float deltaTime)
        {
            if (!_shown)
                return;
            float dt = Mathf.Max(0f, deltaTime);
            // The sheet slides up (a fade with Reduce Motion).
            float sheetTarget = _sheetShown ? 1f : 0f;
            if (_sheetT != sheetTarget)
                ApplySheet(_reduceMotion ? Mathf.MoveTowards(_sheetT, sheetTarget, dt / 0.15f) : Mathf.MoveTowards(_sheetT, sheetTarget, dt / ExpeditionLayout.SheetSeconds));
            float contentTarget = _contentShown ? 1f : 0f;
            if (_contentT != contentTarget)
                ApplyContent(Mathf.MoveTowards(_contentT, contentTarget, dt / ExpeditionLayout.FadeSeconds));
            float dimTarget = _dimmed ? 1f : 0f;
            if (_dimT != dimTarget)
            {
                _dimT = Mathf.MoveTowards(_dimT, dimTarget, dt / 0.3f);
                _parts.dim.color = new Color(0.11f, 0.13f, 0.12f, 0.45f * _dimT);
                _parts.dim.enabled = _dimT > 0f;
            }
            // The slots back glow softly (steady with Reduce Motion).
            _glowT += dt;
            float glow = _reduceMotion ? 0.9f : 0.7f + 0.3f * Mathf.Sin(_glowT * Mathf.PI * 2f / GlowPeriod);
            for (int i = 0; i < _parts.slots.Length; i++)
            {
                var s = _parts.slots[i];
                if (s.glow.enabled)
                    s.glow.color = new Color(_gold.r, _gold.g, _gold.b, glow);
                if (_dustT[i] >= 0f)
                {
                    _dustT[i] += dt;
                    ApplyDust(i, _dustT[i]);
                }
            }
            // The postcards pivot in, their ring and petals; then dock out of the result's way.
            float dockTarget = _docked ? 1f : 0f;
            if (_dockT != dockTarget)
                _dockT = Mathf.MoveTowards(_dockT, dockTarget, dt / 0.35f);
            for (int i = 0; i < _parts.postcards.Length; i++)
            {
                if (!_cardShown[i])
                    continue;
                _cardT[i] += dt;
                ApplyCard(i, _cardT[i]);
            }
            TickNote(dt);
            _parts.paw?.Tick(dt);
        }

        private void TickNote(float dt)
        {
            if (_noteT < 0f)
                return;
            _noteT += dt;
            float total = NoteIn + NoteHold + NoteOut;
            if (_noteT >= total)
            {
                _noteT = -1f;
                _parts.notePill.gameObject.SetActive(false);
                return;
            }
            _parts.noteGroup.alpha = Mathf.Min(Mathf.Clamp01(_noteT / NoteIn), Mathf.Clamp01((total - _noteT) / NoteOut));
        }

        // ---------------- motion ----------------

        private void ApplySheet(float t)
        {
            _sheetT = t;
            var sheet = _parts.sheet;
            float eased = 1f - (1f - t) * (1f - t);
            if (_reduceMotion)
            {
                sheet.anchoredPosition = _sheetHome;
                _parts.sheetGroup.alpha = t;
            }
            else
            {
                sheet.anchoredPosition = _sheetHome + new Vector2(0f, -(1f - eased) * (_sheetHeight + 40f));
                _parts.sheetGroup.alpha = 1f;
            }
            _parts.sheetGroup.blocksRaycasts = _sheetShown;
            _parts.sheetGroup.interactable = _sheetShown;
            if (t <= 0f && !_sheetShown)
                sheet.gameObject.SetActive(false);
        }

        private void ApplyContent(float t)
        {
            _contentT = t;
            _parts.contentGroup.alpha = t;
        }

        private void ApplyDust(int slot, float t)
        {
            var dots = _parts.slots[slot].dust;
            float k = Mathf.Clamp01(t / DustSeconds);
            for (int i = 0; i < dots.Length; i++)
            {
                var dot = dots[i];
                float angle = (i - (dots.Length - 1) * 0.5f) * 0.55f;
                var dir = new Vector2(Mathf.Sin(angle), 0.35f);
                dot.rectTransform.anchoredPosition = DustHome(i, dots.Length) + dir * (40f * (1f - (1f - k) * (1f - k)));
                float s = 0.6f + 0.6f * k;
                dot.rectTransform.localScale = new Vector3(s, s, 1f);
                dot.color = new Color(0.86f, 0.78f, 0.64f, 0.8f * (1f - k));
                if (k >= 1f)
                    dot.enabled = false;
            }
            if (k >= 1f)
                _dustT[slot] = -1f;
        }

        private static Vector2 DustHome(int i, int count) => new Vector2((i - (count - 1) * 0.5f) * 60f, -20f);

        /// <summary>A postcard's place: centred, or fanned for « Tout réclamer »; docked above the result card.</summary>
        private void PlaceCard(int card)
        {
            var root = _parts.postcards[card].root;
            float offset = card - (_cardCount - 1) * 0.5f;
            root.anchoredPosition = new Vector2(offset * 70f, 0f);
            root.localRotation = Quaternion.Euler(0f, 0f, -offset * 4f);
        }

        private void ApplyCard(int card, float t)
        {
            var p = _parts.postcards[card];
            float offset = card - (_cardCount - 1) * 0.5f;
            float dock = UiCurves.EaseOut(_dockT);
            p.root.anchoredPosition = new Vector2(offset * 70f * (1f - 0.5f * dock), Mathf.Lerp(0f, 640f, dock));
            float dockScale = Mathf.Lerp(1f, 0.5f, dock);
            if (t < 0f)
            {
                p.group.alpha = 0f;
                return;
            }
            float pivot = ExpeditionLayout.PostcardPivotSeconds;
            if (_reduceMotion)
            {
                // Doc 05 sheet 23, Reduce Motion: the card fades in, no pivot, no petals.
                p.group.alpha = Mathf.Clamp01(t / 0.15f);
                p.card.localRotation = Quaternion.identity;
                p.card.localScale = new Vector3(dockScale, dockScale, 1f);
                p.ring.enabled = false;
                foreach (var petal in p.petals)
                    petal.enabled = false;
                return;
            }
            float k = Mathf.Clamp01(t / pivot);
            float eased = 1f - Mathf.Pow(1f - k, 3f);
            p.group.alpha = Mathf.Clamp01(t / 0.08f);
            p.card.localRotation = Quaternion.Euler(0f, 90f * (1f - eased), 0f);
            float pop = k < 1f ? 0.92f + 0.08f * eased : 1f;
            p.card.localScale = new Vector3(pop * dockScale, pop * dockScale, 1f);
            // H-VFX-ACT-005-02: a soft ring spreads from behind the card, 6 round petals drift outwards and fade.
            float ringT = Mathf.Clamp01((t - pivot * 0.5f) / RingSeconds);
            p.ring.enabled = ringT > 0f && ringT < 1f && dock < 0.01f;
            if (p.ring.enabled)
            {
                float rs = Mathf.Lerp(0.7f, 1.35f, ringT);
                p.ring.rectTransform.localScale = new Vector3(rs, rs, 1f);
                p.ring.color = new Color(1f, 0.96f, 0.86f, 0.75f * (1f - ringT));
            }
            float petalT = Mathf.Clamp01((t - pivot * 0.6f) / PetalSeconds);
            for (int i = 0; i < p.petals.Length; i++)
            {
                var petal = p.petals[i];
                bool on = petalT > 0f && petalT < 1f && dock < 0.01f;
                petal.enabled = on;
                if (!on)
                    continue;
                float angle = (i / (float)p.petals.Length) * Mathf.PI * 2f + 0.4f;
                float distance = Mathf.Lerp(260f, 470f, 1f - (1f - petalT) * (1f - petalT));
                petal.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance * 1.3f - 40f * petalT);
                petal.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg + petalT * 90f);
                var c = petal.color;
                c.a = petalT < 0.2f ? petalT / 0.2f : 1f - (petalT - 0.2f) / 0.8f;
                petal.color = c;
            }
        }

        // ---------------- layout helpers ----------------

        /// <summary>A horizontal row's content as wide as its shown views (the first ones: the presenter fills them in order).</summary>
        private static void LayoutRow(RectTransform content, int shown, float pitch)
        {
            if (content == null)
                return;
            float width = shown * pitch + RowPadding * 2f;
            if (Mathf.Abs(content.sizeDelta.x - width) > 0.5f)
                content.sizeDelta = new Vector2(width, content.sizeDelta.y);
        }

        private int ShownDestinations()
        {
            int n = 0;
            foreach (var d in _parts.destinations)
                if (d.root.gameObject.activeSelf)
                    n++;
            return n;
        }

        private int ShownAnimals()
        {
            int n = 0;
            foreach (var a in _parts.animals)
                if (a.root.gameObject.activeSelf)
                    n++;
            return n;
        }

        private int ShownChallenges()
        {
            int n = 0;
            foreach (var c in _parts.challenges)
                if (c.root.gameObject.activeSelf)
                    n++;
            return n;
        }

        /// <summary>The reasons' chips as wide as their words, side by side, a second row when the first is full (on a change only).</summary>
        private void LayoutReasons()
        {
            var row = _parts.reasons[0].root.parent as RectTransform;
            float limit = row != null && row.rect.width > 1f ? row.rect.width : 960f;
            float x = 0f, y = 0f;
            foreach (var r in _parts.reasons)
            {
                if (!r.root.gameObject.activeSelf)
                    continue;
                float text = r.label != null ? Mathf.Ceil(r.label.preferredWidth) + 4f : 200f;
                if (r.label != null)
                    r.label.rectTransform.sizeDelta = new Vector2(text, r.label.rectTransform.sizeDelta.y);
                float width = ReasonTextLeft + text + ReasonPadding;
                if (x > 0f && x + width > limit)
                {
                    x = 0f;
                    y -= ReasonRowPitch;
                }
                r.root.sizeDelta = new Vector2(width, r.root.sizeDelta.y);
                r.root.anchoredPosition = new Vector2(x, y);
                x += width + 18f;
            }
        }

        /// <summary>The reason chip: the icon, then the words from this x, then this margin (ExpeditionPipeline builds the same).</summary>
        public const float ReasonTextLeft = 64f;

        public const float ReasonPadding = 24f;
        public const float ReasonRowPitch = 72f;

        private void LayoutDurations()
        {
            int shown = 0;
            foreach (var chip in _parts.durations)
                if (chip.root.gameObject.activeSelf)
                    shown++;
            float width = shown == 1 ? FirstStrollChip : 240f;
            float pitch = 252f;
            int i = 0;
            foreach (var chip in _parts.durations)
            {
                if (!chip.root.gameObject.activeSelf)
                    continue;
                chip.root.sizeDelta = new Vector2(width, chip.root.sizeDelta.y);
                chip.root.anchoredPosition = new Vector2((i - (shown - 1) * 0.5f) * pitch, chip.root.anchoredPosition.y);
                i++;
            }
        }

        private void LayoutLoot(int count)
        {
            float pitch = LootPitch;
            for (int i = 0; i < count && i < _parts.loot.Length; i++)
            {
                var root = _parts.loot[i].root;
                root.anchoredPosition = new Vector2((i - (count - 1) * 0.5f) * pitch, root.anchoredPosition.y);
            }
        }

        // ---------------- content helpers ----------------

        private Sprite ArtOf(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;
            int dot = key.LastIndexOf('.');
            int start = dot + 1;
            int length = key.Length - start;
            for (int i = 0; i < _artNames.Length && i < _arts.Length; i++)
            {
                string name = _artNames[i];
                if (name == null || name.Length != 4 + length || !name.StartsWith("EXP_", StringComparison.Ordinal))
                    continue;
                if (string.CompareOrdinal(name, 4, key, start, length) == 0)
                    return _arts[i];
            }
            return null;
        }

        private void SetPortrait(Image image, uint variant, uint species)
        {
            EnsureCatalog();
            Sprite sprite = null;
            if (_catalog != null && (!_catalog.TryGetVariant(new ContentId(variant), out sprite) || sprite == null))
                _catalog.TryGetSilhouette(new ContentId(species), out sprite);
            image.sprite = sprite;
            image.enabled = sprite != null;
        }

        private void EnsureCatalog()
        {
            if (_catalog != null || _catalogSearched)
                return;
            _catalogSearched = true;
            var found = Resources.FindObjectsOfTypeAll<CodexVisualCatalog>();
            int best = -1;
            for (int i = 0; i < found.Length; i++)
            {
                int count = found[i].variants != null ? found[i].variants.Count : 0;
                if (count > best)
                {
                    best = count;
                    _catalog = found[i];
                }
            }
            if (_catalog == null)
                GameLog.Warning(LogCategory.Presentation, "Expeditions: no codex catalog loaded (DEF_CodexVisuals): the team shows no portraits.");
        }

        private string _slotLockedText;
        private string _slotClubText;

        private void BindTexts(ILocalization text)
        {
            if (text == null)
                return;
            _time.Bind(text);
            _speedUpsLeft = UiCounted.From(text, ExpeditionText.SpeedUpsLeft);
            _reasonBiome = UiCounted.From(text, ExpeditionText.ReasonBiome);
            _speedUpsNone = text.Get(ExpeditionText.SpeedUpsNone);
            _lightMinutes = text.Get(DailyVisitText.RewardLightMinutes);
            _percent = text.Get(ExpeditionText.Percent);
            _slotLockedText = text.Get(ExpeditionText.SlotLocked);
            _slotClubText = text.Get(ExpeditionText.SlotClub);
            var p = _parts;
            SetText(p.title, text.Get(ExpeditionText.Title));
            foreach (var s in p.slots)
            {
                SetText(s.emptyLabel, text.Get(ExpeditionText.PickTeam));
                SetText(s.speedUpLabel, text.Get(ExpeditionText.SpeedUp));
                SetText(s.backLabel, text.Get(ExpeditionText.OpenPostcard));
                SetText(s.lockedLabel, _slotLockedText);
            }
            SetText(p.claimAllLabel, text.Get(ExpeditionText.ClaimAll));
            SetText(p.challengesTitle, text.Get(ExpeditionText.Challenges));
            SetText(p.suggestedLabel, text.Get(ExpeditionText.Suggested));
            SetText(p.ratingLabel, text.Get(ExpeditionText.Rating));
            SetText(p.durationTitle, text.Get(ExpeditionText.Duration));
            SetText(p.lootTitle, text.Get(ExpeditionText.Loot));
            SetText(p.goLabel, text.Get(ExpeditionText.Go));
            SetText(p.reasons[1].label, text.Get(ExpeditionText.ReasonFamily));
            SetText(p.reasons[2].label, text.Get(ExpeditionText.ReasonSpecies));
            for (int i = 0; i < _leftShown.Length; i++)
                _leftShown[i] = -1;
        }

        private void ApplyIcons()
        {
            if (_icons == null)
                return;
            var p = _parts;
            foreach (var s in p.slots)
            {
                SetSprite(s.speedUpGem, IconOf(LootIcon.Gems));
                SetSprite(s.priceGem, IconOf(LootIcon.Gems));
                SetSprite(s.lockIcon, IconOf(LootIcon.Lock));
                SetSprite(s.backIcon, IconOf(LootIcon.Postcard));
                foreach (var star in s.stars)
                    SetSprite(star, IconOf(LootIcon.Star));
            }
            SetSprite(p.gemsIcon, IconOf(LootIcon.Gems));
            foreach (var d in p.destinations)
                SetSprite(d.lockIcon, IconOf(LootIcon.Lock));
            foreach (var a in p.animals)
            {
                SetSprite(a.check, IconOf(LootIcon.Check));
                SetSprite(a.leaf, IconOf(LootIcon.Leaf));
            }
            foreach (var star in p.ratingStars)
                SetSprite(star, IconOf(LootIcon.Star));
            SetSprite(p.reasons[0].icon, IconOf(LootIcon.Leaf));
            SetSprite(p.reasons[1].icon, IconOf(LootIcon.Paw));
            SetSprite(p.reasons[2].icon, IconOf(LootIcon.Dots));
            foreach (var c in p.challenges)
                foreach (var star in c.stars)
                    SetSprite(star, IconOf(LootIcon.Star));
        }

        /// <summary>A star lit (UI_Star_Result_Full) or not (UI_Star_Result_Empty; else the full one, tinted soft).</summary>
        private void Star(Image star, bool on)
        {
            var off = IconOf(LootIcon.StarOff);
            var full = IconOf(LootIcon.Star);
            var sprite = on || off == null ? full : off;
            if (sprite != null && star.sprite != sprite)
                star.sprite = sprite;
            star.color = on ? (full != null && off != null ? Color.white : _gold) : off != null ? Color.white : _starOff;
        }

        private static void SetSprite(Image image, Sprite sprite)
        {
            if (image == null || sprite == null || image.sprite != null && image.sprite == sprite)
                return;
            image.sprite = sprite;
        }

        private void Initialize()
        {
            if (_initialized)
                return;
            _initialized = true;
            _scaler = GetComponent<CanvasScaler>();
            var p = _parts;
            int slots = p.slots.Length;
            _slotState = new ExpeditionSlotState[slots];
            _timeKey = new long[slots];
            _leftShown = new int[slots];
            _dustT = new float[slots];
            _timerLabels = new UiLabel[slots];
            _costLabels = new UiLabel[slots];
            _leftLabels = new UiLabel[slots];
            _priceLabels = new UiLabel[slots];
            for (int i = 0; i < slots; i++)
            {
                var s = p.slots[i];
                _timeKey[i] = long.MinValue;
                _leftShown[i] = -1;
                _dustT[i] = -1f;
                _timerLabels[i] = new UiLabel(s.timer, 96);
                _costLabels[i] = new UiLabel(s.speedUpCost, 24);
                _leftLabels[i] = new UiLabel(s.speedUpsLeft, 64);
                _priceLabels[i] = new UiLabel(s.price, 24);
                foreach (var dot in s.dust)
                    if (dot != null)
                        dot.enabled = false;
                int slot = i;
                s.button?.onClick.AddListener(() => _actions?.TapSlot(slot));
                s.speedUp?.onClick.AddListener(() => _actions?.SpeedUp(slot));
            }
            _gemsLabel = new UiLabel(p.gems, 24);
            _lootLabels = new UiLabel[p.loot.Length];
            for (int i = 0; i < p.loot.Length; i++)
                _lootLabels[i] = new UiLabel(p.loot[i].amount, 32);
            _reasonBiomeLabel = new UiLabel(p.reasons.Length > 0 ? p.reasons[0].label : null, 48);
            for (int i = 0; i < p.destinations.Length; i++)
            {
                int card = i;
                p.destinations[i].button?.onClick.AddListener(() => _actions?.TapDestination(card));
            }
            for (int i = 0; i < p.animals.Length; i++)
            {
                int cell = i;
                p.animals[i].button?.onClick.AddListener(() => _actions?.TapAnimal(cell));
            }
            for (int i = 0; i < p.durations.Length; i++)
            {
                int duration = i;
                p.durations[i].button?.onClick.AddListener(() => _actions?.PickDuration(duration));
            }
            for (int i = 0; i < p.challenges.Length; i++)
            {
                int row = i;
                p.challenges[i].button?.onClick.AddListener(() => _actions?.TapChallenge(row));
            }
            p.close?.onClick.AddListener(() => _actions?.CloseScreen());
            p.claimAll?.onClick.AddListener(() => _actions?.ClaimAll());
            p.sheetClose?.onClick.AddListener(() => _actions?.CloseSheet());
            p.suggested?.onClick.AddListener(() => _actions?.SuggestTeam());
            p.go?.onClick.AddListener(() => _actions?.Go());
            _cardShown = new bool[p.postcards.Length];
            _cardT = new float[p.postcards.Length];
            foreach (var card in p.postcards)
            {
                card.ring.enabled = false;
                foreach (var petal in card.petals)
                    petal.enabled = false;
                card.root.gameObject.SetActive(false);
            }
            p.dim.enabled = false;
            p.dim.color = new Color(0.11f, 0.13f, 0.12f, 0f);
            p.notePill.gameObject.SetActive(false);
            _sheetHome = p.sheet.anchoredPosition;
            p.sheet.gameObject.SetActive(false);
            _sheetT = 0f;
            if (p.paw != null)
                p.paw.ReduceMotion = _reduceMotion;
        }

        private void OnDestroy()
        {
            if (!_initialized || _parts == null)
                return;
            foreach (var s in _parts.slots)
            {
                s.button?.onClick.RemoveAllListeners();
                s.speedUp?.onClick.RemoveAllListeners();
            }
            foreach (var d in _parts.destinations)
                d.button?.onClick.RemoveAllListeners();
            foreach (var a in _parts.animals)
                a.button?.onClick.RemoveAllListeners();
            foreach (var c in _parts.durations)
                c.button?.onClick.RemoveAllListeners();
            foreach (var c in _parts.challenges)
                c.button?.onClick.RemoveAllListeners();
            _parts.close?.onClick.RemoveAllListeners();
            _parts.claimAll?.onClick.RemoveAllListeners();
            _parts.sheetClose?.onClick.RemoveAllListeners();
            _parts.suggested?.onClick.RemoveAllListeners();
            _parts.go?.onClick.RemoveAllListeners();
        }

        private static void SetText(TMP_Text label, string text)
        {
            if (label == null)
                return;
            text ??= string.Empty;
            if (!string.Equals(label.text, text, StringComparison.Ordinal))
                label.text = text;
        }

        private static void SetActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active)
                go.SetActive(active);
        }

        // ---------------- prefab builder hooks (Editor ExpeditionPipeline) ----------------

        public void SetParts(ExpeditionScreenParts parts) => _parts = parts;

        public void SetArt(Sprite slotEmpty, Sprite slotAway, Sprite slotBack, Sprite slotLocked, Sprite lockedDestination, Sprite chip,
            Sprite chipSelected, Sprite[] durationIcons, string[] artNames, Sprite[] arts)
        {
            _slotEmpty = slotEmpty;
            _slotAway = slotAway;
            _slotBack = slotBack;
            _slotLocked = slotLocked;
            _lockedDestination = lockedDestination;
            _chip = chip;
            _chipSelected = chipSelected;
            _durationIcons = durationIcons ?? Array.Empty<Sprite>();
            _artNames = artNames ?? Array.Empty<string>();
            _arts = arts ?? Array.Empty<Sprite>();
        }

        /// <summary>What the screen borrows from the scene at run time: kit sprites by image, fonts by label, icons by role.</summary>
        public void SetBorrowed(Image[] kitImages, string[] kitNames, TMP_Text[] labels, string[] labelFonts, string[] labelMaterials, string[] iconNames,
            Sprite[] ownIcons)
        {
            _kitImages = kitImages ?? Array.Empty<Image>();
            _kitNames = kitNames ?? Array.Empty<string>();
            _labels = labels ?? Array.Empty<TMP_Text>();
            _labelFonts = labelFonts ?? Array.Empty<string>();
            _labelMaterials = labelMaterials ?? Array.Empty<string>();
            _iconNames = iconNames ?? Array.Empty<string>();
            _ownIcons = ownIcons ?? Array.Empty<Sprite>();
        }

        public void SetColors(Color cream, Color card, Color cardLocked, Color text, Color textSoft, Color accent, Color gold, Color starOff, Color leaf,
            Color disabled)
        {
            _cream = cream;
            _card = card;
            _cardLocked = cardLocked;
            _text = text;
            _textSoft = textSoft;
            _accent = accent;
            _gold = gold;
            _starOff = starOff;
            _leaf = leaf;
            _disabled = disabled;
        }

        /// <summary>The names the pipeline asked to borrow (tests: nothing of the scene is referenced, everything is named).</summary>
        public IReadOnlyList<string> BorrowedNames => _kitNames;

        public IReadOnlyList<TMP_Text> BorrowedLabels => _labels;
    }
}
