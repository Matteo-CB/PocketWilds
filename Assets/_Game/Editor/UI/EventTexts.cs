using System.Collections.Generic;
using PocketWilds.Presentation;
using TMPro;
using UnityEngine;

namespace PocketWilds.Editor.UI
{
    /// <summary>
    /// T48 texts (EN, FR): the event hub (event.*), the HUD's announce and end summary, the shop's « Événement » section with the
    /// Carnet d'or and the Coffret, the Memory Shop, the generic reward labels, and the test event's name and currency (captures).
    /// The quest lines are the quest lane's (EventQuestTexts). Writing Guide: short, calm, no pressure words, no countdown;
    /// placeholders, never sentences built from fragments; no em dash.
    /// </summary>
    public static class EventTexts
    {
        public static readonly string[,] Texts =
        {
            { EventText.Ends, "Until {0}", "Jusqu'au {0}" },
            { EventText.Starts, "Starts on {0}", "Commence le {0}" },
            { EventText.Headline, "To win", "À gagner" },
            { EventText.AnnounceNote, "Quests, the egg and the shop open on the first day.", "Les quêtes, l'œuf et la boutique ouvrent le premier jour." },
            { EventText.TabTrack, "Track", "Parcours" },
            { EventText.TabEgg, "Egg", "Œuf" },
            { EventText.TabShop, "Shop", "Boutique" },
            { EventText.TabQuests, "Quests", "Quêtes" },
            { EventText.TabChallenges, "Challenges", "Défis" },
            { EventText.TabGoal, "Goal", "Objectif" },
            { EventText.TabMemory, "Memories", "Souvenirs" },
            { EventText.TrackPoints, "{0} points", "{0} points" },
            { EventText.TrackFree, "Free", "Gratuit" },
            { EventText.ClaimAll, "Claim all", "Tout récupérer" },
            { EventText.Claim, "Claim", "Récupérer" },
            { EventText.GoldGet, "See the Gold book", "Voir le Carnet d'or" },
            { EventText.GoldOwned, "Gold book owned", "Carnet d'or à toi" },
            { EventText.EggTitle, "Event egg", "Œuf de l'événement" },
            { EventText.EggOpen, "Open", "Ouvrir" },
            { EventText.EggNewIn, "New item within {0} eggs", "Nouvel objet dans {0} œufs" },
            { EventText.EggNewIn + ".one", "New item in the next egg", "Nouvel objet au prochain œuf" },
            { EventText.EggComplete, "All the items are yours", "Tous les objets sont à toi" },
            { EventText.EggOddsTitle, "Odds", "Chances" },
            { EventText.EggOddsItem, "Each item: {0}%", "Chaque objet : {0} %" },
            { EventText.EggOddsSecret, "Secret look: {0}%", "Couleur secrète : {0} %" },
            { EventText.EggOddsRule, "A new item at least every {0} eggs, all 7 by egg {1} at the latest.",
                "Un nouvel objet au moins tous les {0} œufs, les 7 au plus tard au {1}e œuf." },
            { EventText.EggNew, "New!", "Nouveau !" },
            { EventText.EggDuplicate, "Already yours, {0} back", "Déjà à toi, {0} rendus" },
            { EventText.NotEnough, "Not enough yet", "Pas encore assez" },
            { EventText.ShopTitle, "Event shop", "Boutique de l'événement" },
            { EventText.ShopLeft, "{0} left", "Reste {0}" },
            { EventText.ShopSoldOut, "Sold out", "Épuisé" },
            { EventText.ShopBuy, "Buy", "Acheter" },
            { EventText.ShopPickSticker, "Pick a sticker", "Choisis un autocollant" },
            { EventText.Owned, "Already yours", "Déjà à toi" },
            { EventText.QuestsDaily, "Daily quests", "Quêtes du jour" },
            { EventText.QuestsLong, "Big quests", "Grandes quêtes" },
            { EventText.QuestsSwap, "Swap", "Échanger" },
            { EventText.QuestsPending, "{0} waiting", "{0} en attente" },
            { EventText.QuestsSwapsLeft, "Swaps: {0}", "Échanges : {0}" },
            { EventText.ChallengesTitle, "Challenges", "Défis" },
            { EventText.ChallengeName, "Challenge {0}", "Défi {0}" },
            { EventText.ChallengesStars, "{0} / 30 stars", "{0} / 30 étoiles" },
            { EventText.ChallengesOutfit, "30 stars: an outfit", "30 étoiles : une tenue" },
            { EventText.ChallengesPractice, "Playable in practice, no ticket needed", "Jouables en entraînement, sans billet" },
            { EventText.GoalTitle, "Your goal", "Ton objectif" },
            { EventText.GoalProgress, "{0} / {1} points", "{0} / {1} points" },
            { EventText.GoalReached, "Goal reached, well done!", "Objectif atteint, bravo !" },
            { EventText.GoalNote, "Just for you, at your own pace.", "Rien que pour toi, à ton rythme." },
            { EventText.MemoryTitle, "Memory shop", "Boutique des souvenirs" },
            { EventText.SummaryTitle, "The event is over", "L'événement est terminé" },
            { EventText.SummaryCurrency, "Left over: {0}", "Restant : {0}" },
            { EventText.SummaryLight, "Light received: {0}", "Lumière reçue : {0}" },
            { EventText.SummaryTier, "Final tier: {0}", "Palier final : {0}" },
            { EventText.SummaryOk, "Thank you", "Merci" },
            { EventText.ShopSection, "Event", "Événement" },
            { EventText.GoldTitle, "Gold book", "Carnet d'or" },
            { EventText.GoldBody, "A golden line of rewards on the track, even for tiers already reached.",
                "Une ligne de récompenses dorées sur le parcours, même pour les paliers déjà passés." },
            { EventText.BoxTitle, "Event box", "Coffret" },
            { EventText.BoxBody, "An outfit, 4 luxury decorations and 800 Gems.", "Une tenue, 4 décors de luxe et 800 Gemmes." },
            { EventText.ShopOneTime, "Once per event", "Une fois par événement" },
            { EventText.GoldContentTrack, "30 golden tiers", "30 paliers dorés" },
            { EventText.GoldContentOutfits, "2 outfits", "2 tenues" },
            { EventText.GoldContentBonus, "Bonus points", "Points en plus" },
            { EventText.BoxContentOutfit, "Outfit", "Tenue" },
            { EventText.BoxContentDecos, "4 decorations", "4 décors" },
            { EventText.RewardPrefix + "gems", "{0} Gems", "{0} Gemmes" },
            { EventText.RewardPrefix + "tickets", "{0} tickets", "{0} billets" },
            { EventText.RewardPrefix + "treats", "{0} treats", "{0} friandises" },
            { EventText.RewardPrefix + "discovery_egg", "Discovery egg", "Œuf Découverte" },
            { EventText.RewardPrefix + "light", "{0} min of Light", "{0} min de Lumière" },
            { EventText.RewardPrefix + "sticker", "Sticker", "Autocollant" },
            { EventText.RewardPrefix + "decoration", "Decoration", "Décor" },
            { EventText.RewardPrefix + "outfit", "Outfit", "Tenue" },
            { EventText.RewardPrefix + "variant", "New look", "Nouvelle couleur" },
            { EventText.RewardPrefix + "frame", "Profile frame", "Cadre de profil" },
            { EventText.RewardPrefix + "hatch_effect", "Hatch effect", "Effet d'éclosion" },
            { EventText.RewardPrefix + "monument", "Monument", "Monument" },
            { EventText.RewardPrefix + "souvenir", "Souvenir", "Souvenir" },
            { EventText.RewardPrefix + "pose", "Photo pose", "Pose photo" },
            { EventText.ShopEgg, "Event egg", "Œuf de l'événement" },
            { EventText.ShopSeeds, "{0} garden seeds", "{0} graines" },
            { "event.test.name", "Trial event", "Événement d'essai" },
            { "event.test.currency", "Tokens", "Jetons" },
        };

