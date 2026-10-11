using System;
using System.Collections.Generic;
using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Content
{
    /// <summary>
    /// T49: authoring asset of one shipped event (DEF_Event_&lt;Name&gt;, written by its editor pipeline, listed in
    /// <see cref="ContentDatabaseAsset.events"/>). Plain data mirroring <see cref="EventDefinition"/>: the track (free and Carnet
    /// d'or lines, the returning-year bonus tiers), the egg, the shop, the Memory Shop, the challenges, the album page, the
    /// Souvenir, the Box items and the theme. Numbers (tier points, prices, guarantees) stay in the event parameters; dates in the
    /// calendar. The quests use the shared pools (<see cref="EventQuestCatalog"/>).
    /// </summary>
    [CreateAssetMenu(menuName = "Pocket Wilds/Event Definition", fileName = "DEF_Event_New")]
    public sealed class EventDefinitionAsset : ScriptableObject
    {
        [Serializable]
        public sealed class Reward
        {
            public EventRewardKind kind;
            [Tooltip("Count of Gems, tickets, treats, eggs, Light minutes; the tier value of a sticker; 1 for an item.")]
            public int amount = 1;
            [Tooltip("Source id of the item, variant, treat or seed plant; empty for counts.")]
            public string item = string.Empty;
            [Tooltip("Profile key of a frame or a pose (« frame.event_burrows »).")]
            public string key = string.Empty;

            public EventReward ToRuntime() => new EventReward(kind, amount, Id(item), key);
        }

        [Serializable]
        public sealed class Tier
        {
            public int number;
            public List<Reward> free = new List<Reward>();
            public List<Reward> gold = new List<Reward>();
        }

        [Serializable]
        public sealed class EggItem
        {
            public string variant = string.Empty;
            [Tooltip("A variant of the featured species (v2, v3, v4); otherwise an event variant of an existing species.")]
            public bool featured;
        }

        [Serializable]
        public sealed class ShopLine
        {
            public string key = "shop.new";
            public EventShopKind kind;
            public EventPriceSlot price;
            public Reward reward = new Reward();
        }

        [Serializable]
        public sealed class MemoryLine
        {
            public string key = "memory.new";
            public EventPriceSlot price;
            public Reward reward = new Reward();
        }

        public string sourceId = "event.new";
        public int version = 1;
        public string nameKey = string.Empty;
        public string currencyKey = string.Empty;
        public string currencyIconKey = string.Empty;
        public string featuredSpecies = string.Empty;
        [Tooltip("ActivityId values (the content assembly does not know the enum).")]
        public List<int> featuredActivities = new List<int>();
        public int fallbackActivity;
        [Tooltip("101 to 112 (page « page.event.N »), 0 for none.")]
        public int albumEventNumber;
        public List<Tier> tiers = new List<Tier>();
        [Tooltip("Returning-year bonus tiers T31 to T36 (free line only).")]
        public List<Tier> bonusTiers = new List<Tier>();
        public List<EggItem> eggItems = new List<EggItem>();
        public string secretVariant = string.Empty;
        public List<ShopLine> shop = new List<ShopLine>();
        public List<MemoryLine> memory = new List<MemoryLine>();
        public List<int> challengeLevels = new List<int>();
        public string challengeOutfit = string.Empty;
        public string souvenir = string.Empty;
        public List<string> boxItems = new List<string>();
        public List<Reward> headline = new List<Reward>();
        public List<string> themeKeys = new List<string>();
        [Tooltip("0xRRGGBB: primary, background, accent.")]
        public List<Color32> themeColors = new List<Color32>();

        public EventDefinition ToRuntime()
        {
            var colors = new uint[themeColors.Count];
            for (int i = 0; i < colors.Length; i++)
                colors[i] = ((uint)themeColors[i].r << 16) | ((uint)themeColors[i].g << 8) | themeColors[i].b;
            var boxes = new ContentId[boxItems.Count];
            for (int i = 0; i < boxes.Length; i++)
                boxes[i] = Id(boxItems[i]);
            var egg = new EventEggItem[eggItems.Count];
            for (int i = 0; i < egg.Length; i++)
                egg[i] = new EventEggItem(Id(eggItems[i].variant), eggItems[i].featured);
            var shopLines = new EventShopItem[shop.Count];
            for (int i = 0; i < shopLines.Length; i++)
                shopLines[i] = new EventShopItem(shop[i].key, shop[i].kind, shop[i].price, shop[i].reward != null ? shop[i].reward.ToRuntime() : default);
            var memoryLines = new EventMemoryItem[memory.Count];
            for (int i = 0; i < memoryLines.Length; i++)
                memoryLines[i] = new EventMemoryItem(memory[i].key, memory[i].price, memory[i].reward != null ? memory[i].reward.ToRuntime() : default);
            return new EventDefinition(sourceId, version, nameKey, currencyKey, Id(featuredSpecies), featuredActivities.ToArray(), fallbackActivity,
                albumEventNumber, Tiers(tiers), Tiers(bonusTiers), egg, Id(secretVariant), shopLines, memoryLines, EventQuestCatalog.Daily,
                EventQuestCatalog.Long, challengeLevels.ToArray(), Id(challengeOutfit), Id(souvenir), boxes, Rewards(headline), themeKeys.ToArray(),
                currencyIconKey, colors);
        }

        private static EventTier[] Tiers(List<Tier> source)
        {
            var result = new EventTier[source.Count];
            for (int i = 0; i < result.Length; i++)
                result[i] = new EventTier(source[i].number, Rewards(source[i].free), Rewards(source[i].gold));
            return result;
        }

        private static EventReward[] Rewards(List<Reward> source)
        {
            if (source == null)
                return Array.Empty<EventReward>();
            var result = new EventReward[source.Count];
            for (int i = 0; i < result.Length; i++)
                result[i] = source[i].ToRuntime();
            return result;
        }

        private static ContentId Id(string source) => string.IsNullOrEmpty(source) ? ContentId.None : ContentId.FromSource(source);
    }
}
