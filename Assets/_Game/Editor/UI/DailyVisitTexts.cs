using System.Collections.Generic;
using PocketWilds.Presentation;
using PocketWilds.Simulation;

namespace PocketWilds.Editor.UI
{
    /// <summary>
    /// T36 shipped texts (doc 01 Contenu; Writing Guide: short, warm, no em dash, no pressure words, real days never a
    /// countdown). Columns: key, en, fr. Merged into LOC_en / LOC_fr by <see cref="UiPipeline.EnsureLocalization"/>.
    /// <para>Counted texts (doc 01 section 0: « une phrase avec {n} existe en forme singulier et pluriel ») come as three
    /// rows: « key.one » and « key.other », read by <see cref="PocketWilds.Core.ILocalization.GetPlural"/>, and the key
    /// itself for screens that read it with Get: the form of the usual amount (the plural, or « 1 » for what comes one
    /// at a time).</para>
    /// <para>Placeholders, filled by <see cref="DailyVisitTextFormatter"/> and by the screens' char templates: {n} and
    /// {total} numbers, {species} and {biome} bare
    /// names, {species_the} and {biome_the} names with their article (« le Lapin », « la Prairie »: the formatter contracts
    /// « de le » into « du », « à le » into « au » and elides « de » before a vowel), {animal} the animal's own name or
    /// its species with the article, {reward} a reward in words. Wishes of later systems also name {crop}, {cake},
    /// {visitor} and {destination}, filled by their tasks.</para>
    /// <para>French uses a no-break space before « : », « ! » and between a number and its unit; thousands take a narrow
    /// no-break space (both are in the Pocket Round and Pocket Plush character set).</para>
    /// </summary>
    public static class DailyVisitTexts
    {
        public static readonly string[,] Texts = Build();

