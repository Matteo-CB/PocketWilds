using System;
using PocketWilds.Core;
using PocketWilds.Simulation;

namespace PocketWilds.Presentation
{
    /// <summary>The activity texts the presenter builds (and the capture tools reuse): what opens an activity.</summary>
    public static class ActivityTextFormatter
    {
        /// <summary>What opens an activity, the conditions still missing first (doc 01, section 0): « Niveau du refuge 10 et Jour 4 ».</summary>
        public static string UnlockHint(ILocalization text, ActivityId activity, in ActivityUnlockInput input)
        {
            if (!ActivityCatalog.TryGet(activity, out var d))
                return text.Get(ActivityText.UnlockSoon);
            string hint = null;
            switch (d.Special)
            {
                case ActivitySpecialUnlock.Lake:
                    if (!input.LakeUnlocked)
                        hint = And(text, hint, text.Get(ActivityText.UnlockLake));
                    break;
                case ActivitySpecialUnlock.Mountain:
                    if (!input.MountainUnlocked)
                        hint = And(text, hint, text.Get(ActivityText.UnlockMountain));
                    break;
                case ActivitySpecialUnlock.Axolotl:
                    if (!input.AxolotlDiscovered)
                        hint = And(text, hint, text.Get(ActivityText.UnlockAxolotl));
                    break;
                case ActivitySpecialUnlock.GardenLevel3:
                    if (input.GardenLevel < 3)
                        hint = And(text, hint, Fill(text.Get(ActivityText.UnlockGardenLevel), "{n}", 3));
                    break;
                case ActivitySpecialUnlock.NightVisitorOrDay10:
                    if (!input.NightVisitorSeen && input.DayNumber < 10)
                        hint = And(text, hint, text.Get(ActivityText.UnlockOr).Replace("{first}", text.Get(ActivityText.UnlockNightVisitor))
                            .Replace("{second}", Fill(text.Get(ActivityText.UnlockDay), "{n}", 10)));
                    break;
                case ActivitySpecialUnlock.Outfit:
                    if (!input.OutfitOwned)
                        hint = And(text, hint, text.Get(ActivityText.UnlockOutfit));
                    break;
            }
            if (d.RefugeLevel > 0 && input.RefugeLevel < d.RefugeLevel)
                hint = And(text, Fill(text.Get(ActivityText.UnlockRefugeLevel), "{n}", d.RefugeLevel), hint);
            if (d.Animals > 0 && input.Animals < d.Animals)
                hint = And(text, hint, Fill(text.GetPlural(ActivityText.UnlockAnimals, d.Animals), "{n}", d.Animals));
            if (d.Day > 0 && input.DayNumber < d.Day && !(d.AnimalsInsteadOfDay > 0 && input.Animals >= d.AnimalsInsteadOfDay))
            {
                string day = Fill(text.Get(ActivityText.UnlockDay), "{n}", d.Day);
                if (d.AnimalsInsteadOfDay > 0)
                    day = text.Get(ActivityText.UnlockOr).Replace("{first}", day)
                        .Replace("{second}", Fill(text.GetPlural(ActivityText.UnlockAnimals, d.AnimalsInsteadOfDay), "{n}", d.AnimalsInsteadOfDay));
                hint = And(text, hint, day);
            }
            // Everything met but the refuge level service still says no: its level is the hint.
            return hint ?? Fill(text.Get(ActivityText.UnlockRefugeLevel), "{n}", Math.Max(1, d.RefugeLevel));
        }

        private static string And(ILocalization text, string first, string second)
        {
            if (string.IsNullOrEmpty(first))
                return second;
            if (string.IsNullOrEmpty(second))
                return first;
            return text.Get(ActivityText.UnlockAnd).Replace("{first}", first).Replace("{second}", second);
        }

        public static string Fill(string text, string token, int value) => text.Replace(token, value.ToString());
    }
}
