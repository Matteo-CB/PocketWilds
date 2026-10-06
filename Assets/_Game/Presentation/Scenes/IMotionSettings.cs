namespace PocketWilds.Presentation
{
    /// <summary>
    /// T34A: a screen that honours the player's Reduce Motion setting (SettingsState.ReduceMotion, doc 05
    /// Accessibility). The scene entry passes the setting to every screen it holds.
    /// </summary>
    public interface IMotionSettings
    {
        bool ReduceMotion { get; set; }
    }
}
