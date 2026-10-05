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
