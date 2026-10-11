using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Events;
using PocketWilds.Editor.UI;
using PocketWilds.Simulation;
using UnityEditor;
using B = PocketWilds.Editor.Events.BurrowsEventPipeline;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T49 « L'Éveil des Terriers »: the real definition against the brief (track, egg, shop, Memory Shop, challenges), every reward
    /// resolving to something the build knows, the 2027 calendar entry, the accelerated event played end to end, the download page
    /// hook and the LiveOps_Burrows size budget.
    /// </summary>
    public class BurrowsEventTests
    {
        private static readonly ContentId GoldProduct = ContentId.FromSource("product.event.goldbook");
        private static readonly ContentId BoxProduct = ContentId.FromSource("product.event.chest");

        // ---------------------------------------------------------------- the brief's track, written out independently

        // G gems, T tickets, R treats, E discovery egg, L light minutes, S sticker tier, D/O/V/H/M/U item (decoration, outfit,
        // variant, hatch effect, monument, souvenir), F frame key.
        private static readonly string[] Free =
        {
            "S1", "G25", "D:decor.burrows.flowery_entrance", "T3", "O:outfit.burrows.01+U:decor.burrows.souvenir", "S6", "E1",
            "D:decor.burrows.moss_mound", "G25", "V:variant.mole.velvet", "S11", "T3", "D:decor.burrows.dew_mushrooms", "G25", "R5", "T3", "E1",
            "D:decor.burrows.watering_can", "S19", "O:outfit.burrows.02", "G25", "L60", "S23", "T3", "G25", "S26", "E1", "T3", "G25",
            "F:frame.event_burrows",
        };

        private static readonly string[] Gold =
        {
            "G50", "T3", "D:decor.burrows.flowery_entrance_gold", "G50", "H:effect.burrows.sprout_burst", "R5", "T3",
            "D:decor.burrows.moss_mound_gold", "R5", "O:outfit.burrows.05", "T3", "G50", "D:decor.burrows.dew_mushrooms_gold", "R5",
            "V:variant.mole.goldenmoss", "T3", "G50", "D:decor.burrows.watering_can_gold", "R5", "F:frame.event_burrows_gold", "T3", "R5", "G50",
            "T3", "O:outfit.burrows.06", "R5", "G50", "T3", "R10", "M:decor.burrows.root_burrow",
        };

        private static string Code(EventReward r)
        {
            string id(string p) => p + ":" + Source(r.Item);
            return r.Kind switch
            {
                EventRewardKind.Gems => "G" + r.Amount,
                EventRewardKind.Tickets => "T" + r.Amount,
                EventRewardKind.Treats => "R" + r.Amount,
                EventRewardKind.DiscoveryEgg => "E" + r.Amount,
                EventRewardKind.LightMinutes => "L" + r.Amount,
                EventRewardKind.Sticker => "S" + r.Amount,
                EventRewardKind.Decoration => id("D"),
                EventRewardKind.Outfit => id("O"),
                EventRewardKind.Variant => id("V"),
                EventRewardKind.HatchEffect => id("H"),
                EventRewardKind.Monument => id("M"),
                EventRewardKind.Souvenir => id("U"),
                EventRewardKind.Frame => "F:" + r.Key,
                EventRewardKind.Pose => "P:" + r.Key,
                _ => "?",
            };
        }

        private static readonly Dictionary<ContentId, string> Sources = BuildSources();

        private static Dictionary<ContentId, string> BuildSources()
        {
            var map = new Dictionary<ContentId, string>();
            foreach (var s in B.ItemIds().Concat(B.Variants))
                map[ContentId.FromSource(s)] = s;
            return map;
        }

        private static string Source(ContentId id) => Sources.TryGetValue(id, out var s) ? s : id.ToString();

        [Test]
        public void Definition_MatchesTheBrief()
        {
            var def = B.Build();
            Assert.AreEqual("event.burrows", def.Key);
            Assert.AreEqual("event.burrows.name", def.NameKey);
            Assert.AreEqual("event.burrows.currency", def.CurrencyKey);
            Assert.AreEqual("ICO_Event_Pousses", def.CurrencyIconKey);
            Assert.AreEqual(ContentId.FromSource("species.mole"), def.FeaturedSpecies);
            CollectionAssert.AreEqual(new[] { (int)ActivityId.HideAndSeek }, def.FeaturedActivities);
            Assert.AreEqual((int)ActivityId.HideAndSeek, def.FallbackActivity);
            Assert.AreEqual(103, def.AlbumEventNumber);
            Assert.AreEqual(3, def.ThemeColors.Length, "Tender green, cream, warm earth.");
            Assert.AreEqual(30, def.Tiers.Length);
            for (int t = 0; t < 30; t++)
            {
                Assert.AreEqual(t + 1, def.Tiers[t].Number);
                Assert.AreEqual(Free[t], string.Join("+", def.Tiers[t].Free.Select(Code)), "Free line, tier " + (t + 1));
                Assert.AreEqual(Gold[t], string.Join("+", def.Tiers[t].Gold.Select(Code)), "Gold line, tier " + (t + 1));
            }
            Assert.AreEqual(6, def.BonusTiers.Length, "Returning-year bonus tiers T31 to T36.");
            Assert.AreEqual(31, def.BonusTiers[0].Number);
            Assert.IsTrue(def.BonusTiers.All(b => b.Gold.Length == 0 && b.Free.Length == 1 && b.Free[0].IsItem));

            Assert.AreEqual(7, def.EggItems.Length);
            CollectionAssert.AreEqual(new[] { "variant.mole.cocoa", "variant.mole.sand", "variant.mole.mocha", "variant.rabbit.dew", "variant.fieldmouse.bud",
                "variant.hedgehog.clover", "variant.hedgehog.softmoss" }, def.EggItems.Select(e => Source(e.Variant)).ToArray());
            CollectionAssert.AreEqual(new[] { true, true, true, false, false, false, false }, def.EggItems.Select(e => e.Featured).ToArray());
            Assert.AreEqual(ContentId.FromSource("variant.mole.starnose"), def.SecretVariant);

            Assert.AreEqual(ContentId.FromSource("outfit.burrows.03"), def.ChallengeOutfit, "30 challenge stars give outfit 3.");
            Assert.AreEqual(ContentId.FromSource("decor.burrows.souvenir"), def.Souvenir);
            CollectionAssert.AreEqual(new[] { "outfit.burrows.07", "decor.burrows.garden_shed", "decor.burrows.mini_greenhouse", "decor.burrows.wooden_bench",
                "decor.burrows.garden_fence" }, def.BoxItems.Select(Source).ToArray());
            var levels = HideSeekRules.Create().Challenges.Select(c => c.Level).ToArray();
            CollectionAssert.AreEqual(levels, def.ChallengeLevels, "The event's challenges are Cache-cache's 10.");
        }

        /// <summary>The 10 challenges' fixed conditions (the counts and thresholds are T39's documented refuge deviations).</summary>
        [Test]
        public void HideSeekChallenges_KeepTheBriefConditions()
        {
            var rules = HideSeekRules.Create();
            double V(int c, int p) => rules.Value(p, rules.Challenges[c].Level, c);
            Assert.AreEqual(6, V(0, HideSeekRules.HintDelay), 1e-6, "01 hint at 6 s.");
            Assert.AreEqual(1, V(1, HideSeekRules.NoHints), 1e-6, "02 no hint.");
            Assert.AreEqual(1, V(1, HideSeekRules.NoItems), 1e-6, "02 no lens nor whistle.");
            Assert.AreEqual(0.10, V(2, HideSeekRules.Visibility), 1e-6, "03 visibility 10 %.");
            Assert.AreEqual(40f, rules.ChallengeSeconds(3, 60f), 1e-4, "04 in 40 s.");
            Assert.AreEqual(0.20, V(4, HideSeekRules.Visibility), 1e-6, "05 visibility 20 %.");
            Assert.AreEqual(4, V(5, HideSeekRules.MistakeLimit), 1e-6, "06 the 4th error ends the game.");
            Assert.AreEqual(0.25, V(6, HideSeekRules.Night), 1e-6, "07 lit circle 25 %.");
            Assert.AreEqual(1, V(7, HideSeekRules.SameSpecies), 1e-6, "08 all the same species.");
            Assert.AreEqual(0.15, V(7, HideSeekRules.Visibility), 1e-6, "08 visibility 15 %.");
            Assert.AreEqual(15, V(8, HideSeekRules.MoveEvery), 1e-6, "09 moves every 15 s.");
            Assert.AreEqual(45f, rules.ChallengeSeconds(9, 60f), 1e-4, "10 in 45 s.");
            Assert.AreEqual(0.08, V(9, HideSeekRules.Visibility), 1e-6, "10 visibility 8 %.");
            Assert.AreEqual(1, V(9, HideSeekRules.NoHints), 1e-6, "10 no hint.");
        }

        [Test]
        public void Calendar_ShipsBurrows2027()
        {
            var entries = DefaultEventCalendar.Create().Entries;
            Assert.AreEqual(1, entries.Count);
            var e = entries[0];
            Assert.AreEqual("event.burrows.2027", e.InstanceId);
            Assert.AreEqual(new DateTimeOffset(2027, 3, 10, 10, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(), e.AnnounceUnix);
            Assert.AreEqual(new DateTimeOffset(2027, 3, 12, 10, 0, 0, TimeSpan.Zero), e.StartUtc);
            Assert.AreEqual(new DateTimeOffset(2027, 3, 26, 10, 0, 0, TimeSpan.Zero), e.EndUtc);
        }

        // ---------------------------------------------------------------- resolution

        [Test]
        public void EveryReward_ResolvesToSomethingTheBuildKnows()
        {
            var rig = new Rig();
            var def = rig.Content.Events.Get(ContentId.FromSource(B.Key));
            var shop = rig.Content.Shop;
            var variants = new HashSet<ContentId>(B.Variants.Select(ContentId.FromSource));
            int page = AlbumData.EventPage(103);
            var rewards = def.Tiers.SelectMany(t => t.Free.Concat(t.Gold)).Concat(def.BonusTiers.SelectMany(t => t.Free)).Concat(def.Headline)
                .Concat(def.Shop.Select(s => s.Reward)).Concat(def.Memory.Select(m => m.Reward)).ToList();
            foreach (var r in rewards)
            {
                switch (r.Kind)
                {
                    case EventRewardKind.Variant:
                        Assert.IsTrue(variants.Contains(r.Item), "Variant " + Source(r.Item));
                        break;
                    case EventRewardKind.Frame:
                    case EventRewardKind.Pose:
                        bool profile = ProfileItemService.TryKindOf(r.Key, out var kind) && ProfileData.IndexOf(kind, r.Key) >= 0;
                        Assert.IsTrue(profile || shop.Entitlements.TryGet(r.Item, out _), "Profile item or owned item " + r.Key);
                        break;
                    case EventRewardKind.Sticker:
                        int slot = Array.IndexOf(EventService.TrackStickerTiers, r.Amount);
                        Assert.GreaterOrEqual(slot, 0, "Track sticker tier " + r.Amount);
                        Assert.Greater(AlbumData.StickerAt(page, slot), -1, "Sticker on page 103, slot " + slot);
                        break;
                    default:
                        if (r.IsItem)
                            Assert.IsTrue(shop.Entitlements.TryGet(r.Item, out _) || shop.Outfits.TryGet(r.Item, out _), "Item " + Source(r.Item));
                        break;
                }
            }
            foreach (var id in def.BoxItems.Append(def.ChallengeOutfit).Append(def.Souvenir))
                Assert.IsTrue(shop.Entitlements.TryGet(id, out _) || shop.Outfits.TryGet(id, out _), "Item " + Source(id));
            for (int i = 1; i <= 7; i++)
            {
                var outfit = shop.Outfits.Get(ContentId.FromSource(B.Outfit(i)));
                Assert.AreEqual(i == 7 ? 2 : 1, outfit.Accessories.Length, "O-OUT-103-0" + i);
                Assert.AreEqual(i == 5 || i == 6 ? "gold" : "default", outfit.Palette);
            }
            Assert.AreEqual(8, B.Accessories.Length);
            foreach (var row in B.Accessories)
            {
                var a = shop.Accessories.Get(ContentId.FromSource(row.Id));
                Assert.AreEqual(row.Socket, a.Socket);
                Assert.IsTrue(a.FitsFamily(RigFamily.SmallQuadruped) && a.FitsFamily(RigFamily.MediumQuadruped), row.Id);
            }
            Assert.AreEqual(EntitlementKind.Decoration, shop.Entitlements.Get(ContentId.FromSource(B.Monument)).Kind);
            Assert.AreEqual(EntitlementKind.Decoration, shop.Entitlements.Get(ContentId.FromSource(B.GoldDeco(4))).Kind);
            // Album page 103: the 12 stickers, their sources as the brief.
            int[] values = { 1, 6, 11, 19, 23, 26, 9, 18, 27, 4, 7, 10 };
            for (int s = 0; s < 12; s++)
            {
                var row = AlbumData.Sticker(AlbumData.StickerAt(page, s));
                Assert.AreEqual(values[s], row.FixedValue, "Sticker " + (s + 1));
            }
            // The page's names and the event's texts exist in both languages (no em dash).
            var keys = new HashSet<string>();
            for (int i = 0; i < BurrowsTexts.Texts.GetLength(0); i++)
            {
                Assert.IsTrue(keys.Add(BurrowsTexts.Texts[i, 0]), "Duplicate " + BurrowsTexts.Texts[i, 0]);
                for (int l = 1; l <= 2; l++)
                    Assert.IsTrue(!string.IsNullOrEmpty(BurrowsTexts.Texts[i, l]) && !BurrowsTexts.Texts[i, l].Contains('—'),BurrowsTexts.Texts[i, 0]);
            }
            for (int i = 0; i < shop.Entitlements.Count; i++)
                Assert.IsTrue(keys.Contains(shop.Entitlements[i].DisplayKey), "Text " + shop.Entitlements[i].DisplayKey);
            for (int i = 1; i <= 7; i++)
                Assert.IsTrue(keys.Contains(shop.Outfits.Get(ContentId.FromSource(B.Outfit(i))).DisplayKey), "Text of outfit " + i);
            Assert.IsTrue(keys.Contains(def.NameKey) && keys.Contains(def.CurrencyKey));
        }

        /// <summary>Once the event joins the shipped database, every variant it names is there (the animal lane's).</summary>
        [Test]
        public void ShippedDatabase_HasTheEventsVariantsWhenAttached()
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>("Assets/_Game/Content/DEF_ContentDatabase.asset");
            if (database == null || !database.events.Any(e => e != null && e.sourceId == B.Key))
                Assert.Ignore("event.burrows is not attached yet: run the animal pipeline, then BurrowsEventPipeline.BuildAll.");
            var registry = database.CreateBuilder().Build();
            Assert.IsTrue(registry.Events.TryGet(ContentId.FromSource(B.Key), out _));
            foreach (var v in B.Variants)
                Assert.IsTrue(registry.Variants.TryGet(ContentId.FromSource(v), out _), "Variant " + v);
        }

        // ---------------------------------------------------------------- prices

        [Test]
        public void Shop_AndMemoryShop_PricesAndLimits()
        {
            var rig = new Rig();
            rig.At(0.5);
            Assert.IsTrue(rig.Events.Join());
            var price = new Dictionary<string, (int price, int left)>
            {
                { "shop.egg", (300, -1) }, { "shop.outfit_4", (900, 1) }, { "shop.deco_5", (500, 1) }, { "shop.deco_6", (500, 1) },
                { "shop.deco_7", (650, 1) }, { "shop.deco_8", (800, 1) }, { "shop.tickets", (120, 5) }, { "shop.sticker", (250, -2) },
                { "shop.seeds", (150, 3) },
            };
            Assert.AreEqual(price.Count, rig.Events.ShopCount);
            for (int i = 0; i < rig.Events.ShopCount; i++)
            {
                var line = rig.Events.ShopItem(i);
                Assert.IsTrue(price.TryGetValue(line.Key, out var expected), line.Key);
                Assert.AreEqual(expected.price, rig.Events.ShopPrice(i), line.Key);
                if (expected.left != -2)
                    Assert.AreEqual(expected.left, rig.Events.ShopRemaining(i), line.Key);
            }
            var memory = new Dictionary<string, int>
            {
                { "memory.variant_1", 1500 }, { "memory.frame", 750 }, { "memory.pose", 1000 }, { "memory.deco_7", 850 }, { "memory.deco_8", 1000 },
            };
            for (int n = 1; n <= 4; n++)
                memory["memory.outfit_" + n] = 1150;
            for (int n = 1; n <= 6; n++)
                memory["memory.deco_" + n] = 650;
            Assert.AreEqual(memory.Count, rig.Events.MemoryCount);
            for (int i = 0; i < rig.Events.MemoryCount; i++)
            {
                var line = rig.Events.MemoryItem(i);
                Assert.AreEqual(memory[line.Key], rig.Events.MemoryPrice(i), line.Key);
            }
        }

        // ---------------------------------------------------------------- end to end

        [Test]
        public void AcceleratedBurrows_PlayedEndToEnd()
        {
            var rig = new Rig();
            var e = rig.Events;
            rig.Clock.Set(rig.Entry.StartUtc.AddHours(-24));
            Assert.AreEqual(EventPhase.Announce, e.Phase);
            rig.At(0.5);
            Assert.AreEqual(EventPhase.Running, e.Phase);
            Assert.IsTrue(e.Join());

            // Challenges: practice or rewarded, 3 stars each, only Cache-cache's count; 30 stars give outfit 3 (its goggles wearable).
            Assert.IsFalse(e.OnChallengePlayed(ActivityId.Grooming, 0, 3), "Only the featured activity's challenges count.");
            for (int c = 0; c < 10; c++)
                Assert.IsTrue(e.OnChallengePlayed(ActivityId.HideAndSeek, c, 3));
            Assert.AreEqual(30, e.TotalChallengeStars);
            Assert.IsTrue(rig.Owns(B.Outfit(3)) && rig.Owns(B.Accessories[2].Id), "Outfit 3 and its Burrow Goggles.");
            // The day's quests may count those stars too (« Gagne 1 étoile de défi »): their pay comes on top.
            int questPay = e.DailyQuests.Count(q => q.Done) * 40 + e.LongQuests.Count(q => q.Done) * 150;
            Assert.AreEqual(750 + questPay, rig.Inst.Currency, "25 Sprouts per new star (plus any quest finished by them).");
            Assert.IsTrue(rig.Albums.Owned(AlbumData.StickerAt(AlbumData.EventPage(103), 8)), "Sticker 9 at 27 stars.");

            // The whole track, then the Carnet d'or (retroactive) and the Box.
            e.GrantEventCurrency(6000, ActivityId.HideAndSeek);
            Assert.AreEqual(30, e.Tier);
            Assert.IsTrue(e.GoalReached, "Personal goal (pose « Sortir du terrier »).");
            Assert.IsTrue(rig.Owns(B.Souvenir), "The dated Souvenir at tier 5.");
            Assert.IsTrue(rig.Grants.GrantPurchase("tx-gold", GoldProduct, ContentId.None).IsNewGrant);
            Assert.AreEqual(60, e.ClaimAll(), "Every tier, free and gold.");
            Assert.IsTrue(rig.Owns(B.Accessories[0].Id), "Sprout Hat (T5).");
            Assert.IsTrue(rig.Owns(B.Accessories[1].Id), "Seed Sack (T20).");
            Assert.IsTrue(rig.Owns(B.Accessories[4].Id) && rig.Owns(B.Accessories[5].Id), "Golden Miner Helmet and Golden Shovel.");
            Assert.IsTrue(rig.Owns(B.Monument) && rig.Owns(B.HatchEffect) && rig.Owns(B.GoldFrame) && rig.Owns(B.GoldDeco(4)));
            Assert.IsTrue(rig.State.Collection.DiscoveredVariants.Contains(ContentId.FromSource(B.Velvet)));
            Assert.IsTrue(rig.State.Collection.DiscoveredVariants.Contains(ContentId.FromSource(B.GoldenMoss)));
            Assert.IsTrue(rig.Grants.GrantPurchase("tx-box", BoxProduct, ContentId.None).IsNewGrant);
            Assert.IsTrue(rig.Owns(B.Accessories[6].Id) && rig.Owns(B.Accessories[7].Id), "Outfit 7's bib and straps from the Box.");
            Assert.IsTrue(rig.Owns(B.Deco(9)) && rig.Owns(B.Deco(12)));

            // The egg: the 7 items within the guarantee.
            e.GrantEventCurrency(21 * 300, ActivityId.HideAndSeek);
            int opened = 0;
            while (e.EggItemsOwned < 7 && opened < 21)
            {
                Assert.AreEqual(EventEggStatus.Opened, e.OpenEgg().Status);
                opened++;
            }
            Assert.AreEqual(7, e.EggItemsOwned, "Every egg item by egg 21.");
            foreach (var v in B.EggVariants)
                Assert.IsTrue(rig.State.Collection.DiscoveredVariants.Contains(ContentId.FromSource(v)), v);

            // The shop: the Gardener's Scarf once.
            int scarf = Line(rig, "shop.outfit_4");
            Assert.AreEqual(EventBuyStatus.Bought, e.Buy(scarf));
            Assert.AreEqual(EventBuyStatus.Owned, e.Buy(scarf));
            Assert.IsTrue(rig.Owns(B.Accessories[3].Id));

            rig.At(15);
            Assert.AreEqual(EventPhase.None, e.Phase);
            Assert.IsTrue(e.TryGetPendingSummary(out var summary));
            Assert.AreEqual("event.burrows.2027", summary.InstanceId);
        }

        [Test]
        public void MissingGroup_ShowsTheDownloadPage()
        {
            var download = new FakeDownload { Bytes = 18L * 1024 * 1024 + 1 };
            var rig = new Rig(download);
            rig.At(0.5);
            Assert.AreEqual(EventPhase.None, rig.Events.Phase, "No phase while the content is missing.");
            Assert.IsNotNull(rig.Events.PendingDownload);
            Assert.AreEqual(19, EventContentGroups.Megabytes(rig.Events.PendingDownloadBytes));
            Assert.AreEqual("LiveOps_Burrows", EventContentGroups.GroupOf(B.Key));
            Assert.AreEqual("liveops.burrows", EventContentGroups.LabelOf(B.Key));
            bool done = false;
            Assert.IsTrue(rig.Events.StartDownload(ok => done = ok));
            Assert.IsTrue(done);
            Assert.AreEqual(EventPhase.Running, rig.Events.Phase);
            Assert.IsNull(rig.Events.PendingDownload);
        }

        [Test]
        public void ContentGroup_StaysUnder25MB()
        {
            var measure = BurrowsContentGroup.Measure();
            Assert.Less(measure.Bytes, BurrowsContentGroup.BudgetBytes, measure.Summary);
            // The whole event's content, the group plus what the base build still holds until T55.
            var footprint = BurrowsContentGroup.Footprint();
            Assert.Less(footprint.Bytes, BurrowsContentGroup.BudgetBytes, $"whole event {footprint.Bytes / 1048576.0:F2} MB");
        }

        // ---------------------------------------------------------------- rig

        private sealed class FakeDownload : IEventContentDownload
        {
            public long Bytes;
            public bool IsPresent(EventDefinition definition) => Bytes <= 0;
            public long DownloadBytes(EventDefinition definition) => Bytes;
            public bool IsDownloading(EventDefinition definition) => false;

            public void Download(EventDefinition definition, Action<bool> done)
            {
                Bytes = 0;
                done?.Invoke(true);
            }
        }

        private static int Line(Rig rig, string key)
        {
            for (int i = 0; i < rig.Events.ShopCount; i++)
                if (rig.Events.ShopItem(i).Key == key)
                    return i;
            throw new AssertionException("No line " + key);
        }

        private sealed class Rig
        {
            public readonly FakeGameClock Clock;
            public readonly PlayerState State;
            public readonly ContentRegistry Content;
            public readonly AlbumService Albums;
            public readonly GrantService Grants;
            public readonly EventService Events;
            public readonly EventCalendarEntry Entry;

            public Rig(IEventContentAvailability availability = null)
            {
                var builder = new ContentRegistryBuilder();
                // Stand-ins for the animal lane's species and variants (same ids), then the real catalog and event.
                foreach (var species in new[] { "species.mole", "species.rabbit", "species.fieldmouse", "species.hedgehog" })
                {
                    builder.AddSpecies(new SpeciesDefinition(ContentId.FromSource(species), species + ".name", RigFamily.SmallQuadruped,
                        ContentId.FromSource("biome.grassland"), 1.0, "PF_Animal_Test"), species);
                    AddVariant(builder, species.Replace("species.", "variant.") + ".plain", species, RarityTier.Common, false);
                }
                foreach (var v in B.Variants)
                    AddVariant(builder, v, "species." + v.Split('.')[1], v == B.StarNose ? RarityTier.Legendary : RarityTier.Rare, v == B.StarNose);
                B.AddCatalog(builder);
                builder.AddEvent(B.Build(), B.Key);
                builder.AddProduct(new ProductDefinition(GoldProduct, "pw.event.goldbook", ProductKind.Consumable, 499, 0, null, EventProductScope.GoldBook),
                    "product.event.goldbook");
                builder.AddProduct(new ProductDefinition(BoxProduct, "pw.event.chest", ProductKind.Consumable, 999, 800, null, EventProductScope.Box),
                    "product.event.chest");
                Content = builder.Build();
                Entry = DefaultEventCalendar.Entries()[0];
                Clock = new FakeGameClock(Entry.StartUtc.AddDays(-5));
                State = PlayerState.CreateNew(Clock.UtcNow);
                var economy = new EconomyService(State, Content, EconomyConfig.Default, null);
                var mastery = new MasteryService(State, economy.Config, economy);
                Albums = new AlbumService(State.Albums) { Unlocked = true };
                Grants = new GrantService(Content, State, null);
                Events = new EventService(State, Content, Clock, null, new EventConfig(), DefaultEventCalendar.Create())
                {
                    Economy = economy, Granter = new RewardGranter(State, economy, null, null), Mastery = mastery, Albums = Albums,
                    DefaultTreat = () => ContentId.FromSource("treat.mushroom_biscuit"),
                };
                if (availability != null)
                    Events.Availability = availability;
                Grants.Events = Events;
                var animal = new AnimalInstance
                {
                    Species = ContentId.FromSource("species.rabbit"), Variant = ContentId.FromSource("variant.rabbit.plain"), Seed = 7, Rarity = RarityTier.Common,
                };
                animal.InstanceId = State.Collection.Add(animal);
                State.Collection.DiscoveredSpecies.Add(animal.Species);
                State.Collection.DiscoveredVariants.Add(animal.Variant);
                economy.OnAnimalAdded(animal);
            }

            public void At(double days) => Clock.Set(Entry.StartUtc.AddDays(days));
            public EventInstanceState Inst => Events.Instance;
            public bool Owns(string item) => State.Entitlements.Owned.Contains(ContentId.FromSource(item));

            private static void AddVariant(ContentRegistryBuilder builder, string id, string species, RarityTier rarity, bool secret) =>
                builder.AddVariant(new VariantDefinition(ContentId.FromSource(id), ContentId.FromSource(species), id + ".name", rarity,
                    new UnityEngine.Color32[VariantDefinition.PaletteSize], PatternChannel.None, 0, isSecret: secret), id);
        }
    }
}
