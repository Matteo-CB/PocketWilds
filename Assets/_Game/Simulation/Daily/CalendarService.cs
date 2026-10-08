#nullable enable
using System;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>One calendar box once stamped.</summary>
    public readonly struct CalendarClaim
    {
        /// <summary>The box, 0 to 27 (boxes 1 to 28).</summary>
        public readonly int Box;

        /// <summary>The cycle the box belonged to, from 1.</summary>
        public readonly int Cycle;

        public readonly GrantedRewards Granted;

        /// <summary>The cycle's decoration (box 28 of cycles 1 to 26), None otherwise.</summary>
        public readonly ContentId Decoration;

        /// <summary>Boxes 7, 14, 21 and 28: the longer stamp.</summary>
        public readonly bool Big;

        /// <summary>Day key of the stamp.</summary>
        public readonly int DayKey;

        public CalendarClaim(int box, int cycle, in GrantedRewards granted, ContentId decoration, bool big, int dayKey)
        {
            Box = box;
            Cycle = cycle;
            Granted = granted;
            Decoration = decoration;
            Big = big;
            DayKey = dayKey;
        }

        /// <summary>The box's number as shown, 1 to 28 (« Jour {n} »).</summary>
        public int Number => Box + 1;

        public bool Substituted => Granted.Bundle.Substituted;
    }

    /// <summary>
    /// The 28-day calendar (doc 01 Contenu, section 4): each login day stamps one box and gives its lot, in one gesture, in
    /// the Today screen.
    /// <list type="bullet">
    /// <item>A missed day resets nothing: the next stamp is the next box. No streak, no paid catch-up.</item>
    /// <item>Gems only at boxes 7, 14 and 21 (20 each) and 28 (40): 100 a cycle. Discovery eggs at boxes 16 and 27.</item>
    /// <item>After box 28 a new cycle starts at box 1 on the next login day. Box 28 gives the cycle's decoration, the dusk
    /// tint from cycle 14, a Discovery egg from cycle 27.</item>
    /// <item>Boxes of systems not open yet are replaced (<see cref="DailyRewardTables.CalendarBox"/>).</item>
    /// </list>
    /// </summary>
    public sealed class CalendarService
    {
        private readonly DailyContext _ctx;

        public CalendarService(DailyContext ctx)
        {
            _ctx = ctx;
        }

        /// <summary>A box was stamped (analytics, the stamp's feel).</summary>
        public event Action<CalendarClaim>? Claimed;

        /// <summary>Today is open, the calendar is enabled (flags.calendar) and today's box is not stamped yet.</summary>
        public bool CanClaim => _ctx.State.DailyVisit.CalendarDayKey < _ctx.TodayKey && _ctx.Config.CalendarEnabled && _ctx.TodayOpen;

        /// <summary>Today's box is already stamped.</summary>
        public bool StampedToday
        {
            get
            {
                int day = _ctx.State.DailyVisit.CalendarDayKey;
                return day != 0 && day >= _ctx.TodayKey;
            }
        }

        /// <summary>The next box to stamp, 0 to 27.</summary>
        public int NextBox => _ctx.State.DailyVisit.CalendarNext % DailyRewardTables.CalendarBoxCount;

        /// <summary>The cycle of the next box, from 1.</summary>
        public int Cycle => Math.Max(1, _ctx.State.DailyVisit.CalendarCycle);

        /// <summary>Boxes stamped since install.</summary>
        public int Stamps => _ctx.State.DailyVisit.CalendarStamps;

        /// <summary>
        /// Stamps still needed to get the next big box (7, 14, 21 or 28), that box included: 1 when the next box is big
        /// (« Encore {n} jours »).
        /// </summary>
        public int DaysToNextBig => 7 - NextBox % 7;

        /// <summary>
        /// A box ahead, as it would be given now: offset 0 is the next box to stamp, 1 the one after, and so on across
        /// cycles (« Demain : {reward} », the next cycle's decoration on day 29).
        /// </summary>
        public RewardBundle PeekBox(int offset)
        {
            Locate(offset, out int box, out int cycle);
            return DailyRewardTables.CalendarBox(box, cycle, _ctx.Rewards(), _ctx.Config);
        }

        /// <summary>The box and cycle <paramref name="offset"/> stamps ahead (offset 0: the next box).</summary>
        public void Locate(int offset, out int box, out int cycle)
        {
            int index = NextBox + Math.Max(0, offset);
            box = index % DailyRewardTables.CalendarBoxCount;
            cycle = Cycle + index / DailyRewardTables.CalendarBoxCount;
        }

        /// <summary>Stamps today's box: grants its lot, then moves to the next box (and the next cycle after box 28).</summary>
        public bool TryClaim(out CalendarClaim claim)
        {
            claim = default;
            if (!CanClaim)
                return false;

            var daily = _ctx.State.DailyVisit;
            int today = _ctx.TodayKey;
            int box = NextBox;
            int cycle = Cycle;
            var bundle = DailyRewardTables.CalendarBox(box, cycle, _ctx.Rewards(), _ctx.Config);

            // The state moves first: whatever the grant's listeners do, today's box is never stamped twice.
            daily.CalendarDayKey = today;
            daily.CalendarStamps++;
            if (box + 1 >= DailyRewardTables.CalendarBoxCount)
            {
                daily.CalendarNext = 0;
                daily.CalendarCycle = cycle + 1;
            }
            else
            {
                daily.CalendarNext = (byte)(box + 1);
                daily.CalendarCycle = cycle;
            }

            var granted = _ctx.Granter.Grant(bundle, EconomyReason.Calendar, DiscoverySource.Calendar);
            claim = new CalendarClaim(box, cycle, granted, bundle.Decoration, DailyRewardTables.IsBigBox(box), today);
            Claimed?.Invoke(claim);
            return true;
        }
    }
}
