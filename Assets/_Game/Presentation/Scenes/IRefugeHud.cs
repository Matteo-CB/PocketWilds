using PocketWilds.Core;

namespace PocketWilds.Presentation
{
    /// <summary>What the refuge HUD (UI assembly) shows. Presentation never references UI types.</summary>
    public interface IRefugeHud
    {
        void Bind(IRefugeActions actions);

        /// <summary>Called every frame; implementations only touch labels when the formatted value changes.</summary>
        void SetLight(BigValue light, BigValue perSecond);

        void SetEgg(BigValue cost, bool free, bool affordable);

        /// <summary>False while a hatch plays: the button speeds it up instead.</summary>
        void SetHatching(bool hatching);

        /// <summary>"Not yet" feedback on the currency chip and the hatch button.</summary>
        void PulseNotEnough();

        /// <summary>Species mastery after a hatch: level, XP bar, "+XP". Duplicates always show progress.</summary>
        void ShowMastery(in PocketWilds.Simulation.MasteryGain gain);

        /// <summary>Small dot on the Codex shortcut: something found and not looked at yet.</summary>
        void SetCodexBadge(bool visible);

        /// <summary>Shop shortcut and Gems chip, shown once the shop is unlocked (after onboarding).</summary>
        void SetShop(bool unlocked, long gems);

        /// <summary>T34: a short note (the device is warm, the picture is lighter); null hides it.</summary>
        void SetNotice(string text);

        void Tick(float unscaledDeltaTime);

        /// <summary>True when a screen point lands on an interactive HUD element (world taps ignore it).</summary>
        bool IsOverUi(UnityEngine.Vector2 screenPosition);
    }

    /// <summary>Player intents raised by the HUD.</summary>
    public interface IRefugeActions
    {
        void RequestHatch();

        void OpenCodex();

        void OpenShop();
    }
}
