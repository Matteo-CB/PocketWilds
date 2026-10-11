using System;
using System.Globalization;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Simulation;

namespace PocketWilds.Presentation
{
    /// <summary>T48 event screens: text keys (FR and EN in Editor/UI/EventTexts.cs). Placeholders, never sentences built from fragments.</summary>
    public static class EventText
    {
        public const string Ends = "event.ends";
        public const string Starts = "event.starts";
        public const string Headline = "event.announce.headline";
        public const string AnnounceNote = "event.announce.note";

        /// <summary>T49: the download page of an event whose content group is missing (texts in Editor/UI/BurrowsTexts.cs).</summary>
        public const string DownloadSize = "event.download.size";
        public const string Download = "event.download.button";
        public const string Downloading = "event.download.progress";
        public const string DownloadFailed = "event.download.failed";

        public const string TabTrack = "event.tab.track";
        public const string TabEgg = "event.tab.egg";
        public const string TabShop = "event.tab.shop";
        public const string TabQuests = "event.tab.quests";
        public const string TabChallenges = "event.tab.challenges";
        public const string TabGoal = "event.tab.goal";
        public const string TabMemory = "event.tab.memory";

        public const string TrackPoints = "event.track.points";
        public const string TrackFree = "event.track.free";
        public const string ClaimAll = "event.track.claim_all";
        public const string Claim = "event.track.claim";
        public const string GoldGet = "event.gold.get";
        public const string GoldOwned = "event.gold.owned";

        public const string EggTitle = "event.egg.title";
        public const string EggOpen = "event.egg.open";
        public const string EggNewIn = "event.egg.new_in";
        public const string EggComplete = "event.egg.complete";
        public const string EggOddsTitle = "event.egg.odds.title";
        public const string EggOddsItem = "event.egg.odds.item";
        public const string EggOddsSecret = "event.egg.odds.secret";
        public const string EggOddsRule = "event.egg.odds.rule";
        public const string EggNew = "event.egg.new";
        public const string EggDuplicate = "event.egg.duplicate";
        public const string NotEnough = "event.not_enough";

        public const string ShopTitle = "event.shop.title";
        public const string ShopLeft = "event.shop.left";
        public const string ShopSoldOut = "event.shop.sold_out";
        public const string ShopBuy = "event.shop.buy";
        public const string ShopPickSticker = "event.shop.pick_sticker";
        public const string Owned = "event.owned";

        public const string QuestsDaily = "event.quests.daily";
        public const string QuestsLong = "event.quests.long";
        public const string QuestsSwap = "event.quests.swap";
        public const string QuestsPending = "event.quests.pending";
        public const string QuestsSwapsLeft = "event.quests.swaps_left";

        public const string ChallengesTitle = "event.challenges.title";
        public const string ChallengeName = "event.challenges.name";
        public const string ChallengesStars = "event.challenges.stars";
        public const string ChallengesOutfit = "event.challenges.outfit";
        public const string ChallengesPractice = "event.challenges.practice";

        public const string GoalTitle = "event.goal.title";
        public const string GoalProgress = "event.goal.progress";
        public const string GoalReached = "event.goal.reached";
        public const string GoalNote = "event.goal.note";

        public const string MemoryTitle = "event.memory_shop.title";

        public const string SummaryTitle = "event.summary.title";
        public const string SummaryCurrency = "event.summary.currency";
        public const string SummaryLight = "event.summary.light";
        public const string SummaryTier = "event.summary.tier";
        public const string SummaryOk = "event.summary.ok";

        /// <summary>The shop's « Événement » section and the Carnet d'or / Coffret cards (titles are the offers' display keys).</summary>
        public const string ShopSection = "event.shop.section";
        public const string GoldTitle = "event.gold.title";
        public const string GoldBody = "event.gold.body";
        public const string BoxTitle = "event.box.title";
        public const string BoxBody = "event.box.body";
        public const string ShopOneTime = "event.shop.one_time";
        public const string GoldContentTrack = "event.gold.content.track";
        public const string GoldContentOutfits = "event.gold.content.outfits";
        public const string GoldContentBonus = "event.gold.content.bonus";
        public const string BoxContentOutfit = "event.box.content.outfit";
        public const string BoxContentDecos = "event.box.content.decos";

        public const string RewardPrefix = "event.reward.";

