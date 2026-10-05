using PocketWilds.Core;

namespace PocketWilds.Presentation
{
    /// <summary>What the sanctuary HUD (UI assembly) shows. Presentation never references UI types.</summary>
    public interface ISanctuaryHud
    {
        void Bind(ISanctuaryActions actions);

        /// <summary>Called every frame; implementations only touch labels when the formatted value changes.</summary>
        void SetLight(BigValue light, BigValue perSecond);

        void SetEgg(BigValue cost, bool free, bool affordable);

        /// <summary>False while a hatch plays: the button speeds it up instead.</summary>
        void SetHatching(bool hatching);

        /// <summary>"Not yet" feedback on the currency chip and the hatch button.</summary>
        void PulseNotEnough();

        void Tick(float unscaledDeltaTime);

        /// <summary>True when a screen point lands on an interactive HUD element (world taps ignore it).</summary>
        bool IsOverUi(UnityEngine.Vector2 screenPosition);
    }

    /// <summary>Player intents raised by the HUD.</summary>
    public interface ISanctuaryActions
    {
        void RequestHatch();
    }
}
