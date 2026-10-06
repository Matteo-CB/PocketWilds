using PocketWilds.Core;
using PocketWilds.Simulation;

namespace PocketWilds.Presentation
{
    /// <summary>PF_UI_Codex as seen by the refuge (implemented in the UI assembly).</summary>
    public interface ICodexView
    {
        /// <summary>True while the Codex covers the screen: the world camera can stop rendering.</summary>
        bool IsOpen { get; }

        void Open(CodexService codex, ILocalization text, ICodexActions actions);

        /// <summary>T34A: opens on one species page (the animal sheet's Details button).</summary>
        void OpenSpecies(CodexService codex, ILocalization text, ICodexActions actions, ContentId species);

        void Tick(float unscaledDeltaTime);
    }

    public interface ICodexActions
    {
        /// <summary>The Codex closed itself (back from the list).</summary>
        void CodexClosed();
    }
}