        /// <summary>The generic label of a reward kind (« {0} Gemmes », « Tenue »); the event's own names come with its content (T49).</summary>
        public static string RewardKey(EventRewardKind kind) => RewardPrefix + kind switch
        {
            EventRewardKind.Gems => "gems",
            EventRewardKind.Tickets => "tickets",
            EventRewardKind.Treats => "treats",
            EventRewardKind.DiscoveryEgg => "discovery_egg",
            EventRewardKind.LightMinutes => "light",
            EventRewardKind.Sticker => "sticker",
            EventRewardKind.Decoration => "decoration",
            EventRewardKind.Outfit => "outfit",
            EventRewardKind.Variant => "variant",
            EventRewardKind.Frame => "frame",
            EventRewardKind.HatchEffect => "hatch_effect",
            EventRewardKind.Monument => "monument",
            EventRewardKind.Souvenir => "souvenir",
            EventRewardKind.Pose => "pose",
            _ => "none",
        };

        /// <summary>Every reward label key, for the text tables and the fit checks.</summary>
        public static readonly string[] RewardKeys =
        {
            RewardKey(EventRewardKind.Gems), RewardKey(EventRewardKind.Tickets), RewardKey(EventRewardKind.Treats), RewardKey(EventRewardKind.DiscoveryEgg),
            RewardKey(EventRewardKind.LightMinutes), RewardKey(EventRewardKind.Sticker), RewardKey(EventRewardKind.Decoration),
            RewardKey(EventRewardKind.Outfit), RewardKey(EventRewardKind.Variant), RewardKey(EventRewardKind.Frame),
            RewardKey(EventRewardKind.HatchEffect), RewardKey(EventRewardKind.Monument), RewardKey(EventRewardKind.Souvenir),
            RewardKey(EventRewardKind.Pose),
        };

        /// <summary>The shop line keys beyond item rewards: the event egg and the garden seeds.</summary>
        public const string ShopEgg = "event.reward.egg";
        public const string ShopSeeds = "event.reward.seeds";
    }

    public enum EventHubTab : byte { Track = 0, Egg = 1, Shop = 2, Quests = 3, Challenges = 4, Goal = 5, Memory = 6 }

    public enum EventCellState : byte
    {
        /// <summary>Tier not reached yet.</summary>
        Locked = 0,
        Claimable = 1,
        Claimed = 2,

        /// <summary>Reached, on the gold line, Carnet d'or not owned: the lock.</summary>
        NeedsGold = 3,
    }

    /// <summary>One tier of the track: the points it needs, its free and gold rewards (and a second free reward, the Souvenir).</summary>
    public readonly struct EventTierRow
    {
        public readonly int Tier;
        public readonly int Points;
        public readonly EventReward Free;
        public readonly EventReward FreeExtra;
        public readonly EventReward Gold;
        public readonly EventCellState FreeState;
        public readonly EventCellState GoldState;

        public EventTierRow(int tier, int points, EventReward free, EventReward freeExtra, EventReward gold, EventCellState freeState, EventCellState goldState)
        {
            Tier = tier;
            Points = points;
            Free = free;
            FreeExtra = freeExtra;
            Gold = gold;
            FreeState = freeState;
            GoldState = goldState;
        }

        public bool HasGold => Gold.Kind != EventRewardKind.None;
    }

    /// <summary>
    /// What the event hub (screen 018) reads: built over <see cref="EventService"/>, never per frame (on open and after each
    /// action). Pure logic, tested in edit mode: tier rows and their states, the ring's tier progress, what can be claimed,
    /// which tabs show, the real dates in the player's language and the odds as short percentages.
    /// </summary>
    public sealed class EventHubModel
    {
        public const int MaxTierRows = 36;
        public const int MaxShopRows = 10;
        public const int MaxMemoryRows = 16;
        public const int MaxDailyRows = 9;
        public const int MaxLongRows = 10;

        private readonly EventService _events;

        public EventHubModel(EventService events) => _events = events ?? throw new ArgumentNullException(nameof(events));

        public EventService Service => _events;
        public EventPhase Phase => _events.Phase;
        public bool Announce => _events.Phase == EventPhase.Announce;
        public bool Running => _events.Phase == EventPhase.Running;
        public EventDefinition Definition => _events.Definition;

        /// <summary>T49: an event is scheduled but its content group is not on the device: the hub shows the download page.</summary>
        public bool NeedsDownload => _events.Definition == null && _events.PendingDownload != null;

