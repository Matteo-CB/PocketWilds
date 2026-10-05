using PocketWilds.Core;

namespace PocketWilds.Presentation
{
    /// <summary>PF_UI_OfflineReturn as seen by the sanctuary (implemented in the UI assembly).</summary>
    public interface IOfflineReturnView
    {
        /// <summary>True from Show until the closing animation ends. The world ignores touches meanwhile.</summary>
        bool IsOpen { get; }

        /// <summary>Opens the screen: "Welcome back", the Light earned, one Collect button.</summary>
        void Show(BigValue light, ILocalization text, IOfflineReturnActions actions);

        void Tick(float unscaledDeltaTime);
    }

    public interface IOfflineReturnActions
    {
        void CollectOffline();
    }
}
