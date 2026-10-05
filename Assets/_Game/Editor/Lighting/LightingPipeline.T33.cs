using PocketWilds.Editor.Environment;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PocketWilds.Editor.Lighting
{
    /// <summary>
    /// T33 moods: LP_Grassland_Morning and LP_Grassland_Evening next to the day profile. All three share
    /// MAT_Sky_Grassland; WorldAtmosphere blends them through shader globals from the device's local hour.
    /// Reference values live here so rerunning the pipeline restores the validated look.
    /// </summary>
    public static partial class LightingPipeline
    {
        public const string MorningProfilePath = LightingFolder + "/LP_Grassland_Morning.asset";
        public const string EveningProfilePath = LightingFolder + "/LP_Grassland_Evening.asset";

        [MenuItem("Pocket Wilds/Lighting/Build Moods (T33)", priority = 181)]
        public static void BuildMoods()
        {
            var day = EnsureProfile();
            EnsureMoodProfiles(day);
            AssetDatabase.SaveAssets();
            WireMoodsInScene();
            Debug.Log("[LightingPipeline] Morning, day and evening profiles built and wired.");
        }

        /// <summary>Day atmosphere (T33 fields): clear blue, light cumulus, a breath of low mist far away.</summary>
        private static void ApplyDayAtmosphere(LightingProfile p)
        {
            p.sunGlowTint = Hex("FFEBC7");
            p.sunGlow = 0.3f;
            p.cloudLight = Hex("FFFCF5");
            p.cloudShade = Hex("C7D1EB");
            p.cloudCoverage = 0.4f;
            p.heightFogStrength = 0.3f;
            p.heightFogBase = 0f;
            p.heightFogFalloff = 0.45f;
            p.heightFogStart = 14f;
            p.heightFogRange = 24f;
            p.windStrength = 0.35f;
            p.windYaw = 35f;
            p.windSpeed = 1.6f;
            p.windWaveFrequency = 0.35f;
            p.gustStrength = 0.5f;
            p.gustFrequency = 0.6f;
            p.flutter = 0.035f;
        }

        public static void EnsureMoodProfiles(LightingProfile day)
        {
            // Morning: low golden sun from the left, peach horizon, a soft mist pooling in the distance, calm air.
            var morning = LoadOrCreate(MorningProfilePath);
            morning.sunColor = Hex("FFE6C8");
            morning.sunIntensity = 1.12f;
            morning.sunAngles = new Vector2(30f, 28f);
            morning.shadowStrength = 0.6f;
            morning.ambientTop = Hex("A8B9D6");
            morning.ambientEquator = Hex("BCBBAA");
            morning.ambientBottom = Hex("606B55");
            morning.sky = day.sky;
            morning.skyTop = Hex("7FB3E6");
            morning.skyHorizon = Hex("F3E2D0");
            morning.skyBottom = Hex("DCDCD2");
            morning.fog = true;
            morning.fogColor = Hex("EDE3D6");
            morning.fogStart = 20f;
            morning.fogEnd = 50f;
            morning.sunGlowTint = Hex("FFD6A3");
            morning.sunGlow = 0.45f;
            morning.cloudLight = Hex("FFF3E6");
            morning.cloudShade = Hex("D3CADF");
            morning.cloudCoverage = 0.34f;
            morning.heightFogStrength = 0.45f;
            morning.heightFogBase = 0f;
            morning.heightFogFalloff = 0.6f;
            morning.heightFogStart = 10f;
            morning.heightFogRange = 22f;
            morning.windStrength = 0.22f;
            morning.windYaw = 20f;
            morning.windSpeed = 1.3f;
            morning.windWaveFrequency = 0.3f;
            morning.gustStrength = 0.3f;
            morning.gustFrequency = 0.45f;
            morning.flutter = 0.03f;
            EditorUtility.SetDirty(morning);

            // Evening: warm raking sun from the right, apricot horizon under a lavender-blue top, pink-lit clouds.
            // Never dark: the latest hours stay on this mood (WorldAtmosphere softened day).
            var evening = LoadOrCreate(EveningProfilePath);
            evening.sunColor = Hex("FFD09C");
            evening.sunIntensity = 1.22f;
            evening.sunAngles = new Vector2(24f, -62f);
            evening.shadowStrength = 0.58f;
            evening.ambientTop = Hex("AAA8CF");
            evening.ambientEquator = Hex("CDB09C");
            evening.ambientBottom = Hex("5E524A");
            evening.sky = day.sky;
            evening.skyTop = Hex("7193D2");
            evening.skyHorizon = Hex("FFD3A8");
            evening.skyBottom = Hex("E6BEA4");
            evening.fog = true;
            evening.fogColor = Hex("F4D2B2");
            evening.fogStart = 22f;
            evening.fogEnd = 54f;
            evening.sunGlowTint = Hex("FFB474");
            evening.sunGlow = 0.6f;
            evening.cloudLight = Hex("FFE4C8");
            evening.cloudShade = Hex("C7A7BE");
            evening.cloudCoverage = 0.42f;
            evening.heightFogStrength = 0.4f;
            evening.heightFogBase = 0f;
            evening.heightFogFalloff = 0.5f;
            evening.heightFogStart = 12f;
            evening.heightFogRange = 24f;
            evening.windStrength = 0.3f;
            evening.windYaw = 50f;
            evening.windSpeed = 1.4f;
            evening.windWaveFrequency = 0.32f;
            evening.gustStrength = 0.4f;
            evening.gustFrequency = 0.5f;
            evening.flutter = 0.03f;
            EditorUtility.SetDirty(evening);
        }

        /// <summary>Sets the moods, the world quality and the ambient life kit on the scene entry (SetupScene, BuildMoods).</summary>
        private static void WireMoods(SerializedObject entry)
        {
            entry.FindProperty("_morning").objectReferenceValue = AssetDatabase.LoadAssetAtPath<LightingProfile>(MorningProfilePath);
            entry.FindProperty("_evening").objectReferenceValue = AssetDatabase.LoadAssetAtPath<LightingProfile>(EveningProfilePath);
            entry.FindProperty("_worldQuality").objectReferenceValue = AssetDatabase.LoadAssetAtPath<WorldQualityProfile>(GrasslandPipeline.WorldQualityPath);
            entry.FindProperty("_ambientLife").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AmbientLifeKit>(GrasslandPipeline.LifeKitPath);
        }

        public static void WireMoodsInScene()
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path != ScenePath && !string.IsNullOrEmpty(active.path) && active.isDirty)
                throw new System.InvalidOperationException("Save or discard changes in " + active.path + " first.");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
            var so = new SerializedObject(entry);
            WireMoods(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static LightingProfile LoadOrCreate(string path)
        {
            var profile = AssetDatabase.LoadAssetAtPath<LightingProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<LightingProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            return profile;
        }
    }
}
