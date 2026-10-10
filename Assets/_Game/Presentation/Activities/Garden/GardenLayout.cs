using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T43: where the garden lives and how its pieces are named (shared by the set pipeline, the stage, the overlay, the
    /// capture and the tests).
    /// </summary>
    public static class GardenLayout
    {
        /// <summary>The Addressables group of the garden set, the plants and the overlay (loaded at the first visit, released on leaving).</summary>
        public const string Group = "Activity_Garden";

        public const string SetAddress = "activity.garden.set";
        public const string SetPrefabName = "PF_Activity_Garden_Set";
        public const string SetPrefabPath = "Assets/_Game/Prefabs/Activities/" + SetPrefabName + ".prefab";

        public const string PlantsAddress = "activity.garden.plants";
        public const string PlantsAssetPath = "Assets/_Game/Content/Garden/DEF_GardenPlantVisuals.asset";

        public const string ScreenAddress = "activity.garden.screen";
        public const string ScreenPrefabName = "PF_UI_Garden";
        public const string ScreenPrefabPath = "Assets/_Game/Prefabs/Activities/" + ScreenPrefabName + ".prefab";

        /// <summary>Hand-made sprites of the overlay (agent GA).</summary>
        public const string SpriteFolder = "Assets/_Game/Art/UI/Activities/Garden";

        /// <summary>The overlay's canvas: over the refuge HUD (10), under the activity HUD (20), the hub (22) and the result (30).</summary>
        public const int SortingOrder = 16;

        /// <summary>Plots along the row (3 owned, the Gems plot, the Club plot); plot i and i + 1 are neighbours.</summary>
        public const int PlotCount = 5;

        /// <summary>The set is placed this far below the refuge (never seen from it).</summary>
        public static readonly Vector3 SetOrigin = new Vector3(0f, -200f, 0f);
    }
}
