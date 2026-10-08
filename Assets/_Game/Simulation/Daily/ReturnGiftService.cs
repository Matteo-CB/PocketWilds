#nullable enable
using System;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>A return gift waiting for its card (doc 01 Contenu, section 6). Kind: 1 short (3 to 13 days), 2 big (14 or more).</summary>
    public readonly struct ReturnGift
    {
        public const byte Short = 1;
        public const byte Big = 2;

        public readonly byte Kind;

        /// <summary>Whole days away, as measured on return.</summary>
        public readonly int DaysAway;

        /// <summary>The Essence node Warm Return is owned: one more Discovery egg (and the gift from 2 days).</summary>
        public readonly bool Warm;

        public readonly RewardBundle Bundle;

        public ReturnGift(byte kind, int daysAway, bool warm, in RewardBundle bundle)
        {
            Kind = kind;
            DaysAway = daysAway;
            Warm = warm;
            Bundle = bundle;
        }

        public bool IsBig => Kind == Big;
    }

    /// <summary>
    /// Return gifts after an absence (doc 01 Contenu, section 6), on top of the normal offline Light.
    /// <list type="bullet">
    /// <item>Less than 3 days: nothing more than the offline summary (T18).</item>
    /// <item>3 to 13 days: Light 120 min, 3 tickets, 1 Discovery egg, 3 treats of the newest biome; at most one return
    /// gift per 7 days.</item>
    /// <item>14 days or more: Light 240 min, 5 tickets, 1 Discovery egg, 5 treats and 1 missing sticker of the player's
    /// choice; at most one big gift per 30 days, the short one in between.</item>
    /// <item>Essence Warm Return (doc 02 Renouveau, section 2): the gift from 2 days, with one more Discovery egg.</item>
    /// <item>Only after the welcome, never after a clock moved back, never twice for one absence; a false
    /// flags.return_gift hides it without erasing anything.</item>
    /// </list>
    /// Tickets become Light (15 min each) while the activities are closed and the sticker 3 more treats before the albums
    /// (owner decision, plan section 10). The gift is decided on return (<see cref="Evaluate"/>), kept in the save until its
    /// card is collected (<see cref="TryClaim"/>), and its Light is worth the production at that moment.
    /// </summary>
    public sealed class ReturnGiftService
    {
        public const int SecondsPerDay = 86_400;

        /// <summary>Warm Return gives the gift from this many days away.</summary>
        public const int WarmMinDays = 2;

        private readonly DailyContext _ctx;

        public ReturnGiftService(DailyContext ctx)
        {
            _ctx = ctx;
        }

        /// <summary>The gift was collected (analytics return_gift, the card's feel).</summary>
        public event Action<ReturnGift>? Granted;

        public bool HasPending => _ctx.State.DailyVisit.PendingReturnGift != 0;

        /// <summary>The waiting gift, its lot built for the systems open now (default when none).</summary>
        public ReturnGift Pending
        {
            get
            {
                var visit = _ctx.State.DailyVisit;
                if (visit.PendingReturnGift == 0)
                    return default;
                byte kind = visit.PendingReturnGift >= ReturnGift.Big ? ReturnGift.Big : ReturnGift.Short;
                bool warm = WarmReturn;
                var rewards = _ctx.Rewards();
                return new ReturnGift(kind, visit.PendingReturnDays, warm, Bundle(kind == ReturnGift.Big, warm, rewards));
            }
        }

        /// <summary>The Essence node Warm Return is owned.</summary>
        public bool WarmReturn
        {
            get
            {
                var essence = _ctx.Progression?.Essence;
                return essence != null && essence.Sum(EssenceEffect.WarmReturn) > 0.0;
            }
        }

        /// <summary>
        /// On return (after <see cref="OfflineService.ResolveReturn"/>, at boot and when the app comes back): decides the
        /// gift of this absence and keeps it until collected. Records the day numbers the frequency rules read.
        /// </summary>
        public void Evaluate(in OfflineReport report)
        {
            var config = _ctx.Config;
            var state = _ctx.State;
            var visit = state.DailyVisit;
            if (!config.ReturnGiftEnabled || visit.PendingReturnGift != 0 || report.ClockRolledBack || !state.IsOnboardingDone)
                return;

            int days = DaysAway(report, _ctx.Economy.Config.OfflineMaxSeconds);
            int minDays = WarmReturn ? Math.Min(config.ReturnGiftMinDays, WarmMinDays) : config.ReturnGiftMinDays;
            if (days < Math.Max(1, minDays))
                return;

            int today = _ctx.DayNumber;
            if (visit.LastReturnGiftDay > 0 && today - visit.LastReturnGiftDay < config.ReturnRepeatDays)
                return;
            bool big = days >= config.ReturnBigDays
                && (visit.LastBigReturnGiftDay <= 0 || today - visit.LastBigReturnGiftDay >= config.ReturnBigRepeatDays);

            visit.PendingReturnGift = big ? ReturnGift.Big : ReturnGift.Short;
            visit.PendingReturnDays = days;
            visit.LastReturnGiftDay = today;
            if (big)
                visit.LastBigReturnGiftDay = today;
            _ctx.RequestSave(SaveUrgency.Priority);
        }

        /// <summary>The card's « Récupérer »: gives the waiting gift (Light at the current production) and clears it.</summary>
        public bool TryClaim(out GrantedRewards granted)
        {
            granted = default;
            var visit = _ctx.State.DailyVisit;
            if (visit.PendingReturnGift == 0)
                return false;
            var gift = Pending;
            visit.PendingReturnGift = 0;
            visit.PendingReturnDays = 0;
            granted = _ctx.Granter.Grant(gift.Bundle, EconomyReason.ReturnGift, DiscoverySource.ReturnGift);
            Granted?.Invoke(gift);
            return true;
        }

        /// <summary>
        /// Whole days of the real absence: <see cref="OfflineReport.UncappedSeconds"/>, the time since the later of the last
        /// save and the high-water mark (what the profile has not seen yet, rollback-safe) without the offline cap
        /// (econ.offline.max_days, as low as 7). The cap only limits the Light paid, never the days counted.
        /// </summary>
        public static int DaysAway(in OfflineReport report, double offlineMaxSeconds)
        {
            double seconds = report.UncappedSeconds;
            if (!(seconds > 0.0))
                return 0;
            double days = Math.Floor(seconds / SecondsPerDay);
            return days >= int.MaxValue ? int.MaxValue : (int)days;
        }

        /// <summary>The lot of doc 01, sections 6.2 and 6.3, with the substitutions of the systems still closed (shared table).</summary>
        private static RewardBundle Bundle(bool big, bool warm, in RewardContext ctx) => DailyRewardTables.Return(big, warm, in ctx);
    }
}