        public EventDefinition PendingDefinition => _events.PendingDownload;
        public DateTimeOffset PendingStartUtc => _events.PendingEntry.StartUtc;

        /// <summary>« Téléchargement de l'événement : {0} Mo »: whole megabytes, rounded up.</summary>
        public int DownloadMegabytes => EventContentGroups.Megabytes(_events.PendingDownloadBytes);

        public bool Downloading => _events.Downloading;

        /// <summary>Rows of the track: 30, or 36 with a returning year's bonus tiers.</summary>
        public int TierRows => Math.Min(MaxTierRows, Math.Max(0, _events.Definition != null ? _events.TierCount : 0));

        public EventTierRow GetTier(int tier)
        {
            var def = _events.Definition;
            EventTier row = null;
            if (def != null)
                row = tier <= def.Tiers.Length ? def.Tiers[tier - 1] : tier - def.Tiers.Length <= def.BonusTiers.Length ? def.BonusTiers[tier - def.Tiers.Length - 1] : null;
            var free = row != null && row.Free.Length > 0 ? row.Free[0] : default;
            var extra = row != null && row.Free.Length > 1 ? row.Free[1] : default;
            var gold = row != null && row.Gold.Length > 0 ? row.Gold[0] : default;
            return new EventTierRow(tier, _events.PointsForTier(tier), free, extra, gold, State(tier, false),
                gold.Kind == EventRewardKind.None ? EventCellState.Locked : State(tier, true));
        }

        public EventCellState State(int tier, bool gold)
        {
            if (_events.IsClaimed(tier, gold))
                return EventCellState.Claimed;
            if (tier > _events.Tier)
                return EventCellState.Locked;
            if (_events.CanClaim(tier, gold))
                return EventCellState.Claimable;
            return gold && !_events.GoldOwned ? EventCellState.NeedsGold : EventCellState.Locked;
        }

        /// <summary>The ring around the HUD button: share of the way from the current tier to the next (1 at the last tier).</summary>
        public float TierProgress => Progress(_events.Points, _events.Tier, _events.TierCount, _events.PointsForTier);

        public static float Progress(int points, int tier, int tierCount, Func<int, int> pointsFor)
        {
            if (tier >= tierCount)
                return 1f;
            int from = pointsFor(tier), to = pointsFor(tier + 1);
            return to <= from ? 1f : Math.Max(0f, Math.Min(1f, (points - from) / (float)(to - from)));
        }

        /// <summary>Rewards waiting on the track (free and gold), shown by the dot and « Tout récupérer ».</summary>
        public int ClaimableCount
        {
            get
            {
                int count = 0, rows = TierRows;
                for (int t = 1; t <= rows; t++)
                {
                    if (_events.CanClaim(t, false))
                        count++;
                    if (_events.CanClaim(t, true))
                        count++;
                }
                return count;
            }
        }

        public bool TabVisible(EventHubTab tab) => tab switch
        {
            EventHubTab.Memory => Running && _events.MemoryShopOpen,
            EventHubTab.Shop => Running && _events.ShopOpen,
            _ => Running,
        };

        /// <summary>Missing stickers of the event's album page the shop can sell (global sticker indices), at most <paramref name="into"/>.Length.</summary>
        public int MissingStickers(int[] into)
        {
            var def = _events.Definition;
            var albums = _events.Albums;
            int page = def != null ? AlbumData.EventPage(def.AlbumEventNumber) : -1;
            if (page < 0 || albums == null || !albums.IsOpen)
                return 0;
            int count = 0;
            for (int slot = 0; slot < AlbumData.StickersPerPage && count < into.Length; slot++)
            {
                int sticker = AlbumData.StickerAt(page, slot);
                if (!albums.Owned(sticker))
                    into[count++] = sticker;
            }
            return count;
        }

        /// <summary>« 12 novembre » / « November 12 »: a real date in the player's time zone, never a countdown.</summary>
        public static string FormatDate(DateTimeOffset utc, string language, TimeSpan? offset = null)
        {
            var local = offset.HasValue ? utc.ToOffset(offset.Value) : utc.ToLocalTime();
            bool fr = string.Equals(language, "fr", StringComparison.OrdinalIgnoreCase);
            var culture = CultureInfo.GetCultureInfo(fr ? "fr-FR" : "en-US");
            return local.ToString(fr ? "d MMMM" : "MMMM d", culture);
        }