        private static string[,] Build()
        {
            var rows = new List<string[]>(640);

            void Plain(string key, string en, string fr) => rows.Add(new[] { key, en, fr });

            // The key itself carries the plural so a screen that reads it without GetPlural still says something right.
            void Counted(string key, string enOne, string enOther, string frOne, string frOther)
            {
                rows.Add(new[] { key, enOther, frOther });
                rows.Add(new[] { key + ".one", enOne, frOne });
                rows.Add(new[] { key + ".other", enOther, frOther });
            }

            // Things that come one at a time (a Discovery egg, a decoration, a sticker): the key itself says « 1 ».
            void CountedSingly(string key, string enOne, string enOther, string frOne, string frOther)
            {
                rows.Add(new[] { key, enOne, frOne });
                rows.Add(new[] { key + ".one", enOne, frOne });
                rows.Add(new[] { key + ".other", enOther, frOther });
            }

            void Wish(int number, string en, string fr) => Plain(WishCatalog.TextKey((WishTemplateId)number), en, fr);

            void WishN(int number, string enOne, string enOther, string frOne, string frOther) =>
                Counted(WishCatalog.TextKey((WishTemplateId)number), enOne, enOther, frOne, frOther);

            void Goal(int number, string en, string fr) => Plain(WeeklyJournalCatalog.TextKey((JournalGoalId)number), en, fr);

            void GoalN(int number, string enOne, string enOther, string frOne, string frOther) =>
                Counted(WeeklyJournalCatalog.TextKey((JournalGoalId)number), enOne, enOther, frOne, frOther);

            // ---------------- HUD rail and toasts (doc 01 A10, A12; section 5.5) ----------------
            Plain(DailyVisitText.HudToday, "Today", "Aujourd'hui");
            Plain(DailyVisitText.ToastWishDone, "Wish come true", "Souhait réalisé");
            Plain(DailyVisitText.ToastJournalNew, "A new journal is ready", "Un nouveau carnet t'attend");
            Plain(DailyVisitText.ToastGemsFirst, "Your first Gems!", "Tes premières Gemmes !");

            // ---------------- Today (screen 031, A12) ----------------
            Plain(DailyVisitText.TodayTitle, "Today", "Aujourd'hui");
            Plain(DailyVisitText.TodayClaim, "Claim", "Récupérer");
            Plain(DailyVisitText.TodayClaimAll, "Claim all", "Tout récupérer");
            Plain(DailyVisitText.TodayClose, "Close", "Fermer");

            // ---------------- Welcome gifts (section 1.4) ----------------
            Plain(DailyVisitText.WelcomeTitle, "Welcome gift", "Cadeau de bienvenue");
            Plain(DailyVisitText.WelcomeDay, "Day {n} of 3", "Jour {n} sur 3");

            // ---------------- Daily gift (section 3.6; the doc's daily.gift.claimed is our daily.gift.tomorrow) ----------------
            Plain(DailyVisitText.GiftTitle, "Daily gift", "Cadeau du jour");
            Plain(DailyVisitText.GiftOpen, "Open", "Ouvrir");
            Plain(DailyVisitText.GiftTomorrow, "See you tomorrow", "À demain");
            Plain(DailyVisitText.GiftSmall, "Small gift", "Petit cadeau");
            Plain(DailyVisitText.GiftNice, "Nice gift", "Beau cadeau");
            Plain(DailyVisitText.GiftBig, "Big gift", "Grand cadeau");
            Plain(DailyVisitText.GiftInfoTitle, "Daily gift", "Cadeau du jour");
            Plain(DailyVisitText.GiftInfo, "At least one big gift every 15 days", "Au moins un grand cadeau tous les 15 jours");
            // The bonus gift (section 3.1) stays hidden in T36; its texts wait for the rewarded video.
            Plain("daily.bonus.title", "Bonus gift", "Cadeau bonus");
            Plain("daily.bonus.body", "A short video, 5 Gems", "Une courte vidéo, 5 Gemmes");

            // ---------------- Wishes: fixed texts (section 2.1) ----------------
            Plain(DailyVisitText.WishesTitle, "Wishes", "Souhaits");
            Plain(DailyVisitText.WishWaiting, "Your wishes are waiting", "Tes souhaits t'attendent");
            Plain(DailyVisitText.WishDone, "Wish come true", "Souhait réalisé");
            Plain(DailyVisitText.WishSwap, "Swap", "Changer");
            Plain(DailyVisitText.WishSwapUsed, "Free swap used. Next one tomorrow.", "Changement gratuit utilisé. Le prochain arrive demain.");
            Plain(DailyVisitText.WishClaim, "Claim", "Récupérer");
            Plain(DailyVisitText.WishProgress, "{n}/{total}", "{n}/{total}");

            // ---------------- Calendar (section 4.4, doc 05 sheet 19) ----------------
            Plain(DailyVisitText.CalendarTitle, "Calendar", "Calendrier");
            Plain(DailyVisitText.CalendarStamp, "Stamp", "Tamponner");
            Plain(DailyVisitText.CalendarTomorrow, "Tomorrow: {reward}", "Demain : {reward}");
            Counted(DailyVisitText.CalendarBigIn, "{n} more day", "{n} more days", "Encore {n} jour", "Encore {n} jours");
            Plain(DailyVisitText.CalendarBigTomorrow, "Big box tomorrow", "Grande case demain");
            Plain("daily.calendar.big_today", "Big box today", "Grande case aujourd'hui");
            Plain(DailyVisitText.CalendarCycle, "Cycle {n} decoration", "Décoration du cycle {n}");
            Plain(DailyVisitText.CalendarDone, "Stamped for today", "Tamponné pour aujourd'hui");
            Plain("daily.calendar.day", "Day {n}", "Jour {n}");

            // ---------------- Weekly journal (sections 5.4, 5.5) ----------------
            Plain(DailyVisitText.JournalTitle, "Weekly journal", "Carnet de la semaine");
            // The Today row: the goals reached out of the journal's, {n} and {total} (doc 06 screen 031).
            Plain(DailyVisitText.JournalRow, "Weekly journal {n}/{total}", "Carnet de la semaine {n}/{total}");
            Counted(DailyVisitText.JournalDaysLeft, "{n} day left", "{n} days left", "Encore {n} jour", "Encore {n} jours");
            Plain(DailyVisitText.JournalLastDay, "Last day", "Dernier jour");
            Plain(DailyVisitText.JournalFirst, "Your weekly journal", "Ton carnet de la semaine");
            Plain(DailyVisitText.JournalHeadStart, "Off to a good start", "Déjà bien commencé");
            Plain(DailyVisitText.JournalClaim, "Claim", "Récupérer");
            Plain(DailyVisitText.JournalComplete, "Journal complete", "Carnet complet");
            Plain(DailyVisitText.JournalCarry, "Saved for you from last week", "Gardé pour toi depuis la semaine dernière");
            Plain(DailyVisitText.JournalEachGoal, "Each goal", "Chaque objectif");

            // ---------------- Return gifts (sections 6.2, 6.3): no reproach, nothing lost ----------------
            Plain(DailyVisitText.ReturnTitle, "Welcome back", "Bon retour");
            Plain(DailyVisitText.ReturnSubtitle, "A gift for you", "Un cadeau pour toi");
            Plain(DailyVisitText.ReturnSubtitleBig, "A big gift for you", "Un grand cadeau pour toi");
            Plain("return.while_away", "While you were away", "Pendant ton absence");
            Counted(DailyVisitText.ReturnLineWishes, "A wish is waiting", "{n} wishes are waiting", "Un souhait t'attend", "{n} souhaits t'attendent");
            Plain(DailyVisitText.ReturnLineGift, "Today's gift is waiting", "Le cadeau du jour t'attend");
            Plain(DailyVisitText.ReturnLineJournal, "Your journal has rewards for you", "Ton carnet a des récompenses pour toi");
            Plain(DailyVisitText.ReturnLineCalendar, "A calendar stamp is waiting", "Une case du calendrier t'attend");

            // ---------------- Rewards in words (accessibility labels, « Demain : {reward} ») ----------------
            // reward.gems itself belongs to UiPipeline (« +{n} Gems », the level reward card); its forms are the words.
            rows.Add(new[] { DailyVisitText.RewardGems + ".one", "{n} Gem", "{n} Gemme" });
            rows.Add(new[] { DailyVisitText.RewardGems + ".other", "{n} Gems", "{n} Gemmes" });
            // reward.light_minutes is the cell's amount beside the Light icon; the words say what it is.
            Plain(DailyVisitText.RewardLightMinutes, "{n} min", "{n} min");
            Plain("reward.light_minutes.words", "{n} min of Light", "{n} min de Lumière");
            Plain("reward.light", "{n} Light", "{n} de Lumière");
            Counted(DailyVisitText.RewardTickets, "{n} ticket", "{n} tickets", "{n} billet", "{n} billets");
            Counted(DailyVisitText.RewardTreats, "{n} treat", "{n} treats", "{n} friandise", "{n} friandises");
            Counted(DailyVisitText.RewardSeeds, "{n} seed", "{n} seeds", "{n} graine", "{n} graines");
            CountedSingly(DailyVisitText.RewardEgg, "{n} Discovery egg", "{n} Discovery eggs", "{n} œuf Découverte", "{n} œufs Découverte");
            CountedSingly(DailyVisitText.RewardDecoration, "{n} decoration", "{n} decorations", "{n} décoration", "{n} décorations");
            CountedSingly(DailyVisitText.RewardSticker, "{n} sticker", "{n} stickers", "{n} autocollant", "{n} autocollants");
            CountedSingly(DailyVisitText.RewardFlowerSugar, "{n} Flower Sugar", "{n} Flower Sugar", "{n} Sucre de fleurs", "{n} Sucres de fleurs");
            Counted(DailyVisitText.RewardBond, "{n} bond point", "{n} bond points", "{n} point de lien", "{n} points de lien");
            // T40: the pastry ingredients a game gives (Nut Drop's Hazelnuts and Strawberries).
            Counted(DailyVisitText.RewardIngredients, "{n} ingredient", "{n} ingredients", "{n} ingrédient", "{n} ingrédients");

            // ---------------- Animal sheet (section 10.1) ----------------
            Plain(DailyVisitText.SheetTreat, "Treat", "Friandise");

            // ---------------- Names with their article, for sentences (« Donne une friandise au Lapin ») ----------------
            Plain("species.rabbit.name_the", "the Rabbit", "le Lapin");
            Plain("species.squirrel.name_the", "the Squirrel", "l'Écureuil");
            Plain("species.hedgehog.name_the", "the Hedgehog", "le Hérisson");

            // ---------------- Items the rendez-vous gives (sections 4.3, 10.2, 1.4, 11.2) ----------------
            Plain("treat.clover_crunch.name", "Clover Crunch", "Trèfle croquant");
            Plain("treat.toasted_hazelnut.name", "Toasted Hazelnut", "Noisette grillée");
            Plain("treat.watercress_cookie.name", "Watercress Cookie", "Biscuit au cresson");
            Plain("treat.alpine_biscuit.name", "Alpine Biscuit", "Biscuit des alpages");
            Plain("treat.frost_berry.name", "Frost Berry", "Baie de givre");
            Plain("treat.mango_bite.name", "Mango Bite", "Bouchée de mangue");
            Plain("treat.golden_date.name", "Golden Date", "Datte dorée");
            Plain("treat.cherry_bite.name", "Cherry Bite", "Bouchée de cerise");
            Plain("treat.mushroom_biscuit.name", "Mushroom Biscuit", "Biscuit champignon");
            Plain("treat.cloud_marshmallow.name", "Cloud Marshmallow", "Guimauve de nuage");
            Plain("treat.moon_honey.name", "Moon Honey Drops", "Gouttes de miel de lune");
            Plain("treat.sweet_fern.name", "Sweet Fern Leaf", "Fougère sucrée");
            Plain("treat.star_candy.name", "Star Candy", "Bonbon étoile");
            Plain("treat.seed_cookie.name", "Seed Cookie", "Biscuit aux graines");
            Plain("treat.treat_basket.name", "Treat Basket", "Panier gourmand");
            Plain("treat.golden_treat.name", "Golden Treat", "Friandise dorée");
            Plain("plant.sweet_clover.name", "Sweet Clover", "Trèfle doux");
            Plain("ingredient.flower_sugar.name", "Flower Sugar", "Sucre de fleurs");
            string[,] decorations =
            {
                { "moss_armchair", "Moss Armchair", "Fauteuil de mousse" },
                { "pebble_fountain", "Pebble Fountain", "Fontaine aux galets" },
                { "branch_swing", "Branch Swing", "Balançoire de branches" },
                { "round_garden_lantern", "Round Garden Lantern", "Lanterne de jardin ronde" },
                { "woven_linen_hammock", "Woven Linen Hammock", "Hamac de lin tressé" },
                { "giant_watering_can", "Giant Flower Watering Can", "Arrosoir fleuri géant" },
                { "birdhouse", "Birdhouse", "Cabane à oiseaux" },
                { "log_bridge", "Log Bridge", "Pont de rondins" },
                { "stump_shelf", "Stump Shelf", "Étagère en souche" },
                { "daisy_carousel", "Daisy Carousel", "Manège de pâquerettes" },
                { "cloud_mobile", "Cloud Mobile", "Mobile de nuages" },
                { "sunflower_clock", "Sunflower Clock", "Horloge tournesol" },
                { "round_greenhouse", "Round Greenhouse", "Grande serre ronde" },
            };
            // Cycles 14 to 26 give the same decorations in the « Crépuscule » tint (section 4.3).
            for (int i = 0; i < decorations.GetLength(0); i++)
                Plain("decor." + decorations[i, 0] + ".name", decorations[i, 1], decorations[i, 2]);
            for (int i = 0; i < decorations.GetLength(0); i++)
                Plain("decor." + decorations[i, 0] + "_dusk.name", "Dusk " + decorations[i, 1], decorations[i, 2] + " Crépuscule");

            // ---------------- Wish templates (section 2.3), WISH-001 to WISH-083 ----------------
            // SYS-01 Hatching
            WishN(1, "Hatch an egg", "Hatch {n} eggs", "Fais éclore un œuf", "Fais éclore {n} œufs");
            WishN(2, "Hatch a {biome} egg", "Hatch {n} {biome} eggs", "Fais éclore un œuf de {biome}", "Fais éclore {n} œufs de {biome}");
            Wish(3, "Hatch a precious egg", "Fais éclore un œuf précieux");
            // SYS-02 Discoveries and mastery
            Wish(4, "Discover a variant", "Découvre une variante");
            Wish(5, "Raise {species} mastery by one level", "Fais monter {species_the} d'un niveau de maîtrise");
            Wish(6, "Look at {species_the} in the Codex", "Regarde {species_the} dans le Codex");
            Wish(7, "Discover a new species", "Découvre une nouvelle espèce");
            // SYS-24 Breeding
            Wish(8, "Start a breeding", "Lance une reproduction");
            Wish(9, "Welcome a newborn", "Accueille un nouveau-né");
            // SYS-25 Albums
            Wish(10, "Earn a sticker", "Gagne un autocollant");
            Wish(11, "Open the {biome} album", "Ouvre l'album de {biome_the}");
            // SYS-26 Rare visitors
            Wish(12, "Say hi to {visitor}", "Dis bonjour à {visitor}");
            Wish(13, "Take a photo of a visitor", "Prends une photo d'un visiteur");
            // SYS-03 Cuddles and treats
            WishN(14, "Tap an animal", "Tap {n} animals", "Touche un animal", "Touche {n} animaux");
            WishN(15, "Give {animal} a cuddle", "Give {animal} {n} cuddles", "Fais un câlin à {animal}", "Fais {n} câlins à {animal}");
            Wish(16, "Give {animal} a treat", "Donne une friandise à {animal}");
            WishN(17, "Give a treat", "Give {n} treats", "Donne une friandise", "Donne {n} friandises");
            WishN(18, "Earn a bond point", "Earn {n} bond points", "Gagne un point de lien", "Gagne {n} points de lien");
            // SYS-09 Grooming
            Wish(19, "Groom {animal}", "Brosse {animal}");
            WishN(20, "Earn a star in Grooming", "Earn {n} stars in Grooming", "Gagne une étoile au Toilettage", "Gagne {n} étoiles au Toilettage");
            WishN(21, "Pick off a leaf or a twig", "Pick off {n} leaves and twigs", "Enlève une feuille ou une brindille",
                "Enlève {n} feuilles et brindilles");
            Wish(22, "Finish a grooming with a bow", "Finis un Toilettage avec un nœud");
            // SYS-27 Outfits
            Wish(23, "Dress up {animal}", "Mets une tenue à {animal}");
            WishN(24, "Change an animal's outfit", "Change outfits on {n} animals", "Change la tenue d'un animal", "Change la tenue de {n} animaux");
            // SYS-04 Upgrades. WISH-026 names the upgrade with its biome: one sentence per upgrade reads naturally in
            // both languages (« Améliore les Nids douillets de la Prairie »); the doc's sentence stays as the fallback.
            WishN(25, "Buy an upgrade", "Buy {n} upgrades", "Achète une amélioration", "Achète {n} améliorations");
            Wish(26, "Upgrade {upgrade} in {biome_the}", "Améliore {upgrade} dans {biome_the}");
            Plain(WishCatalog.TextKey(WishTemplateId.UpgradeInBiome) + ".production", "Upgrade {biome} Glow", "Améliore la Lumière de {biome_the}");
            Plain(WishCatalog.TextKey(WishTemplateId.UpgradeInBiome) + ".nests", "Upgrade Cozy Nests in {biome_the}",
                "Améliore les Nids douillets de {biome_the}");
            Plain(WishCatalog.TextKey(WishTemplateId.UpgradeInBiome) + ".curiosity", "Upgrade Curious Eye in {biome_the}",
                "Améliore le Regard curieux de {biome_the}");
            // SYS-05 Decoration
            Wish(27, "Place a decoration", "Place une décoration");
            WishN(28, "Place a decoration in {biome_the}", "Place {n} decorations in {biome_the}", "Place une décoration dans {biome_the}",
                "Place {n} décorations dans {biome_the}");
            Wish(29, "Move a decoration", "Déplace une décoration");
            // SYS-06 Expansions
            Wish(30, "Expand a biome", "Agrandis un biome");
            // SYS-07 Garden
            WishN(31, "Plant a seed", "Plant {n} seeds", "Plante une graine", "Plante {n} graines");
            WishN(32, "Harvest a plant", "Harvest {n} plants", "Récolte une plante", "Récolte {n} plantes");
            WishN(33, "Water a sprout", "Water {n} sprouts", "Arrose une pousse", "Arrose {n} pousses");
            WishN(34, "Harvest {n} {crop}", "Harvest {n} {crop}", "Récolte {n} {crop}", "Récolte {n} {crop}");
            WishN(35, "Bring {animal} {n} {crop}", "Bring {animal} {n} {crop}", "Apporte {n} {crop} à {animal}", "Apporte {n} {crop} à {animal}");
            Wish(36, "Harvest a dye", "Récolte une teinture");
            // SYS-08 Bakery
            Wish(37, "Bake a cake", "Prépare un gâteau");
            Wish(38, "Bake a {cake} for {animal}", "Prépare un {cake} pour {animal}");
            Wish(39, "Bake a cake with no mistakes", "Réussis un gâteau sans erreur");
            WishN(40, "Bake a cake", "Bake {n} cakes", "Prépare un gâteau", "Prépare {n} gâteaux");
            // SYS-13 Expeditions
            Wish(41, "Start an expedition", "Lance une expédition");
            Wish(42, "Start an expedition of 4 h or more", "Lance une expédition de 4 h ou plus");
            Wish(43, "Send {animal} on an expedition", "Envoie {animal} en expédition");
            Wish(44, "Send a team of 3 animals", "Envoie une équipe de 3 animaux");
            Wish(45, "Open a postcard", "Ouvre une carte postale");
            Wish(46, "Head to {destination}", "Pars vers {destination}");
            // SYS-16 Photo
            Wish(47, "Take a photo of {animal}", "Prends une photo de {animal}");
            WishN(48, "Take a Photo Safari shot", "Take {n} Photo Safari shots", "Réussis une photo au Safari", "Réussis {n} photos au Safari");
            WishN(49, "Photograph an animal", "Photograph {n} animals together", "Photographie un animal", "Photographie {n} animaux ensemble");
            Wish(50, "Photograph {animal} napping", "Photographie {animal} pendant sa sieste");
            // SYS-10 Hide and Seek
            Wish(51, "Play Hide and Seek", "Joue à Cache-cache");
            WishN(52, "Find a hidden animal", "Find {n} hidden animals", "Trouve un animal caché", "Trouve {n} animaux cachés");
            Wish(53, "Find every animal in one game", "Trouve tous les animaux d'une partie");
            // SYS-11 Nut Drop
            Wish(54, "Play Nut Drop", "Joue à Pluie de noisettes");
            WishN(55, "Catch a nut in one game", "Catch {n} nuts in one game", "Attrape une noisette en une partie", "Attrape {n} noisettes en une partie");
            WishN(56, "Get a {n} streak in Nut Drop", "Get a {n} streak in Nut Drop", "Fais une série de {n} à Pluie de noisettes",
                "Fais une série de {n} à Pluie de noisettes");
            // SYS-12 Pawprint Memory
            Wish(57, "Play Pawprint Memory", "Joue au Memory des empreintes");
            WishN(58, "Find a pair in Memory", "Find {n} pairs in Memory", "Trouve une paire au Memory", "Trouve {n} paires au Memory");
            WishN(59, "Finish Memory in {n} move", "Finish Memory in {n} moves or fewer", "Finis un Memory en {n} coup",
                "Finis un Memory en {n} coups ou moins");
            // SYS-14 Treasure Fishing
            Wish(60, "Play Treasure Fishing", "Joue à la Pêche aux trésors");
            WishN(61, "Reel in a treasure", "Reel in {n} treasures", "Remonte un trésor", "Remonte {n} trésors");
            Wish(62, "Reel in a pearl", "Remonte une perle");
            // SYS-15 Lily Pad Hop
            Wish(63, "Play Lily Pad Hop", "Joue au Saut de nénuphars");
            WishN(64, "Hop {n} meter in one game", "Hop {n} meters in one game", "Saute {n} mètre en une partie", "Saute {n} mètres en une partie");
            WishN(65, "Land a perfect hop", "Land {n} perfect hops", "Réussis un atterrissage parfait", "Réussis {n} atterrissages parfaits");
            // SYS-17 Cuddle Match
            WishN(66, "Clear a Cuddle Match level", "Clear {n} Cuddle Match levels", "Termine un niveau de Match des câlins",
                "Termine {n} niveaux de Match des câlins");
            WishN(67, "Free an animal from a bush", "Free {n} animals from bushes", "Libère un animal d'un buisson", "Libère {n} animaux des buissons");
            WishN(68, "Make a big hug", "Make {n} big hugs", "Crée un gros câlin", "Crée {n} gros câlins");
            // SYS-18 Glide
            Wish(69, "Play Glide", "Joue à la Glissade");
            WishN(70, "Glide {n} meter in one game", "Glide {n} meters in one game", "Glisse {n} mètre en une partie", "Glisse {n} mètres en une partie");
            WishN(71, "Collect a star in Glide", "Collect {n} stars in Glide", "Ramasse une étoile à la Glissade", "Ramasse {n} étoiles à la Glissade");
            // SYS-19 Constellations
            Wish(72, "Draw a constellation", "Dessine une constellation");
            Wish(73, "Draw the {species} constellation", "Dessine la constellation de {species_the}");
            // SYS-20 Firefly Rhythm
            Wish(74, "Play a Firefly Rhythm song", "Joue un morceau au Rythme des lucioles");
            WishN(75, "Hit a {n}-note streak", "Hit a {n}-note streak", "Fais une série de {n} note", "Fais une série de {n} notes");
            // SYS-21 Bubbles
            WishN(76, "Pop a bubble", "Pop {n} bubbles", "Éclate une bulle", "Éclate {n} bulles");
            WishN(77, "Pop a bubble group", "Pop a group of {n} bubbles", "Éclate un groupe de bulles", "Éclate un groupe de {n} bulles");
            // SYS-22 Tile Puzzle
            Wish(78, "Finish a Tile Puzzle", "Termine un Taquin");
            Wish(79, "Finish a 16-tile puzzle", "Termine un Taquin de 16 pièces");
            // SYS-23 Burrow Maze
            Wish(80, "Find the way out of the Burrow Maze", "Trouve la sortie du Labyrinthe");
            WishN(81, "Collect a seed in the Burrow Maze", "Collect {n} seeds in the Burrow Maze", "Ramasse une graine au Labyrinthe",
                "Ramasse {n} graines au Labyrinthe");
            // SYS-28 Grand Contest
            Wish(82, "Enter an animal in the Grand Contest", "Inscris un animal au Grand Concours");
            WishN(83, "Vote once in the Grand Contest", "Vote {n} times in the Grand Contest", "Vote une fois au Grand Concours",
                "Vote {n} fois au Grand Concours");
            // WISH-T01, the welcome's wish (A08): « Fais éclore 2 œufs ».
            Counted(WishCatalog.TextKey(WishTemplateId.Tutorial), "Hatch an egg", "Hatch {n} eggs", "Fais éclore un œuf", "Fais éclore {n} œufs");

            // ---------------- Journal goals (section 5.2), JRN-001 to JRN-052 ----------------
            GoalN(1, "Hatch an egg", "Hatch {n} eggs", "Fais éclore un œuf", "Fais éclore {n} œufs");
            GoalN(2, "Discover a variant", "Discover {n} variants", "Découvre une variante", "Découvre {n} variantes");
            GoalN(3, "Gain a mastery level", "Gain {n} mastery levels", "Gagne un niveau de maîtrise", "Gagne {n} niveaux de maîtrise");
            Goal(4, "Discover a new species", "Découvre une nouvelle espèce");
            GoalN(5, "Hatch a precious egg", "Hatch {n} precious eggs", "Fais éclore un œuf précieux", "Fais éclore {n} œufs précieux");
            GoalN(6, "Welcome a newborn", "Welcome {n} newborns", "Accueille un nouveau-né", "Accueille {n} nouveau-nés");
            GoalN(7, "Earn a sticker", "Earn {n} stickers", "Gagne un autocollant", "Gagne {n} autocollants");
            GoalN(8, "Say hi to a rare visitor", "Say hi to {n} rare visitors", "Dis bonjour à un visiteur rare", "Dis bonjour à {n} visiteurs rares");
            GoalN(9, "Earn a star in activities", "Earn {n} stars in activities", "Gagne une étoile en activités", "Gagne {n} étoiles en activités");
            GoalN(10, "Play a rewarded game", "Play {n} rewarded games", "Joue une partie récompensée", "Joue {n} parties récompensées");
            GoalN(11, "Beat a personal best", "Beat {n} personal bests", "Bats un record", "Bats {n} records");
            GoalN(12, "Clear an activity challenge", "Clear {n} activity challenges", "Réussis un défi d'activité", "Réussis {n} défis d'activité");
            GoalN(13, "Play an activity", "Play {n} different activities", "Joue à une activité", "Joue à {n} activités différentes");
            GoalN(14, "Get 3 stars once", "Get 3 stars {n} times", "Obtiens 3 étoiles une fois", "Obtiens 3 étoiles {n} fois");
            GoalN(15, "Clear a Cuddle Match level", "Clear {n} Cuddle Match levels", "Termine un niveau de Match des câlins",
                "Termine {n} niveaux de Match des câlins");
            GoalN(16, "Find an animal in Hide and Seek", "Find {n} animals in Hide and Seek", "Trouve un animal à Cache-cache",
                "Trouve {n} animaux à Cache-cache");
            GoalN(17, "Catch a nut", "Catch {n} nuts", "Attrape une noisette", "Attrape {n} noisettes");
            GoalN(18, "Reel in a treasure", "Reel in {n} treasures", "Remonte un trésor", "Remonte {n} trésors");
            GoalN(19, "Draw a constellation", "Draw {n} constellations", "Dessine une constellation", "Dessine {n} constellations");
            GoalN(20, "Play a Firefly Rhythm song", "Play {n} Firefly Rhythm songs", "Joue un morceau au Rythme des lucioles",
                "Joue {n} morceaux au Rythme des lucioles");
            GoalN(21, "Make a wish come true", "Make {n} wishes come true", "Réalise un souhait", "Réalise {n} souhaits");
            GoalN(22, "Open a daily gift", "Open {n} daily gifts", "Ouvre un cadeau du jour", "Ouvre {n} cadeaux du jour");
            GoalN(23, "Stamp a day on the calendar", "Stamp {n} days on the calendar", "Avance d'une case au calendrier", "Avance de {n} cases au calendrier");
            GoalN(24, "Complete an expedition", "Complete {n} expeditions", "Fais une expédition", "Fais {n} expéditions");
            GoalN(25, "Complete an expedition of 8 h or more", "Complete {n} expeditions of 8 h or more", "Fais une expédition de 8 h ou plus",
                "Fais {n} expéditions de 8 h ou plus");
            GoalN(26, "Bring back a postcard", "Bring back postcards from {n} places", "Rapporte une carte postale",
                "Rapporte des cartes postales de {n} destinations");
            GoalN(27, "Harvest a plant", "Harvest {n} plants", "Récolte une plante", "Récolte {n} plantes");
            GoalN(28, "Water a sprout", "Water {n} sprouts", "Arrose une pousse", "Arrose {n} pousses");
            GoalN(29, "Harvest a three-star plant", "Harvest {n} three-star plants", "Récolte une plante à 3 étoiles", "Récolte {n} plantes à 3 étoiles");
            GoalN(30, "Harvest a dye", "Harvest {n} dyes", "Récolte une teinture", "Récolte {n} teintures");
            GoalN(31, "Earn a bond point", "Earn {n} bond points", "Gagne un point de lien", "Gagne {n} points de lien");
            GoalN(32, "Give a treat", "Give {n} treats", "Donne une friandise", "Donne {n} friandises");
            GoalN(33, "Groom an animal", "Groom {n} animals", "Brosse un animal", "Brosse {n} animaux");
            GoalN(34, "Tap an animal", "Tap {n} animals", "Touche un animal", "Touche {n} animaux");
            GoalN(35, "Change an outfit", "Change {n} outfits", "Change une tenue", "Change {n} tenues");
            GoalN(36, "Buy an upgrade", "Buy {n} upgrades", "Achète une amélioration", "Achète {n} améliorations");
            GoalN(37, "Place a decoration", "Place {n} decorations", "Place une décoration", "Place {n} décorations");
            GoalN(38, "Expand a biome", "Expand your biomes {n} times", "Agrandis un biome", "Agrandis {n} fois tes biomes");
            GoalN(39, "Bake a cake", "Bake {n} cakes", "Prépare un gâteau", "Prépare {n} gâteaux");
            GoalN(40, "Give a cake", "Give {n} cakes", "Offre un gâteau", "Offre {n} gâteaux");
            GoalN(41, "Earn a garden point", "Earn {n} garden points", "Gagne un point de Jardin", "Gagne {n} points de Jardin");
            GoalN(42, "Take a photo", "Take {n} photos", "Prends une photo", "Prends {n} photos");
            GoalN(43, "Take a Photo Safari shot", "Take {n} Photo Safari shots", "Réussis une photo au Safari", "Réussis {n} photos au Safari");
            Goal(44, "Enter the Grand Contest", "Inscris un animal au Grand Concours");
            GoalN(45, "Vote once in the Grand Contest", "Vote {n} times in the Grand Contest", "Vote une fois au Grand Concours",
                "Vote {n} fois au Grand Concours");
            GoalN(46, "Earn an album star", "Earn {n} album stars", "Gagne une étoile d'album", "Gagne {n} étoiles d'album");
            GoalN(47, "Pop a bubble", "Pop {n} bubbles", "Éclate une bulle", "Éclate {n} bulles");
            GoalN(48, "Glide {n} meter", "Glide {n} meters", "Glisse {n} mètre", "Glisse {n} mètres");
            GoalN(49, "Collect a seed in the Burrow Maze", "Collect {n} seeds in the Burrow Maze", "Ramasse une graine au Labyrinthe",
                "Ramasse {n} graines au Labyrinthe");
            GoalN(50, "Finish a Tile Puzzle", "Finish {n} Tile Puzzles", "Termine un Taquin", "Termine {n} Taquins");
            GoalN(51, "Land a perfect hop", "Land {n} perfect hops", "Réussis un atterrissage parfait", "Réussis {n} atterrissages parfaits");
            GoalN(52, "Find a pair in Memory", "Find {n} pairs in Memory", "Trouve une paire au Memory", "Trouve {n} paires au Memory");

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
