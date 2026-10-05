using System;
using System.IO;
using PocketWilds.Core;
using PocketWilds.Editor.Eggs;
using PocketWilds.Editor.Setup;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.Lighting
{
    /// <summary>
    /// T16 lighting reference: quality tiers (via ProjectSetup), LP_Grassland_Day + MAT_Sky_Grassland,
    /// MAT_BlobShadow, a minimal color-grading volume (Medium/High only), PF_CameraRig_Sanctuary, and the
    /// vertical slice scene wired to them. Idempotent.
    /// </summary>
    public static class LightingPipeline
    {
        public const string LightingFolder = "Assets/_Game/Art/Lighting/Grassland";
        public const string ProfilePath = LightingFolder + "/LP_Grassland_Day.asset";
        public const string SkyMaterialPath = LightingFolder + "/MAT_Sky_Grassland.mat";
        public const string VolumeProfilePath = LightingFolder + "/VP_Grassland_Day.asset";
        public const string BlobMaterialPath = "Assets/_Game/Art/Materials/MAT_BlobShadow.mat";
        public const string CameraRigPath = "Assets/_Game/Prefabs/World/PF_CameraRig_Sanctuary.prefab";
        public const string ScenePath = "Assets/_Game/Scenes/SC_VerticalSlice_Grassland.unity";

        [MenuItem("Pocket Wilds/Lighting/Build Lighting Reference (T16)", priority = 180)]
        public static void BuildAll()
        {
            ProjectSetup.ApplyQualityTiers();
            var profile = EnsureProfile();
            EnsureBlobMaterial();
            var volume = EnsureVolumeProfile();
            var rig = EnsureCameraRig();
            AssetDatabase.SaveAssets();
            SetupScene(profile, volume, rig);
            Debug.Log("[LightingPipeline] Quality tiers, LP_Grassland_Day, sky, blob shadows, volume, camera rig and scene up to date.");
        }

        // ---------------- Profile and sky ----------------

        public static LightingProfile EnsureProfile()
        {
            Directory.CreateDirectory(LightingFolder);
            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find("PocketWilds/SkyGradient")) { name = "MAT_Sky_Grassland" };
                AssetDatabase.CreateAsset(sky, SkyMaterialPath);
            }
            var profile = AssetDatabase.LoadAssetAtPath<LightingProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<LightingProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            // Reference values live here, like the palettes: rerunning the pipeline restores the validated look.
            // Grassland identity: warm neutral sun, soft cool ambient, sky-blue light (Biomes doc).
            profile.sunColor = Hex("FFF1DC");
            profile.sunIntensity = 1.0f;
            profile.sunAngles = new Vector2(50f, -32f);
            profile.shadowStrength = 0.7f;
            profile.ambientTop = Hex("9EB3D1");
            profile.ambientEquator = Hex("9AA89A");
            profile.ambientBottom = Hex("5E6B52");
            profile.skyTop = Hex("6DA8E6");
            profile.skyHorizon = Hex("D6EBF5");
            profile.skyBottom = Hex("C8DCDF");
            profile.fog = true;
            profile.fogColor = Hex("D6EBF5");
            profile.fogStart = 20f;
            profile.fogEnd = 48f;
            profile.sky = sky;
            EditorUtility.SetDirty(profile);
            WriteSky(profile);
            return profile;
        }

        /// <summary>Copies the profile sky colors into its material (editor time only; runtime never edits materials).</summary>
        public static void WriteSky(LightingProfile profile)
        {
            var sky = profile.sky;
            sky.shader = Shader.Find("PocketWilds/SkyGradient");
            sky.SetColor("_SkyTop", profile.skyTop);
            sky.SetColor("_SkyHorizon", profile.skyHorizon);
            sky.SetColor("_SkyBottom", profile.skyBottom);
            sky.SetColor("_SunTint", profile.sunColor);
            var toSun = Quaternion.Euler(profile.sunAngles.x, profile.sunAngles.y, 0f) * Vector3.back;
            sky.SetVector("_SunDirection", new Vector4(toSun.x, toSun.y, toSun.z, 0f));
            EditorUtility.SetDirty(sky);
        }

        // ---------------- Blob shadows ----------------

        public static Material EnsureBlobMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(BlobMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("PocketWilds/BlobShadow")) { name = "MAT_BlobShadow" };
                AssetDatabase.CreateAsset(material, BlobMaterialPath);
            }
            EggPipeline.EnsureVfxAtlas();
            material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(EggPipeline.VfxAtlasPath));
            material.SetColor("_ShadowColor", Hex("6B807A"));
            material.SetFloat("_Strength", 0.7f);
            material.enableInstancing = false;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Adds the flat soft disc used on Low. Disabled in the prefab; views enable it per quality tier.</summary>
        public static MeshRenderer AddBlobShadow(Transform parent, Vector2 size)
        {
            var go = new GameObject("BlobShadow");
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(new Vector3(0f, 0.015f, 0f), Quaternion.Euler(90f, 0f, 0f));
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = EnsureBlobMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.enabled = false;
            return renderer;
        }

        // ---------------- Post-processing ----------------

        public static VolumeProfile EnsureVolumeProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumeProfilePath);
            }
            // Minimal finishing only (doc 07): the look comes from palettes and light, post just tightens it.
            if (!profile.TryGet<ColorAdjustments>(out var color))
            {
                color = profile.Add<ColorAdjustments>(true);
                color.name = "ColorAdjustments";
                AssetDatabase.AddObjectToAsset(color, profile);
            }
            color.contrast.Override(12f);
            color.saturation.Override(-4f);
            color.postExposure.Override(0f);
            if (!profile.TryGet<WhiteBalance>(out var balance))
            {
                balance = profile.Add<WhiteBalance>(true);
                balance.name = "WhiteBalance";
                AssetDatabase.AddObjectToAsset(balance, profile);
            }
            balance.temperature.Override(4f);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        // ---------------- Camera rig ----------------

        public static SanctuaryCameraRig EnsureCameraRig()
        {
            var root = new GameObject("PF_CameraRig_Sanctuary");
            try
            {
                var rig = root.AddComponent<SanctuaryCameraRig>();
                root.AddComponent<SanctuaryCameraInput>();
                var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cameraGo.transform.SetParent(root.transform, false);
                var camera = cameraGo.AddComponent<Camera>();
                camera.fieldOfView = 30f;
                camera.nearClipPlane = 0.5f;
                camera.farClipPlane = 80f;
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.allowHDR = false;
                camera.allowMSAA = true;
                var data = cameraGo.AddComponent<UniversalAdditionalCameraData>();
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.None;
                data.renderShadows = true;
                data.requiresDepthOption = CameraOverrideOption.Off;
                data.requiresColorOption = CameraOverrideOption.Off;
                cameraGo.AddComponent<AudioListener>();
                rig.SetCamera(camera);
                Directory.CreateDirectory(Path.GetDirectoryName(CameraRigPath));
                return PrefabUtility.SaveAsPrefabAsset(root, CameraRigPath).GetComponent<SanctuaryCameraRig>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ---------------- Scene ----------------

        private static void SetupScene(LightingProfile profile, VolumeProfile volumeProfile, SanctuaryCameraRig rigPrefab)
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path != ScenePath && !string.IsNullOrEmpty(active.path) && active.isDirty)
                throw new InvalidOperationException("Save or discard changes in " + active.path + " first.");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            // Saving assets can reimport them behind the references we hold: reload everything by path.
            profile = AssetDatabase.LoadAssetAtPath<LightingProfile>(ProfilePath);
            volumeProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);

            Light sun = null;
            VerticalSliceEntry entry = null;
            SanctuaryCameraRig rig = null;
            Volume volume = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "Main Camera" && root.GetComponent<SanctuaryCameraRig>() == null)
                {
                    Object.DestroyImmediate(root); // replaced by the rig (prefab owns the camera now)
                    continue;
                }
                sun = sun ?? root.GetComponent<Light>();
                entry = entry ?? root.GetComponent<VerticalSliceEntry>();
                rig = rig ?? root.GetComponent<SanctuaryCameraRig>();
                volume = volume ?? root.GetComponent<Volume>();
            }
            if (rig == null)
            {
                // Reload by path: saving assets may have reimported the prefab behind the reference we got.
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CameraRigPath);
                rig = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, scene)).GetComponent<SanctuaryCameraRig>();
                rig.name = "CameraRig_Sanctuary";
            }
            if (volume == null)
            {
                volume = new GameObject("GlobalVolume").AddComponent<Volume>();
                EditorSceneManager.MoveGameObjectToScene(volume.gameObject, scene);
            }
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = volumeProfile;
            if (sun != null)
                sun.name = "Sun";

            var so = new SerializedObject(entry);
            so.FindProperty("_lighting").objectReferenceValue = profile;
            so.FindProperty("_sun").objectReferenceValue = sun;
            so.FindProperty("_cameraRig").objectReferenceValue = rig;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Bake the Medium reference into the saved scene so the editor view matches the game.
            profile.Apply(sun, rig.Camera, QualityTier.Medium);
            // The start chunk is streamed (T27): frame the egg spawn read from the chunk prefab.
            rig.SnapTo(PocketWilds.Editor.World.WorldContentPipeline.StartEggSpawn());
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            return color;
        }
    }
}
