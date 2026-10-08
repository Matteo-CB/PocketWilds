#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using PocketWilds.Content;
using UnityEditor;
using UnityEngine;

namespace PocketWilds.Editor.Economy
{
    /// <summary>
    /// The simulator's outputs (doc 02 Gemmes, section 7): one CSV row per day and profile, and a Markdown report with
    /// every guardrail, in Docs/Economy. Menu: Pocket Wilds/Economy/Run Simulator (T35C).
    /// </summary>
    public static class SimReport
    {
        public const string Folder = "Docs/Economy";
        public const string CsvName = "T35C_Simulation.csv";
        public const string ReportName = "T35C_Simulation.md";
        public const string DecisionsName = "T35C_Decisions.md";
        private const string DatabasePath = "Assets/_Game/Content/DEF_ContentDatabase.asset";

        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        [MenuItem("Pocket Wilds/Economy/Run Simulator (T35C)", priority = 700)]
        public static void RunMenu() => UnityEngine.Debug.Log("[EconomySimulator] " + RunAndWrite());

        public static ContentRegistry LoadShipped()
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(DatabasePath);
            if (database == null)
                throw new FileNotFoundException("Content database missing.", DatabasePath);
            return database.CreateBuilder().Build();
        }

        /// <summary>Runs the 6 profiles, checks the guardrails, writes the CSV and the report; returns a one-line summary.</summary>
        public static string RunAndWrite()
        {
            var shipped = LoadShipped();
            var watch = Stopwatch.StartNew();
            var runs = EconomySimulator.RunAll(shipped);
            double seconds = watch.Elapsed.TotalSeconds;
            var checks = SimGuardrails.Evaluate(runs, seconds);
            var optimizer = RunOptimizer(shipped);
            Write(runs, checks, seconds, optimizer);
            int pass = checks.Count(c => c.Pass);
            int pending = checks.Count(c => !c.Pass && SimGuardrails.PendingOwnerDecision.ContainsKey(c.Id));
            return $"{runs.Count} profiles × {EconomySimulator.DefaultDays} days in {seconds:F2} s: {pass}/{checks.Count} guardrails pass, "
                + $"{pending} wait for the owner, {checks.Count - pass - pending} out of bounds";
        }

        /// <summary>The regular player who buys whatever pays back fastest: the report's sensitivity check.</summary>
        public static SimRun RunOptimizer(ContentRegistry shipped) =>
            EconomySimulator.Run(shipped, SimProfile.All()[(int)SimProfileKind.Regular], EconomySimulator.DefaultDays, null, SimPolicy.Optimizer);

