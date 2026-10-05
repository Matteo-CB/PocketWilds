namespace PocketWilds.Core
{
    /// <summary>
    /// Player-facing text by key (Writing and Localization Bible: every visible string uses a key, never
    /// the English sentence). Screens read their labels once when they open, never per frame.
    /// </summary>
    public interface ILocalization
    {
        /// <summary>Two-letter language in use, for example "en" or "fr".</summary>
        string Language { get; }

        /// <summary>Text for the key in the current language, then English, then the key itself.</summary>
        string Get(string key);
    }
}
