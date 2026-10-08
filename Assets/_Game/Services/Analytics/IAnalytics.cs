#nullable enable
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Simulation;

namespace PocketWilds.Services.Analytics
{
    /// <summary>
    /// What presentation reports (screens, offers seen and tapped, funnel steps only it can see). Gameplay facts
    /// (hatches, economy, purchases) are read from the services' events by <see cref="AnalyticsService"/>, so
    /// presentation never has to remember to send them. Every call is allocation free.
    /// </summary>
    public interface IAnalytics
    {
        void ScreenView(ScreenId screen);

        /// <summary>An offer card or modal was shown. Counted once per offer per session.</summary>
        void OfferImpression(ContentId offer, OfferPlacement placement);

        void OfferClick(ContentId offer, OfferPlacement placement);

        /// <summary>Fires once per profile; later calls are ignored.</summary>
        void Milestone(FunnelMilestone milestone);

        /// <summary>T35A: a biome gate was shown, with the conditions it shows met.</summary>
        void BiomeGateSeen(ContentId biome, GateConditions met)
        {
        }

        /// <summary>T35B: the odds window of an egg was opened (doc 02 Œufs, section 8).</summary>
        void EggOddsOpened(ContentId egg)
        {
        }

        /// <summary>
        /// T36: everything claimable in Today was claimed: seconds from Today opened to the last claim, taps on the way,
        /// items claimed (the « under two minutes » criterion measured in the field).
        /// </summary>
        void DailyVisitDone(double seconds, int taps, int items)
        {
        }

        /// <summary>T37 (doc 08, today_open): Today opened, with the wishes pending, the gift ready and the calendar's day.</summary>
        void TodayOpen(int wishesPending, bool giftReady, int calendarDay)
        {
        }

        /// <summary>
        /// T37 (doc 08, tickets_empty): the « no ticket » card closed, with the choice made (0 closed, 1 wait (practice
        /// instead), 2 an ad, 3 a pack for Gems) and the seconds left before the next free ticket.
        /// </summary>
        void TicketsEmpty(int choice, double refillSeconds)
        {
        }
    }

    public sealed class NullAnalytics : IAnalytics
    {
        public static readonly NullAnalytics Instance = new NullAnalytics();

        public void ScreenView(ScreenId screen)
        {
        }

        public void OfferImpression(ContentId offer, OfferPlacement placement)
        {
        }

        public void OfferClick(ContentId offer, OfferPlacement placement)
        {
        }

        public void Milestone(FunnelMilestone milestone)
        {
        }
    }
}