        /// <summary>A chance as a short percentage: 1/7 « 14,3 » (fr) or « 14.3 » (en), 0.01 « 1 ».</summary>
        public static string FormatPercent(double chance, string language)
        {
            double percent = Math.Round(chance * 1000.0) / 10.0;
            bool fr = string.Equals(language, "fr", StringComparison.OrdinalIgnoreCase);
            return percent.ToString(percent == Math.Floor(percent) ? "0" : "0.0", CultureInfo.GetCultureInfo(fr ? "fr-FR" : "en-US"));
        }

        /// <summary>A reward's label in the player's language: the kind's text, its amount in place of {0}.</summary>
        public static string RewardLabel(ILocalization text, in EventReward reward)
        {
            if (reward.Kind == EventRewardKind.None)
                return string.Empty;
            string format = text.Get(EventText.RewardKey(reward.Kind));
            int amount = reward.Kind == EventRewardKind.Sticker ? 1 : reward.Amount;
            return format.Replace("{0}", amount.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>A shop line's label: the egg, the seeds, else its reward's.</summary>
        public static string ShopLabel(ILocalization text, EventShopItem item)
        {
            if (item == null)
                return string.Empty;
            return item.Kind switch
            {
                EventShopKind.Egg => text.Get(EventText.ShopEgg),
                EventShopKind.Seeds => text.Get(EventText.ShopSeeds).Replace("{0}", Math.Max(1, item.Reward.Amount).ToString(CultureInfo.InvariantCulture)),
                EventShopKind.Sticker => text.Get(EventText.RewardKey(EventRewardKind.Sticker)),
                _ => RewardLabel(text, item.Reward),
            };
        }

        /// <summary>The icon slot of a shop line: the egg, the seeds, else the reward's kind.</summary>
        public static int ShopIcon(EventShopItem item) => item == null ? 0 : item.Kind switch
        {
            EventShopKind.Egg => EventIconSlots.Egg,
            EventShopKind.Seeds => EventIconSlots.Seeds,
            EventShopKind.Sticker => (int)EventRewardKind.Sticker,
            _ => (int)item.Reward.Kind,
        };
    }

    /// <summary>The hub's icon table: one per <see cref="EventRewardKind"/> (0 to 14), then the egg and the seeds.</summary>
    public static class EventIconSlots
    {
        public const int Egg = 15;
        public const int Seeds = 16;
        public const int Count = 17;
    }

    /// <summary>PF_UI_EventHub (UI assembly): the event's opaque full screen (tabs), the announce page and the end summary card.</summary>
    public interface IEventHubView
    {
        bool IsOpen { get; }

        /// <summary>The summary card alone is up (over the refuge).</summary>
        bool SummaryOpen { get; }

        void Open(EventHubModel model, ILocalization text, IEventHubActions actions, EventHubTab tab);

        /// <summary>The service changed (a claim, an egg, a purchase, a quest, a new day): the open page binds again.</summary>
        void Refresh();

        void ShowEggResult(in EventEggResult result);

        /// <summary>The first open after an event's end: the card with the currency left, the Light received, the final tier.</summary>
        void ShowSummary(in EventEndSummary summary, string eventName, ILocalization text, IEventHubActions actions);

        void Close();

        void Tick(float unscaledDeltaTime);

        /// <summary>Builds the layout once at scene entry, invisibly.</summary>
        void Prewarm(ILocalization text);
    }

    public interface IEventHubActions
    {
        void ClaimTier(int tier, bool gold);
        void ClaimAllTiers();
        void OpenEventEgg();
        void BuyEventItem(int index, int sticker);
        void BuyMemoryItem(int index);
        void SwapQuest(int index);

        /// <summary>« Voir le Carnet d'or »: the hub closes, the shop opens on its « Événement » section (never a popup).</summary>
        void OpenGoldBook();

        void SummarySeen(string instanceId);
        void EventHubClosed();

        /// <summary>T49: « Télécharger » on the download page (the event's content group).</summary>
        void DownloadEvent();
    }

    /// <summary>T48: the event button on the HUD's right rail (Play, Today, Event), its tier progress ring and its dot.</summary>
    public interface IEventHud
    {
        void BindEvent(IEventHudActions actions);
        void SetEvent(bool visible, float ring, bool dot);
    }

    public interface IEventHudActions
    {
        void OpenEvent();
    }
}
