using System;
using NUnit.Framework;
using PocketWilds.Core;
using PocketWilds.Simulation;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>T44A: the draws and their guarantees (doc 01 Albums, sections 1.4 to 1.7, tests of section 1.14).</summary>
    public class AlbumDrawTests
    {
        private const int P1 = 0;
        private const int P2 = 1;
        private static readonly int Grooming = AlbumData.ActivityPage(ActivityId.Grooming);
        private static readonly int Spring = AlbumData.SeasonPage(1);

        internal static AlbumService Open(AlbumConfig config = null, ulong seed = 12345UL, params int[] pages)
        {
            var state = new AlbumState { Seed = seed };
            var albums = new AlbumService(state, config) { Unlocked = true };
            foreach (int p in pages)
                albums.OpenPage(p);
            return albums;
        }

        private static void Own(AlbumService albums, int sticker)
        {
            AlbumState.SetBit(albums.State.Owned, sticker, true);
            albums.Recount();
        }

        private static void OwnDrawables(AlbumService albums, int page, int except = -1)
        {
            for (int slot = 0; slot < 12; slot++)
            {
                int s = AlbumData.StickerAt(page, slot);
                if (AlbumData.Sticker(s).Drawable && s != except)
                    AlbumState.SetBit(albums.State.Owned, s, true);
            }
            albums.Recount();
        }

        /// <summary>Draws without giving: the page counts of <paramref name="n"/> draws (Shiny counters reset each time).</summary>
        private static int[] PageCounts(AlbumService albums, StickerDrawRequest request, int n, int[] stickers = null)
        {
            var counts = new int[AlbumState.Pages];
            for (int i = 0; i < n; i++)
            {
                Array.Clear(albums.State.ShinyDraws, 0, AlbumState.Pages);
                albums.State.DuplicateStreak = 0;
                Assert.IsTrue(albums.Draws.Draw(in request, out var draw));
                counts[draw.Page]++;
                if (stickers != null)
                    stickers[draw.Sticker]++;
            }
            return counts;
        }

        // ---------------- Page choice (section 1.5) ----------------

        [Test]
        public void PageChoice_RemovesClosedPages_AndProratesTheRest()
        {
            var albums = Open(null, 1UL, P1, Grooming);
            var counts = PageCounts(albums, new StickerDrawRequest(StickerSource.ActivityGame, ActivityId.Grooming, 0), 6000);
            Assert.AreEqual(0, counts[P2], "Page 2 is closed: removed.");
            Assert.AreEqual(2.0 / 3.0, counts[Grooming] / 6000.0, 0.03, "50 / (50 + 25).");
            Assert.AreEqual(1.0 / 3.0, counts[P1] / 6000.0, 0.03);
        }

        [Test]
        public void PageChoice_RemovesCompletePages_UnlessAllAre()
        {
            var albums = Open(null, 2UL, P1, P2, Grooming);
            OwnDrawables(albums, P1);
            var request = new StickerDrawRequest(StickerSource.ActivityGame, ActivityId.Grooming, 0);
            var counts = PageCounts(albums, request, 3000);
            Assert.AreEqual(0, counts[P1], "Page 1's drawables are all owned: removed.");
            Assert.AreEqual(2.0 / 3.0, counts[Grooming] / 3000.0, 0.03, "50 / (50 + 25) with page 2.");

            OwnDrawables(albums, P2);
            OwnDrawables(albums, Grooming);
            counts = PageCounts(albums, request, 4000);
            Assert.AreEqual(0.5, counts[Grooming] / 4000.0, 0.03, "All complete: all kept, a duplicate.");
            Assert.AreEqual(0.25, counts[P1] / 4000.0, 0.03);
            Assert.AreEqual(0.25, counts[P2] / 4000.0, 0.03);
        }

        [Test]
        public void SeasonDraw_Takes15Percent_OnlyWhileTheSeasonPageMissesOne()
        {
            var albums = Open(null, 3UL, P1, P2, Grooming);
            albums.SetSeason(1);
            Assert.IsTrue(albums.IsPageOpen(Spring));
            var game = new StickerDrawRequest(StickerSource.ActivityGame, ActivityId.Grooming, 0);
            var counts = PageCounts(albums, game, 8000);
            Assert.AreEqual(0.15, counts[Spring] / 8000.0, 0.02, "The season roll.");

            counts = PageCounts(albums, new StickerDrawRequest(StickerSource.Record, ActivityId.Grooming, 0), 500);
            Assert.AreEqual(0, counts[Spring], "A record never goes to the season page.");

            OwnDrawables(albums, Spring);
            counts = PageCounts(albums, game, 2000);
            Assert.AreEqual(0, counts[Spring], "No drawable missing on the season page: its table as usual.");
        }

        // ---------------- Rarity and preferred source (section 1.6) ----------------

        [Test]
        public void Rarity_Weighs70_25_5_AndSpreadsAMissingRarity()
        {
            var albums = Open(null, 4UL, P1);
            var stickers = new int[AlbumState.Stickers];
            PageCounts(albums, new StickerDrawRequest(StickerSource.Wish, ActivityId.None, 0), 10000, stickers);
            int common = 0, rare = 0, shiny = 0;
            for (int s = 0; s < 12; s++)
            {
                var rarity = AlbumData.Sticker(s).Rarity;
                if (rarity == StickerRarity.Common) common += stickers[s];
                else if (rarity == StickerRarity.Rare) rare += stickers[s];
                else shiny += stickers[s];
            }
            Assert.AreEqual(0, stickers[6], "The companion (07) is never drawn.");
            Assert.AreEqual(0.70, common / 10000.0, 0.02);
            Assert.AreEqual(0.25, rare / 10000.0, 0.02);
            Assert.AreEqual(0.05, shiny / 10000.0, 0.01);

            // The Grooming page has no drawable Shiny: 70 and 25 spread to 100.
            albums = Open(null, 5UL, Grooming);
            Array.Clear(stickers, 0, stickers.Length);
            PageCounts(albums, new StickerDrawRequest(StickerSource.ActivityGame, ActivityId.Grooming, -1), 8000, stickers);
            int rareGrooming = stickers[AlbumData.StickerAt(Grooming, 7)];
            Assert.AreEqual(25.0 / 95.0, rareGrooming / 8000.0, 0.02);
        }

        [Test]
        public void PreferredSource_WeighsThree()
        {
            var albums = Open(null, 6UL, P1);
            var stickers = new int[AlbumState.Stickers];
            var wish = new StickerDrawRequest(StickerSource.Wish, ActivityId.None, 0) { Species = ContentId.FromSource("species.rabbit") };
            PageCounts(albums, wish, 12000, stickers);
            int commons = 0;
            for (int s = 0; s < 6; s++)
                commons += stickers[s];
            Assert.AreEqual(3.0 / 8.0, stickers[0] / (double)commons, 0.03, "STK-GL-01 Souhait d'un Lapin: 3 against 5 × 1.");

            Array.Clear(stickers, 0, stickers.Length);
            PageCounts(albums, new StickerDrawRequest(StickerSource.Wish, ActivityId.None, 0) { Species = ContentId.FromSource("species.fox") },
                6000, stickers);
            commons = 0;
            for (int s = 0; s < 6; s++)
                commons += stickers[s];
            Assert.AreEqual(1.0 / 6.0, stickers[0] / (double)commons, 0.03, "Another species: weight 1.");
        }

        // ---------------- Guarantees ----------------

        [Test]
        public void Newness_AfterThreeDuplicates_GivesAMissingOne()
        {
            var albums = Open(null, 7UL, Grooming);
            int missing = AlbumData.StickerAt(Grooming, 3);
            OwnDrawables(albums, Grooming, missing);
            var game = new StickerDrawRequest(StickerSource.ActivityGame, ActivityId.Grooming, -1);
            int duplicates = 0;
            for (int i = 0; i < 200; i++)
            {
                albums.State.DuplicateStreak = 0;
                albums.Draws.Draw(in game, out var d);
                if (albums.State.IsOwned(d.Sticker))
                    duplicates++;
            }
            Assert.Greater(duplicates, 0, "Without the guarantee a duplicate can come.");
            for (int i = 0; i < 50; i++)
            {
                albums.State.DuplicateStreak = 3;
                Assert.IsTrue(albums.Draws.Draw(in game, out var d));
                Assert.AreEqual(missing, d.Sticker, "The 4th draw after 3 duplicates.");
                Assert.IsTrue(d.NewnessGuarantee);
                Assert.AreEqual(0, albums.State.DuplicateStreak);
            }

            // The streak counts every draw source and is reset by a new sticker.
            albums = Open(null, 8UL, Grooming);
            OwnDrawables(albums, Grooming);
            for (int i = 0; i < 3; i++)
                albums.Draw(game);
            Assert.AreEqual(3, albums.State.DuplicateStreak);
        }

        [Test]
        public void Shiny_GivenAtThe30thDrawOfItsPage_AndTheCounterStops()
        {
            var albums = Open(null, 9UL, P1);
            int shiny = AlbumData.StickerAt(P1, 11);
            var wish = new StickerDrawRequest(StickerSource.Wish, ActivityId.None, 0);
            albums.State.ShinyDraws[P1] = 29;
            Assert.IsTrue(albums.Draws.Draw(in wish, out var d));
            Assert.AreEqual(shiny, d.Sticker);
            Assert.IsTrue(d.ShinyGuarantee);

            albums = Open(null, 10UL, P1);
            for (int i = 0; i < 30 && !albums.Owned(shiny); i++)
                albums.Draw(wish);
            Assert.IsTrue(albums.Owned(shiny), "Owned at the latest on the 30th draw of the page.");
            byte counter = albums.State.ShinyDraws[P1];
            albums.Draw(wish);
            Assert.AreEqual(counter, albums.State.ShinyDraws[P1], "The counter stops once the Shiny is owned.");
        }

        [Test]
        public void Record_GivesAMissingDrawableOfItsActivityPage()
        {
            var albums = Open(null, 11UL, P1, P2, Grooming);
            for (int slot = 0; slot < 5; slot++)
                Own(albums, AlbumData.StickerAt(Grooming, slot));
            var record = new StickerDrawRequest(StickerSource.Record, ActivityId.Grooming, 0);
            for (int i = 0; i < 100; i++)
            {
                albums.State.DuplicateStreak = 0;
                Assert.IsTrue(albums.Draws.Draw(in record, out var d));
                Assert.AreEqual(Grooming, d.Page, "Page of the activity 100 %.");
                Assert.IsFalse(albums.State.IsOwned(d.Sticker));
                Assert.IsTrue(AlbumData.Sticker(d.Sticker).Drawable);
            }
        }

        [Test]
        public void DailyGift_NeverDuplicates_WhileOneIsMissing()
        {
            var albums = Open(null, 12UL, P1, P2, Grooming);
            albums.SetSeason(2);
            int missing = 0;
            for (int p = 0; p < AlbumState.Pages; p++)
                if (albums.IsPageOpen(p))
                    missing += albums.Draws.MissingDrawables(p);
            Assert.AreEqual(11 + 4 + 7 + 9, missing);
            for (int i = 0; i < missing; i++)
            {
                int before = albums.AlbumStars;
                int s = albums.Draw(new StickerDrawRequest(i % 2 == 0 ? StickerSource.DailyGift : StickerSource.Calendar));
                Assert.GreaterOrEqual(s, 0);
                Assert.AreEqual(before, albums.AlbumStars, "No duplicate while a drawable is missing.");
            }
            int stars = albums.AlbumStars;
            albums.Draw(new StickerDrawRequest(StickerSource.DailyGift));
            Assert.Greater(albums.AlbumStars, stars, "Nothing missing any more: a duplicate, turned into stars.");
        }

        [Test]
        public void Availability_NeverDrawsAStickerWithoutArtOrSubject()
        {
            var albums = Open(null, 13UL, P1, Grooming);
            albums.SetAvailableArt(new[] { "sticker.gl.02", "sticker.gl.03", "sticker.act.001.01" });
            Assert.IsTrue(albums.Available(1) && albums.Available(2) && !albums.Available(0));
            var stickers = new int[AlbumState.Stickers];
            PageCounts(albums, new StickerDrawRequest(StickerSource.ActivityGame, ActivityId.Grooming, 0), 2000, stickers);
            for (int s = 0; s < AlbumState.Stickers; s++)
                if (stickers[s] > 0)
                    Assert.IsTrue(albums.Available(s), AlbumData.Sticker(s).Key + " drawn without art.");

            albums.SetSubjects(a => a == ActivityId.Grooming, sp => sp == ContentId.FromSource("species.rabbit"), null, b => b == 0);
            albums.SetAvailableArt(new[] { "sticker.gl.01", "sticker.gl.02", "sticker.act.002.01", "sticker.act.001.01", "sticker.fo.01" });
            Assert.IsTrue(albums.Available(0), "Rabbit: shipped and captured.");
            Assert.IsFalse(albums.Available(1), "Squirrel not in this rule's build.");
            Assert.IsFalse(albums.Available(AlbumData.IndexOf("sticker.act.002.01")), "Hide and Seek not shipped here.");
            Assert.IsFalse(albums.Available(AlbumData.IndexOf("sticker.fo.01")), "The Forest is not built.");
            Assert.IsFalse(albums.TryChoose(1), "A sticker without art is never chosen.");
        }

        // ---------------- Paid games (section 1.4, decision of 7 October) ----------------

        [Test]
        public void PaidGame_FollowsTheFixedOrder_WithoutAnyDraw()
        {
            var albums = Open(null, 14UL, P1, Grooming);
            ulong seed = albums.State.Seed;
            int[] order = { 0, 1, 2, 3, 4, 5, 7 };
            Own(albums, AlbumData.StickerAt(Grooming, 2));
            Assert.AreEqual(AlbumData.StickerAt(Grooming, 0), albums.NextPaidSticker(ActivityId.Grooming), "Shown before buying.");
            foreach (int slot in order)
            {
                if (slot == 2)
                    continue;
                Assert.AreEqual(AlbumData.StickerAt(Grooming, slot), albums.GivePaid(ActivityId.Grooming));
            }
            Assert.AreEqual(seed, albums.State.Seed, "No draw: the generator never moved.");
            Assert.AreEqual(0, albums.State.DuplicateStreak);
            Assert.AreEqual(0, albums.AlbumStars);
            Assert.AreEqual(-1, albums.GivePaid(ActivityId.Grooming), "Nothing left: 1 album star instead.");
            Assert.AreEqual(1, albums.AlbumStars);
            Assert.AreEqual(seed, albums.State.Seed);
            for (int s = 0; s < 12; s++)
                Assert.IsFalse(albums.Owned(s), "Never a biome page.");
        }

        [Test]
        public void Draws_AreReplayedFromTheSeed()
        {
            int[] a = Sequence(99UL), b = Sequence(99UL), c = Sequence(100UL);
            Assert.AreEqual(a, b);
            Assert.AreNotEqual(a, c);
        }

        private static int[] Sequence(ulong seed)
        {
            var albums = Open(null, seed, P1, P2, Grooming);
            var result = new int[20];
            for (int i = 0; i < result.Length; i++)
                result[i] = albums.Draw(new StickerDrawRequest(StickerSource.ActivityGame, ActivityId.Grooming, 0));
            return result;
        }

        // ---------------- Allocation ----------------

        [Test]
        public void ReadsAndADraw_DoNotAllocate()
        {
            var albums = Open(null, 15UL, P1, P2, Grooming);
            albums.SetSeason(3);
            var request = new StickerDrawRequest(StickerSource.ActivityGame, ActivityId.Grooming, 0);
            var gift = new StickerDrawRequest(StickerSource.DailyGift);
            int sink = 0;
            void Reads()
            {
                sink += albums.IsOpen ? 1 : 0;
                for (int p = 0; p < albums.PageCount; p++)
                {
                    var page = albums.PageAt(p);
                    sink += page.FirstSticker + albums.OwnedOnPage(p) + (albums.IsPageOpen(p) ? 1 : 0);
                }
                for (int s = 0; s < albums.StickerCount; s++)
                {
                    var info = albums.StickerAt(s);
                    sink += info.Slot + (albums.Owned(s) ? 1 : 0) + (albums.Unseen(s) ? 1 : 0) + (albums.Available(s) ? 1 : 0);
                }
                sink += albums.AlbumStars + albums.ChoicePrice + albums.PendingCount + albums.AlbumKey(0).Length + (int)albums.KindOfAlbum(15);
                if (albums.PendingCount > 0)
                    sink += albums.PendingAt(0).Sticker;
            }
            void Draw()
            {
                albums.Draws.Draw(in request, out var d);
                sink += d.Sticker;
                sink += albums.Draw(in gift);
                sink += albums.GivePaid(ActivityId.Grooming);
            }
            Reads();
            Draw();
            Assert.That(Reads, Is.Not.AllocatingGCMemory());
            Assert.That(Draw, Is.Not.AllocatingGCMemory());
            Assert.Greater(sink, 0);
        }
    }
}
