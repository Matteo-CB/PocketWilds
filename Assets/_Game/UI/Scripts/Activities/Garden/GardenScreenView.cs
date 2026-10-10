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
    /// PF_UI_Garden (T43 « Jardin », doc 01 Contenu section 8 and S-ACT-006, doc 05 sheet 24; built by GardenScreenPipeline,
    /// Addressables group Activity_Garden). A screen-space canvas over the garden set (the set camera draws the beds behind
    /// it, the refuge HUD steps aside): the top bar (« Jardin », the level pill with its progress, the Gems, close); one
    /// floating label per bed, placed where the set camera sees the bed (« Planter », « Prêt dans 25 min », « Prêt à
    /// récolter », a drop when watered, two small green hearts when the neighbours help, the cover's sign « Nouvelle
    /// parcelle » with its 400 Gems or « Avec le Wild Club »); the bottom tools (the watering can, « Tout récolter »,
    /// « Finir maintenant » with its Gems and the speed-ups left) and the « Défis » row; the seed sheet (the packets with
    /// their counts, the locked plants with « Jardin niveau {n} » or their biome); the harvest's product chips counting up;
    /// the level-up banner; a note pill; the tutorial paw. Reduce Motion: fades instead of slides and pops. The fonts, the UI
    /// kit and the scene's icons are borrowed from what the scene already loaded (<see cref="Lend"/>); the garden's own
    /// sprites (packets, products, dyes, can, pill, label, sign) are in its bundle. Nothing allocates per frame after
    /// <see cref="Bind"/>: labels are written from reused char buffers only when their characters change.
    /// </summary>
    public sealed class GardenScreenView : MonoBehaviour, IGardenScreenView
    {
        /// <summary>The overlay's role icons (borrowed by name, or the bundle's own).</summary>
        public enum Icon
        {
            Gems = 0,
            Star = 1,
            StarOff = 2,
            Lock = 3,
            Heart = 4,
            Drop = 5,
            Can = 6,
            Seed = 7,
            Check = 8,
        }

        private const float NoteIn = 0.15f;
        private const float NoteHold = 2f;
        private const float NoteOut = 0.25f;
        private const float BannerIn = 0.25f;
        private const float BannerOut = 0.4f;
        private const float RingPeriod = 1.2f;

        /// <summary>The seed sheet's grid (GardenScreenPipeline places the packets on it from the content's top left).</summary>
        public const int PacketColumns = 4;

        public const float PacketPitchX = 250f;
        public const float PacketPitchY = 340f;
        public const float ChallengePitch = 420f;
        public const float ProductPitch = 214f;
        public const float RowPadding = 20f;

        [SerializeField] private GardenScreenParts _parts = new GardenScreenParts();

        [Header("Agent GA's sprites (in the bundle)")]
        [SerializeField] private Sprite _levelPillSprite;
        [SerializeField] private Sprite _bedLabelSprite;
        [SerializeField] private Sprite _coverSignSprite;

        [Header("Borrowed at run time from the scene (never in the bundle)")]
        [SerializeField] private Image[] _kitImages = Array.Empty<Image>();
        [SerializeField] private string[] _kitNames = Array.Empty<string>();
        [SerializeField] private TMP_Text[] _labels = Array.Empty<TMP_Text>();
        [SerializeField] private string[] _labelFonts = Array.Empty<string>();
        [SerializeField] private string[] _labelMaterials = Array.Empty<string>();

        /// <summary>One name per <see cref="Icon"/>, a second choice after « | » (an own sprite of the bundle wins).</summary>
        [SerializeField] private string[] _iconNames = Array.Empty<string>();

        [SerializeField] private Sprite[] _ownIcons = Array.Empty<Sprite>();

        [Header("Items: seeds (plant ids), products and dyes, with their sprites")]
        [SerializeField] private string[] _itemIds = Array.Empty<string>();
        [SerializeField] private string[] _itemIconNames = Array.Empty<string>();
        [SerializeField] private Sprite[] _itemOwn = Array.Empty<Sprite>();

        [Header("Colours")]
        [SerializeField] private Color _card = new Color(1f, 0.99f, 0.96f, 1f);
        [SerializeField] private Color _cardLocked = new Color(0.93f, 0.90f, 0.85f, 1f);
        [SerializeField] private Color _text = new Color(0.29f, 0.23f, 0.17f, 1f);
        [SerializeField] private Color _textSoft = new Color(0.54f, 0.46f, 0.38f, 1f);
        [SerializeField] private Color _accent = new Color(1f, 0.69f, 0.37f, 1f);
        [SerializeField] private Color _gold = new Color(0.949f, 0.710f, 0.267f, 1f);
        [SerializeField] private Color _starOff = new Color(0.89f, 0.86f, 0.81f, 1f);
        [SerializeField] private Color _leaf = new Color(0.47f, 0.72f, 0.38f, 1f);
        [SerializeField] private Color _water = new Color(0.45f, 0.70f, 0.92f, 1f);
        [SerializeField] private Color _disabled = new Color(0.80f, 0.77f, 0.72f, 1f);

        private readonly SafeAreaFitter _fitter = new SafeAreaFitter();
        private readonly GardenTimeText _time = new GardenTimeText();
        private IGardenScreenActions _actions;
        private CanvasScaler _scaler;
        private Sprite[] _icons;
        private readonly Dictionary<uint, Sprite> _itemSprites = new Dictionary<uint, Sprite>();
        private bool _lent;
        private bool _initialized;
        private bool _shown;
        private bool _reduceMotion;

        // Templates read at Bind.
        private string _levelTemplate = "{n}";
        private string _plantText;
        private string _readyText;
        private string _newPlotText;
        private string _clubText;
        private string _progressTemplate = "{n}/{total}";
        private string _speedUpsNone;
        private UiCounted _speedUpsLeft;

        // Labels from char buffers.
        private UiLabel _gemsLabel;
        private UiLabel _levelLabel;
        private UiLabel[] _stateLabels;
        private UiLabel[] _priceLabels;
        private UiLabel[] _countLabels;
        private UiLabel[] _progressLabels;
        private UiLabel[] _amountLabels;
        private UiLabel _costLabel;
        private UiLabel _leftLabel;
        private UiLabel _bannerLabel;
        private int _leftShown = -1;

        // Beds.
        private GardenPlotState[] _bedState;
        private bool[] _bedHidden;
        private bool[] _bedVisible;
        private long[] _timeKey;

        // The seed sheet.
        private bool _seedsShown;
        private float _sheetT;
        private float _sheetHeight = 1100f;
        private Vector2 _sheetHome;

        // The can's ring (armed).
        private bool _canArmed;
        private float _ringT;

        // Product chips.
        private uint[] _productItem;
        private int[] _productFrom;
        private int[] _productTo;
        private float[] _productT;
        private int _productOrder;
        private int[] _productAge;

        // The banner and the note.
        private float _bannerT = -1f;
        private float _noteT = -1f;

        // ---------------- IGardenScreenView ----------------

        public bool Valid
        {
            get
            {
                var p = _parts;
                if (p == null || p.canvas == null || p.content == null || p.labelLayer == null || p.sheet == null || p.sheetGroup == null
                    || p.notePill == null || p.noteGroup == null || p.noteLabel == null || p.banner == null || p.bannerGroup == null
                    || p.levelLabel == null || p.levelFill == null || p.can == null || p.harvestAll == null || p.speedUp == null
                    || p.productRow == null || p.packetContent == null || p.challengeContent == null)
                    return false;
                if (p.beds.Length < GardenLayout.PlotCount || p.packets.Length < GardenScreenLayout.PacketCapacity
                    || p.challenges.Length < GardenScreenLayout.ChallengeCapacity || p.products.Length < GardenScreenLayout.ProductCapacity)
                    return false;
                foreach (var b in p.beds)
                    if (b == null || b.root == null || b.group == null || b.owned == null || b.state == null || b.drop == null || b.heart0 == null
                        || b.heart1 == null || b.locked == null || b.signLabel == null || b.price == null || b.outline == null)
                        return false;
                foreach (var k in p.packets)
                    if (k == null || k.root == null || k.button == null || k.icon == null || k.name == null || k.count == null || k.lockLabel == null)
                        return false;
                foreach (var c in p.challenges)
                    if (c == null || c.root == null || c.button == null || c.name == null || c.goal == null || c.progress == null || c.stars.Length < 3)
                        return false;
                foreach (var d in p.products)
                    if (d == null || d.root == null || d.group == null || d.icon == null || d.amount == null)
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

        public GardenScreenParts Parts => _parts;

        public bool SeedsShown => _seedsShown;

        public bool PawShown => _parts.paw != null && _parts.paw.Visible;

        public bool NoteShown => _noteT >= 0f;

        public string NoteText => _parts.noteLabel.text;

        public bool BannerShown => _bannerT >= 0f;

        public string BannerTitle => _parts.bannerTitle != null ? _parts.bannerTitle.text : string.Empty;

        public string BedText(int plot) => _parts.beds[plot].state.text;

        public bool BedShown(int plot) => _parts.beds[plot].root.gameObject.activeSelf;

        public bool BedDrop(int plot) => _parts.beds[plot].drop.enabled;

        public bool BedHearts(int plot) => _parts.beds[plot].heart0.enabled;

        public bool BedLocked(int plot) => _parts.beds[plot].locked.activeSelf;

        public string LevelText => _parts.levelLabel.text;

        /// <summary>A product chip showing (tests: the harvest's products counted up).</summary>
        public int ProductsShown
        {
            get
            {
                int n = 0;
                if (_productT == null)
                    return 0;
                for (int i = 0; i < _productT.Length; i++)
                    if (_productT[i] >= 0f)
                        n++;
                return n;
            }
        }

        public string ProductText(int chip) => _parts.products[chip].amount.text;

        /// <summary>Agent GA's pill, label and sign were found when the prefab was built (else the kit's plain shapes).</summary>
        public bool HasOwnArt => _levelPillSprite != null || _bedLabelSprite != null || _coverSignSprite != null;

        public Sprite IconOf(Icon icon) => _icons != null && (int)icon < _icons.Length ? _icons[(int)icon] : null;

        /// <summary>The sprite of a seed, product or dye (null: not drawn yet; the role icon stands in).</summary>
        public Sprite ItemSprite(ContentId item) => _itemSprites.TryGetValue(item.Value, out var sprite) ? sprite : null;

        public void Bind(ILocalization text, IGardenScreenActions actions)
        {
            Initialize();
            _actions = actions;
            if (!_lent)
                Lend(Resources.FindObjectsOfTypeAll<Sprite>(), Resources.FindObjectsOfTypeAll<TMP_FontAsset>(), Resources.FindObjectsOfTypeAll<Material>());
            BindTexts(text);
        }

        /// <summary>
        /// Borrows the scene's sprites (the UI kit, the icons), fonts and font materials by name: the bundle never holds a
        /// copy. Captures and tests lend what they loaded. Allocates (lookups), once per load.
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
            foreach (var names in _itemIconNames)
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
                GameLog.Warning(LogCategory.Presentation, "Garden: kit sprites not loaded by the scene (plain shapes shown): " + string.Join(", ", missing));
            int iconCount = Enum.GetValues(typeof(Icon)).Length;
            _icons = new Sprite[iconCount];
            for (int i = 0; i < iconCount; i++)
                _icons[i] = i < _ownIcons.Length && _ownIcons[i] != null ? _ownIcons[i] : Find(found, i < _iconNames.Length ? _iconNames[i] : null);
            _itemSprites.Clear();
            for (int i = 0; i < _itemIds.Length; i++)
            {
                var sprite = i < _itemOwn.Length && _itemOwn[i] != null ? _itemOwn[i] : Find(found, i < _itemIconNames.Length ? _itemIconNames[i] : null);
                if (sprite != null)
                    _itemSprites[ContentId.FromSource(_itemIds[i]).Value] = sprite;
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

        private static Sprite Find(Dictionary<string, Sprite> found, string names)
        {
            if (string.IsNullOrEmpty(names))
                return null;
            foreach (var name in names.Split('|'))
                if (found.TryGetValue(name, out var sprite))
                    return sprite;
            return null;
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
            ApplySheet(_seedsShown ? 1f : 0f);
        }

        public void Hide()
        {
            Initialize();
            _shown = false;
            _noteT = -1f;
            _bannerT = -1f;
            if (_parts.notePill != null)
                _parts.notePill.gameObject.SetActive(false);
            if (_parts.banner != null)
                _parts.banner.gameObject.SetActive(false);
            for (int i = 0; i < _productT.Length; i++)
                HideProduct(i);
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

        public void SetLevel(int level, int points, int pointsForNext)
        {
            Initialize();
            _levelLabel.SetTemplate(_levelTemplate, level);
            float fill = pointsForNext > 0 ? Mathf.Clamp01(points / (float)pointsForNext) : 1f;
            if (Mathf.Abs(_parts.levelFill.fillAmount - fill) > 0.0005f)
                _parts.levelFill.fillAmount = fill;
        }

        public void SetBed(int plot, in GardenBedModel model)
        {
            Initialize();
            if ((uint)plot >= (uint)_parts.beds.Length)
                return;
            var b = _parts.beds[plot];
            var state = model.State;
            bool hidden = state == GardenPlotState.Locked && model.Unlock == GardenPlotUnlock.None;
            _bedHidden[plot] = hidden;
            _bedState[plot] = state;
            _timeKey[plot] = long.MinValue;
            SetActive(b.root.gameObject, !hidden && _bedVisible[plot]);
            bool locked = state == GardenPlotState.Locked;
            SetActive(b.owned, !locked);
            SetActive(b.locked, locked);
            if (locked)
            {
                bool gems = model.Unlock == GardenPlotUnlock.Gems;
                SetText(b.signLabel, gems ? _newPlotText : _clubText);
                SetActive(b.price.gameObject, gems);
                if (b.priceGem != null)
                    SetActive(b.priceGem.gameObject, gems);
                if (gems)
                    _priceLabels[plot].SetNumber(model.Price);
                if (b.sign != null)
                    b.sign.color = model.Armed ? _accent : _coverSignSprite != null ? Color.white : _card;
                return;
            }
            if (state == GardenPlotState.Empty)
                _stateLabels[plot].SetString(_plantText);
            else if (state == GardenPlotState.Ripe)
                _stateLabels[plot].SetString(_readyText);
            b.state.color = state == GardenPlotState.Ripe ? _leaf * 0.75f + _text * 0.25f : _text;
            b.drop.enabled = state == GardenPlotState.Growing && model.Watered;
            b.drop.color = IconOf(Icon.Drop) != null ? Color.white : _water;
            b.heart0.enabled = b.heart1.enabled = model.Helped;
            b.heart0.color = b.heart1.color = _leaf;
            b.outline.enabled = model.Selected;
            b.outline.color = _accent;
        }

        public void SetBedTime(int plot, double secondsLeft)
        {
            if ((uint)plot >= (uint)_parts.beds.Length || _bedState[plot] != GardenPlotState.Growing)
                return;
            long key = GardenTimeText.Key(secondsLeft);
            if (key == _timeKey[plot])
                return;
            _timeKey[plot] = key;
            var label = _stateLabels[plot];
            label.Commit(_time.WriteReadyIn(label.Buffer, secondsLeft));
        }

        public void PlaceBed(int plot, Vector2 screenPoint, bool visible)
        {
            Initialize();
            if ((uint)plot >= (uint)_parts.beds.Length)
                return;
            var b = _parts.beds[plot];
            _bedVisible[plot] = visible;
            if (visible && RectTransformUtility.ScreenPointToLocalPointInRectangle(_parts.labelLayer, screenPoint, EventCamera, out var local))
                b.root.anchoredPosition = local;
            SetActive(b.root.gameObject, visible && !_bedHidden[plot]);
        }

        public void ShowSeeds(bool shown)
        {
            Initialize();
            if (_seedsShown == shown)
                return;
            _seedsShown = shown;
            if (shown)
                _parts.sheet.gameObject.SetActive(true);
            if (!_shown)
                ApplySheet(shown ? 1f : 0f);
            if (shown)
                _parts.packetScroll?.StopMovement();
        }

        public void SetPacket(int cell, in GardenPacketModel model)
        {
            Initialize();
            if ((uint)cell >= (uint)_parts.packets.Length)
                return;
            var k = _parts.packets[cell];
            SetActive(k.root.gameObject, model.Shown);
            if (!model.Shown)
            {
                LayoutPackets();
                return;
            }
            var sprite = !string.IsNullOrEmpty(model.Key) ? ItemSprite(ContentId.FromSource(model.Key)) : null;
            k.icon.sprite = sprite != null ? sprite : IconOf(Icon.Seed);
            k.icon.enabled = k.icon.sprite != null;
            k.icon.color = model.Open ? Color.white : new Color(0.75f, 0.73f, 0.70f, 0.6f);
            if (k.back != null)
                k.back.color = model.Open ? _card : _cardLocked;
            if (k.lockIcon != null)
                SetActive(k.lockIcon.gameObject, !model.Open);
            SetText(k.name, model.Name);
            k.name.color = model.Open ? _text : _textSoft;
            SetActive(k.count.gameObject, model.Open);
            SetActive(k.lockLabel.gameObject, !model.Open);
            if (model.Open)
            {
                var label = _countLabels[cell];
                int i = UiChars.Char(label.Buffer, 0, UiChars.Times);
                label.Commit(UiChars.Int(label.Buffer, i, model.Seeds));
                k.count.color = model.Seeds > 0 ? _text : _textSoft;
            }
            else
            {
                SetText(k.lockLabel, model.Lock);
            }
            if (cell == _parts.packets.Length - 1)
                LayoutPackets();
        }

        public void SetTools(in GardenToolsModel model)
        {
            Initialize();
            var p = _parts;
            _canArmed = model.CanArmed;
            if (p.canRing != null)
            {
                p.canRing.enabled = model.CanArmed;
                if (!model.CanArmed)
                    p.canRing.rectTransform.localScale = Vector3.one;
            }
            if (p.canBack != null)
                p.canBack.color = model.CanArmed ? _accent : model.CanUsable ? _card : _cardLocked;
            if (p.canIcon != null)
                p.canIcon.color = model.CanUsable || model.CanArmed ? Color.white : new Color(1f, 1f, 1f, 0.5f);
            SetActive(p.harvestAll.gameObject, model.HarvestAll);
            SetActive(p.speedUp.gameObject, model.SpeedUpShown);
            if (model.SpeedUpShown)
            {
                _costLabel.SetNumber(model.SpeedUpCost);
                bool can = model.SpeedUpsLeft > 0;
                p.speedUp.interactable = can;
                if (p.speedUpBack != null)
                    p.speedUpBack.color = can ? _accent : _disabled;
                if (_leftShown != model.SpeedUpsLeft)
                {
                    _leftShown = model.SpeedUpsLeft;
                    if (can)
                        _leftLabel.SetTemplate(_speedUpsLeft.For(model.SpeedUpsLeft), model.SpeedUpsLeft);
                    else
                        _leftLabel.SetString(_speedUpsNone);
                }
            }
            if (p.speedUpsLeft != null)
                SetActive(p.speedUpsLeft.gameObject, model.SpeedUpShown);
        }

        public void SetChallenge(int row, in GardenChallengeModel model)
        {
            Initialize();
            if ((uint)row >= (uint)_parts.challenges.Length)
                return;
            var c = _parts.challenges[row];
            SetActive(c.root.gameObject, model.Shown);
            if (!model.Shown)
            {
                LayoutRow(_parts.challengeContent, ShownChallenges(), ChallengePitch);
                return;
            }
            SetText(c.name, model.Name);
            SetText(c.goal, model.Goal);
            c.name.color = model.Open ? _text : _textSoft;
            for (int k = 0; k < c.stars.Length; k++)
                Star(c.stars[k], k < model.Stars);
            if (c.back != null)
                c.back.color = model.Active ? _accent : model.Open ? _card : _cardLocked;
            SetActive(c.progress.gameObject, model.Active);
            if (model.Active)
                _progressLabels[row].SetTemplate(_progressTemplate, model.Harvests, model.Needed);
            LayoutRow(_parts.challengeContent, ShownChallenges(), ChallengePitch);
        }

        public void AddProduct(ContentId item, int count)
        {
            Initialize();
            if (count <= 0)
                return;
            int chip = -1;
            for (int i = 0; i < _productT.Length; i++)
                if (_productT[i] >= 0f && _productItem[i] == item.Value)
                {
                    chip = i;
                    break;
                }
            if (chip >= 0)
            {
                // The same product again (« Tout récolter »): it counts on from what it shows.
                _productFrom[chip] = CurrentCount(chip);
                _productTo[chip] += count;
                _productT[chip] = 0f;
                return;
            }
            for (int i = 0; i < _productT.Length; i++)
                if (_productT[i] < 0f)
                {
                    chip = i;
                    break;
                }
            if (chip < 0)
            {
                // Every chip busy: the oldest makes room.
                int oldest = int.MaxValue;
                for (int i = 0; i < _productAge.Length; i++)
                    if (_productAge[i] < oldest)
                    {
                        oldest = _productAge[i];
                        chip = i;
                    }
            }
            var d = _parts.products[chip];
            _productItem[chip] = item.Value;
            _productFrom[chip] = 0;
            _productTo[chip] = count;
            _productT[chip] = 0f;
            _productAge[chip] = ++_productOrder;
            var sprite = ItemSprite(item);
            d.icon.sprite = sprite != null ? sprite : IconOf(Icon.Seed);
            d.icon.enabled = d.icon.sprite != null;
            d.root.gameObject.SetActive(true);
            d.group.alpha = 0f;
            WriteAmount(chip, _reduceMotion ? count : 0);
            LayoutProducts();
        }

        public void ShowLevelUp(int level, string opened)
        {
            Initialize();
            _bannerLabel?.SetTemplate(_levelTemplate, level);
            bool line = !string.IsNullOrEmpty(opened);
            if (_parts.bannerLine != null)
            {
                SetActive(_parts.bannerLine.gameObject, line);
                if (line)
                    SetText(_parts.bannerLine, opened);
            }
            _bannerT = 0f;
            _parts.bannerGroup.alpha = 0f;
            _parts.banner.gameObject.SetActive(true);
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

        public Vector2 PacketPoint(int cell) => ScreenOf(_parts.packets[Mathf.Clamp(cell, 0, _parts.packets.Length - 1)].root);

        public Vector2 CanPoint => ScreenOf((RectTransform)_parts.can.transform);

        public Vector2 HarvestAllPoint => ScreenOf((RectTransform)_parts.harvestAll.transform);

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
            float sheetTarget = _seedsShown ? 1f : 0f;
            if (_sheetT != sheetTarget)
                ApplySheet(Mathf.MoveTowards(_sheetT, sheetTarget, dt / (_reduceMotion ? 0.15f : GardenScreenLayout.SheetSeconds)));
            if (_canArmed && _parts.canRing != null)
            {
                _ringT += dt;
                float pulse = _reduceMotion ? 1f : 1f + 0.06f * Mathf.Sin(_ringT * Mathf.PI * 2f / RingPeriod);
                _parts.canRing.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
            }
            for (int i = 0; i < _productT.Length; i++)
                if (_productT[i] >= 0f)
                    TickProduct(i, dt);
            TickBanner(dt);
            TickNote(dt);
            _parts.paw?.Tick(dt);
        }

        private void TickProduct(int chip, float dt)
        {
            _productT[chip] += dt;
            float t = _productT[chip];
            var d = _parts.products[chip];
            float total = GardenScreenLayout.CountSeconds + GardenScreenLayout.ProductHoldSeconds + 0.3f;
            if (t >= total)
            {
                HideProduct(chip);
                LayoutProducts();
                return;
            }
            d.group.alpha = Mathf.Min(Mathf.Clamp01(t / 0.12f), Mathf.Clamp01((total - t) / 0.3f));
            WriteAmount(chip, CurrentCount(chip));
            if (!_reduceMotion)
            {
                float pop = t < 0.2f ? 1f + 0.12f * Mathf.Sin(t / 0.2f * Mathf.PI) : 1f;
                d.root.localScale = new Vector3(pop, pop, 1f);
            }
        }

        private int CurrentCount(int chip)
        {
            if (_reduceMotion)
                return _productTo[chip];
            float k = Mathf.Clamp01(_productT[chip] / GardenScreenLayout.CountSeconds);
            return Mathf.RoundToInt(Mathf.Lerp(_productFrom[chip], _productTo[chip], k));
        }

        private void WriteAmount(int chip, int value)
        {
            var label = _amountLabels[chip];
            int i = UiChars.Char(label.Buffer, 0, '+');
            label.Commit(UiChars.Int(label.Buffer, i, value));
        }

        private void HideProduct(int chip)
        {
            _productT[chip] = -1f;
            _productItem[chip] = 0u;
            _productTo[chip] = 0;
            if (_parts.products[chip].root != null)
                _parts.products[chip].root.gameObject.SetActive(false);
        }

        private void TickBanner(float dt)
        {
            if (_bannerT < 0f)
                return;
            _bannerT += dt;
            float total = GardenScreenLayout.BannerSeconds;
            if (_bannerT >= total)
            {
                _bannerT = -1f;
                _parts.banner.gameObject.SetActive(false);
                return;
            }
            float alpha = Mathf.Min(Mathf.Clamp01(_bannerT / BannerIn), Mathf.Clamp01((total - _bannerT) / BannerOut));
            _parts.bannerGroup.alpha = alpha;
            float s = _reduceMotion ? 1f : Mathf.Lerp(0.92f, 1f, UiCurves.EaseOut(Mathf.Clamp01(_bannerT / BannerIn)));
            _parts.banner.localScale = new Vector3(s, s, 1f);
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
            _parts.sheetGroup.blocksRaycasts = _seedsShown;
            _parts.sheetGroup.interactable = _seedsShown;
            if (t <= 0f && !_seedsShown)
                sheet.gameObject.SetActive(false);
        }

        // ---------------- layout helpers ----------------

        private static void LayoutRow(RectTransform content, int shown, float pitch)
        {
            if (content == null)
                return;
            float width = shown * pitch + RowPadding * 2f;
            if (Mathf.Abs(content.sizeDelta.x - width) > 0.5f)
                content.sizeDelta = new Vector2(width, content.sizeDelta.y);
        }

        private int ShownChallenges()
        {
            int n = 0;
            foreach (var c in _parts.challenges)
                if (c.root.gameObject.activeSelf)
                    n++;
            return n;
        }

        /// <summary>The seed grid's content as tall as its shown rows.</summary>
        private void LayoutPackets()
        {
            int shown = 0;
            foreach (var k in _parts.packets)
                if (k.root.gameObject.activeSelf)
                    shown++;
            int rows = (shown + PacketColumns - 1) / PacketColumns;
            float height = rows * PacketPitchY + RowPadding * 2f;
            var content = _parts.packetContent;
            if (Mathf.Abs(content.sizeDelta.y - height) > 0.5f)
                content.sizeDelta = new Vector2(content.sizeDelta.x, height);
        }

        /// <summary>The product chips side by side, centred (on a change only).</summary>
        private void LayoutProducts()
        {
            int shown = 0;
            for (int i = 0; i < _productT.Length; i++)
                if (_productT[i] >= 0f)
                    shown++;
            int at = 0;
            for (int i = 0; i < _productT.Length; i++)
            {
                if (_productT[i] < 0f)
                    continue;
                var root = _parts.products[i].root;
                root.anchoredPosition = new Vector2((at - (shown - 1) * 0.5f) * ProductPitch, root.anchoredPosition.y);
                at++;
            }
        }

        // ---------------- content helpers ----------------

        private void BindTexts(ILocalization text)
        {
            if (text == null)
                return;
            _time.Bind(text);
            _levelTemplate = text.Get(GardenText.LevelUp);
            _plantText = text.Get(GardenText.Plant);
            _readyText = text.Get(GardenText.Ready);
            _newPlotText = text.Get(GardenText.NewPlot);
            _clubText = text.Get(GardenText.PlotClub);
            _progressTemplate = text.Get(GardenText.ChallengeProgress);
            _speedUpsNone = text.Get(GardenText.SpeedUpsNone);
            _speedUpsLeft = UiCounted.From(text, GardenText.SpeedUpsLeft);
            var p = _parts;
            SetText(p.title, text.Get(GardenText.Title));
            SetText(p.harvestAllLabel, text.Get(GardenText.HarvestAll));
            SetText(p.speedUpLabel, text.Get(GardenText.SpeedUp));
            SetText(p.challengesTitle, text.Get(GardenText.Challenges));
            SetText(p.sheetTitle, text.Get(GardenText.Seeds));
            for (int i = 0; i < p.beds.Length; i++)
            {
                _timeKey[i] = long.MinValue;
                SetText(p.beds[i].signLabel, _newPlotText);
            }
            _leftShown = -1;
        }

        private void ApplyIcons()
        {
            if (_icons == null)
                return;
            var p = _parts;
            SetSprite(p.gemsIcon, IconOf(Icon.Gems));
            SetSprite(p.speedUpGem, IconOf(Icon.Gems));
            SetSprite(p.canIcon, IconOf(Icon.Can));
            foreach (var b in p.beds)
            {
                SetSprite(b.drop, IconOf(Icon.Drop));
                SetSprite(b.heart0, IconOf(Icon.Heart));
                SetSprite(b.heart1, IconOf(Icon.Heart));
                SetSprite(b.priceGem, IconOf(Icon.Gems));
            }
            foreach (var k in p.packets)
                SetSprite(k.lockIcon, IconOf(Icon.Lock));
            foreach (var c in p.challenges)
                foreach (var star in c.stars)
                    SetSprite(star, IconOf(Icon.Star));
        }

        /// <summary>A star lit (UI_Star_Result_Full) or not (UI_Star_Result_Empty; else the full one, tinted soft).</summary>
        private void Star(Image star, bool on)
        {
            var off = IconOf(Icon.StarOff);
            var full = IconOf(Icon.Star);
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
            int beds = p.beds.Length;
            _bedState = new GardenPlotState[beds];
            _bedHidden = new bool[beds];
            _bedVisible = new bool[beds];
            _timeKey = new long[beds];
            _stateLabels = new UiLabel[beds];
            _priceLabels = new UiLabel[beds];
            for (int i = 0; i < beds; i++)
            {
                var b = p.beds[i];
                _timeKey[i] = long.MinValue;
                _bedVisible[i] = true;
                _stateLabels[i] = new UiLabel(b.state, 96);
                _priceLabels[i] = new UiLabel(b.price, 24);
                b.drop.enabled = false;
                b.heart0.enabled = b.heart1.enabled = false;
                b.outline.enabled = false;
            }
            _gemsLabel = new UiLabel(p.gems, 24);
            _levelLabel = new UiLabel(p.levelLabel, 64);
            _costLabel = new UiLabel(p.speedUpCost, 24);
            _leftLabel = new UiLabel(p.speedUpsLeft, 64);
            _bannerLabel = new UiLabel(p.bannerTitle, 64);
            _countLabels = new UiLabel[p.packets.Length];
            for (int i = 0; i < p.packets.Length; i++)
            {
                _countLabels[i] = new UiLabel(p.packets[i].count, 24);
                int cell = i;
                p.packets[i].button?.onClick.AddListener(() => _actions?.TapPacket(cell));
            }
            _progressLabels = new UiLabel[p.challenges.Length];
            for (int i = 0; i < p.challenges.Length; i++)
            {
                _progressLabels[i] = new UiLabel(p.challenges[i].progress, 48);
                int row = i;
                p.challenges[i].button?.onClick.AddListener(() => _actions?.TapChallenge(row));
            }
            int products = p.products.Length;
            _productItem = new uint[products];
            _productFrom = new int[products];
            _productTo = new int[products];
            _productT = new float[products];
            _productAge = new int[products];
            _amountLabels = new UiLabel[products];
            for (int i = 0; i < products; i++)
            {
                _amountLabels[i] = new UiLabel(p.products[i].amount, 24);
                _productT[i] = -1f;
                p.products[i].root.gameObject.SetActive(false);
            }
            p.close?.onClick.AddListener(() => _actions?.CloseScreen());
            p.can?.onClick.AddListener(() => _actions?.TapCan());
            p.harvestAll?.onClick.AddListener(() => _actions?.HarvestAll());
            p.speedUp?.onClick.AddListener(() => _actions?.SpeedUp());
            p.sheetClose?.onClick.AddListener(() => _actions?.CloseSeeds());
            if (p.canRing != null)
                p.canRing.enabled = false;
            p.notePill.gameObject.SetActive(false);
            p.banner.gameObject.SetActive(false);
            p.harvestAll.gameObject.SetActive(false);
            p.speedUp.gameObject.SetActive(false);
            if (p.speedUpsLeft != null)
                p.speedUpsLeft.gameObject.SetActive(false);
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
            foreach (var k in _parts.packets)
                k.button?.onClick.RemoveAllListeners();
            foreach (var c in _parts.challenges)
                c.button?.onClick.RemoveAllListeners();
            _parts.close?.onClick.RemoveAllListeners();
            _parts.can?.onClick.RemoveAllListeners();
            _parts.harvestAll?.onClick.RemoveAllListeners();
            _parts.speedUp?.onClick.RemoveAllListeners();
            _parts.sheetClose?.onClick.RemoveAllListeners();
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

        // ---------------- prefab builder hooks (Editor GardenScreenPipeline) ----------------

        public void SetParts(GardenScreenParts parts) => _parts = parts;

        public void SetArt(Sprite levelPill, Sprite bedLabel, Sprite coverSign)
        {
            _levelPillSprite = levelPill;
            _bedLabelSprite = bedLabel;
            _coverSignSprite = coverSign;
        }

        /// <summary>What the overlay borrows from the scene at run time: kit sprites by image, fonts by label, icons by role.</summary>
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

        /// <summary>The seeds, products and dyes: their source ids, sprite names (borrowed) and the bundle's own sprites.</summary>
        public void SetItems(string[] ids, string[] iconNames, Sprite[] own)
        {
            _itemIds = ids ?? Array.Empty<string>();
            _itemIconNames = iconNames ?? Array.Empty<string>();
            _itemOwn = own ?? Array.Empty<Sprite>();
        }

        public void SetColors(Color card, Color cardLocked, Color text, Color textSoft, Color accent, Color gold, Color starOff, Color leaf, Color water,
            Color disabled)
        {
            _card = card;
            _cardLocked = cardLocked;
            _text = text;
            _textSoft = textSoft;
            _accent = accent;
            _gold = gold;
            _starOff = starOff;
            _leaf = leaf;
            _water = water;
            _disabled = disabled;
        }

        /// <summary>The names the pipeline asked to borrow (tests: nothing of the scene is referenced, everything is named).</summary>
        public IReadOnlyList<string> BorrowedNames => _kitNames;

        public IReadOnlyList<TMP_Text> BorrowedLabels => _labels;
    }
}
