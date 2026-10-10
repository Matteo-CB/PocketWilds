using System;
using System.Collections.Generic;
using NUnit.Framework;
using PocketWilds.Core;
using PocketWilds.Simulation;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>T44A: the 720 stickers are validated data (doc 01 Albums, sections 1.1, 1.3 and 2 to 5).</summary>
    public class AlbumDataTests
    {
        [Test]
        public void Counts_16Albums_60Pages_720Stickers()
        {
            Assert.AreEqual(16, AlbumData.AlbumCount);
            Assert.AreEqual(60, AlbumData.PageCount);
            Assert.AreEqual(720, AlbumData.StickerCount);
            Assert.AreEqual(AlbumState.Stickers, AlbumData.StickerCount);
            Assert.AreEqual(AlbumState.Pages, AlbumData.PageCount);
            int biome = 0, evt = 0, season = 0, activity = 0;
            for (int a = 0; a < AlbumData.AlbumCount; a++)
            {
                var row = AlbumData.Album(a);
                switch (row.Kind)
                {
                    case AlbumKind.Biome: biome++; Assert.AreEqual(2, row.Pages); break;
                    case AlbumKind.Event: evt++; Assert.AreEqual(12, row.Pages); break;
                    case AlbumKind.Season: season++; Assert.AreEqual(4, row.Pages); break;
                    default: activity++; Assert.AreEqual(18, row.Pages); break;
                }
                for (int p = row.FirstPage; p < row.FirstPage + row.Pages; p++)
                    Assert.AreEqual(a, AlbumData.Page(p).Album);
            }
            Assert.AreEqual(new[] { 13, 1, 1, 1 }, new[] { biome, evt, season, activity });
        }

        [Test]
        public void EveryPage_Has7Common4Rare1Shiny_AndNoDuplicateId()
        {
            var keys = new HashSet<string>();
            for (int p = 0; p < AlbumData.PageCount; p++)
            {
                int common = 0, rare = 0, shiny = 0;
                for (int slot = 0; slot < 12; slot++)
                {
                    int index = AlbumData.StickerAt(p, slot);
                    var s = AlbumData.Sticker(index);
                    Assert.AreEqual(p, s.Page);
                    Assert.AreEqual(slot, s.Slot);
                    Assert.IsTrue(keys.Add(s.Key), "Duplicate id " + s.Key);
                    Assert.IsTrue(s.Key.StartsWith("sticker.", StringComparison.Ordinal));
                    Assert.AreEqual(index, AlbumData.IndexOf(s.Key));
                    if (s.Rarity == StickerRarity.Common) common++;
                    else if (s.Rarity == StickerRarity.Rare) rare++;
                    else shiny++;
                }
                Assert.AreEqual(new[] { 7, 4, 1 }, new[] { common, rare, shiny }, AlbumData.Page(p).Key);
            }
            Assert.AreEqual(720, keys.Count);
            Assert.AreEqual(0, AlbumData.IndexOf("sticker.gl.01"));
            Assert.AreEqual(12, AlbumData.IndexOf("sticker.gl.13"));
            Assert.AreEqual(AlbumData.StickerAt(AlbumData.EventPage(101), 0), AlbumData.IndexOf("sticker.evt.101.01"));
            Assert.AreEqual(AlbumData.StickerAt(AlbumData.SeasonPage(1), 0), AlbumData.IndexOf("sticker.sea.1.01"));
            Assert.AreEqual(AlbumData.StickerAt(AlbumData.ActivityPage(ActivityId.Grooming), 0), AlbumData.IndexOf("sticker.act.001.01"));
            Assert.AreEqual(-1, AlbumData.IndexOf("sticker.none"));
        }

        [Test]
        public void PageKeys_FollowTheContentIds()
        {
            Assert.AreEqual("page.grassland.1", AlbumData.Page(0).Key);
            Assert.AreEqual("page.starlight.2", AlbumData.Page(25).Key);
            Assert.AreEqual("page.event.101", AlbumData.Page(AlbumData.FirstEventPage).Key);
            Assert.AreEqual("page.season.4", AlbumData.Page(AlbumData.FirstSeasonPage + 3).Key);
            Assert.AreEqual("page.activity.018", AlbumData.Page(AlbumData.ActivityPage(ActivityId.GrandContest)).Key);
            Assert.AreEqual("album.grassland", AlbumData.Album(0).Key);
            Assert.AreEqual("album.event", AlbumData.Album(AlbumData.EventAlbum).Key);
            Assert.AreEqual("album.season", AlbumData.Album(AlbumData.SeasonAlbum).Key);
            Assert.AreEqual("album.activity", AlbumData.Album(AlbumData.ActivityAlbum).Key);
            Assert.AreEqual(0, AlbumData.BiomeIndex(ContentId.FromSource("biome.grassland")));
            Assert.AreEqual(12, AlbumData.BiomeIndex(ContentId.FromSource("biome.starlight")));
            Assert.AreEqual(-1, AlbumData.BiomeIndex(ContentId.FromSource("biome.nowhere")));
        }

        [Test]
        public void DrawableAndFixed_FollowSection13()
        {
            for (int b = 0; b < AlbumData.BiomeAlbums; b++)
            {
                int p1 = AlbumData.BiomePage(b, 1), p2 = p1 + 1;
                for (int slot = 0; slot < 12; slot++)
                    Assert.AreEqual(slot != 6, AlbumData.Sticker(AlbumData.StickerAt(p1, slot)).Drawable, "Page 1: all but 07.");
                Assert.AreEqual(StickerFixedSource.Companion, AlbumData.Sticker(AlbumData.StickerAt(p1, 6)).Fixed);
                int zones = b == 0 ? 8 : b == 4 ? 6 : 7;
                for (int slot = 0; slot < 12; slot++)
                {
                    var s = AlbumData.Sticker(AlbumData.StickerAt(p2, slot));
                    Assert.AreEqual(slot >= zones, s.Drawable, AlbumData.Page(p2).Key + " slot " + slot);
                    if (slot < zones)
                    {
                        Assert.AreEqual(StickerFixedSource.Zone, s.Fixed);
                        Assert.AreEqual(slot, s.FixedValue);
                    }
                }
                var signature = AlbumData.Sticker(AlbumData.StickerAt(p2, 11));
                Assert.AreEqual(StickerFixedSource.SignatureMoment, signature.Fixed);
                Assert.AreEqual(StickerRarity.Shiny, signature.Rarity);
                Assert.IsTrue(signature.Drawable, "24 is drawable and guaranteed once.");
            }
            Assert.AreEqual(StickerFixedSource.Offered, AlbumData.Sticker(0).Fixed, "STK-GL-01 offered at the opening.");
            Assert.IsTrue(AlbumData.Sticker(0).Drawable);
            Assert.AreEqual(ContentId.FromSource("species.rabbit"), AlbumData.PreferredSpecies(0));

            for (int e = 101; e <= 112; e++)
            {
                int page = AlbumData.EventPage(e);
                var tiers = new[] { 1, 6, 11, 19, 23, 26 };
                for (int slot = 0; slot < 12; slot++)
                {
                    var s = AlbumData.Sticker(AlbumData.StickerAt(page, slot));
                    Assert.IsFalse(s.Drawable, "Event stickers are all fixed.");
                    if (slot < 6)
                    {
                        Assert.AreEqual(StickerFixedSource.EventTrack, s.Fixed);
                        Assert.AreEqual(tiers[slot], s.FixedValue);
                    }
                    else if (slot < 9)
                    {
                        Assert.AreEqual(StickerFixedSource.EventChallengeStars, s.Fixed);
                        Assert.AreEqual(9 * (slot - 5), s.FixedValue);
                    }
                    else
                    {
                        Assert.AreEqual(StickerFixedSource.EventQuest, s.Fixed);
                        Assert.AreEqual(new[] { 4, 7, 10 }[slot - 9], s.FixedValue);
                    }
                }
            }

            for (int season = 1; season <= 4; season++)
            {
                int page = AlbumData.SeasonPage(season);
                for (int slot = 0; slot < 12; slot++)
                {
                    var s = AlbumData.Sticker(AlbumData.StickerAt(page, slot));
                    Assert.AreEqual(slot >= 3, s.Drawable);
                    if (slot < 3)
                        Assert.AreEqual(StickerFixedSource.Visitor, s.Fixed);
                }
            }

            for (int a = 1; a <= 18; a++)
            {
                int page = AlbumData.ActivityPage((ActivityId)a);
                Assert.AreEqual((ActivityId)a, AlbumData.ActivityOfPage(page));
                for (int slot = 0; slot < 12; slot++)
                    Assert.AreEqual(slot <= 5 || slot == 7, AlbumData.Sticker(AlbumData.StickerAt(page, slot)).Drawable);
                bool meta = a == 5 || a == 6 || a == 11 || a == 18;
                if (!meta)
                {
                    Assert.AreEqual(StickerFixedSource.ThreeStars, AlbumData.Sticker(AlbumData.StickerAt(page, 6)).Fixed);
                    Assert.AreEqual(10, AlbumData.Sticker(AlbumData.StickerAt(page, 8)).FixedValue);
                    Assert.AreEqual(20, AlbumData.Sticker(AlbumData.StickerAt(page, 9)).FixedValue);
                    Assert.AreEqual(StickerFixedSource.Records, AlbumData.Sticker(AlbumData.StickerAt(page, 10)).Fixed);
                    Assert.AreEqual(5, AlbumData.Sticker(AlbumData.StickerAt(page, 10)).FixedValue);
                    Assert.AreEqual(30, AlbumData.Sticker(AlbumData.StickerAt(page, 11)).FixedValue);
                }
            }
            int garden = AlbumData.ActivityPage(ActivityId.Garden);
            Assert.AreEqual(StickerFixedSource.GardenLevel, AlbumData.Sticker(AlbumData.StickerAt(garden, 11)).Fixed);
            Assert.AreEqual(10, AlbumData.Sticker(AlbumData.StickerAt(garden, 11)).FixedValue);
            int cuddle = AlbumData.ActivityPage(ActivityId.CuddleMatch);
            Assert.AreEqual(300, AlbumData.Sticker(AlbumData.StickerAt(cuddle, 11)).FixedValue);
        }

        [Test]
        public void Preferences_AreReadable()
        {
            for (int i = 0; i < AlbumData.StickerCount; i++)
            {
                var s = AlbumData.Sticker(i);
                if (!s.Drawable)
                    Assert.AreEqual(StickerPreference.None, s.Preferred, s.Key + ": a fixed sticker has no preferred source.");
                switch (s.Preferred)
                {
                    case StickerPreference.Wish:
                        Assert.IsTrue(s.PreferredSubject.StartsWith("species.", StringComparison.Ordinal), s.Key);
                        Assert.IsTrue(AlbumData.PreferredSpecies(i).IsValid);
                        break;
                    case StickerPreference.ActivityInBiome:
                        Assert.AreNotEqual(ActivityId.None, s.PreferredActivity, s.Key);
                        break;
                    case StickerPreference.Weather:
                    case StickerPreference.SeasonWeather:
                        Assert.IsNotEmpty(s.PreferredSubject, s.Key);
                        break;
                }
            }
            Assert.AreEqual(StickerPreference.ActivityInBiome, AlbumData.Sticker(7).Preferred, "GL-08 Toilettage dans la Prairie.");
            Assert.AreEqual(ActivityId.Grooming, AlbumData.Sticker(7).PreferredActivity);
            Assert.AreEqual(StickerPreference.DailyGift, AlbumData.Sticker(10).Preferred, "GL-11 Cadeau du jour.");
            Assert.AreEqual("light_rain", AlbumData.Sticker(21).PreferredSubject, "GL-22 pendant la pluie fine.");
        }

        [Test]
        public void Names_AreTextRowsWithoutForbiddenCharacters()
        {
            var texts = PocketWilds.Editor.UI.StickerTexts.Texts;
            Assert.AreEqual(16 + 60 + 720, texts.GetLength(0));
            Assert.AreEqual(3, texts.GetLength(1));
            var keys = new HashSet<string>();
            for (int i = 0; i < texts.GetLength(0); i++)
            {
                Assert.IsTrue(keys.Add(texts[i, 0]), "Duplicate text key " + texts[i, 0]);
                Assert.IsTrue(texts[i, 0].EndsWith(".name", StringComparison.Ordinal));
                Assert.IsNotEmpty(texts[i, 1]);
                Assert.IsNotEmpty(texts[i, 2]);
                Assert.IsFalse(texts[i, 2].Contains("—"), "No em dash in French: " + texts[i, 0]);
                for (int c = 1; c <= 2; c++)
                    Assert.IsFalse(texts[i, c].Contains("\u2728") || texts[i, c].Contains("\u2726") || texts[i, c].Contains("\u2727"));
            }
            for (int s = 0; s < AlbumData.StickerCount; s++)
                Assert.IsTrue(keys.Contains(AlbumData.Sticker(s).Key + ".name"));
            for (int p = 0; p < AlbumData.PageCount; p++)
                Assert.IsTrue(keys.Contains(AlbumData.Page(p).Key + ".name"));
        }
    }
}
