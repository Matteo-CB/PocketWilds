using System.IO;
using System.Linq;
using PocketWilds.Presentation;
using PocketWilds.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.Editor.UI
{
    /// <summary>
    /// T48B texts (EN, FR): the four season names and their card, the six special weekends (name and effect line), the time
    /// left of the chip's sheet, and NOTIF-EVT-03 stored for the notifications task. Writing Guide: short, calm, no pressure
    /// words; placeholders, never sentences built from fragments; no em dash. Merged by UiPipeline.EnsureLocalization.
    /// </summary>
    public static class SeasonTexts
    {
        public static readonly string[,] Texts =
        {
            { SeasonText.CardTitle(PocketWilds.Simulation.RefugeSeason.Awakening), "Awakening is here", "L'Éveil commence" },
            { SeasonText.CardTitle(PocketWilds.Simulation.RefugeSeason.Sun), "Sun season is here", "Le Soleil commence" },
            { SeasonText.CardTitle(PocketWilds.Simulation.RefugeSeason.Harvest), "Harvest is here", "La Récolte commence" },
            { SeasonText.CardTitle(PocketWilds.Simulation.RefugeSeason.Frost), "Frost is here", "Le Givre commence" },
            { SeasonText.Name(PocketWilds.Simulation.RefugeSeason.Awakening), "Awakening", "Éveil" },
            { SeasonText.Name(PocketWilds.Simulation.RefugeSeason.Sun), "Sun", "Soleil" },
            { SeasonText.Name(PocketWilds.Simulation.RefugeSeason.Harvest), "Harvest", "Récolte" },
            { SeasonText.Name(PocketWilds.Simulation.RefugeSeason.Frost), "Frost", "Givre" },
            { SeasonText.CardBody(PocketWilds.Simulation.RefugeSeason.Awakening), "Fresh greens, flowers everywhere and soft spring showers.",
                "Verdure fraîche, fleurs partout et douces averses de printemps." },
            { SeasonText.CardBody(PocketWilds.Simulation.RefugeSeason.Sun), "Warm light all day long, and fireflies when evening comes.",
                "Lumière chaude toute la journée, et des lucioles le soir venu." },
            { SeasonText.CardBody(PocketWilds.Simulation.RefugeSeason.Harvest), "Golden leaves, ochre grass and a soft mist in the morning.",
                "Feuilles dorées, herbe ocre et brume légère le matin." },
            { SeasonText.CardBody(PocketWilds.Simulation.RefugeSeason.Frost), "Frosted greens, pink mornings and fine snow drifting by.",
                "Verdure givrée, matins roses et fine neige qui danse." },
            { SeasonText.WeekendName(PocketWilds.Simulation.WeekendKind.Activities), "Activities Weekend", "Week-end des Activités" },
            { SeasonText.WeekendName(PocketWilds.Simulation.WeekendKind.Eggs), "Egg Weekend", "Week-end des Œufs" },
            { SeasonText.WeekendName(PocketWilds.Simulation.WeekendKind.Cuddles), "Cuddle Weekend", "Week-end des Câlins" },
            { SeasonText.WeekendName(PocketWilds.Simulation.WeekendKind.Garden), "Garden Weekend", "Week-end du Jardin" },
            { SeasonText.WeekendName(PocketWilds.Simulation.WeekendKind.Expeditions), "Expedition Weekend", "Week-end des Expéditions" },
            { SeasonText.WeekendName(PocketWilds.Simulation.WeekendKind.Albums), "Album Weekend", "Week-end des Albums" },
            { SeasonText.WeekendEffect(PocketWilds.Simulation.WeekendKind.Activities), "Activity rewards x2", "Récompenses des activités x2" },
            { SeasonText.WeekendEffect(PocketWilds.Simulation.WeekendKind.Eggs), "More new finds in every egg", "Plus de nouveautés dans tous les œufs" },
            { SeasonText.WeekendEffect(PocketWilds.Simulation.WeekendKind.Cuddles), "Bond points x2", "Points de lien x2" },
            { SeasonText.WeekendEffect(PocketWilds.Simulation.WeekendKind.Garden), "Growth x2, seeds given back", "Pousse x2, graines rendues" },
            { SeasonText.WeekendEffect(PocketWilds.Simulation.WeekendKind.Expeditions), "Expeditions 25% shorter", "Expéditions 25 % plus courtes" },
            { SeasonText.WeekendEffect(PocketWilds.Simulation.WeekendKind.Albums), "Duplicate stars x2", "Étoiles des doublons x2" },
            { SeasonText.WeekendEnds, "Ends in {time}", "Se termine dans {time}" },
            { SeasonText.TimeDaysHours, "{d} d {h} h", "{d} j {h} h" },
            { SeasonText.TimeHoursMinutes, "{h} h {m} min", "{h} h {m} min" },
            { SeasonText.TimeMinutes, "{m} min", "{m} min" },
            { SeasonText.WeekendNotification, "It's {weekend} in your refuge.", "C'est le {weekend} au refuge." },
        };
    }

    /// <summary>T48B the weekend chip of the HUD (called by UiPipeline.AddHudDaily, right after the Event button).</summary>
    public static class SeasonUiPipeline
    {
        private static readonly Vector2 Top = new Vector2(0.5f, 1f);
        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        /// <summary>Icons per weekend kind (1 to 6): activities, eggs, cuddles, garden, expeditions, albums.</summary>
        public static readonly string[] KindIcons =
        {
            "ICO_AchCat_Activities", "ICO_AchCat_Hatching", "ICO_AchCat_Bonds", "ICO_Act_Garden", "ICO_Act_Expeditions", "ICO_Album",
        };

        /// <summary>
        /// The chip, in the Event button's slot (third on the rail; RefugeHud.LayoutRail packs the visible items): the cream
        /// circle, a full warm ring around it, the weekend's icon. No label: the sheet carries the words. Saved hidden.
        /// </summary>
        public static RectTransform AddHudWeekend(RectTransform rail, RefugeHud hud, UiColorTokens tokens)
        {
            var circle = AssetDatabase.LoadAssetAtPath<Sprite>(UiPipeline.CirclePath)
                         ?? throw new FileNotFoundException("Run SourceArt/UI/Tools/pw_ui_sprites.py first.", UiPipeline.CirclePath);
            var ringSprite = AssetDatabase.LoadAssetAtPath<Sprite>(UiPipeline.KitFolder + "/UI_Bubble_Ring.png") ?? circle;
            var root = UiPipeline.Rect("Weekend", rail);
            UiPipeline.Place(root, Top, Center, new Vector2(0f, -RefugeHud.RailPitch * 2.5f), new Vector2(UiPipeline.RailButtonSize, UiPipeline.RailButtonSize));
            var image = UiPipeline.AddImage("Button", root, circle, tokens.panelPrimary, Image.Type.Simple);
            image.raycastTarget = true;
            UiPipeline.Stretch(image.rectTransform);
            var button = image.gameObject.AddComponent<Button>();
            UiPipeline.ConfigureButton(button, image);
            var ring = UiPipeline.AddImage("Ring", root, ringSprite, tokens.accentPrimary, Image.Type.Simple);
            ring.raycastTarget = false;
            UiPipeline.Place(ring.rectTransform, Center, Center, Vector2.zero, new Vector2(176f, 176f));
            var icons = KindIcons.Select(n => OutfitUiPipeline.FindSprite(n) ?? OutfitUiPipeline.FindSprite("ICO_Paw")).ToArray();
            var icon = UiPipeline.AddImage("Icon", image.rectTransform, icons[0], Color.white, Image.Type.Simple);
            icon.raycastTarget = false;
            UiPipeline.Place(icon.rectTransform, Center, Center, new Vector2(0f, 4f), new Vector2(108f, 108f));
            root.gameObject.SetActive(false);
            hud.SetWeekendReferences(button, root, icon, icons);
            return root;
        }
    }
}
