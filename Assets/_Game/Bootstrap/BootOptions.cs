using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Presentation;

namespace PocketWilds.Bootstrap
{
    /// <summary>
    /// Overrides for tests and dev tools. Production boots with defaults only.
    /// </summary>
    public sealed class BootOptions
    {
        /// <summary>Save folder. Null uses Application.persistentDataPath/Saves.</summary>
        public string SaveDirectory;

        /// <summary>Scene loaded after boot. Null uses the bootstrap's configured scene.</summary>
        public string TargetScene;

        /// <summary>Content used instead of the shipped registry (tests only).</summary>
        public ContentRegistry ContentOverride;

        /// <summary>Visual catalog used when the bootstrap has none assigned (tests creating it from code).</summary>
        public AnimalVisualCatalog VisualCatalogOverride;

        /// <summary>Text tables used when the bootstrap has no content database (tests).</summary>
        public LocalizationTableAsset[] LocalizationOverride;

        /// <summary>Quality tier forced for this boot (benchmarks), instead of device settings.</summary>
        public QualityTier? QualityOverride;

        /// <summary>Wall clock replacement (tests that simulate time away).</summary>
        public PocketWilds.Core.IGameClock ClockOverride;

        /// <summary>Store provider replacement (tests script purchases with the sandbox store).</summary>
        public PocketWilds.Services.Store.IStoreProvider StoreProviderOverride;

        /// <summary>Analytics batch folder. Null uses Application.persistentDataPath/Analytics.</summary>
        public string AnalyticsDirectory;

        /// <summary>Consent used instead of the stored choice (tests).</summary>
        public PocketWilds.Services.Analytics.AnalyticsConsent? AnalyticsConsentOverride;

        /// <summary>Backend replacement (tests capture what would be sent).</summary>
        public PocketWilds.Services.Analytics.IAnalyticsTransport AnalyticsTransportOverride;

        /// <summary>When false, stays in the bootstrap scene after boot (tests).</summary>
        public bool LoadTargetScene = true;

        /// <summary>T34B: benchmarks and tests run the audio (mixing, voices, decoding) without making a sound.</summary>
        public bool MuteAudio;

        /// <summary>T34B: tests record the haptic patterns instead of the platform's vibrator.</summary>
        public PocketWilds.Services.Feedback.IHapticBackend HapticBackendOverride;
    }
}
