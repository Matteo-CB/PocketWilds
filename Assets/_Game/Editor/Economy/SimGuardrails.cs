#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PocketWilds.Editor.Economy
{
    /// <summary>One guardrail's verdict: what the doc asks, what the simulation measured.</summary>
    public readonly struct SimCheck
    {
        public readonly string Id;
        public readonly string Rule;
        public readonly string Measured;
        public readonly bool Pass;

        public SimCheck(string id, string rule, string measured, bool pass)
        {
            Id = id;
            Rule = rule;
            Measured = measured;
            Pass = pass;
        }
    }

    /// <summary>
    /// The guardrails the simulator checks (doc 02 Améliorations, section 17; doc 02 Gemmes, sections 4 to 6; doc 02
    /// Formules, biome targets; doc 02 Renouveau, section 1.3). Each check names its source; the EditMode test fails on
    /// any check that does not pass.
    /// </summary>
    public static class SimGuardrails
    {
        /// <summary>
        /// Guardrails the docs' own numbers cannot reach, waiting for the owner's decision (id, then why, in French for the
        /// report; the decisions are numbered in Docs/Economy/T35C_Decisions.md). The test lets these fail and fails on any
        /// other; the report lists them with their measures. A decision taken, the values change and the ids leave the list.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> PendingOwnerDecision = BuildPending();

        private static Dictionary<string, string> BuildPending()
        {
            var pending = new Dictionary<string, string>();
            void Add(string reason, params string[] ids)
            {
                foreach (var id in ids)
                    pending[id] = reason;
            }

            Add("Décision 1 : les formules (œuf 10 × 1,18^n par type d'œuf, 1 Lumière par seconde et par animal, hors ligne à 100 % "
                + "pendant 24 h) donnent 3 à 10 fois plus d'éclosions que les bandes ; même les réglages les plus lents permis en donnent "
                + "112 le jour 1, et les premiers niveaux du refuge tombent dans la première minute.",
                "A17.hatches.day1", "A17.hatches.week1", "A17.egg.minutes.early", "A17.egg.minutes.late", "A17.refuge.4", "A17.refuge.9");
            Add("Décision 2 : le Renouveau remet les compteurs d'œufs à zéro (doc Renouveau, section 1.4) : des centaines d'éclosions "
                + "le jour d'un Renouveau ; sans cette remise, les œufs deviennent trop chers et les autres jours n'en ont presque plus.",
                "A17.hatches.later");
            Add("Décision 3 : agrandissements et décorations coûtent des multiples fixes de B, quand œufs et améliorations suivent la "
                + "courbe : moins de 0,1 % des dépenses après le jour 1 ; la part œufs et améliorations dépend du joueur (guidé ou optimiseur).",
                "A1.share.eggs", "A1.share.upgrades", "A1.share.zones", "A1.share.decorations");
            Add("Décision 4 : le niveau 15 (918 points) demande presque tout le contenu des trois premiers biomes, puis chaque porte "
                + "demande plus de points et de maîtrise de biome qu'un biome n'en apporte : les biomes, le Renouveau et la sixième zone "
                + "arrivent trop tard ou jamais.",
                "A17.biome.regular.mountain", "A17.biome.regular.snow", "A17.biome.regular.jungle", "A17.biome.regular.desert",
                "A17.biome.regular.sakura", "A17.biome.regular.mushroom", "A17.biome.regular.cloudlands", "A17.biome.regular.lunar",
                "A17.biome.regular.prehistoric", "A17.biome.regular.starlight", "A17.biome.casual.mountain", "A17.biome.casual.snow",
                "A17.biome.casual.jungle", "A17.zones.sixth", "A17.refuge.15", "R.first.day", "G5.renewal.casual", "R.rhythm");
            Add("Décision 5 : 20 à 40 Essence au premier Renouveau ; le cadeau de 20 et le bonus de complétion dépassent déjà 40, "
                + "le simulateur lit la part liée à la Lumière, qui dépend aussi du jour du premier Renouveau (décision 4).",
                "R.first.light");
            Add("Décision 6 : la maîtrise 30 demande environ 24 000 XP sur une espèce (20 × L^1,35, 2 XP par doublon), soit "
                + "12 000 doublons ; le régulier atteint la maîtrise 8 au jour 120.",
                "G5.target.regular.mastery30");
            Add("Décision 7 : au rythme d'éclosion des formules et avec les garanties de nouveauté, les Codex se complètent bien avant "
                + "les cibles, et l'écart du payeur se mesure sur des dates hors cibles (suit la décision 1).",
                "G5.target.regular.codex.grassland", "G5.target.casual.codex.grassland", "G5.target.regular.codex.softlaunch",
                "G5.target.casual.codex.softlaunch", "G5.target.club_pass_packs.codex.softlaunch", "G4.payer.speed");
            Add("Décision 8 : quand les portes se bloquent, plus rien de nouveau ne s'ouvre : des jours sans découverte ni objectif "
                + "(suit la décision 4).",
                "G5.discovery.early.casual", "G5.discovery.late.casual", "G5.goals.casual",
                "G5.discovery.early.regular", "G5.discovery.late.regular", "G5.goals.regular",
                "G5.discovery.early.passionate", "G5.discovery.late.passionate", "G5.goals.passionate",
                "G5.discovery.early.club", "G5.discovery.late.club", "G5.goals.club",
                "G5.discovery.early.club_pass_packs", "G5.discovery.late.club_pass_packs", "G5.goals.club_pass_packs",
                "G5.discovery.early.collector", "G5.discovery.late.collector", "G5.goals.collector");
            return pending;
        }

        /// <summary>Regular player's biome targets (day, or play minutes for the Forest), in opening order.</summary>
        public static readonly (string biome, double low, double high)[] RegularBiomes =
        {
            ("biome.lake", 2, 2), ("biome.mountain", 4, 5), ("biome.snow", 8, 8), ("biome.jungle", 12, 12), ("biome.desert", 16, 16),
            ("biome.sakura", 21, 21), ("biome.mushroom", 27, 27), ("biome.cloudlands", 34, 34), ("biome.lunar", 42, 42),
            ("biome.prehistoric", 51, 51), ("biome.starlight", 60, 60),
        };

        public static readonly (string biome, double low, double high)[] CasualBiomes =
        {
            ("biome.forest", 1, 2), ("biome.lake", 3, 4), ("biome.mountain", 6, 8), ("biome.snow", 12, 12), ("biome.jungle", 18, 18),
        };

        /// <summary>« Jamais avant »: Forest 30 min of play, Lake day 2, Mountain day 3, Snow day 6, Jungle day 9.</summary>
        public static readonly (string biome, double day)[] Floors =
        {
            ("biome.lake", 2), ("biome.mountain", 3), ("biome.snow", 6), ("biome.jungle", 9),
        };

        /// <summary>The report's number format (decimal comma), whatever the machine's culture.</summary>
        public static readonly CultureInfo FrenchNumbers = CultureInfo.GetCultureInfo("fr-FR");

        public static List<SimCheck> Evaluate(IReadOnlyList<SimRun> runs, double seconds)
        {
            var previous = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = FrenchNumbers;
            try
            {
                return EvaluateFrench(runs, seconds);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        private static List<SimCheck> EvaluateFrench(IReadOnlyList<SimRun> runs, double seconds)
        {
            var checks = new List<SimCheck>();
            var regular = runs.First(r => r.Profile.Kind == SimProfileKind.Regular);
            var casual = runs.First(r => r.Profile.Kind == SimProfileKind.Casual);

            // ---- Doc 02 Améliorations, section 17 ----
            Share(checks, regular, SpendKind.Eggs, 0.55, 0.65);
            Share(checks, regular, SpendKind.Upgrades, 0.20, 0.30);
            Share(checks, regular, SpendKind.Zones, 0.05, 0.10);
            Share(checks, regular, SpendKind.Gates, 0.0, 0.02);
            Share(checks, regular, SpendKind.Decorations, 0.03, 0.05);
            Share(checks, regular, SpendKind.Treats, 0.0, 0.01);

            var forest = regular.First("biome", "biome.forest");
            checks.Add(new SimCheck("A17.biome.regular.forest", "Régulier : Forêt après 30 à 60 min de jeu cumulé",
                forest == null ? "jamais" : $"{forest.Value.PlayMinutes:F0} min (jour {forest.Value.Day:F1})",
                forest != null && forest.Value.PlayMinutes >= 30 && forest.Value.PlayMinutes <= 60));
            foreach (var (biome, low, high) in RegularBiomes)
                Biome(checks, regular, biome, low, high);
            foreach (var (biome, low, high) in CasualBiomes)
                Biome(checks, casual, biome, low, high);
            foreach (var run in runs)
            {
                var f = run.First("biome", "biome.forest");
                checks.Add(new SimCheck($"F.floor.{run.Profile.Id}.forest", $"{run.Profile.Label} : jamais la Forêt avant 30 min de jeu",
                    f == null ? "jamais" : $"{f.Value.PlayMinutes:F0} min", f == null || f.Value.PlayMinutes >= 30));
                foreach (var (biome, day) in Floors)
                {
                    var m = run.First("biome", biome);
                    checks.Add(new SimCheck($"F.floor.{run.Profile.Id}.{Short(biome)}", $"{run.Profile.Label} : {French(biome)} jamais avant le jour {day:F0}",
                        m == null ? "jamais" : $"jour {m.Value.Day:F1}", m == null || Math.Floor(m.Value.Day) >= day));
                }
            }

            double early = 0.0, late = 0.0;
            foreach (var d in regular.Days)
            {
                if (d.Day <= 7)
                    early = Math.Max(early, d.EggCostMaxMinutes);
                else
                    late = Math.Max(late, d.EggCostMaxMinutes);
            }
            checks.Add(new SimCheck("A17.egg.minutes.early", "Régulier, jours 1 à 7 : un œuf ne coûte jamais plus de 15 min de production",
                $"{early:F1} min au plus", early <= 15.0));
            checks.Add(new SimCheck("A17.egg.minutes.late", "Régulier, ensuite : jamais plus de 45 min", $"{late:F1} min au plus", late <= 45.0));

            var day1 = regular.Days[0].Hatches;
            int lowWeek = regular.Days.Skip(1).Take(6).Min(d => d.Hatches), highWeek = regular.Days.Skip(1).Take(6).Max(d => d.Hatches);
            int lowLate = regular.Days.Skip(7).Min(d => d.Hatches), highLate = regular.Days.Skip(7).Max(d => d.Hatches);
            checks.Add(new SimCheck("A17.hatches.day1", "Régulier : 40 à 80 éclosions le jour 1", day1.ToString(), day1 >= 40 && day1 <= 80));
            checks.Add(new SimCheck("A17.hatches.week1", "Régulier : 30 à 60 éclosions par jour jusqu'au jour 7", $"{lowWeek} à {highWeek}",
                lowWeek >= 30 && highWeek <= 60));
            checks.Add(new SimCheck("A17.hatches.later", "Régulier : 20 à 50 éclosions par jour ensuite", $"{lowLate} à {highLate}",
                lowLate >= 20 && highLate <= 50));

            var zone2 = regular.First("zone", "zone.grassland.streambend");
            var zone6 = regular.First("zone", "zone.grassland.pondedge");
            checks.Add(new SimCheck("A17.zones.hour", "Régulier : deux zones de la Prairie dans la première heure",
                zone2 == null ? "pas encore" : $"{zone2.Value.PlayMinutes:F0} min", zone2 != null && zone2.Value.PlayMinutes <= 60));
            checks.Add(new SimCheck("A17.zones.sixth", "Régulier : la sixième entre les jours 5 et 10",
                zone6 == null ? "pas encore" : $"jour {zone6.Value.Day:F1}", zone6 != null && zone6.Value.Day >= 5 && zone6.Value.Day < 11));

            var level4 = regular.First("refuge", "4");
            var level9 = regular.First("refuge", "9");
            var level15 = regular.First("refuge", "15");
            checks.Add(new SimCheck("A17.refuge.4", "Régulier : niveau 4 entre 30 et 60 min", level4 == null ? "jamais" : $"{level4.Value.PlayMinutes:F0} min",
                level4 != null && level4.Value.PlayMinutes >= 30 && level4.Value.PlayMinutes <= 60));
            checks.Add(new SimCheck("A17.refuge.9", "Régulier : niveau 9 au jour 2", level9 == null ? "jamais" : $"jour {level9.Value.Day:F1}",
                level9 != null && Math.Floor(level9.Value.Day) == 2));
            checks.Add(new SimCheck("A17.refuge.15", "Régulier : niveau 15 entre les jours 4 et 5", level15 == null ? "jamais" : $"jour {level15.Value.Day:F1}",
                level15 != null && level15.Value.Day >= 4 && level15.Value.Day < 6));

            // ---- Doc 02 Renouveau, section 1.3 ----
            var first = regular.First("renewal", "1");
            checks.Add(new SimCheck("R.first.day", "Régulier : premier Renouveau entre les jours 5 et 7",
                first == null ? "jamais" : $"jour {first.Value.Day:F1}", first != null && first.Value.Day >= 5 && first.Value.Day < 8));
            // The 20 to 40 Essence is RenewalScale's target, so the Light-linked part: the 20 offered and the completion bonus
            // (24 species and 3 biomes by then, 39 Essence) come on top (see the report, owner decisions).
            var light = regular.First("renewal.light", "1");
            checks.Add(new SimCheck("R.first.light", "Régulier : 20 à 40 Essence liée à la Lumière au premier Renouveau",
                light == null ? "jamais" : $"{light.Value.Value} Essence (en tout {first?.Value ?? 0})",
                light != null && light.Value.Value >= 20 && light.Value.Value <= 40));
            var casualFirst = casual.First("renewal", "1");
            checks.Add(new SimCheck("G5.renewal.casual", "Occasionnel : premier Renouveau entre les jours 8 et 10",
                casualFirst == null ? "jamais" : $"jour {casualFirst.Value.Day:F1}",
                casualFirst != null && casualFirst.Value.Day >= 8 && casualFirst.Value.Day < 11));
            var renewals = regular.Milestones.Where(m => m.Kind == "renewal").Select(m => m.Day).ToList();
            double maxGap = 0.0, minGap = double.MaxValue;
            for (int i = 1; i < renewals.Count; i++)
            {
                maxGap = Math.Max(maxGap, renewals[i] - renewals[i - 1]);
                minGap = Math.Min(minGap, renewals[i] - renewals[i - 1]);
            }
            checks.Add(new SimCheck("R.rhythm", "Régulier : ensuite un Renouveau environ tous les 4 à 7 jours",
                renewals.Count < 2 ? $"{renewals.Count} Renouveau" : $"{renewals.Count} Renouveaux, écarts de {minGap:F1} à {maxGap:F1} jours",
                renewals.Count >= 2 && minGap >= 3.0 && maxGap <= 8.0));

            // ---- Doc 02 Gemmes, section 5: progression targets ----
            Target(checks, regular, "codex.grassland", "Régulier : Codex Prairie complet (variantes de base) entre les jours 12 et 16", 12, 16);
            Target(checks, casual, "codex.grassland", "Occasionnel : Codex Prairie complet entre les jours 18 et 25", 18, 25);
            Target(checks, regular, "codex.softlaunch", "Régulier : variantes de base des 24 espèces du lancement entre les jours 100 et 120", 100, 120,
                allowLater: false);
            Target(checks, casual, "codex.softlaunch", "Occasionnel : variantes de base des 24 espèces au jour 150 ou plus", 150, double.MaxValue,
                allowLater: true);
            var payer = runs.FirstOrDefault(r => r.Profile.Kind == SimProfileKind.ClubPassPacks);
            if (payer != null)
                Target(checks, payer, "codex.softlaunch", "Club + Pass : variantes de base des 24 espèces entre les jours 80 et 95", 80, 95);
            Target(checks, regular, "mastery30", "Régulier : maîtrise 30 sur une espèce entre les jours 35 et 45", 35, 45);
            Target(checks, casual, "mastery30", "Occasionnel : maîtrise 30 au jour 60 ou plus", 60, double.MaxValue, allowLater: true);

            // ---- Doc 02 Gemmes, section 5: frustration guardrails ----
            foreach (var run in runs)
            {
                Gap(checks, run, 1, 30, 1, "nouvelle découverte", d => d.Discoveries > 0, "G5.discovery.early");
                Gap(checks, run, 31, 120, 3, "nouvelle découverte", d => d.Discoveries > 0, "G5.discovery.late");
                Gap(checks, run, 1, 120, 2, "objectif atteint", d => d.Goals > 0, "G5.goals");
            }

            // ---- Doc 02 Gemmes, sections 4 and 6 ----
            if (regular.Days.Count >= 30)
            {
                long gems30 = regular.Days[29].Gems;
                checks.Add(new SimCheck("G6.unspent", "Régulier gratuit : Gemmes non dépensées sous 1 500 au jour 30", gems30.ToString(), gems30 < 1500));
            }
            var club = runs.FirstOrDefault(r => r.Profile.Kind == SimProfileKind.Club);
            if (club != null)
            {
                double ratio = (double)club.Days.Sum(d => d.GemsEarned) / Math.Max(1, regular.Days.Sum(d => d.GemsEarned));
                checks.Add(new SimCheck("G4.club", "Club : environ deux fois les Gemmes d'un régulier gratuit (1,6 à 2,5)", $"× {ratio:F2}",
                    ratio >= 1.6 && ratio <= 2.5));
            }
            if (payer != null)
            {
                var r = Reached(regular, "codex.softlaunch");
                var p = Reached(payer, "codex.softlaunch");
                double speed = r.HasValue && p.HasValue ? 1.0 - p.Value / r.Value : double.NaN;
                checks.Add(new SimCheck("G4.payer.speed", "Club + Pass : collection 20 à 25 % plus rapide, jamais plus",
                    double.IsNaN(speed) ? "non mesurable" : $"{speed * 100:F0} %", !double.IsNaN(speed) && speed <= 0.25));
            }

            checks.Add(new SimCheck("G7.time", "6 profils sur 120 jours en moins de 10 s", $"{seconds:F2} s", seconds < 10.0));
            return checks;
        }

        private static void Share(List<SimCheck> checks, SimRun run, SpendKind kind, double low, double high)
        {
            double share = run.SpendShare30(kind);
            checks.Add(new SimCheck($"A1.share.{kind.ToString().ToLowerInvariant()}",
                $"Régulier, jours 1 à 30 : {Name(kind)} entre {low * 100:F0} et {high * 100:F0} % des dépenses de Lumière",
                $"{share * 100:F1} %", share >= low && share <= high));
        }

        private static void Biome(List<SimCheck> checks, SimRun run, string biome, double low, double high)
        {
            var m = run.First("biome", biome);
            // Tolerance (doc 02 Améliorations, section 17): ± 1 day before day 10, ± 20 % after.
            double from = low < 10 ? low - 1 : low * 0.8;
            double to = high < 10 ? high + 1 : high * 1.2;
            checks.Add(new SimCheck($"A17.biome.{run.Profile.Id}.{Short(biome)}",
                $"{run.Profile.Label} : {French(biome)} au jour {(low == high ? low.ToString("F0") : low.ToString("F0") + " à " + high.ToString("F0"))} ({from:F1} à {to:F1})",
                m == null ? "jamais" : $"jour {m.Value.Day:F1}", m != null && m.Value.Day >= from && m.Value.Day < to + 1));
        }

        private static void Target(List<SimCheck> checks, SimRun run, string what, string rule, double low, double high, bool allowLater = false)
        {
            var day = Reached(run, what);
            bool pass = day.HasValue ? day.Value >= low && day.Value <= high : allowLater;
            checks.Add(new SimCheck($"G5.target.{run.Profile.Id}.{what}", rule, day.HasValue ? $"jour {day.Value:F0}" : "pas dans les 120 jours", pass));
        }

        /// <summary>First day a target is reached: a Codex complete (base colors), mastery 30.</summary>
        public static double? Reached(SimRun run, string what)
        {
            var m = run.First("target", what);
            return m?.Day;
        }

        private static void Gap(List<SimCheck> checks, SimRun run, int from, int to, int maxDays, string thing, Func<SimDay, bool> hit, string id)
        {
            int worst = 0, current = 0, worstEnd = 0;
            foreach (var d in run.Days)
            {
                if (d.Day < from || d.Day > to)
                    continue;
                if (hit(d))
                {
                    current = 0;
                    continue;
                }
                current++;
                if (current > worst)
                {
                    worst = current;
                    worstEnd = d.Day;
                }
            }
            checks.Add(new SimCheck($"{id}.{run.Profile.Id}", $"{run.Profile.Label}, jours {from} à {to} : jamais plus de {maxDays} jour(s) sans {thing}",
                worst == 0 ? "aucun jour sans" : $"{worst} jour(s) de suite (jusqu'au jour {worstEnd})", worst <= maxDays));
        }

        private static string Name(SpendKind kind)
        {
            switch (kind)
            {
                case SpendKind.Eggs: return "les œufs";
                case SpendKind.Upgrades: return "les améliorations";
                case SpendKind.Zones: return "les agrandissements";
                case SpendKind.Gates: return "l'ouverture des biomes";
                case SpendKind.Decorations: return "les décorations communes";
                default: return "l'épicerie";
            }
        }

        public static string Short(string biome) => biome.StartsWith("biome.", StringComparison.Ordinal) ? biome.Substring(6) : biome;

        /// <summary>The biome's French name, for the report.</summary>
        public static string French(string biome)
        {
            switch (Short(biome))
            {
                case "grassland": return "Prairie";
                case "forest": return "Forêt";
                case "lake": return "Lac";
                case "mountain": return "Montagne";
                case "snow": return "Neige";
                case "jungle": return "Jungle";
                case "desert": return "Désert";
                case "sakura": return "Sakura";
                case "mushroom": return "Champignons";
                case "cloudlands": return "Îles des nuages";
                case "lunar": return "Lunaire";
                case "prehistoric": return "Préhistorique";
                case "starlight": return "Ciel étoilé";
                default: return Short(biome);
            }
        }
    }
}
