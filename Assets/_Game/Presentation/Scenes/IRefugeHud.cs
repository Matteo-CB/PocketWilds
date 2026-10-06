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

        /// <summary>T34A: the reveal banner after a burst (new species, new variant, or a rarity from Rare up).</summary>
        void ShowReveal(in RevealInfo info);

        /// <summary>T34A: one short toast (bounded queue, one panel reused).</summary>
        void ShowToast(ToastKind kind, string text);

        /// <summary>T34A tutorial gesture: a hand taps on a screen point (the first egg) until the player does.</summary>
        void SetTapHint(bool visible, UnityEngine.Vector2 screenPosition);

        /// <summary>T34A collected Light: motes fly from a screen point into the Light chip, which pops as they land.</summary>
        void PlayCollect(UnityEngine.Vector2 fromScreenPosition);

        /// <summary>T34A: builds the hidden feedback parts once at scene entry (first banner or toast never hitches).</summary>
        void PrewarmFeedback(string sampleTitle, string sampleText);

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

        /// <summary>T34A: the egg's odds (always available, doc 05 Hatch UX).</summary>
        void OpenEggInfo();
    }
}
