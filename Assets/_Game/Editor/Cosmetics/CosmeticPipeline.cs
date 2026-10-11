using PocketWilds.Editor.Animals;
using PocketWilds.Editor.Shop;
using PocketWilds.Editor.UI;
using UnityEditor;
using UnityEngine;

namespace PocketWilds.Editor.Cosmetics
{
    /// <summary>
    /// T47 simulation data in one step: the Field Hamster's species definition (companion, three Rare looks, no TailBase), the
    /// shop catalog (Meadow Pack, its items, the Flower Crown and Seed Basket accessories) and the localization tables. The
    /// models, prefabs, icons and fits come from the art pipelines. Idempotent.
    /// </summary>
    public static class CosmeticPipeline
    {
        [MenuItem("Pocket Wilds/Cosmetics/Build Simulation Data (T47)", priority = 191)]
        public static void BuildAllMenu() => Debug.Log("[CosmeticPipeline] " + BuildAll());

        public static string BuildAll()
        {
            AnimalPipeline.UpdateSpeciesDefinitions();
            string shop = ShopPipeline.Build();
            UiPipeline.EnsureLocalization();
            return "species definitions updated (Field Hamster companion); shop: " + shop;
        }
    }
}
