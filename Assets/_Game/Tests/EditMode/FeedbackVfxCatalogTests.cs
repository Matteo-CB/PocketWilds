using System.Linq;
using NUnit.Framework;
using PocketWilds.Editor.Vfx;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEngine;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T34A Gate 10, data side: every feedback effect is listed, shares the one particle material, never loops or plays
    /// on awake, emits by bursts only (so the tier scales it) and caps its particle size. Runtime side: FeedbackVfxPlayTests.
    /// </summary>
    public class FeedbackVfxCatalogTests
    {
        private static VfxCatalog Catalog => AssetDatabase.LoadAssetAtPath<VfxCatalog>(VfxPipeline.CatalogPath);

        [Test]
        public void Catalog_ListsEveryEffect_WithOneSharedMaterial_AndThePlannedPaths()
        {
            var catalog = Catalog;
            Assert.IsNotNull(catalog, "Run Pocket Wilds/VFX/Build Feedback VFX (T34A).");
            for (int i = 0; i < (int)VfxId.Count; i++)
                Assert.IsTrue(catalog.TryGet((VfxId)i, out _), "Missing effect " + (VfxId)i);
            var materials = catalog.effects.SelectMany(e => e.prefab.GetComponentsInChildren<ParticleSystemRenderer>(true))
                .Select(r => r.sharedMaterial).Distinct().ToArray();
            Assert.AreEqual(1, materials.Length, "Every feedback effect shares MAT_VFX_AdditiveLite (no per-effect material).");
            Assert.AreEqual(VfxPipeline.MaterialPath, AssetDatabase.GetAssetPath(materials[0]));
            foreach (var effect in catalog.effects)
            {
                StringAssert.StartsWith(VfxPipeline.Folder + "/VFX_", AssetDatabase.GetAssetPath(effect.prefab));
                foreach (var ps in effect.prefab.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = ps.main;
                    Assert.IsFalse(main.playOnAwake, effect.id + ": pooled effects never play on awake.");
                    Assert.IsFalse(main.loop, effect.id + ": one-shot.");
                    Assert.AreEqual(0f, ps.emission.rateOverTime.constant, effect.id + ": bursts only (scaled by tier).");
                    var renderer = ps.GetComponent<ParticleSystemRenderer>();
                    Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, renderer.shadowCastingMode);
                    Assert.LessOrEqual(renderer.maxParticleSize, 0.35f, effect.id + ": no particle covers more than a third of the screen.");
                }
            }
        }
    }
}