        public static void Write(IReadOnlyList<SimRun> runs, IReadOnlyList<SimCheck> checks, double seconds, SimRun? optimizer = null)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string folder = Path.Combine(root, Folder);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, CsvName), Csv(runs), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(folder, ReportName), Markdown(runs, checks, seconds, optimizer), new UTF8Encoding(false));
        }

        public static string Csv(IReadOnlyList<SimRun> runs)
        {
            var sb = new StringBuilder(64 * 1024);
            sb.Append("profile,day,play_minutes,light_log10,rate_log10,lifetime_log10,hatches,new_species,new_variants,mastery_variants,")
              .Append("event_objects,pass_variants,discoveries,biomes,zones,refuge_level,refuge_score,biome_mastery_max,species_mastery_max,")
              .Append("gems_earned,gems_spent,gems,essence,renewals,goals,egg_cost_max_minutes,base_variants_found,next_gate\n");
            foreach (var run in runs)
            {
                foreach (var d in run.Days)
                {
                    sb.Append(run.Profile.Id).Append(',').Append(d.Day).Append(',')
                      .Append(F(d.PlayMinutes, 1)).Append(',').Append(F(d.LightLog10, 2)).Append(',').Append(F(d.RateLog10, 2)).Append(',')
                      .Append(F(d.LifetimeLog10, 2)).Append(',').Append(d.Hatches).Append(',').Append(d.NewSpecies).Append(',')
                      .Append(d.NewVariants).Append(',').Append(d.MasteryVariants).Append(',').Append(d.EventObjects).Append(',')
                      .Append(d.PassVariants).Append(',').Append(d.Discoveries).Append(',').Append(d.Biomes).Append(',').Append(d.Zones).Append(',')
                      .Append(d.RefugeLevel).Append(',').Append(d.RefugeScore).Append(',').Append(d.BiomeMastery).Append(',').Append(d.MaxSpeciesMastery).Append(',')
                      .Append(d.GemsEarned).Append(',').Append(d.GemsSpent).Append(',').Append(d.Gems).Append(',').Append(d.Essence).Append(',')
                      .Append(d.Renewals).Append(',').Append(d.Goals).Append(',').Append(F(d.EggCostMaxMinutes, 1)).Append(',')
                      .Append(d.BaseVariantsFound).Append(',').Append(d.NextGate.Replace(',', ';')).Append('\n');
                }
            }
            return sb.ToString();
        }

        public static string Markdown(IReadOnlyList<SimRun> runs, IReadOnlyList<SimCheck> checks, double seconds, SimRun? optimizer = null)
        {
            var previous = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = SimGuardrails.FrenchNumbers;
            _numbers = SimGuardrails.FrenchNumbers;
            try
            {
                return MarkdownFrench(runs, checks, seconds, optimizer);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
                _numbers = Invariant;
            }
        }

        private static string MarkdownFrench(IReadOnlyList<SimRun> runs, IReadOnlyList<SimCheck> checks, double seconds, SimRun? optimizer)
        {
            var sb = new StringBuilder(48 * 1024);
            int pass = checks.Count(c => c.Pass);
            int pending = checks.Count(c => !c.Pass && SimGuardrails.PendingOwnerDecision.ContainsKey(c.Id));
            int outOfBounds = checks.Count - pass - pending;
            sb.Append("# T35C : simulation de l'économie\n\n");
            sb.Append("Rapport écrit par le simulateur (menu Pocket Wilds/Economy/Run Simulator, test EconomySimulatorTests), avec les valeurs ")
              .Append("des docs telles que le jeu les a. ")
              .Append($"{runs.Count} profils sur {EconomySimulator.DefaultDays} jours en {F(seconds, 2)} s. ")
              .Append($"Garde-fous : {pass} respectés sur {checks.Count}, {pending} en attente d'une décision du propriétaire, {outOfBounds} hors bornes.\n\n");
            sb.Append("Analyse, contradictions entre les docs et décisions à prendre : `").Append(DecisionsName).Append("`. ")
              .Append("Données jour par jour : `").Append(CsvName).Append("` (même dossier).\n\n");

            sb.Append("## Garde-fous\n\n| Règle | Mesuré | Résultat |\n| --- | --- | --- |\n");
            foreach (var c in checks)
            {
                string verdict = c.Pass ? "respecté" : SimGuardrails.PendingOwnerDecision.ContainsKey(c.Id) ? "en attente de décision" : "**hors bornes**";
                sb.Append("| ").Append(c.Rule).Append(" | ").Append(c.Measured).Append(" | ").Append(verdict).Append(" |\n");
            }

            var stale = checks.Where(c => c.Pass && SimGuardrails.PendingOwnerDecision.ContainsKey(c.Id)).Select(c => c.Id).ToList();
            if (stale.Count > 0)
                sb.Append("\nEn attente de décision mais respectés aujourd'hui (à retirer de la liste) : ").Append(string.Join(", ", stale)).Append(".\n");

            sb.Append("\n## Décisions en attente\n\n");
            foreach (var group in SimGuardrails.PendingOwnerDecision.GroupBy(p => p.Value))
            {
                int open = group.Count(p => checks.Any(c => c.Id == p.Key && !c.Pass));
                sb.Append("- ").Append(group.Key).Append(" (").Append(open).Append(open > 1 ? " garde-fous hors bornes)\n" : " garde-fou hors bornes)\n");
            }

            sb.Append("\n## Profils\n\n| Profil | Jeu par jour (j. 2 à 120) | Éclosions | Découvertes | Biomes au jour 30 / 120 | Niveau du refuge au jour 30 / 120 | Renouveaux | Gemmes gagnées / dépensées |\n")
              .Append("| --- | --- | --- | --- | --- | --- | --- | --- |\n");
            foreach (var run in runs)
            {
                var days = run.Days;
                double play = days.Skip(1).Average(d => d.PlayMinutes);
                var d30 = days[Math.Min(29, days.Count - 1)];
                var last = days[days.Count - 1];
                sb.Append("| ").Append(run.Profile.Label).Append(" | ").Append(F(play, 0)).Append(" min | ").Append(days.Sum(d => d.Hatches))
                  .Append(" | ").Append(days.Sum(d => d.Discoveries)).Append(" | ").Append(d30.Biomes).Append(" / ").Append(last.Biomes)
                  .Append(" | ").Append(d30.RefugeLevel).Append(" / ").Append(last.RefugeLevel).Append(" | ").Append(days.Sum(d => d.Renewals))
                  .Append(" | ").Append(days.Sum(d => d.GemsEarned)).Append(" / ").Append(days.Sum(d => d.GemsSpent)).Append(" |\n");
            }

            sb.Append("\n## Ouverture des biomes (jour, minutes de jeu cumulées)\n\n| Biome |");
            foreach (var run in runs)
                sb.Append(' ').Append(run.Profile.Label).Append(" |");
            sb.Append("\n| --- |");
            foreach (var _ in runs)
                sb.Append(" --- |");
            sb.Append('\n');
            var biomes = new ProgressionConfigProbe().Biomes;
            foreach (var biome in biomes)
            {
                sb.Append("| ").Append(SimGuardrails.French(biome)).Append(" |");
                foreach (var run in runs)
                {
                    var m = run.First("biome", biome);
                    sb.Append(' ').Append(m == null ? "pas dans les 120 jours" : $"j. {F(m.Value.Day, 1)} ({F(m.Value.PlayMinutes, 0)} min)").Append(" |");
                }
                sb.Append('\n');
            }

            var regular = runs.FirstOrDefault(r => r.Profile.Kind == SimProfileKind.Regular);
            if (regular != null)
            {
                sb.Append("\n## Journal du joueur régulier, jours 1 à 30\n\n")
                  .Append("Ce qui retient la porte du biome suivant à la fin de chaque jour (niveau du refuge, espèces du biome précédent, maîtrise, ")
                  .Append("jour plancher, Lumière en pourcentage du prix).\n\n")
                  .Append("| Jour | Éclosions | Découvertes | Niveau (score) | Biomes | Renouveau | Œuf le plus cher (min de production) | Porte suivante |\n")
                  .Append("| --- | --- | --- | --- | --- | --- | --- | --- |\n");
                foreach (var d in regular.Days.Take(30))
                {
                    sb.Append("| ").Append(d.Day).Append(" | ").Append(d.Hatches).Append(" | ").Append(d.Discoveries).Append(" | ")
                      .Append(d.RefugeLevel).Append(" (").Append(d.RefugeScore).Append(") | ").Append(d.Biomes).Append(" | ")
                      .Append(d.Renewals > 0 ? "oui" : "").Append(" | ").Append(F(d.EggCostMaxMinutes, 0)).Append(" | ")
                      .Append(string.IsNullOrEmpty(d.NextGate) ? "tout est ouvert" : d.NextGate).Append(" |\n");
                }
            }

            sb.Append("\n## Dépenses de Lumière, jours 1 à 30\n\n| Profil | Œufs | Améliorations | Zones | Biomes | Décorations | Épicerie |\n| --- | --- | --- | --- | --- | --- | --- |\n");
            foreach (var run in runs)
                SpendRow(sb, run.Profile.Label, run);
            if (optimizer != null)
                SpendRow(sb, "Régulier, joueur optimiseur", optimizer);

            if (optimizer != null && regular != null)
            {
                sb.Append("\n## Sensibilité : le joueur optimiseur\n\n")
                  .Append("Même profil régulier, mais chaque achat est celui qui se rembourse le plus vite (sans suivre les conseils du jeu). ")
                  .Append("L'écart avec le joueur guidé montre ce qui dépend du comportement du joueur et ce qui dépend des formules.\n\n")
                  .Append("| Mesure | Joueur guidé | Joueur optimiseur |\n| --- | --- | --- |\n");
                Compare(sb, "Éclosions le jour 1", regular.Days[0].Hatches.ToString(), optimizer.Days[0].Hatches.ToString());
                Compare(sb, "Éclosions sur 120 jours", regular.Days.Sum(d => d.Hatches).ToString(), optimizer.Days.Sum(d => d.Hatches).ToString());
                Compare(sb, "Œufs, part des dépenses (j. 1 à 30)", F(regular.SpendShare30(SpendKind.Eggs) * 100, 1) + " %", F(optimizer.SpendShare30(SpendKind.Eggs) * 100, 1) + " %");
                Compare(sb, "Améliorations, part des dépenses", F(regular.SpendShare30(SpendKind.Upgrades) * 100, 1) + " %", F(optimizer.SpendShare30(SpendKind.Upgrades) * 100, 1) + " %");
                foreach (var biome in new[] { "biome.mountain", "biome.snow", "biome.jungle", "biome.desert" })
                    Compare(sb, "Ouverture : " + SimGuardrails.French(biome), Day(regular.First("biome", biome)), Day(optimizer.First("biome", biome)));
                Compare(sb, "Premier Renouveau", Day(regular.First("renewal", "1")), Day(optimizer.First("renewal", "1")));
                Compare(sb, "Niveau du refuge au jour 120", regular.Days[regular.Days.Count - 1].RefugeLevel.ToString(), optimizer.Days[optimizer.Days.Count - 1].RefugeLevel.ToString());
            }

            sb.Append("\n## Renouveaux\n\n| Profil | Jour et Essence de chaque Renouveau (dont liée à la Lumière) |\n| --- | --- |\n");
            foreach (var run in runs)
            {
                var light = run.Milestones.Where(m => m.Kind == "renewal.light").ToDictionary(m => m.What, m => m.Value);
                var list = run.Milestones.Where(m => m.Kind == "renewal")
                    .Select(m => $"j. {F(m.Day, 1)} ({m.Value}, dont {(light.TryGetValue(m.What, out long l) ? l : 0)})").ToList();
                sb.Append("| ").Append(run.Profile.Label).Append(" | ").Append(list.Count == 0 ? "aucun" : string.Join(" ; ", list)).Append(" |\n");
            }

            sb.Append("\n## Ce que le simulateur suppose\n\n");
            sb.Append("- Contenu : les définitions livrées (Prairie : 3 espèces, 18 variantes, œufs Basic, Focus et Découverte), complétées par le roster prévu ")
              .Append("(6 espèces par biome, 6 variantes de base, 13 biomes, zones de la section 4.2). Les espèces prévues ne sont jamais livrées.\n");
            sb.Append("- Jours : le jour calendaire des docs (le jour de l'installation est le jour 1, installation à midi) ; 1,5 veut dire midi du jour 1.\n");
            sb.Append("- Joueur guidé, le même pour tous les profils : il éclot surtout ; il prend l'œuf qui cache encore une nouveauté (le moins cher), ")
              .Append("sinon celui qui produit le plus pour son prix ; une amélioration quand le HUD la conseille (au moins 5 % de production totale, ")
              .Append("ou le palier × 2 que la Lumière atteint) ; une zone dès que son panneau s'allume ; Nids douillets et Regard curieux quand ils ")
              .Append("coûtent moins de 10 et 30 minutes de production ; quand l'œuf voulu coûte plus de 10 minutes de production, il ouvre l'écran ")
              .Append("Améliorer et prend la carte « Conseillé » (le plus court temps de retour). Il garde sa Lumière pour une porte de biome dont ")
              .Append("les autres conditions sont remplies, achète les décorations communes et les friandises bon marché, éclot les œufs Découverte ")
              .Append("tout de suite, par 1, 3 ou 10 selon le niveau du refuge.\n");
            sb.Append("- Renouveau : le premier dès que le bouton s'active, les suivants quand ils ajoutent au moins la moitié de l'Essence déjà gagnée ")
              .Append("(« rentable ») ; Essence au nœud ouvert le moins cher, branche Économie d'abord.\n");
            sb.Append("- Rendez-vous : cadeau du jour (table et garantie), calendrier de 28 cases, souhaits, carnet de la semaine, activités (minutes de production), ")
              .Append("expéditions (minutes de production, œufs Découverte et garantie), un événement par mois (7 objets), Pass de 30 jours.\n");
            sb.Append("- Systèmes prévus mais pas encore construits, comptés comme leurs docs le disent : traits aux taux du doc Formules, visiteurs rares, ")
              .Append("autocollants et pages d'album, décorations posées, variantes de maîtrise (niveaux 10, 20, 30), objets d'événement et couleurs du Pass ")
              .Append("(3 points chacun), souhaits (maîtrise du biome).\n");
            sb.Append("- Pas encore simulés : la reproduction (T56) et les 5 % de Lumière du compagnon de pack.\n");
            sb.Append("- La clé econ.egg.growth du doc Améliorations (section 15) n'est pas encore lue par le jeu : chaque œuf garde son costGrowth (1,18). ")
              .Append("Le simulateur l'applique à tous les œufs pour les essais.\n");
            return sb.ToString();
        }

        private static void SpendRow(StringBuilder sb, string label, SimRun run)
        {
            sb.Append("| ").Append(label);
            foreach (SpendKind kind in Enum.GetValues(typeof(SpendKind)))
                sb.Append(" | ").Append(F(run.SpendShare30(kind) * 100.0, 1)).Append(" %");
            sb.Append(" |\n");
        }

        private static void Compare(StringBuilder sb, string what, string guided, string optimizer) =>
            sb.Append("| ").Append(what).Append(" | ").Append(guided).Append(" | ").Append(optimizer).Append(" |\n");

        private static string Day(SimMilestone? m) => m == null ? "pas dans les 120 jours" : "jour " + F(m.Value.Day, 1);

        // Decimal point in the CSV, decimal comma in the report.
        private static CultureInfo _numbers = Invariant;

        private static string F(double value, int decimals) => value.ToString("F" + decimals, _numbers);

        /// <summary>The 13 biomes' source ids in opening order.</summary>
        private sealed class ProgressionConfigProbe
        {
            public readonly List<string> Biomes = new Simulation.ProgressionConfig().Biomes.Select(b => b.SourceId).ToList();
        }
    }
}