        private static Dictionary<string, string>[] _lookup;

        /// <summary>The text of a key in one language (0 en, 1 fr): this table, the quest lane's, the UI and activity tables.</summary>
        public static string Lookup(string key, int language)
        {
            if (_lookup == null)
            {
                _lookup = new[] { new Dictionary<string, string>(), new Dictionary<string, string>() };
                Add(UiPipeline.Texts);
                Add(ActivityTexts.Texts);
                Add(Texts);
                // T49: the download page and L'Éveil des Terriers' names.
                Add(BurrowsTexts.Texts);
                // The quest lines (agent lane T48 quests), found by reflection so this table builds without them.
                var quests = typeof(EventTexts).Assembly.GetType("PocketWilds.Editor.UI.EventQuestTexts");
                if (quests?.GetField("Texts")?.GetValue(null) is string[,] questTexts)
                    Add(questTexts);
            }
            return _lookup[language].TryGetValue(key, out var text) ? text : key;
        }

        private static void Add(string[,] table)
        {
            for (int i = 0; i < table.GetLength(0); i++)
            {
                _lookup[0][table[i, 0]] = table[i, 1];
                _lookup[1][table[i, 0]] = table[i, 2];
            }
        }

        /// <summary>
        /// Text spacing rule (same measure as <see cref="OutfitTexts.Fit"/>): every key in both languages at the label's smallest size,
        /// inside its box less 8 on each side (wrapped labels: within their height), placeholders filled with long values. Leaves
        /// the widest text in the label. Returns the problems, empty when everything fits.
        /// </summary>
        public static string Fit(TMP_Text label, params string[] keys)
        {
            var problems = new System.Text.StringBuilder();
            var box = label.rectTransform.rect.size;
            if (box.x <= 0f)
                box = label.rectTransform.sizeDelta;
            float keep = label.fontSize;
            bool auto = label.enableAutoSizing;
            float size = auto ? label.fontSizeMin : label.fontSize;
            label.enableAutoSizing = false;
            bool wrap = label.textWrappingMode != TextWrappingModes.NoWrap;
            string widest = label.text;
            float widestWidth = -1f;
            label.fontSize = size;
            foreach (var key in keys)
            {
                for (int language = 0; language < UiPipeline.Languages.Length; language++)
                {
                    string text = Lookup(key, language).Replace("{0}", "13 000").Replace("{1}", "21").Replace("{n}", "10")
                        .Replace("{activity}", language == 0 ? "Hide and Seek" : "Cache-cache").Replace("{currency}", language == 0 ? "Tokens" : "Jetons");
                    var preferred = label.GetPreferredValues(text, wrap ? box.x - 16f : 0f, 0f);
                    bool fits = wrap ? preferred.y <= box.y + 0.5f : preferred.x <= box.x - 16f;
                    if (!fits)
                        problems.Append($"{label.name} '{text}' {preferred.x:F0}x{preferred.y:F0} in {box.x:F0}x{box.y:F0}; ");
                    if (preferred.x > widestWidth)
                    {
                        widestWidth = preferred.x;
                        widest = text;
                    }
                }
            }
            label.fontSize = keep;
            label.enableAutoSizing = auto;
            label.text = widest;
            return problems.ToString();
        }
    }
}
