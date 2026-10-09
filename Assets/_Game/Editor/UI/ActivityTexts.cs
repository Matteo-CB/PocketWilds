using System.Collections.Generic;
using PocketWilds.Presentation;
using PocketWilds.Simulation;

namespace PocketWilds.Editor.UI
{
    /// <summary>
    /// T37 shipped texts of the activities (doc « Mini-jeux & activités », doc 01 Contenu sections 0, A15 and 12; Writing
    /// Guide: short, warm, no em dash, no pressure words, real time never a countdown), and T38's Grooming texts (its tips,
    /// the sheet's « Brosser » and its 10 challenges, doc 01 S-ACT-001), and T39's Hide and Seek texts (its tips and its 10
    /// challenges, doc 01 S-ACT-002). Columns: key, en, fr. Merged into
    /// LOC_en / LOC_fr by <see cref="UiPipeline.EnsureLocalization"/>. Keys are <see cref="ActivityText"/>'s.
    /// <para>Counted texts come as three rows: « key.one » and « key.other », read by
    /// <see cref="PocketWilds.Core.ILocalization.GetPlural"/>, and the key itself (the usual plural) for a plain Get.</para>
    /// <para>Placeholders: {n} and {total} numbers, {h} hours and {m} minutes (the screens write {m} on two digits after
    /// hours: « 1 h 05 »), {price} Gems, {goal} a challenge goal, {first} and {second} two unlock conditions. French uses a
    /// no-break space before « : » and « ! » and between a number and its unit. The 18 activity names are doc 01's table
    /// « Noms anglais des biomes et des activités » (section 0).</para>
    /// </summary>
    public static class ActivityTexts
    {
        public static readonly string[,] Texts = Build();

        private const string Nbsp = " ";

