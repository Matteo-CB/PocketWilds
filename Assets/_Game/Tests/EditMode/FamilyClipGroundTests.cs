using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.Validation;
using UnityEditor;
using UnityEngine;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// « Aucune collision » (Docs/Design/Collision_Rule.md): an animal never goes into the ground. Every clip a species plays
    /// (its family controller, or its override controller) is sampled twice per frame on the species prefab, and the lowest
    /// skinned vertex of LOD0 and LOD2 (prefab scale included) must stay within 2 mm of the ground. Found 2026-10-12: the
    /// SmallQuadruped Sleep, Hop, Walk, Happy, Happy_Bounce, ReactTouch, Eat_Loop and Social_Nose went 2 to 40 mm below.
    /// </summary>
    public class FamilyClipGroundTests
    {
        /// <summary>Families whose clips are checked on every species that rides them.</summary>
        private static readonly string[] Families = { "SmallQuadruped" };

        private const float Tolerance = 0.002f;
        private const int SamplesPerFrame = 2;

        public static IEnumerable<string> Species()
        {
            var controllers = Families.Select(f => AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimalPipeline.ControllerPath(f)))
                .Where(c => c != null).ToList();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { AssetValidator.AnimalPrefabFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var animator = prefab != null ? prefab.GetComponentInChildren<Animator>(true) : null;
                var controller = animator != null ? animator.runtimeAnimatorController : null;
                if (controller is AnimatorOverrideController aoc)
                    controller = aoc.runtimeAnimatorController;
                if (controller != null && controllers.Contains(controller))
                    yield return path;
            }
        }

        [Test]
        public void EveryFamilyHasSpecies()
        {
            Assert.IsNotEmpty(Species().ToList(), "No species prefab rides " + string.Join(", ", Families));
        }

        [TestCaseSource(nameof(Species))]
        public void PlayedClips_KeepTheSpeciesAboveTheGround(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var go = Object.Instantiate(prefab);
            go.hideFlags = HideFlags.HideAndDontSave;
            var baked = new Mesh();
            try
            {
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var animator = go.GetComponentInChildren<Animator>(true);
                // The animal models import with an optimized hierarchy (no bone transforms): clips sample nothing until
                // the instance gets its bones back.
                if (!animator.hasTransformHierarchy)
                    AnimatorUtility.DeoptimizeTransformHierarchy(animator.gameObject);
                Assert.IsTrue(animator.hasTransformHierarchy, prefabPath + ": no bone transforms to sample");
                var skins = go.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Where(s => s.sharedMesh != null && (s.name.Contains("LOD0") || s.name.Contains("LOD2"))).ToArray();
                Assert.IsNotEmpty(skins, prefabPath + ": no LOD0 or LOD2 skinned mesh");
                var clips = animator.runtimeAnimatorController.animationClips.Distinct().ToArray();
                Assert.IsNotEmpty(clips, prefabPath + ": the controller plays no clip");

                var vertices = new List<Vector3>();
                var (_, restTop, _) = Extremes(skins, baked, vertices);
                bool moved = false;
                var problems = new List<(float Depth, string Text)>();
                foreach (var clip in clips)
                {
                    int samples = Mathf.Max(1, Mathf.RoundToInt(clip.length * clip.frameRate)) * SamplesPerFrame;
                    float lowest = float.MaxValue, at = 0f;
                    string where = "";
                    for (int k = 0; k <= samples; k++)
                    {
                        float time = clip.length * k / samples;
                        clip.SampleAnimation(animator.gameObject, time);
                        var (low, top, skin) = Extremes(skins, baked, vertices);
                        moved |= Mathf.Abs(top - restTop) > 0.001f;
                        if (low < lowest)
                        {
                            lowest = low;
                            at = time * clip.frameRate;
                            where = skin;
                        }
                    }
                    if (lowest < -Tolerance)
                        problems.Add((lowest, $"{clip.name}: {lowest * 1000f:F1} mm at frame {at:F1} ({where})"));
                }
                Assert.IsTrue(moved, prefabPath + ": sampling the clips never moved the mesh; the check would pass on anything");
                Assert.IsEmpty(problems, $"{prefab.name}: clips take the animal below the ground (tolerance {Tolerance * 1000f:F0} mm):\n"
                                         + string.Join("\n", problems.OrderBy(p => p.Depth).Select(p => p.Text)));
            }
            finally
            {
                Object.DestroyImmediate(baked);
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>Lowest and highest world height of the skinned meshes as posed now, and the mesh holding the lowest.</summary>
        private static (float Lowest, float Highest, string Mesh) Extremes(SkinnedMeshRenderer[] skins, Mesh baked, List<Vector3> vertices)
        {
            float lowest = float.MaxValue, highest = float.MinValue;
            string mesh = "";
            foreach (var skin in skins)
            {
                // useScale: the baked vertices are in the renderer's own space, so its full matrix (the VisualRoot scale
                // of the scaled species included) takes them to the world.
                skin.BakeMesh(baked, true);
                baked.GetVertices(vertices);
                var toWorld = skin.transform.localToWorldMatrix;
                for (int i = 0; i < vertices.Count; i++)
                {
                    float y = toWorld.MultiplyPoint3x4(vertices[i]).y;
                    highest = Mathf.Max(highest, y);
                    if (y < lowest)
                    {
                        lowest = y;
                        mesh = skin.name;
                    }
                }
            }
            return (lowest, highest, mesh);
        }
    }
}
