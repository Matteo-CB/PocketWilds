using PocketWilds.Content;

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

        /// <summary>When false, stays in the bootstrap scene after boot (tests).</summary>
        public bool LoadTargetScene = true;
    }
}