        private static string[,] Build()
        {
            var rows = new List<string[]>(128);

            void Plain(string key, string en, string fr) => rows.Add(new[] { key, en, fr });

            void Counted(string key, string enOne, string enOther, string frOne, string frOther)
            {
                rows.Add(new[] { key, enOther, frOther });
                rows.Add(new[] { key + ".one", enOne, frOne });
                rows.Add(new[] { key + ".other", enOther, frOther });
            }

            // The 18 activities (doc 01 section 0) and the framework's own test activity.
            Plain(ActivityText.NameKey(ActivityId.Grooming), "Grooming", "Toilettage");
            Plain(ActivityText.NameKey(ActivityId.HideAndSeek), "Hide and Seek", "Cache-cache");
            Plain(ActivityText.NameKey(ActivityId.NutRain), "Nut Drop", "Pluie de noisettes");
            Plain(ActivityText.NameKey(ActivityId.PawMemory), "Pawprint Memory", "Memory des empreintes");
            Plain(ActivityText.NameKey(ActivityId.Expeditions), "Expeditions", "Expéditions");
            Plain(ActivityText.NameKey(ActivityId.Garden), "Garden", "Jardin");
            Plain(ActivityText.NameKey(ActivityId.TreasureFishing), "Treasure Fishing", "Pêche aux trésors");
            Plain(ActivityText.NameKey(ActivityId.LilyHop), "Lily Pad Hop", "Saut de nénuphars");
            Plain(ActivityText.NameKey(ActivityId.Bakery), "Baking", "Pâtisserie");
            Plain(ActivityText.NameKey(ActivityId.PhotoSafari), "Photo Safari", "Safari photo");
            Plain(ActivityText.NameKey(ActivityId.CuddleMatch), "Cuddle Match", "Match des câlins");
            Plain(ActivityText.NameKey(ActivityId.Slide), "Glide", "Glissade");
            Plain(ActivityText.NameKey(ActivityId.Constellations), "Constellations", "Constellations");
            Plain(ActivityText.NameKey(ActivityId.FireflyRhythm), "Firefly Rhythm", "Rythme des lucioles");
            Plain(ActivityText.NameKey(ActivityId.Bubbles), "Bubbles", "Bulles");
            Plain(ActivityText.NameKey(ActivityId.TilePuzzle), "Tile Puzzle", "Taquin");
            Plain(ActivityText.NameKey(ActivityId.BurrowMaze), "Burrow Maze", "Labyrinthe du terrier");
            Plain(ActivityText.NameKey(ActivityId.GrandContest), "Grand Contest", "Grand Concours");
            Plain(ActivityText.NameKey(ActivityId.Test), "Test game", "Partie test");

            // HUD rail and hub (screen 029).
            Plain(ActivityText.HudPlay, "Play", "Jouer");
            Plain(ActivityText.HubTitle, "Activities", "Activités");
            Plain(ActivityText.HubPlay, "Play", "Jouer");
            Plain(ActivityText.HubFree, "Free", "Gratuit");
            Plain(ActivityText.HubNew, "New", "Nouveau");
            Plain(ActivityText.HubBest, "Best {n}", "Record {n}");
            Plain(ActivityText.HubChallenges, "Challenges", "Défis");
            Plain(ActivityText.HubChallengePick, "Tap a challenge to pick it", "Touche un défi pour le choisir");
            Plain(ActivityText.HubChallengeGoal, "Challenge {n}: {goal}", "Défi {n}" + Nbsp + ": {goal}");
            Plain(ActivityText.HubChallengeLocked, "A star on challenge {n} opens this one", "Une étoile au défi {n} ouvre celui-ci");
            Plain(ActivityText.HubTheme, "Theme {n}", "Thème {n}");
            Plain(ActivityText.Challenge, "Challenge {n}", "Défi {n}");

            // Tickets: the real time to the next free one, in minutes, never seconds.
            Plain(ActivityText.TicketsNextMinutes, "Next ticket in {n} min", "Prochain billet dans {n}" + Nbsp + "min");
            Plain(ActivityText.TicketsNextHours, "Next ticket in {h} h", "Prochain billet dans {h}" + Nbsp + "h");
            Plain(ActivityText.TicketsNextHoursMinutes, "Next ticket in {h} h {m} min", "Prochain billet dans {h}" + Nbsp + "h" + Nbsp + "{m}");
            Plain(ActivityText.TicketsFull, "All your tickets are ready", "Tous tes billets sont prêts");

            // Practice.
            Plain(ActivityText.Practice, "Practice", "Entraînement");
            Plain(ActivityText.PracticeLine, "Free, no rewards", "Gratuit, sans récompense");

            // Unlock hints (doc 01 section 0).
            Plain(ActivityText.UnlockRefugeLevel, "Refuge level {n}", "Niveau du refuge {n}");
            Plain(ActivityText.UnlockDay, "Day {n}", "Jour {n}");
            Counted(ActivityText.UnlockAnimals, "{n} animal", "{n} animals", "{n} animal", "{n} animaux");
            Plain(ActivityText.UnlockAnd, "{first} and {second}", "{first} et {second}");
            Plain(ActivityText.UnlockOr, "{first} or {second}", "{first} ou {second}");
            Plain(ActivityText.UnlockLake, "Open the Lake", "Ouvre le Lac");
            Plain(ActivityText.UnlockMountain, "Open the Mountain", "Ouvre la Montagne");
            Plain(ActivityText.UnlockAxolotl, "Meet the Axolotl", "Découvre l'Axolotl");
            Plain(ActivityText.UnlockGardenLevel, "Garden level {n}", "Jardin niveau {n}");
            Plain(ActivityText.UnlockNightVisitor, "A visitor at night", "Un visiteur de nuit");
            Plain(ActivityText.UnlockOutfit, "An accessory or outfit", "Un accessoire ou une tenue");
            Plain(ActivityText.UnlockSoon, "Coming soon", "Bientôt");

            // In-game HUD and pause (screen 030).
            Plain(ActivityText.HudStreak, "Streak ×{n}", "Série ×{n}");
            Plain(ActivityText.PauseTitle, "Paused", "Pause");
            Plain(ActivityText.PauseResume, "Resume", "Reprendre");
            Plain(ActivityText.PauseQuit, "Quit", "Quitter");

            // Result (doc 05 sheet 20).
            Plain(ActivityText.ResultTitleNone, "Nice try", "Joli essai");
            Plain(ActivityText.ResultTitleOne, "Well played", "Bien joué");
            Plain(ActivityText.ResultTitleTwo, "Great job", "Très bien");
            Plain(ActivityText.ResultTitleThree, "Perfect!", "Parfait" + Nbsp + "!");
            Plain(ActivityText.ResultRecord, "New record", "Nouveau record");
            Plain(ActivityText.ResultBest, "Best: {n}", "Record" + Nbsp + ": {n}");
            Plain(ActivityText.ResultReplay, "Play again", "Rejouer");
            Plain(ActivityText.ResultBack, "Back", "Retour");
            Plain(ActivityText.ResultNextSticker, "Next sticker on the page", "Prochain autocollant de la page");
            Plain(ActivityText.ResultPractice, "Practice game", "Partie d'entraînement");
            Plain(ActivityText.ResultNewStar, "New star", "Nouvelle étoile");

            // « No ticket » card.
            Plain(ActivityText.EmptyTitle, "Tickets on the way", "Billets en route");
            Plain(ActivityText.EmptyPractice, "Play for practice", "Jouer en entraînement");
            // The pack button: « 3 billets » beside its price chip (a Gems icon and « 30 »).
            Counted(ActivityText.EmptyPack, "{n} ticket", "{n} tickets", "{n} billet", "{n} billets");
            Plain(ActivityText.EmptyPacksUsed, "More packs tomorrow", "Les packs reviennent demain");
            Plain(ActivityText.EmptyNeedGems, "Not enough Gems yet", "Pas encore assez de Gemmes");

            // T38 Grooming (doc 01 S-ACT-001 and A15): the HUD's tips, shown a few seconds over the game.
            Plain(ActivityText.GroomingTipBrush, "Brush the fur", "Brosse le pelage");
            Plain(ActivityText.GroomingTipLeaves, "Pick off the leaves", "Enlève les feuilles");
            Plain(ActivityText.GroomingTipBurr, "Two little taps for burrs", "Deux petits coups pour les bardanes");
            Plain(ActivityText.GroomingTipBow, "Pick a bow", "Choisis un nœud");
            Plain(ActivityText.GroomingTipBubble, "Tap the bubble", "Touche la bulle");
            // The animal sheet's button (doc 01 S-ACT-001: « Brosser » / « Groom »).
            Plain(ActivityText.SheetGroom, "Groom", "Brosser");

            // The 10 Grooming challenges (doc 01 S-ACT-001 « Défis »): the doc's names, and each goal in one short line
            // for the hub's « Défi n : goal » (every goal fits one line of the hub at 40 units, two lines with 30 % more).
            Challenge(1, "First Brush", "Premier coup de brosse", "Clean the whole coat", "Nettoie tout le pelage");
            Challenge(2, "Gently Does It", "Tout doux", "Brush only the dusty spots", "Nettoie sans rebrosser");
            Challenge(3, "Leaf Shower", "Pluie de feuilles", "Pick off lots of leaves", "Enlève plein de feuilles");
            Challenge(4, "Quick and Neat", "Vite fait, bien fait", "Finish in 30 seconds", "Finis en 30" + Nbsp + "secondes");
            Challenge(5, "Little Wiggler", "Petit agité", "Groom a wiggly animal", "Brosse un animal qui bouge");
            Challenge(6, "Thick Coat", "Pelage épais", "Three strokes for each spot", "Trois coups de brosse par zone");
            Challenge(7, "Big Buddy", "Grand gabarit", "Groom a big animal", "Brosse un grand animal");
            Challenge(8, "Quick Finish", "Finition rapide", "Place the bow before 35 s", "Pose le nœud avant 35" + Nbsp + "s");
            Challenge(9, "Stubborn Burrs", "Bardanes têtues", "Pick off all the burrs", "Enlève toutes les bardanes");
            Challenge(10, "Grand Grooming", "Grand toilettage", "Everything at once", "Tout à la fois");

            void Challenge(int number, string enName, string frName, string enGoal, string frGoal) =>
                ActivityChallenge(ActivityId.Grooming, number, enName, frName, enGoal, frGoal);

            // T39 Hide and Seek (doc 01 S-ACT-002 and A18): the HUD's tips (the tutorial's first words with the tapping paw,
            // the first hint, the first time the items are there, a kind word while touches rest after 3 empty touches).
            Plain(ActivityText.HideSeekTipFind, "Tap the hidden animals", "Touche les animaux cachés");
            Plain(ActivityText.HideSeekTipHint, "A wiggly ear!", "Une oreille qui bouge" + Nbsp + "!");
            Plain(ActivityText.HideSeekTipItems, "The magnifier and whistle can help", "La loupe et le sifflet t'aident");
            Plain(ActivityText.HideSeekTipPaused, "Gently…", "Doucement…");

            // The 10 Hide and Seek challenges (doc 01 S-ACT-002 « Défis »): the doc's French names, natural English ones, and
            // each goal in one short line with the challenge's twist (« Défi n : goal » fits one line of the hub at 40 units:
            // 890 at most, box 952; two lines with 30 % more). The counts are T39's (the game plays in a refuge zone of 3 to 6
            // spots: 3, 4 or 5 animals instead of the doc's 4 to 8; Docs/Design/T39_Cache_cache.md).
            HideSeek(1, "First Finds", "Premiers cachés", "Find 3 animals", "Trouve 3 animaux");
            HideSeek(2, "No Hints", "Sans indice", "4 animals, no hints or items", "4 animaux, sans indice ni objet");
            HideSeek(3, "Well Hidden", "Bien cachés", "4 animals, very well hidden", "4 animaux très bien cachés");
            HideSeek(4, "Race the Clock", "Course contre la montre", "Find 4 animals in 40 s", "Trouve 4 animaux en 40" + Nbsp + "s");
            HideSeek(5, "Big Family", "Grande famille", "Find 5 animals", "Trouve 5 animaux");
            HideSeek(6, "Sharp Eyes", "Un seul regard", "4 animals, 3 misses allowed", "4 animaux, 3 essais à côté permis");
            HideSeek(7, "In the Dark", "Dans le noir", "Find 4 animals in the dark", "Trouve 4 animaux dans la nuit");
            HideSeek(8, "All Alike", "Tous pareils", "5 animals of the same kind", "5 animaux de la même espèce");
            HideSeek(9, "Shifting Spots", "Cachettes mouvantes", "4 animals that move around", "4 animaux qui changent de place");
            HideSeek(10, "Grand Hide and Seek", "Le grand cache-cache", "5 animals in 45 s, no hints",
                "5 animaux en 45" + Nbsp + "s, sans indice");

            void HideSeek(int number, string enName, string frName, string enGoal, string frGoal) =>
                ActivityChallenge(ActivityId.HideAndSeek, number, enName, frName, enGoal, frGoal);

            void ActivityChallenge(ActivityId activity, int number, string enName, string frName, string enGoal, string frGoal)
            {
                Plain(ActivityText.ChallengeNameKey(activity, number - 1), enName, frName);
                Plain(ActivityText.ChallengeGoalKey(activity, number - 1), enGoal, frGoal);
            }

            var table = new string[rows.Count, 3];
            for (int i = 0; i < rows.Count; i++)
            {
                table[i, 0] = rows[i][0];
                table[i, 1] = rows[i][1];
                table[i, 2] = rows[i][2];
            }
            return table;
        }
    }
}
