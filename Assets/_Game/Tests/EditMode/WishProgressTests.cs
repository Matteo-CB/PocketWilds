using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Simulation;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T36, doc « 01 — Contenu des rendez-vous, des activités et de l'accueil », sections 2.1, 2.3, 2.5, 2.6 and welcome steps
    /// A07 to A10 (plan section 8, item 5): every shipped template progresses from its hook only once it is in the stock, a
    /// × 10 counts ten, the tutorial wish ends the welcome after 2 eggs even without a touch, the claim's rewards and the
    /// cumulative Gem cap, the auto-claim at the Today opening, the free swap, the stock through a long absence, events and
    /// allocation-free hooks.
    /// </summary>
    public class WishProgressTests
    {
        private static readonly ContentId Grassland = WishTestRig.Grassland;

        private static HatchResult Hatched(ContentId egg, bool newSpecies, bool newVariant) =>
            new HatchResult(HatchStatus.Hatched, default, egg, BigValue.Zero, RarityTier.Common, newSpecies, newVariant, false);

        private static int Progress(WishTestRig rig, WishTemplateId template) => rig.Find(template).Progress;

        private static bool Done(WishTestRig rig, WishTemplateId template) => rig.Find(template).IsDone;

        private static int Owned(WishTestRig rig, ContentId treat) => rig.State.Progression.Treats.TryGetValue(treat, out int n) ? n : 0;

        // ---------------- Progress per template (section 2.3) ----------------

        [Test]
        public void HatchWishes_CountTheirEggs_OnlyOnceInTheStock()
        {
            var rig = WishTestRig.Create();
            var wishes = rig.Wishes;
            int key = rig.SetDay(2);
            wishes.OnHatched(Hatched(WishTestRig.BasicEgg, true, true));
            Assert.AreEqual(0, wishes.PendingCount, "An egg with no wish changes nothing.");

            rig.AddWish(WishTemplateId.HatchEggs, rig.AnimalId(0), 3, key);
            rig.AddWish(WishTemplateId.HatchBiomeEggs, rig.AnimalId(1), 2, key, Grassland);
            rig.AddWish(WishTemplateId.HatchPreciousEgg, rig.AnimalId(2), 1, key);
            wishes.OnHatched(Hatched(WishTestRig.BasicEgg, false, false));
            Assert.AreEqual(1, Progress(rig, WishTemplateId.HatchEggs));
            Assert.AreEqual(1, Progress(rig, WishTemplateId.HatchBiomeEggs), "A Prairie egg for a Prairie wish.");
            Assert.AreEqual(0, Progress(rig, WishTemplateId.HatchPreciousEgg), "A Basic egg is not precious.");
            wishes.OnHatched(HatchResult.Failed(HatchStatus.NotEnoughLight, WishTestRig.BasicEgg, BigValue.Zero));
            Assert.AreEqual(1, Progress(rig, WishTemplateId.HatchEggs), "A refused hatch counts nothing.");

            // The discovery wishes appear now: the new species of the first egg above never counts for them.
            rig.AddWish(WishTemplateId.DiscoverVariant, rig.AnimalId(3), 1, key);
            rig.AddWish(WishTemplateId.DiscoverSpecies, rig.AnimalId(4), 1, key);
            Assert.AreEqual(0, Progress(rig, WishTemplateId.DiscoverSpecies));
            wishes.OnHatched(Hatched(WishTestRig.FocusEgg, false, true));
            Assert.AreEqual(2, Progress(rig, WishTemplateId.HatchEggs));
            Assert.IsTrue(Done(rig, WishTemplateId.HatchBiomeEggs), "The Focus egg is a Prairie egg too.");
            Assert.IsTrue(Done(rig, WishTemplateId.HatchPreciousEgg));
            Assert.IsTrue(Done(rig, WishTemplateId.DiscoverVariant), "A new color.");
            Assert.AreEqual(0, Progress(rig, WishTemplateId.DiscoverSpecies));

            wishes.OnHatched(Hatched(ContentId.FromSource("egg.test.discovery"), true, true));
            Assert.IsTrue(Done(rig, WishTemplateId.HatchEggs));
            Assert.AreEqual(3, Progress(rig, WishTemplateId.HatchEggs), "Capped at its target.");
            Assert.IsTrue(Done(rig, WishTemplateId.DiscoverSpecies));
        }

        [Test]
        public void MasteryAndCodexWishes_CountTheirSpecies()
        {
            var rig = WishTestRig.Create();
            var wishes = rig.Wishes;
            int key = rig.SetDay(2);
            wishes.OnMasteryLeveled(WishTestRig.Rabbit, 1);
            wishes.OnCodexPageOpened(WishTestRig.Squirrel);
            rig.AddWish(WishTemplateId.RaiseSpeciesMastery, rig.AnimalId(0), 1, key, WishTestRig.Rabbit);
            rig.AddWish(WishTemplateId.LookAtSpeciesInCodex, rig.AnimalId(1), 1, key, WishTestRig.Squirrel);
            Assert.AreEqual(0, Progress(rig, WishTemplateId.RaiseSpeciesMastery), "Earlier events never count.");
            Assert.AreEqual(0, Progress(rig, WishTemplateId.LookAtSpeciesInCodex));

            wishes.OnMasteryLeveled(WishTestRig.Squirrel, 1);
            wishes.OnCodexPageOpened(WishTestRig.Rabbit);
            wishes.OnMasteryLeveled(WishTestRig.Rabbit, 0);
            Assert.AreEqual(0, Progress(rig, WishTemplateId.RaiseSpeciesMastery), "Another species does not count.");
            Assert.AreEqual(0, Progress(rig, WishTemplateId.LookAtSpeciesInCodex));

            wishes.OnMasteryLeveled(WishTestRig.Rabbit, 2);
            wishes.OnCodexPageOpened(WishTestRig.Squirrel);
            Assert.IsTrue(Done(rig, WishTemplateId.RaiseSpeciesMastery));
            Assert.AreEqual(1, Progress(rig, WishTemplateId.RaiseSpeciesMastery));
            Assert.IsTrue(Done(rig, WishTemplateId.LookAtSpeciesInCodex));
        }

        [Test]
        public void CareWishes_CountTouchesTreatsAndBond()
        {
            var rig = WishTestRig.Create();
            var wishes = rig.Wishes;
            int key = rig.SetDay(2);
            var a = Enumerable.Range(0, 6).Select(i => rig.AnimalId(i)).ToArray();
            wishes.OnAnimalTouched(a[3], true);
            rig.AddWish(WishTemplateId.TapAnimals, a[0], 5, key);
            rig.AddWish(WishTemplateId.CuddleAnimal, a[1], 3, key);
            rig.AddWish(WishTemplateId.GiveTreats, a[2], 2, key);

            // WISH-014: different animals; touches beyond the bond caps still count.
            for (int i = 0; i < 3; i++)
                wishes.OnAnimalTouched(a[0], true);
            Assert.AreEqual(1, Progress(rig, WishTemplateId.TapAnimals));
            wishes.OnAnimalTouched(a[1], true);
            Assert.AreEqual(2, Progress(rig, WishTemplateId.TapAnimals));
            Assert.AreEqual(1, Progress(rig, WishTemplateId.CuddleAnimal), "The first cuddle counts.");

            // WISH-015: its animal only, touches at least 2 s apart.
            rig.Clock.Advance(TimeSpan.FromSeconds(1));
            wishes.OnAnimalTouched(a[1], true);
            Assert.AreEqual(1, Progress(rig, WishTemplateId.CuddleAnimal), "1 s later: too soon.");
            rig.Clock.Advance(TimeSpan.FromSeconds(1));
            wishes.OnAnimalTouched(a[1], true);
            Assert.AreEqual(2, Progress(rig, WishTemplateId.CuddleAnimal), "2 s after the last cuddle that counted.");
            wishes.OnAnimalTouched(a[2], false);
            Assert.AreEqual(2, Progress(rig, WishTemplateId.CuddleAnimal), "Another animal.");
            Assert.AreEqual(3, Progress(rig, WishTemplateId.TapAnimals));
            rig.Clock.Advance(TimeSpan.FromSeconds(2));
            wishes.OnAnimalTouched(a[1], true);
            Assert.IsTrue(Done(rig, WishTemplateId.CuddleAnimal));
            wishes.OnAnimalTouched(a[3], true);
            wishes.OnAnimalTouched(a[4], true);
            Assert.IsTrue(Done(rig, WishTemplateId.TapAnimals), "Five different animals.");

            // WISH-017 counts every treat; WISH-016 only the ones to its animal, once it exists.
            wishes.OnTreatGiven(a[5]);
            Assert.AreEqual(1, Progress(rig, WishTemplateId.GiveTreats));
            rig.AddWish(WishTemplateId.GiveAnimalTreat, a[5], 1, key);
            wishes.OnTreatGiven(a[4]);
            Assert.IsTrue(Done(rig, WishTemplateId.GiveTreats));
            Assert.AreEqual(0, Progress(rig, WishTemplateId.GiveAnimalTreat));
            wishes.OnTreatGiven(a[5]);
            Assert.IsTrue(Done(rig, WishTemplateId.GiveAnimalTreat));

            // WISH-018: bond points of any animal.
            rig.AddWish(WishTemplateId.EarnBond, a[3], 20, key);
            wishes.OnBondGained(a[0], 0);
            wishes.OnBondGained(a[0], 7);
            Assert.AreEqual(7, Progress(rig, WishTemplateId.EarnBond));
            wishes.OnBondGained(a[4], 15);
            Assert.IsTrue(Done(rig, WishTemplateId.EarnBond));
            Assert.AreEqual(20, Progress(rig, WishTemplateId.EarnBond));
        }

        [Test]
        public void UpgradeAndExpansionWishes_CountLevelsAndZones()
        {
            var rig = WishTestRig.Create();
            int key = rig.SetDay(2);
            Assert.IsTrue(rig.P.Upgrades.TryBuy(Grassland, UpgradeKind.Production, PurchaseQty.One, out _));
            rig.Wishes.OnUpgradesBought(1);
            rig.Wishes.OnZoneOpened();
            rig.AddWish(WishTemplateId.UpgradeInBiome, rig.AnimalId(0), 1, key, Grassland, (byte)UpgradeKind.Production);
            rig.AddWish(WishTemplateId.BuyUpgrades, rig.AnimalId(1), 2, key);
            rig.AddWish(WishTemplateId.ExpandBiome, rig.AnimalId(2), 1, key);
            // As after a load: the upgrade's level now is where WISH-026 counts from.
            var wishes = rig.NewWishService();

            Assert.IsTrue(rig.P.Upgrades.TryBuy(Grassland, UpgradeKind.Nests, PurchaseQty.One, out var nests));
            wishes.OnUpgradesBought(nests.LevelTo - nests.LevelFrom);
            Assert.AreEqual(1, Progress(rig, WishTemplateId.BuyUpgrades));
            Assert.AreEqual(0, Progress(rig, WishTemplateId.UpgradeInBiome), "Cozy Nests is not the Production upgrade.");

            Assert.IsTrue(rig.P.Upgrades.TryBuy(Grassland, UpgradeKind.Production, PurchaseQty.Ten, out var ten));
            Assert.AreEqual(10, ten.LevelTo - ten.LevelFrom);
            wishes.OnUpgradesBought(10);
            Assert.IsTrue(Done(rig, WishTemplateId.BuyUpgrades), "A × 10 counts every level.");
            Assert.IsTrue(Done(rig, WishTemplateId.UpgradeInBiome));
            Assert.AreEqual(0, Progress(rig, WishTemplateId.ExpandBiome));
            wishes.OnZoneOpened();
            Assert.IsTrue(Done(rig, WishTemplateId.ExpandBiome));

            // A Renewal sets Production back to 0 while WISH-026 waits: the next level bought still counts.
            var again = WishTestRig.Create(profile: 2);
            key = again.SetDay(2);
            Assert.IsTrue(again.P.Upgrades.TryBuy(Grassland, UpgradeKind.Production, PurchaseQty.Ten, out _));
            again.AddWish(WishTemplateId.UpgradeInBiome, again.AnimalId(0), 1, key, Grassland, (byte)UpgradeKind.Production);
            var service = again.NewWishService();
            var progress = again.P.Store.Get(Grassland);
            progress.ProductionLevel = 0;
            again.P.Store.Set(progress);
            Assert.IsTrue(again.P.Upgrades.TryBuy(Grassland, UpgradeKind.Nests, PurchaseQty.One, out _));
            service.OnUpgradesBought(1);
            Assert.AreEqual(0, Progress(again, WishTemplateId.UpgradeInBiome));
            Assert.IsTrue(again.P.Upgrades.TryBuy(Grassland, UpgradeKind.Production, PurchaseQty.One, out _));
            service.OnUpgradesBought(1);
            Assert.IsTrue(Done(again, WishTemplateId.UpgradeInBiome));
        }

        [Test]
        public void FacadeEntryPoints_CountUpgradesOnce_OpenSystems_AndDrawOnceADay()
        {
            var rig = WishTestRig.Create();
            var visit = rig.State.DailyVisit;
            int key = rig.SetDay(2);
            rig.AddWish(WishTemplateId.UpgradeInBiome, rig.AnimalId(0), 3, key, Grassland, (byte)UpgradeKind.Production);
            var wishes = rig.NewWishService();

            Assert.IsTrue(rig.P.Upgrades.TryBuy(Grassland, UpgradeKind.Nests, PurchaseQty.One, out var nests));
            wishes.OnUpgrade(in nests);
            wishes.OnUpgradesBought(1);
            Assert.AreEqual(0, Progress(rig, WishTemplateId.UpgradeInBiome), "Another upgrade kind.");
            Assert.AreEqual(2, visit.SystemUnlockDay[(int)WishSystem.Upgrades], "The first upgrade bought opens SYS-04.");

            Assert.IsTrue(rig.P.Upgrades.TryBuy(Grassland, UpgradeKind.Production, PurchaseQty.One, out var one));
            wishes.OnUpgrade(in one);
            Assert.AreEqual(1, Progress(rig, WishTemplateId.UpgradeInBiome));
            wishes.OnUpgradesBought(1);
            Assert.AreEqual(1, Progress(rig, WishTemplateId.UpgradeInBiome), "Counted once, not again by the level-reading fallback.");

            Assert.IsTrue(rig.P.Upgrades.TryBuy(Grassland, UpgradeKind.Production, PurchaseQty.Ten, out var ten));
            wishes.OnUpgradesBought(10);
            wishes.OnUpgrade(in ten);
            Assert.IsTrue(Done(rig, WishTemplateId.UpgradeInBiome));
            Assert.AreEqual(3, Progress(rig, WishTemplateId.UpgradeInBiome), "Either order counts the levels once.");

            visit.SystemUnlockDay[(int)WishSystem.Expansions] = 0;
            wishes.OnZone(default);
            Assert.AreEqual(2, visit.SystemUnlockDay[(int)WishSystem.Expansions], "An expansion bought means SYS-06 is open.");

            var fresh = WishTestRig.Create(profile: 3);
            int day2 = fresh.SetDay(2);
            fresh.Wishes.RefreshSystemUnlocks();
            Assert.IsTrue(fresh.Wishes.IsSystemOpen(WishSystem.Expansions));
            Assert.AreEqual(3, fresh.Wishes.EnsureDrawn(day2));
            var first = fresh.Stock();
            Assert.IsTrue(first.All(w => WishTestRig.SystemOf(w) <= WishSystem.Upgrades), "The account's first draw follows the day-1 rules.");
            Assert.IsTrue(first.Any(w => WishTestRig.SystemOf(w) == WishSystem.CuddlesAndTreats && WishTestRig.TemplateOf(w).Quick));
            Assert.AreEqual(0, fresh.Wishes.EnsureDrawn(day2), "Once a day.");
            Assert.AreEqual(3, fresh.Wishes.PendingCount);
            Assert.AreEqual(3, fresh.Wishes.EnsureDrawn(fresh.SetDay(3)), "Then the daily draw.");
            Assert.AreEqual(6, fresh.Wishes.PendingCount);
        }

        [Test]
        public void MultiHatchOfTen_CountsTen()
        {
            var rig = WishTestRig.Create();
            int key = rig.SetDay(2);
            rig.AddWish(WishTemplateId.HatchEggs, rig.AnimalId(0), 10, key);
            var results = new HatchResult[10];
            Assert.AreEqual(10, rig.Hatch.Hatch(WishTestRig.BasicEgg, 10, results));
            for (int i = 0; i < 9; i++)
                rig.Wishes.OnHatched(results[i]);
            Assert.AreEqual(9, Progress(rig, WishTemplateId.HatchEggs));
            Assert.IsFalse(Done(rig, WishTemplateId.HatchEggs));
            rig.Wishes.OnHatched(results[9]);
            Assert.IsTrue(Done(rig, WishTemplateId.HatchEggs));
            Assert.AreEqual(10, Progress(rig, WishTemplateId.HatchEggs));
        }

        // ---------------- Tutorial wish (welcome steps A07 to A10, section 1.3) ----------------

        [Test]
        public void TutorialWish_TwoEggsAfterItStarts_EndsTheWelcome_EvenWithoutATouch()
        {
            var rig = WishTestRig.Create(animals: 2, onboarded: false);
            var visit = rig.State.DailyVisit;
            var wishes = rig.Wishes;
            int key = rig.SetDay(1);
            uint mochi = rig.AnimalId(0);
            wishes.OnHatched(rig.Hatch.Hatch(WishTestRig.BasicEgg));
            Assert.AreEqual(0, wishes.PendingCount, "The third egg (A07) comes before the wish.");
            Assert.AreEqual(0, wishes.DrawForDay(key), "No daily draw during the welcome.");
            Assert.IsFalse(wishes.TutorialActive);

            var drawn = new WishDraw(-1, 0, WishSystem.None, 0, 0, false, false);
            wishes.Drawn += d => drawn = d;
            wishes.StartTutorialWish(mochi);
            Assert.AreEqual(0, drawn.Slot, "The bubble appears over Mochi (A08).");
            Assert.IsTrue(drawn.IsTutorial);
            Assert.AreEqual(WishTemplateId.Tutorial, drawn.TemplateId);
            Assert.AreEqual(1, drawn.PendingAfter);
            Assert.IsTrue(wishes.TutorialActive);
            Assert.AreEqual(2, visit.TutorialStep);
            var wish = wishes.Get(0);
            Assert.AreEqual((ushort)WishTemplateId.Tutorial, wish.Template);
            Assert.IsTrue(wish.IsTutorial);
            Assert.AreEqual(2, wish.Target);
            Assert.AreEqual(mochi, wish.Animal);
            Assert.IsTrue(wishes.WishOf(mochi, out int slot) && slot == 0);
            wishes.StartTutorialWish(mochi);
            Assert.AreEqual(1, wishes.PendingCount, "Once only.");
            Assert.IsFalse(wishes.CanSwap, "The tutorial wish is never swapped.");
            Assert.IsFalse(wishes.TrySwap(0));

            rig.Clock.Advance(TimeSpan.FromMinutes(2));
            wishes.OnHatched(rig.Hatch.Hatch(WishTestRig.BasicEgg));
            Assert.AreEqual(1, wishes.Get(0).Progress);
            Assert.IsFalse(visit.OnboardingDone);
            Assert.IsFalse(rig.State.IsOnboardingDone);

            int completed = -1, finished = 0;
            wishes.Completed += s => completed = s;
            wishes.OnboardingFinished += () => finished++;
            rig.Clock.Advance(TimeSpan.FromMinutes(1));
            wishes.OnHatched(rig.Hatch.Hatch(WishTestRig.BasicEgg));
            Assert.AreEqual(0, completed, "2 eggs since A08: the bubble turns into a check (A10).");
            Assert.AreEqual(1, finished, "The facade hears the end of the welcome once.");
            Assert.IsTrue(wishes.Get(0).IsDone);
            Assert.IsTrue(visit.OnboardingDone, "The welcome ends without a touch.");
            Assert.AreEqual(3, visit.TutorialStep);
            Assert.IsFalse(wishes.TutorialActive);
            Assert.IsTrue(wishes.HasDoneUnclaimed);

            long gems = rig.State.Wallet.Gems;
            rig.Clock.Advance(TimeSpan.FromMinutes(5));
            Assert.IsTrue(wishes.TryClaim(0, WishClaimPath.Animal, out var claim));
            Assert.AreEqual(3.0, claim.MinutesFromDraw, 1e-6, "From the bubble to the second egg, not to the touch.");
            Assert.AreEqual(0, claim.PendingAfter);
            Assert.AreEqual(3, claim.Gems);
            Assert.AreEqual(gems + 3, rig.State.Wallet.Gems, "3 Gems: the Gems counter appears with them.");
            Assert.AreEqual(WishTestRig.CloverCrunch, claim.Treat, "1 Trèfle croquant.");
            Assert.AreEqual(1, Owned(rig, WishTestRig.CloverCrunch));
            Assert.AreEqual(1, rig.Bond.Calls);
            Assert.AreEqual(mochi, rig.Bond.LastAnimal);
            Assert.AreEqual(15, rig.Bond.LastPoints, "+15 bond for Mochi.");
            Assert.AreEqual(BondSource.Wish, rig.Bond.LastSource);
            Assert.AreEqual(15, claim.Bond);
            Assert.AreEqual(0, visit.WishGemsTotal, "Outside the cap of the daily wishes.");
            Assert.AreEqual(1, visit.WishesCompleted);
            Assert.AreEqual(0, wishes.PendingCount);
            wishes.StartTutorialWish(mochi);
            Assert.AreEqual(0, wishes.PendingCount, "Never twice.");
            Assert.AreEqual(3, wishes.DrawFirstDay(key), "Then the first daily draw (A12) can come the same day.");
        }

        // ---------------- Claims (sections 2.1 and 2.6) ----------------

        [Test]
        public void Claim_GivesGemsBondTreatBiomeXpAndSpeciesXp()
        {
            var rig = WishTestRig.Create();
            int key = rig.SetDay(2);
            uint animal = rig.AnimalId(0);
            var species = rig.State.Collection[0].Species;
            rig.AddWish(WishTemplateId.EarnBond, animal, 20, key, done: true);
            rig.AddWish(WishTemplateId.HatchEggs, rig.AnimalId(1), 3, key);
            long gems = rig.State.Wallet.Gems;
            long biomeXp = rig.P.Store.Get(Grassland).MasteryXp;
            long speciesXp = TotalXp(rig.SpeciesMastery, species);
            int treats = Owned(rig, WishTestRig.CloverCrunch);
            var reasons = new List<EconomyReason>();
            rig.Economy.Changed += change =>
            {
                if (change.Currency == CurrencyId.Gems)
                    reasons.Add(change.Reason);
            };
            WishClaim seen = default;
            rig.Wishes.Claimed += c => seen = c;

            Assert.IsFalse(rig.Wishes.TryClaim(1, WishClaimPath.TodayRow, out _), "Not done yet.");
            Assert.IsFalse(rig.Wishes.TryClaim(7, WishClaimPath.TodayRow, out _));
            Assert.IsTrue(rig.Wishes.TryClaim(0, WishClaimPath.TodayRow, out var claim));

            Assert.AreEqual(3, claim.Gems);
            Assert.AreEqual(gems + 3, rig.State.Wallet.Gems);
            CollectionAssert.AreEqual(new[] { EconomyReason.Wish }, reasons);
            Assert.AreEqual(1, rig.Bond.Calls);
            Assert.AreEqual(animal, rig.Bond.LastAnimal);
            Assert.AreEqual(15, rig.Bond.LastPoints);
            Assert.AreEqual(BondSource.Wish, rig.Bond.LastSource);
            Assert.AreEqual(15, claim.Bond);
            Assert.AreEqual(WishTestRig.CloverCrunch, claim.Treat, "The treat of the animal's biome (Prairie).");
            Assert.AreEqual(treats + 1, Owned(rig, WishTestRig.CloverCrunch));
            Assert.AreEqual(biomeXp + rig.ProgressionConfig.XpWish, rig.P.Store.Get(Grassland).MasteryXp, "+10 Prairie mastery XP.");
            Assert.AreEqual(speciesXp + rig.Config.WishSpeciesXp, TotalXp(rig.SpeciesMastery, species), "+5 species mastery XP.");
            Assert.AreEqual(5, rig.Config.WishSpeciesXp);
            Assert.AreEqual(1, rig.State.DailyVisit.WishesCompleted);
            Assert.AreEqual(species, claim.Species);
            Assert.AreEqual(Grassland, claim.Biome);
            Assert.AreEqual(0, claim.Slot);
            Assert.AreEqual(WishTemplateId.EarnBond, claim.TemplateId);
            Assert.AreEqual((ushort)WishTemplateId.EarnBond, claim.Template);
            Assert.AreEqual(1, claim.PendingAfter);
            Assert.AreEqual(12 * 60.0, claim.MinutesFromDraw, 1e-6, "A wish from a save: counted from the start of its draw day.");
            Assert.AreEqual(WishClaimPath.TodayRow, seen.Path);
            Assert.AreEqual(1, rig.Wishes.PendingCount);
            Assert.AreEqual((ushort)WishTemplateId.HatchEggs, rig.Wishes.Get(0).Template, "The stock stays compact.");
        }

        private static long TotalXp(MasteryService mastery, ContentId species)
        {
            var progress = mastery.Get(species);
            long total = progress.Xp;
            for (int level = 0; level < progress.Level; level++)
                total += mastery.XpForNext(level);
            return total;
        }

        private static void AddDone(WishTestRig rig, int count, int key)
        {
            for (int i = 0; i < count; i++)
                rig.AddWish((WishTemplateId)(i + 1), rig.AnimalId(i % rig.State.Collection.Count), 1, key, done: true);
        }

        [Test]
        public void WishGems_AreCappedInTotalAtNinePerDaySinceInstall()
        {
            var rig = WishTestRig.Create();
            var visit = rig.State.DailyVisit;
            long start = rig.State.Wallet.Gems;
            var gems = new List<long>();
            rig.Wishes.Claimed += c => gems.Add(c.Gems);

            int key = rig.SetDay(1);
            AddDone(rig, 4, key);
            Assert.AreEqual(4, rig.Wishes.ClaimAll(WishClaimPath.ClaimAll));
            CollectionAssert.AreEqual(new long[] { 3, 3, 3, 0 }, gems, "9 on day 1.");
            Assert.AreEqual(9, visit.WishGemsTotal);

            gems.Clear();
            key = rig.SetDay(2);
            AddDone(rig, 4, key);
            rig.Wishes.ClaimAll(WishClaimPath.ClaimAll);
            CollectionAssert.AreEqual(new long[] { 3, 3, 3, 0 }, gems, "18 by day 2.");

            gems.Clear();
            key = rig.SetDay(5);
            AddDone(rig, 10, key);
            rig.Wishes.ClaimAll(WishClaimPath.ClaimAll);
            CollectionAssert.AreEqual(Enumerable.Repeat(3L, 9).Concat(new[] { 0L }), gems, "A catch-up day: up to 27, never above 9 × 5.");
            Assert.AreEqual(45, visit.WishGemsTotal);

            gems.Clear();
            rig.AddWish(WishTemplateId.Tutorial, rig.AnimalId(0), 2, key, done: true, tutorial: true);
            rig.Wishes.ClaimAll(WishClaimPath.Animal);
            CollectionAssert.AreEqual(new long[] { 3 }, gems, "The tutorial wish is outside the daily wishes' cap.");
            Assert.AreEqual(45, visit.WishGemsTotal);
            Assert.AreEqual(start + 48, rig.State.Wallet.Gems);

            var six = new DailyVisitConfig();
            Assert.IsTrue(six.TryOverride("gems.wish_daily_cap", 6));
            var other = WishTestRig.Create(config: six);
            var paid = new List<long>();
            other.Wishes.Claimed += c => paid.Add(c.Gems);
            AddDone(other, 3, other.SetDay(1));
            other.Wishes.ClaimAll(WishClaimPath.ClaimAll);
            CollectionAssert.AreEqual(new long[] { 3, 3, 0 }, paid, "Remote Config gems.wish_daily_cap.");
        }

        [Test]
        public void TodayOpening_AutoClaimsDoneWishes_AndClaimAllTakesTheRest()
        {
            var rig = WishTestRig.Create();
            int key = rig.SetDay(2);
            rig.AddWish(WishTemplateId.HatchEggs, rig.AnimalId(0), 3, key, done: true);
            rig.AddWish(WishTemplateId.TapAnimals, rig.AnimalId(1), 5, key);
            rig.AddWish(WishTemplateId.CuddleAnimal, rig.AnimalId(2), 3, key, done: true);
            var paths = new List<WishClaimPath>();
            rig.Wishes.Claimed += c => paths.Add(c.Path);
            Assert.IsTrue(rig.Wishes.HasDoneUnclaimed);

            Assert.AreEqual(2, rig.Wishes.AutoClaimOnTodayOpen());
            CollectionAssert.AreEqual(new[] { WishClaimPath.Auto, WishClaimPath.Auto }, paths);
            Assert.AreEqual(1, rig.Wishes.PendingCount);
            Assert.AreEqual((ushort)WishTemplateId.TapAnimals, rig.Wishes.Get(0).Template);
            Assert.IsFalse(rig.Wishes.HasDoneUnclaimed);
            Assert.AreEqual(0, rig.Wishes.AutoClaimOnTodayOpen());

            rig.State.DailyVisit.Wishes[0].Flags |= WishRecord.FlagDone;
            Assert.AreEqual(1, rig.Wishes.ClaimAll(WishClaimPath.ClaimAll));
            Assert.AreEqual(WishClaimPath.ClaimAll, paths.Last());
            Assert.AreEqual(0, rig.Wishes.PendingCount);
        }

        [Test]
        public void ACompletionHandler_MayClaimAtOnce()
        {
            var rig = WishTestRig.Create();
            int key = rig.SetDay(2);
            rig.AddWish(WishTemplateId.HatchEggs, rig.AnimalId(0), 1, key);
            rig.AddWish(WishTemplateId.DiscoverVariant, rig.AnimalId(1), 1, key);
            rig.AddWish(WishTemplateId.EarnBond, rig.AnimalId(2), 50, key);
            var claimed = new List<WishTemplateId>();
            rig.Wishes.Completed += slot =>
            {
                // As the facade would: claim at once, and the bond service reports the bond back to the hooks.
                Assert.IsTrue(rig.Wishes.TryClaim(slot, WishClaimPath.Bubble, out var claim));
                claimed.Add(claim.TemplateId);
                rig.Wishes.OnBondGained(claim.Wish.Animal, claim.Bond);
            };
            rig.Wishes.OnHatched(Hatched(WishTestRig.BasicEgg, false, true));
            CollectionAssert.AreEquivalent(new[] { WishTemplateId.HatchEggs, WishTemplateId.DiscoverVariant }, claimed);
            Assert.AreEqual(1, rig.Wishes.PendingCount);
            Assert.AreEqual((ushort)WishTemplateId.EarnBond, rig.Wishes.Get(0).Template);
            Assert.AreEqual(30, rig.Wishes.Get(0).Progress, "The bond of both claims counted for the bond wish, nothing twice.");
        }

        // ---------------- Free swap (section 2.5) ----------------

        [Test]
        public void Swap_OneFreeADay_NeverTheSameTemplateNorSystem_NeverAccumulated()
        {
            var rig = WishTestRig.Create(animals: 8);
            var visit = rig.State.DailyVisit;
            int key = rig.SetDay(2);
            Assert.AreEqual(3, rig.Wishes.DrawForDay(key));
            Assert.IsTrue(rig.Wishes.CanSwap);
            Assert.AreEqual(1, rig.Wishes.SwapsLeft);

            var old = rig.Wishes.Get(0);
            var swap = new WishSwapInfo(-1, 0, 0);
            var redrawn = new List<WishDraw>();
            rig.Wishes.Swapped += s => swap = s;
            rig.Wishes.Drawn += redrawn.Add;
            Assert.IsTrue(rig.Wishes.TrySwap(0));
            var fresh = rig.Wishes.Get(0);
            Assert.AreEqual(0, swap.Slot);
            Assert.AreEqual(old.Template, swap.OldTemplate);
            Assert.AreEqual(fresh.Template, swap.NewTemplate);
            Assert.AreEqual(1, redrawn.Count);
            Assert.IsTrue(redrawn[0].IsSwap);
            Assert.AreEqual(0, redrawn[0].Slot);
            Assert.AreNotEqual(old.Template, fresh.Template);
            Assert.AreNotEqual(WishTestRig.SystemOf(old), WishTestRig.SystemOf(fresh));
            Assert.AreEqual(old.Animal, fresh.Animal, "The same animal asks for something else.");
            Assert.AreEqual(key, fresh.DrawnDay);
            Assert.AreEqual(0, fresh.Progress);
            Assert.AreEqual(3, rig.Stock().Select(w => WishTestRig.SystemOf(w)).Distinct().Count(), "Today's rules still hold.");
            Assert.AreEqual(0, rig.Wishes.SwapsLeft);
            Assert.IsFalse(rig.Wishes.CanSwap, "« Changement gratuit utilisé. Le prochain arrive demain. »");
            Assert.IsFalse(rig.Wishes.TrySwap(1));
            Assert.AreEqual(1, visit.SwapsUsed);

            rig.SetDay(3);
            Assert.AreEqual(1, rig.Wishes.SwapsLeft);
            key = rig.SetDay(9);
            Assert.AreEqual(1, rig.Wishes.SwapsLeft, "An unused swap never accumulates.");
            visit.Wishes[1].Flags |= WishRecord.FlagDone;
            Assert.IsFalse(rig.Wishes.TrySwap(1), "A done wish is claimed, not swapped.");
            int tutorial = rig.AddWish(WishTemplateId.Tutorial, 999999, 2, key, tutorial: true);
            Assert.IsFalse(rig.Wishes.TrySwap(tutorial));
            Assert.AreEqual(1, rig.Wishes.SwapsLeft, "Refused swaps use nothing.");

            var none = new DailyVisitConfig();
            Assert.IsTrue(none.TryOverride("daily.wish_free_rerolls", 0));
            var noSwap = WishTestRig.Create(config: none);
            Assert.AreEqual(3, noSwap.Wishes.DrawForDay(noSwap.SetDay(2)));
            Assert.IsFalse(noSwap.Wishes.CanSwap);
            Assert.IsFalse(noSwap.Wishes.TrySwap(0));

            int swapped = 0;
            for (int profile = 1; profile <= 25; profile++)
            {
                var p = WishTestRig.Create(profile);
                int day = p.SetDay(2);
                Assert.AreEqual(3, p.Wishes.DrawForDay(day));
                int slot = profile % 3;
                var before = p.Wishes.Get(slot);
                if (!p.Wishes.TrySwap(slot))
                    continue;
                var after = p.Wishes.Get(slot);
                Assert.AreNotEqual(before.Template, after.Template);
                Assert.AreNotEqual(WishTestRig.SystemOf(before), WishTestRig.SystemOf(after));
                Assert.AreEqual(3, p.Stock().Select(w => WishTestRig.SystemOf(w)).Distinct().Count());
                swapped++;
            }
            Assert.Greater(swapped, 20);
        }

        // ---------------- Stock (sections 2.1 and 6.4) ----------------

        [Test]
        public void Stock_SurvivesALongAbsence_AndDrawsOnlyFillItUp()
        {
            var rig = WishTestRig.Create(animals: 14);
            int key = rig.SetDay(2);
            Assert.AreEqual(3, rig.Wishes.DrawForDay(key));
            var first = rig.Stock();

            key = rig.SetDay(40);
            Assert.AreEqual(3, rig.Wishes.DrawForDay(key), "38 days away: one draw, on top of the waiting wishes.");
            Assert.AreEqual(6, rig.Wishes.PendingCount);
            for (int day = 41; rig.Wishes.PendingCount < 9 && day < 90; day++)
            {
                int before = rig.Wishes.PendingCount;
                int drawn = rig.Wishes.DrawForDay(rig.SetDay(day));
                Assert.LessOrEqual(before + drawn, 9);
            }
            Assert.AreEqual(9, rig.Wishes.PendingCount);
            Assert.AreEqual(0, rig.Wishes.DrawForDay(rig.SetDay(200)), "Full: nothing drawn, nothing taken away.");
            Assert.AreEqual(9, rig.Wishes.PendingCount);
            for (int i = 0; i < first.Count; i++)
            {
                var now = rig.Wishes.Get(i);
                Assert.AreEqual(first[i].Template, now.Template, "A wish never disappears as a penalty.");
                Assert.AreEqual(first[i].Animal, now.Animal);
                Assert.AreEqual(first[i].Target, now.Target);
                Assert.AreEqual(first[i].DrawnDay, now.DrawnDay);
            }
        }

        // ---------------- Events and 0 GC ----------------

        [Test]
        public void Events_AreRaisedForDrawsProgressCompletionAndClaims()
        {
            var rig = WishTestRig.Create();
            var drawn = new List<int>();
            var progressed = new List<int>();
            var completed = new List<int>();
            var claimed = new List<WishClaim>();
            var draws = new List<WishDraw>();
            rig.Wishes.Drawn += d =>
            {
                drawn.Add(d.Slot);
                draws.Add(d);
            };
            rig.Wishes.Progressed += progressed.Add;
            rig.Wishes.Completed += completed.Add;
            rig.Wishes.Claimed += claimed.Add;

            int key = rig.SetDay(2);
            Assert.AreEqual(3, rig.Wishes.DrawForDay(key));
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, drawn);
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, draws.Select(d => d.PendingAfter));
            for (int i = 0; i < 3; i++)
            {
                var wish = rig.Wishes.Get(i);
                Assert.AreEqual(wish.Template, draws[i].Template);
                Assert.AreEqual(WishTestRig.SystemOf(wish), draws[i].System);
                Assert.AreEqual(1, draws[i].Tier);
                Assert.IsFalse(draws[i].IsSwap || draws[i].IsTutorial);
            }

            rig.State.DailyVisit.WishCount = 0;
            rig.AddWish(WishTemplateId.EarnBond, rig.AnimalId(0), 20, key);
            rig.Wishes.OnBondGained(rig.AnimalId(1), 5);
            CollectionAssert.AreEqual(new[] { 0 }, progressed);
            CollectionAssert.IsEmpty(completed);
            rig.Wishes.OnBondGained(rig.AnimalId(1), 15);
            CollectionAssert.AreEqual(new[] { 0 }, progressed, "The completing step raises Completed only.");
            CollectionAssert.AreEqual(new[] { 0 }, completed);
            rig.Wishes.OnBondGained(rig.AnimalId(1), 5);
            CollectionAssert.AreEqual(new[] { 0 }, completed, "A done wish no longer moves.");
            Assert.IsTrue(rig.Wishes.TryClaim(0, WishClaimPath.Bubble, out _));
            Assert.AreEqual(1, claimed.Count);
            Assert.AreEqual(WishClaimPath.Bubble, claimed[0].Path);
        }

        [Test]
        public void ProgressHooks_DoNotAllocate()
        {
            var rig = WishTestRig.Create();
            int key = rig.SetDay(2);
            uint a = rig.AnimalId(0);
            const int far = 1_000_000;
            rig.AddWish(WishTemplateId.HatchEggs, a, far, key);
            rig.AddWish(WishTemplateId.HatchBiomeEggs, 900001, far, key, Grassland);
            rig.AddWish(WishTemplateId.HatchPreciousEgg, 900002, far, key);
            rig.AddWish(WishTemplateId.DiscoverVariant, 900003, far, key);
            rig.AddWish(WishTemplateId.RaiseSpeciesMastery, 900004, far, key, WishTestRig.Rabbit);
            rig.AddWish(WishTemplateId.LookAtSpeciesInCodex, 900005, far, key, WishTestRig.Rabbit);
            rig.AddWish(WishTemplateId.DiscoverSpecies, 900006, far, key);
            rig.AddWish(WishTemplateId.TapAnimals, 900007, far, key);
            rig.AddWish(WishTemplateId.CuddleAnimal, a, far, key);
            rig.AddWish(WishTemplateId.GiveAnimalTreat, 900008, far, key);
            rig.AddWish(WishTemplateId.GiveTreats, 900009, far, key);
            rig.AddWish(WishTemplateId.EarnBond, 900010, far, key);
            rig.AddWish(WishTemplateId.BuyUpgrades, 900011, far, key);
            rig.AddWish(WishTemplateId.UpgradeInBiome, 900012, far, key, Grassland, (byte)UpgradeKind.Production);
            rig.AddWish(WishTemplateId.ExpandBiome, 900013, far, key);
            var wishes = rig.NewWishService();
            var result = Hatched(WishTestRig.FocusEgg, true, true);
            RunHooks(wishes, result, a);
            Assert.That(() =>
            {
                for (int i = 0; i < 1000; i++)
                    RunHooks(wishes, result, a);
            }, Is.Not.AllocatingGCMemory());
            Assert.AreEqual(1001, Progress(rig, WishTemplateId.HatchEggs));
            Assert.AreEqual(1001, Progress(rig, WishTemplateId.EarnBond));
        }

        private static void RunHooks(WishService wishes, in HatchResult result, uint animal)
        {
            wishes.OnHatched(result);
            wishes.OnAnimalTouched(animal, true);
            wishes.OnTreatGiven(animal);
            wishes.OnBondGained(animal, 1);
            wishes.OnUpgradesBought(1);
            wishes.OnZoneOpened();
            wishes.OnMasteryLeveled(WishTestRig.Rabbit, 1);
            wishes.OnCodexPageOpened(WishTestRig.Rabbit);
            wishes.OnWishCompleted();
            wishes.OnGiftOpened();
            wishes.OnCalendarStamped();
        }
    }
}
