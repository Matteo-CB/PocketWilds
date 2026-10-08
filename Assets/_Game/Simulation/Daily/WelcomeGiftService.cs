#nullable enable
using System;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// The welcome gifts (doc 01 Contenu, section 1.4; doc Gemmes: 300 Gems), card « Cadeau de bienvenue », button
    /// « Récupérer », in the Today screen:
    /// <list type="bullet">
    /// <item>login day 1: 100 Gems, at step A12, when Today first opens;</item>
    /// <item>login day 2: 100 Gems and 2 tickets (Light 15 min each before the Play button);</item>
    /// <item>login day 3: 100 Gems and 5 seeds of Trèfle doux (kept in the inventory until the Garden).</item>
    /// </list>
    /// One a login day, three in all. A skipped day loses nothing: the next gift waits for the next login day.
    /// </summary>
    public sealed class WelcomeGiftService
    {
        private readonly DailyContext _ctx;

        public WelcomeGiftService(DailyContext ctx)
        {
            _ctx = ctx;
        }

        /// <summary>A welcome gift was claimed: its index (0 to 2) and what it gave.</summary>
        public event Action<int, GrantedRewards>? Claimed;

        /// <summary>A welcome gift is left, none was claimed today, and Today is open.</summary>
        public bool CanClaim
        {
            get
            {
                var daily = _ctx.State.DailyVisit;
                return daily.WelcomeClaimed < DailyRewardTables.WelcomeCount && daily.WelcomeDayKey < _ctx.TodayKey && _ctx.TodayOpen;
            }
        }

        /// <summary>The next gift's index, 0 to 2; 3 once all are claimed.</summary>
        public int NextIndex => Math.Min((int)_ctx.State.DailyVisit.WelcomeClaimed, DailyRewardTables.WelcomeCount);

        /// <summary>The three gifts are claimed (the card is gone for good).</summary>
        public bool AllClaimed => NextIndex >= DailyRewardTables.WelcomeCount;

        /// <summary>A welcome gift was claimed today (the card shows it taken until the next day).</summary>
        public bool ClaimedToday
        {
            get
            {
                int day = _ctx.State.DailyVisit.WelcomeDayKey;
                return day != 0 && day >= _ctx.TodayKey;
            }
        }

        /// <summary>The next gift as it would be given now (empty once all are claimed).</summary>
        public RewardBundle Peek() => DailyRewardTables.Welcome(NextIndex, _ctx.Rewards(), _ctx.Config);

        public bool TryClaim(out GrantedRewards granted)
        {
            granted = default;
            if (!CanClaim)
                return false;

            var daily = _ctx.State.DailyVisit;
            int index = NextIndex;
            var bundle = DailyRewardTables.Welcome(index, _ctx.Rewards(), _ctx.Config);

            // The state moves first: whatever the grant's listeners do, a welcome gift is never given twice.
            daily.WelcomeClaimed = (byte)(index + 1);
            daily.WelcomeDayKey = _ctx.TodayKey;

            // Welcome gifts hold no Discovery egg: the source is never used.
            granted = _ctx.Granter.Grant(bundle, EconomyReason.WelcomeGift, DiscoverySource.DailyGift);
            Claimed?.Invoke(index, granted);
            return true;
        }
    }
}
