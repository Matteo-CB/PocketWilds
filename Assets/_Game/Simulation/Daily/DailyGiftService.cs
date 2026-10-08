#nullable enable
using System;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>A daily gift once opened: the lot, its size (the band's color), how it was drawn and what it gave.</summary>
    public readonly struct GiftOpening
    {
        public readonly GiftLot Lot;
        public readonly GiftSize Size;

        /// <summary>Drawn among the big lots only (the guarantee, doc 01 Contenu, section 3.3).</summary>
        public readonly bool Forced;

        /// <summary>The account's first gift, the fixed Discovery egg (step A12).</summary>
        public readonly bool FirstFixed;

        /// <summary>Gifts in a row without a big lot before this one.</summary>
        public readonly int SinceBigBefore;

        public readonly GrantedRewards Granted;

        /// <summary>Day key of the opening.</summary>
        public readonly int DayKey;

        public GiftOpening(GiftLot lot, GiftSize size, bool forced, bool firstFixed, int sinceBigBefore, in GrantedRewards granted,
            int dayKey)
        {
            Lot = lot;
            Size = size;
            Forced = forced;
            FirstFixed = firstFixed;
            SinceBigBefore = sinceBigBefore;
            Granted = granted;
            DayKey = dayKey;
        }

        /// <summary>A closed system's part was replaced (the size stays the lot's).</summary>
        public bool Substituted => Granted.Bundle.Substituted;

        /// <summary>Analytics flags: 1 forced, 2 first fixed, 4 substituted.</summary>
        public int Flags => (Forced ? 1 : 0) | (FirstFixed ? 2 : 0) | (Substituted ? 4 : 0);
    }

    /// <summary>
    /// The daily gift (doc 01 Contenu, section 3): one free gift a day in the Today screen, touched open and given at once.
    /// <list type="bullet">
    /// <item>The account's first gift is fixed: a Discovery egg (step A12). It counts as a big lot.</item>
    /// <item>Then one lot of the table (<see cref="DailyRewardTables"/>), with the substitutions of the systems still closed.</item>
    /// <item>The guarantee: after <see cref="DailyVisitConfig.GiftBigEvery"/> − 1 gifts without a big lot, the next one is
    /// drawn among G10, G15 and EGG (6, 4, 2). The counter goes back to 0 at every big lot, drawn or guaranteed.</item>
    /// <item>The draw is seeded by the profile and the day: the same day always gives the same lot, so killing the app
    /// before the save can never reroll. The roll is granted and saved (priority) at opening.</item>
    /// <item>A day without a visit gives nothing and takes nothing: no streak, the counter waits. An unopened gift is not
    /// carried over to the next day (owner decision of T36).</item>
    /// </list>
    /// The bonus gift (video or Wild Club) waits for T54 and T55.
    /// </summary>
    public sealed class DailyGiftService
    {
        /// <summary>Salt of the day's gift draw (<see cref="DailyContext.Seed"/>).</summary>
        public const uint RollSalt = 0x6A1F7u;

        private readonly DailyContext _ctx;

        public DailyGiftService(DailyContext ctx)
        {
            _ctx = ctx;
        }

        /// <summary>A gift was opened (analytics, the box's reveal).</summary>
        public event Action<GiftOpening>? Opened;

        /// <summary>Today is open, the gift is enabled (flags.daily_gift) and today's gift is still closed.</summary>
        public bool CanOpen => _ctx.State.DailyVisit.GiftDayKey < _ctx.TodayKey && _ctx.Config.GiftEnabled && _ctx.TodayOpen;

        /// <summary>Today's gift is already open (the box shows « À demain »).</summary>
        public bool OpenedToday
        {
            get
            {
                int day = _ctx.State.DailyVisit.GiftDayKey;
                return day != 0 && day >= _ctx.TodayKey;
            }
        }

        /// <summary>The last lot opened (shown again until the next day).</summary>
        public GiftLot LastLot
        {
            get
            {
                byte lot = _ctx.State.DailyVisit.LastGiftLot;
                return lot < DailyRewardTables.GiftLotCount ? (GiftLot)lot : GiftLot.G3;
            }
        }

        /// <summary>The last lot's content as it would be given now (Today shows it again after opening).</summary>
        public RewardBundle LastBundle => DailyRewardTables.Gift(LastLot, _ctx.Rewards());

        /// <summary>Gifts opened since install.</summary>
        public int GiftsOpened => _ctx.State.DailyVisit.GiftsOpened;

        /// <summary>Opens today's gift: draws (or takes the fixed first egg), grants and records it.</summary>
        public bool TryOpen(out GiftOpening opening)
        {
            opening = default;
            if (!CanOpen)
                return false;

            var daily = _ctx.State.DailyVisit;
            int today = _ctx.TodayKey;
            int sinceBefore = Math.Max(0, daily.GiftsSinceBig);
            bool first = daily.GiftsOpened <= 0;
            bool forced = false;
            GiftLot lot;
            if (first)
            {
                lot = GiftLot.Egg;
            }
            else
            {
                forced = DailyRewardTables.MustForceBig(sinceBefore, _ctx.Config.GiftBigEvery);
                var rng = new DeterministicRandom(_ctx.Seed(today, RollSalt));
                lot = Roll(ref rng, forced);
            }
            var size = DailyRewardTables.SizeOf(lot);
            var bundle = DailyRewardTables.Gift(lot, _ctx.Rewards());

            // The state moves first: whatever the grant's listeners do, today's gift is never opened twice.
            daily.GiftDayKey = today;
            daily.GiftsOpened++;
            daily.GiftsSinceBig = size == GiftSize.Big ? 0 : sinceBefore + 1;
            daily.LastGiftLot = (byte)lot;

            var granted = _ctx.Granter.Grant(bundle, EconomyReason.DailyGift, DiscoverySource.DailyGift);
            opening = new GiftOpening(lot, size, forced, first, sinceBefore, granted, today);
            Opened?.Invoke(opening);
            return true;
        }

        /// <summary>
        /// One draw of the table (section 3.2): the 12 lots by their weights, or the big lots only (G10, G15, EGG at 6, 4, 2)
        /// when <paramref name="forceBig"/>. One random number, no allocation.
        /// </summary>
        public static GiftLot Roll(ref DeterministicRandom rng, bool forceBig)
        {
            int pick = rng.Next(forceBig ? DailyRewardTables.ForcedWeightTotal : DailyRewardTables.GiftWeightTotal);
            for (int i = 0; i < DailyRewardTables.GiftLotCount; i++)
            {
                var lot = (GiftLot)i;
                pick -= forceBig ? DailyRewardTables.ForcedWeight(lot) : DailyRewardTables.Weight(lot);
                if (pick < 0)
                    return lot;
            }
            return GiftLot.Egg;
        }
    }
}
