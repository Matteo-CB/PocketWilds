using System;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Presentation;
using PocketWilds.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>One reward cell of the track (free or gold line), or a headline reward of the announce page.</summary>
    [Serializable]
    public sealed class EventCellRefs
    {
        public RectTransform root;
        public Image back;
        public Image icon;
        public Image extra;
        public TMP_Text label;
        public Button claim;
        public TMP_Text claimLabel;
        public GameObject done;
        public GameObject locked;
    }

    /// <summary>One tier row: its number badge, the free cell and the gold cell.</summary>
    [Serializable]
    public sealed class EventTierRefs
    {
        public RectTransform root;
        public Image badge;
        public TMP_Text number;
        public EventCellRefs free = new EventCellRefs();
        public EventCellRefs gold = new EventCellRefs();
    }

    /// <summary>A list row (shop, Memory Shop, quests, challenges): icon, title, a sub line, a button, a done mark, stars.</summary>
    [Serializable]
    public sealed class EventLineRefs
    {
        public RectTransform root;
        public Image icon;
        public TMP_Text title;
        public TMP_Text sub;
        public Image subIcon;
        public Button button;
        public TMP_Text buttonLabel;
        public GameObject done;
        public Image[] stars = Array.Empty<Image>();
    }

    /// <summary>Every reference of PF_UI_EventHub, set by the prefab builder (Editor EventUiPipeline).</summary>
    [Serializable]
    public sealed class EventHubRefs
    {
        public UiColorTokens tokens;
        public Canvas canvas;
        public GameObject content;
        public CanvasGroup group;
        public RectTransform safe;

        public GameObject hub;
        public Button close;
        public TMP_Text name;
        public TMP_Text date;
        public Image featured;
        public TMP_Text currency;
        public Button[] tabs = Array.Empty<Button>();
        public Image[] tabBacks = Array.Empty<Image>();
        public TMP_Text[] tabLabels = Array.Empty<TMP_Text>();
        public ScrollRect scroll;

        /// <summary>One page per <see cref="EventHubTab"/>, then the announce page.</summary>
        public RectTransform[] pages = Array.Empty<RectTransform>();

        public TMP_Text announceTitle;
        public EventCellRefs[] headline = Array.Empty<EventCellRefs>();
        public TMP_Text announceNote;

        public TMP_Text trackPoints;
        public RectTransform trackFill;
        public Button claimAll;
        public TMP_Text claimAllLabel;
        public Button goldGet;
        public TMP_Text goldGetLabel;
        public TMP_Text freeHeader;
        public TMP_Text goldHeader;
        public EventTierRefs[] tiers = Array.Empty<EventTierRefs>();

        public TMP_Text eggTitle;
        public TMP_Text eggCost;
        public Button eggOpen;
        public TMP_Text eggOpenLabel;
        public TMP_Text eggNewIn;
        public TMP_Text eggResult;
        public Image[] eggSlots = Array.Empty<Image>();
        public GameObject[] eggSlotDone = Array.Empty<GameObject>();
        public Button odds;
        public GameObject oddsPanel;
        public TMP_Text oddsTitle;
        public TMP_Text oddsItem;
        public TMP_Text oddsSecret;
        public TMP_Text oddsRule;

        public TMP_Text shopTitle;
        public EventLineRefs[] shop = Array.Empty<EventLineRefs>();
        public RectTransform stickerPanel;
        public TMP_Text stickerTitle;
        public Button[] stickerChips = Array.Empty<Button>();
        public TMP_Text[] stickerLabels = Array.Empty<TMP_Text>();

        public TMP_Text dailyTitle;
        public TMP_Text pending;
        public TMP_Text swaps;
        public EventLineRefs[] daily = Array.Empty<EventLineRefs>();
        public TMP_Text longTitle;
        public EventLineRefs[] longQuests = Array.Empty<EventLineRefs>();

        public TMP_Text starsTitle;
        public RectTransform starsFill;
        public TMP_Text outfitLine;
        public TMP_Text practice;
        public EventLineRefs[] challenges = Array.Empty<EventLineRefs>();

        public TMP_Text goalTitle;
        public RectTransform goalFill;
        public TMP_Text goalProgress;
        public TMP_Text goalNote;

        public TMP_Text memoryTitle;
        public EventLineRefs[] memory = Array.Empty<EventLineRefs>();

        public GameObject summary;
        public TMP_Text summaryTitle;
        public TMP_Text summaryName;
        public TMP_Text summaryCurrency;
        public TMP_Text summaryLight;
        public TMP_Text summaryTier;
        public Button summaryOk;
        public TMP_Text summaryOkLabel;

        /// <summary>One sprite per <see cref="EventIconSlots"/> slot (reward kinds, the egg, the seeds).</summary>
        public Sprite[] icons = Array.Empty<Sprite>();
        public Sprite paw;
        public Sprite secret;
        public Sprite starFull;
        public Sprite starEmpty;
        public string[] featuredKeys = Array.Empty<string>();
        public Sprite[] featuredSprites = Array.Empty<Sprite>();

        /// <summary>Layout of the scrolled pages (canvas units), written by the builder.</summary>
        public float trackTop;
        public float tierPitch;
        public float shopTop;
        public float linePitch;
        public float questTop;
        public float questPitch;
        public float challengeTop;
        public float challengePitch;
    }

    /// <summary>
    /// PF_UI_EventHub (T48, screen 018): the event's opaque full screen. A banner (the event's name, the featured species, the real
    /// end date « Jusqu'au {date} », never a countdown), the currency counter, tabs (Parcours, Œuf, Boutique, Quêtes, Défis,
    /// Objectif, and Souvenirs for a returning event) over one scrolled page: the 30-tier track (free and Carnet d'or lines, the
    /// lock on gold when not owned, « Tout récupérer »), the egg (cost, « Nouvel objet dans {n} œufs », 7 items and the secret,
    /// the odds panel), the shop (prices, what is left, the sticker of choice), the quests (daily with swap, long), the 10 x 3
    /// challenge stars, the personal goal, the Memory Shop. The announce page shows the real start date and 3 headline rewards.
    /// The end summary card opens alone over the refuge. Bound on open and after each action, never per frame; content saved
    /// inactive; ticked by the refuge.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EventHubScreen : MonoBehaviour, IEventHubView, IMotionSettings
    {
        public const int AnnouncePage = 7;
        public const int EggSlots = 8;
        public const int StickerChips = 12;
        private const float FadeSeconds = 0.16f;

        [SerializeField] private EventHubRefs _ui = new EventHubRefs();

        private EventHubModel _model;
        private ILocalization _text;
        private IEventHubActions _actions;
        private EventHubTab _tab;
        private bool _announce;
        private bool _listening;
        private bool _stickerPick;
        private int _stickerLine = -1;
        private string _summaryId = string.Empty;
        private float _time = -1f;
        private EventQuestText _questText;
        private ILocalization _questTextFor;
        private readonly int[] _stickers = new int[StickerChips];
        private readonly char[] _number = new char[48];
        private readonly SafeAreaFitter _fitter = new SafeAreaFitter();

        public bool IsOpen { get; private set; }
        public bool SummaryOpen { get; private set; }
        public bool ReduceMotion { get; set; }
        public EventHubTab Tab => _tab;
        public bool AnnounceShown => IsOpen && _announce;
        public EventHubRefs Ui => _ui;
        public bool TabShown(EventHubTab tab) => _ui.tabs[(int)tab].gameObject.activeSelf;
        public bool OddsShown => _ui.oddsPanel != null && _ui.oddsPanel.activeSelf;
        public bool StickerPickShown => _stickerPick;
        public string DateText => _ui.date != null ? _ui.date.text : string.Empty;
        public string EggLineText => _ui.eggNewIn != null ? _ui.eggNewIn.text : string.Empty;
        public string SummaryCurrencyText => _ui.summaryCurrency != null ? _ui.summaryCurrency.text : string.Empty;

        // ---------------- IEventHubView ----------------

        public void Open(EventHubModel model, ILocalization text, IEventHubActions actions, EventHubTab tab)
        {
            _model = model;
            _text = text;
            _actions = actions;
            Listen();
            _announce = model != null && model.Announce;
            _tab = model != null && !_announce && !model.TabVisible(tab) ? EventHubTab.Track : tab;
            _stickerPick = false;
            SummaryOpen = false;
            _ui.summary.SetActive(false);
            _ui.hub.SetActive(true);
            if (_ui.oddsPanel != null)
                _ui.oddsPanel.SetActive(false);
            _ui.eggResult.text = string.Empty;
            WriteStaticTexts();
            Show();
            Bind();
            _ui.scroll.verticalNormalizedPosition = 1f;
        }

        public void Refresh()
        {
            if (IsOpen && !SummaryOpen)
                Bind();
        }

        public void SelectTab(EventHubTab tab)
        {
            if (!IsOpen || SummaryOpen || _announce || _model == null || !_model.TabVisible(tab))
                return;
            _tab = tab;
            _stickerPick = false;
            Bind();
            _ui.scroll.verticalNormalizedPosition = 1f;
        }

        public void ShowEggResult(in EventEggResult result)
        {
            if (!IsOpen || _ui.eggResult == null)
                return;
            if (result.Status == EventEggStatus.Opened)
                _ui.eggResult.text = result.IsNew ? T(EventText.EggNew) : F(EventText.EggDuplicate, result.Refund.ToString());
            else if (result.Status == EventEggStatus.NotEnoughCurrency)
                _ui.eggResult.text = T(EventText.NotEnough);
            else
                _ui.eggResult.text = string.Empty;
        }

        public void ShowSummary(in EventEndSummary summary, string eventName, ILocalization text, IEventHubActions actions)
        {
            _text = text;
            _actions = actions;
            Listen();
            _summaryId = summary.InstanceId ?? string.Empty;
            _ui.hub.SetActive(false);
            _ui.summary.SetActive(true);
            _ui.summaryTitle.text = T(EventText.SummaryTitle);
            _ui.summaryName.text = eventName ?? string.Empty;
            _ui.summaryCurrency.text = F(EventText.SummaryCurrency, Grouped(summary.CurrencyLeft));
            _ui.summaryLight.text = F(EventText.SummaryLight, Light(summary.Light));
            _ui.summaryTier.text = F(EventText.SummaryTier, summary.Tier.ToString());
            _ui.summaryOkLabel.text = T(EventText.SummaryOk);
            SummaryOpen = true;
            Show();
        }

        public void Close()
        {
            if (!IsOpen)
                return;
            bool summary = SummaryOpen;
            Hide();
            if (summary)
                _actions?.SummarySeen(_summaryId);
            else
                _actions?.EventHubClosed();
        }

        public void Prewarm(ILocalization text)
        {
            if (IsOpen)
                return;
            _text = text;
            WriteStaticTexts();
            _ui.canvas.enabled = true;
            _ui.content.SetActive(true);
            _ui.group.alpha = 0f;
            Canvas.ForceUpdateCanvases();
            _ui.content.SetActive(false);
            _ui.canvas.enabled = false;
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!IsOpen)
                return;
            // Android Back closes the screen (the Input System maps it to Escape).
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }
            if (_time < 0f)
                return;
            _time += unscaledDeltaTime;
            float k = ReduceMotion ? 1f : Mathf.Clamp01(_time / FadeSeconds);
            _ui.group.alpha = k;
            if (k >= 1f)
                _time = -1f;
        }

        // ---------------- Buttons (also called by tests) ----------------

        public void PressClaim(int tier, bool gold) => _actions?.ClaimTier(tier, gold);
        public void PressClaimAll() => _actions?.ClaimAllTiers();
        public void PressOpenEgg() => _actions?.OpenEventEgg();
        public void PressSwap(int index) => _actions?.SwapQuest(index);
        public void PressMemory(int index) => _actions?.BuyMemoryItem(index);

        public void PressShop(int index)
        {
            var item = _model?.Service.ShopItem(index);
            if (item == null)
                return;
            if (item.Kind == EventShopKind.Sticker)
            {
                // The sticker of choice: the missing stickers of the event page show as chips under the shop.
                _stickerPick = !_stickerPick;
                _stickerLine = index;
                Bind();
                return;
            }
            _actions?.BuyEventItem(index, -1);
        }

        public void PressSticker(int chip)
        {
            if (!_stickerPick || chip < 0 || chip >= StickerChips || _stickerLine < 0)
                return;
            _stickerPick = false;
            _actions?.BuyEventItem(_stickerLine, _stickers[chip]);
        }

        public void ToggleOdds()
        {
            if (_ui.oddsPanel != null)
                _ui.oddsPanel.SetActive(!_ui.oddsPanel.activeSelf);
        }

        // ---------------- Binding ----------------

        private void Show()
        {
            IsOpen = true;
            _ui.canvas.enabled = true;
            _ui.content.SetActive(true);
            _fitter.Fit(_ui.safe);
            _time = 0f;
            _ui.group.alpha = ReduceMotion ? 1f : 0f;
        }

        private void Hide()
        {
            IsOpen = false;
            SummaryOpen = false;
            _time = -1f;
            _ui.content.SetActive(false);
            _ui.canvas.enabled = false;
        }

        private void Listen()
        {
            if (_listening)
                return;
            _listening = true;
            _ui.close.onClick.AddListener(Close);
            for (int i = 0; i < _ui.tabs.Length; i++)
            {
                var tab = (EventHubTab)i;
                _ui.tabs[i].onClick.AddListener(() => SelectTab(tab));
            }
            _ui.claimAll.onClick.AddListener(PressClaimAll);
            _ui.goldGet.onClick.AddListener(() => _actions?.OpenGoldBook());
            for (int i = 0; i < _ui.tiers.Length; i++)
            {
                int tier = i + 1;
                _ui.tiers[i].free.claim.onClick.AddListener(() => PressClaim(tier, false));
                _ui.tiers[i].gold.claim.onClick.AddListener(() => PressClaim(tier, true));
            }
            _ui.eggOpen.onClick.AddListener(PressOpenEgg);
            if (_ui.odds != null)
                _ui.odds.onClick.AddListener(ToggleOdds);
            for (int i = 0; i < _ui.shop.Length; i++)
            {
                int line = i;
                _ui.shop[i].button.onClick.AddListener(() => PressShop(line));
            }
            for (int i = 0; i < _ui.stickerChips.Length; i++)
            {
                int chip = i;
                _ui.stickerChips[i].onClick.AddListener(() => PressSticker(chip));
            }
            for (int i = 0; i < _ui.daily.Length; i++)
            {
                int index = i;
                _ui.daily[i].button.onClick.AddListener(() => PressSwap(index));
            }
            for (int i = 0; i < _ui.memory.Length; i++)
            {
                int index = i;
                _ui.memory[i].button.onClick.AddListener(() => PressMemory(index));
            }
            _ui.summaryOk.onClick.AddListener(Close);
        }

        /// <summary>Words that never change while the screen is open (tabs, headers, buttons).</summary>
        private void WriteStaticTexts()
        {
            string[] tabs = { EventText.TabTrack, EventText.TabEgg, EventText.TabShop, EventText.TabQuests, EventText.TabChallenges, EventText.TabGoal,
                EventText.TabMemory };
            for (int i = 0; i < _ui.tabLabels.Length && i < tabs.Length; i++)
                _ui.tabLabels[i].text = T(tabs[i]);
            _ui.announceTitle.text = T(EventText.Headline);
            _ui.announceNote.text = T(EventText.AnnounceNote);
            _ui.claimAllLabel.text = T(EventText.ClaimAll);
            _ui.goldGetLabel.text = T(EventText.GoldGet);
            _ui.freeHeader.text = T(EventText.TrackFree);
            _ui.goldHeader.text = T(EventText.GoldTitle);
            for (int i = 0; i < _ui.tiers.Length; i++)
            {
                _ui.tiers[i].free.claimLabel.text = T(EventText.Claim);
                _ui.tiers[i].gold.claimLabel.text = T(EventText.Claim);
            }
            _ui.eggTitle.text = T(EventText.EggTitle);
            _ui.eggOpenLabel.text = T(EventText.EggOpen);
            _ui.oddsTitle.text = T(EventText.EggOddsTitle);
            _ui.shopTitle.text = T(EventText.ShopTitle);
            _ui.stickerTitle.text = T(EventText.ShopPickSticker);
            _ui.dailyTitle.text = T(EventText.QuestsDaily);
            _ui.longTitle.text = T(EventText.QuestsLong);
            _ui.outfitLine.text = T(EventText.ChallengesOutfit);
            _ui.practice.text = T(EventText.ChallengesPractice);
            _ui.goalTitle.text = T(EventText.GoalTitle);
            _ui.memoryTitle.text = T(EventText.MemoryTitle);
        }

        private void Bind()
        {
            if (_model == null)
                return;
            var events = _model.Service;
            var def = _model.Definition;
            _announce = _model.Announce;
            _ui.name.text = def != null ? T(def.NameKey) : string.Empty;
            string language = _text != null ? _text.Language : "en";
            _ui.date.text = _announce
                ? F(EventText.Starts, EventHubModel.FormatDate(events.StartUtc, language))
                : F(EventText.Ends, EventHubModel.FormatDate(events.EndUtc, language));
            _ui.currency.text = Grouped(events.Currency);
            _ui.currency.transform.parent.gameObject.SetActive(!_announce);
            BindFeatured(def);
            for (int i = 0; i < _ui.tabs.Length; i++)
            {
                bool visible = !_announce && _model.TabVisible((EventHubTab)i);
                if (_ui.tabs[i].gameObject.activeSelf != visible)
                    _ui.tabs[i].gameObject.SetActive(visible);
                if (_ui.tokens != null)
                    _ui.tabBacks[i].color = i == (int)_tab ? _ui.tokens.accentPrimary : _ui.tokens.panelSecondary;
            }
            int page = _announce ? AnnouncePage : (int)_tab;
            for (int i = 0; i < _ui.pages.Length; i++)
            {
                bool active = i == page;
                if (_ui.pages[i].gameObject.activeSelf != active)
                    _ui.pages[i].gameObject.SetActive(active);
            }
            _ui.scroll.content = _ui.pages[page];
            float height;
            if (_announce)
                height = BindAnnounce(def);
            else
            {
                switch (_tab)
                {
                    case EventHubTab.Egg: height = BindEgg(events); break;
                    case EventHubTab.Shop: height = BindShop(events); break;
                    case EventHubTab.Quests: height = BindQuests(events); break;
                    case EventHubTab.Challenges: height = BindChallenges(events); break;
                    case EventHubTab.Goal: height = BindGoal(events); break;
                    case EventHubTab.Memory: height = BindMemory(events); break;
                    default: height = BindTrack(events); break;
                }
            }
            var rect = _ui.pages[page];
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
        }

        private void BindFeatured(EventDefinition def)
        {
            Sprite sprite = null;
            if (def != null && def.FeaturedSpecies.IsValid)
            {
                for (int i = 0; i < _ui.featuredKeys.Length && sprite == null; i++)
                    if (ContentId.FromSource(_ui.featuredKeys[i]) == def.FeaturedSpecies)
                        sprite = _ui.featuredSprites[i];
            }
            _ui.featured.sprite = sprite != null ? sprite : _ui.paw;
        }

        private float BindAnnounce(EventDefinition def)
        {
            for (int i = 0; i < _ui.headline.Length; i++)
            {
                var reward = def != null && i < def.Headline.Length ? def.Headline[i] : default;
                var cell = _ui.headline[i];
                cell.root.gameObject.SetActive(reward.Kind != EventRewardKind.None);
                if (reward.Kind == EventRewardKind.None)
                    continue;
                cell.icon.sprite = Icon((int)reward.Kind);
                cell.label.text = EventHubModel.RewardLabel(_text, reward);
            }
            return _ui.pages[AnnouncePage].sizeDelta.y;
        }

        private float BindTrack(EventService events)
        {
            int tier = events.Tier, count = _model.TierRows;
            _ui.trackPoints.text = F(EventText.TrackPoints, Grouped(events.Points)) + "  " + tier + " / " + count;
            SetFill(_ui.trackFill, _model.TierProgress);
            _ui.claimAll.interactable = _model.ClaimableCount > 0;
            _ui.goldGet.gameObject.SetActive(!events.GoldOwned);
            for (int i = 0; i < _ui.tiers.Length; i++)
            {
                var refs = _ui.tiers[i];
                bool visible = i < count;
                if (refs.root.gameObject.activeSelf != visible)
                    refs.root.gameObject.SetActive(visible);
                if (!visible)
                    continue;
                var row = _model.GetTier(i + 1);
                refs.number.text = row.Tier.ToString();
                if (_ui.tokens != null)
                    refs.badge.color = row.Tier <= tier ? _ui.tokens.accentPrimary : _ui.tokens.accentIdle;
                BindCell(refs.free, row.Free, row.FreeExtra, row.FreeState);
                refs.gold.root.gameObject.SetActive(row.HasGold);
                if (row.HasGold)
                    BindCell(refs.gold, row.Gold, default, row.GoldState);
            }
            return _ui.trackTop + count * _ui.tierPitch + 60f;
        }

        private void BindCell(EventCellRefs cell, in EventReward reward, in EventReward extra, EventCellState state)
        {
            cell.icon.sprite = Icon((int)reward.Kind);
            cell.label.text = EventHubModel.RewardLabel(_text, reward);
            bool hasExtra = extra.Kind != EventRewardKind.None;
            cell.extra.gameObject.SetActive(hasExtra);
            if (hasExtra)
                cell.extra.sprite = Icon((int)extra.Kind);
            cell.claim.gameObject.SetActive(state == EventCellState.Claimable);
            cell.done.SetActive(state == EventCellState.Claimed);
            cell.locked.SetActive(state == EventCellState.NeedsGold);
            var tint = state == EventCellState.Locked ? new Color(1f, 1f, 1f, 0.55f) : Color.white;
            cell.icon.color = tint;
        }

        private float BindEgg(EventService events)
        {
            _ui.eggCost.text = Grouped(events.EggCost);
            _ui.eggOpen.interactable = events.Currency >= events.EggCost;
            int left = events.EggsUntilNew;
            _ui.eggNewIn.text = left <= 0 ? T(EventText.EggComplete)
                : (_text != null ? _text.GetPlural(EventText.EggNewIn, left) : EventText.EggNewIn).Replace("{0}", left.ToString());
            string language = _text != null ? _text.Language : "en";
            _ui.oddsItem.text = F(EventText.EggOddsItem, EventHubModel.FormatPercent(events.ItemChance, language));
            _ui.oddsSecret.text = F(EventText.EggOddsSecret, EventHubModel.FormatPercent(events.SecretChance, language));
            _ui.oddsRule.text = T(EventText.EggOddsRule)
                .Replace("{0}", events.Params.Int(EventParam.EggNewEvery).ToString())
                .Replace("{1}", events.Params.Int(EventParam.EggCompleteBy).ToString());
            var def = _model.Definition;
            int items = def != null ? def.EggItems.Length : EventDefinition.EggItemCount;
            for (int i = 0; i < _ui.eggSlots.Length; i++)
            {
                bool secret = i == _ui.eggSlots.Length - 1;
                bool owned = secret ? events.SecretOwned : i < items && events.EggItemOwned(i);
                _ui.eggSlots[i].sprite = secret ? _ui.secret : _ui.paw;
                _ui.eggSlots[i].color = owned ? Color.white : new Color(0.47f, 0.4f, 0.33f, 0.35f);
                _ui.eggSlotDone[i].SetActive(owned);
            }
            return _ui.pages[(int)EventHubTab.Egg].sizeDelta.y;
        }

        private float BindShop(EventService events)
        {
            int count = Mathf.Min(events.ShopCount, _ui.shop.Length);
            for (int i = 0; i < _ui.shop.Length; i++)
            {
                var line = _ui.shop[i];
                bool visible = i < count;
                line.root.gameObject.SetActive(visible);
                if (!visible)
                    continue;
                var item = events.ShopItem(i);
                int price = events.ShopPrice(i), remaining = events.ShopRemaining(i);
                bool owned = item.Kind == EventShopKind.Item && events.IsOwned(item.Reward);
                line.icon.sprite = Icon(EventHubModel.ShopIcon(item));
                line.title.text = EventHubModel.ShopLabel(_text, item);
                line.sub.text = owned ? T(EventText.Owned)
                    : remaining == 0 ? T(EventText.ShopSoldOut)
                    : remaining > 0 ? Grouped(price) + "   " + F(EventText.ShopLeft, remaining.ToString())
                    : Grouped(price);
                line.subIcon.gameObject.SetActive(!owned && remaining != 0);
                line.button.gameObject.SetActive(!owned && remaining != 0);
                line.button.interactable = events.Currency >= price;
                line.buttonLabel.text = T(EventText.ShopBuy);
                line.done.SetActive(owned);
            }
            float y = _ui.shopTop + count * _ui.linePitch + 30f;
            int stickers = _stickerPick ? _model.MissingStickers(_stickers) : 0;
            _ui.stickerPanel.gameObject.SetActive(stickers > 0);
            if (stickers > 0)
            {
                _ui.stickerPanel.anchoredPosition = new Vector2(_ui.stickerPanel.anchoredPosition.x, -y);
                var def = _model.Definition;
                int page = def != null ? AlbumData.EventPage(def.AlbumEventNumber) : 0;
                for (int i = 0; i < _ui.stickerChips.Length; i++)
                {
                    bool visible = i < stickers;
                    _ui.stickerChips[i].gameObject.SetActive(visible);
                    if (visible)
                        _ui.stickerLabels[i].text = (_stickers[i] - AlbumData.StickerAt(page, 0) + 1).ToString();
                }
                y += _ui.stickerPanel.sizeDelta.y + 30f;
            }
            return y + 60f;
        }

        private float BindQuests(EventService events)
        {
            _ui.pending.text = F(EventText.QuestsPending, events.PendingDailyCount.ToString());
            _ui.swaps.text = F(EventText.QuestsSwapsLeft, events.SwapsLeft.ToString());
            var daily = events.DailyQuests;
            int dailyCount = Mathf.Min(daily.Count, _ui.daily.Length);
            bool canSwap = events.SwapsLeft > 0;
            for (int i = 0; i < _ui.daily.Length; i++)
            {
                bool visible = i < dailyCount;
                _ui.daily[i].root.gameObject.SetActive(visible);
                if (visible)
                {
                    var record = daily[i];
                    BindQuest(_ui.daily[i], events, record, true);
                    _ui.daily[i].button.gameObject.SetActive(!record.Done && canSwap);
                }
            }
            float y = _ui.questTop + dailyCount * _ui.questPitch + 40f;
            var title = _ui.longTitle.rectTransform;
            title.anchoredPosition = new Vector2(title.anchoredPosition.x, -y);
            y += 100f;
            var longs = events.LongQuests;
            int longCount = Mathf.Min(longs.Count, _ui.longQuests.Length);
            for (int i = 0; i < _ui.longQuests.Length; i++)
            {
                var refs = _ui.longQuests[i];
                bool visible = i < longCount;
                refs.root.gameObject.SetActive(visible);
                if (!visible)
                    continue;
                refs.root.anchoredPosition = new Vector2(refs.root.anchoredPosition.x, -(y + i * _ui.questPitch));
                BindQuest(refs, events, longs[i], false);
                refs.button.gameObject.SetActive(false);
            }
            return y + longCount * _ui.questPitch + 60f;
        }

        private void BindQuest(EventLineRefs refs, EventService events, in EventQuestRecord record, bool daily)
        {
            // The quest lane's formatter fills {n}, {activity} and {currency} (cached: no allocation when nothing changed).
            if (_questText == null || _questTextFor != _text)
            {
                _questText = new EventQuestText(_text);
                _questTextFor = _text;
            }
            refs.title.text = _questText.Line(events, record, daily);
            int target = Mathf.Max(1, record.Target);
            refs.sub.text = Grouped(Mathf.Min(record.Progress, target)) + " / " + Grouped(target) + "   +"
                            + events.Params.Int(daily ? EventParam.DailyQuestReward : EventParam.LongQuestReward);
            refs.done.SetActive(record.Done);
            refs.icon.color = record.Done ? new Color(1f, 1f, 1f, 0.6f) : Color.white;
        }

        private float BindChallenges(EventService events)
        {
            int total = events.TotalChallengeStars, max = EventDefinition.ChallengeCount * 3;
            _ui.starsTitle.text = F(EventText.ChallengesStars, total.ToString());
            SetFill(_ui.starsFill, max > 0 ? total / (float)max : 0f);
            for (int i = 0; i < _ui.challenges.Length; i++)
            {
                var refs = _ui.challenges[i];
                refs.title.text = F(EventText.ChallengeName, (i + 1).ToString());
                int stars = events.ChallengeStars(i);
                for (int s = 0; s < refs.stars.Length; s++)
                    refs.stars[s].sprite = s < stars ? _ui.starFull : _ui.starEmpty;
            }
            return _ui.challengeTop + _ui.challenges.Length * _ui.challengePitch + 60f;
        }

        private float BindGoal(EventService events)
        {
            int goal = Mathf.Max(1, events.PersonalGoal);
            SetFill(_ui.goalFill, Mathf.Clamp01(events.Points / (float)goal));
            _ui.goalProgress.text = T(EventText.GoalProgress).Replace("{0}", Grouped(Mathf.Min(events.Points, goal))).Replace("{1}", Grouped(goal));
            _ui.goalNote.text = T(events.GoalReached ? EventText.GoalReached : EventText.GoalNote);
            return _ui.pages[(int)EventHubTab.Goal].sizeDelta.y;
        }

        private float BindMemory(EventService events)
        {
            int count = Mathf.Min(events.MemoryCount, _ui.memory.Length);
            for (int i = 0; i < _ui.memory.Length; i++)
            {
                var line = _ui.memory[i];
                bool visible = i < count;
                line.root.gameObject.SetActive(visible);
                if (!visible)
                    continue;
                var item = events.MemoryItem(i);
                bool owned = events.MemoryOwned(i);
                int price = events.MemoryPrice(i);
                line.icon.sprite = Icon((int)item.Reward.Kind);
                line.title.text = EventHubModel.RewardLabel(_text, item.Reward);
                line.sub.text = owned ? T(EventText.Owned) : Grouped(price);
                line.subIcon.gameObject.SetActive(!owned);
                line.button.gameObject.SetActive(!owned);
                line.button.interactable = events.Currency >= price;
                line.buttonLabel.text = T(EventText.ShopBuy);
                line.done.SetActive(owned);
            }
            return _ui.shopTop + count * _ui.linePitch + 60f;
        }

        // ---------------- Helpers ----------------

        private Sprite Icon(int slot) =>
            slot >= 0 && slot < _ui.icons.Length && _ui.icons[slot] != null ? _ui.icons[slot] : _ui.paw;

        private static void SetFill(RectTransform fill, float k)
        {
            if (fill == null)
                return;
            var bar = (RectTransform)fill.parent;
            fill.sizeDelta = new Vector2(Mathf.Max(fill.rect.height, bar.sizeDelta.x * Mathf.Clamp01(k)), fill.sizeDelta.y);
            fill.gameObject.SetActive(k > 0f);
        }

        private string T(string key) => _text != null ? _text.Get(key) : key;

        private string F(string key, string value) => T(key).Replace("{0}", value);

        private string Grouped(long value) => ShopText.Grouped(value, ShopText.GroupSeparator(_text));

        private string Light(BigValue value)
        {
            bool fr = _text != null && _text.Language == "fr";
            var settings = fr ? new NumberFormatSettings(',', ' ') : new NumberFormatSettings('.', ',');
            int length = BigValueFormatter.Format(value, _number, 0, settings);
            return new string(_number, 0, length);
        }

        /// <summary>Prefab builder hook (Editor EventUiPipeline).</summary>
        public void SetReferences(EventHubRefs refs) => _ui = refs;
    }
}
