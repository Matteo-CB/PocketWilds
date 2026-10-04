using System.Linq;
using NUnit.Framework;
using PocketWilds.Editor.Validation;
using UnityEditor;
using UnityEngine;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>T13 gate: egg base and fragments exported from BL_Egg_Family.blend, under budget, with mask layout.</summary>
    public class EggAssetTests
    {
        [Test]
        public void EggModels_PassAssetValidator()
        {
            var errors = AssetValidator.ValidateEggModels().Where(i => i.Severity == IssueSeverity.Error).ToList();
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void EggPrefab_PassesAssetValidator()
        {
            var errors = AssetValidator.ValidateEggPrefab().Where(i => i.Severity == IssueSeverity.Error).ToList();
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void Fragments_FitTheIntactEgg()
        {
            var base0 = AssetDatabase.LoadAllAssetsAtPath(AssetValidator.EggBaseModelPath).OfType<Mesh>().First(m => m.name == "SM_Egg_Base_LOD0");
            var bounds = base0.bounds;
            foreach (var mesh in AssetDatabase.LoadAllAssetsAtPath(AssetValidator.EggFragmentsModelPath).OfType<Mesh>())
            {
                var b = mesh.bounds;
                Assert.LessOrEqual(b.max.y, bounds.max.y + 0.002f, mesh.name);
                Assert.GreaterOrEqual(b.min.y, bounds.min.y - 0.002f, mesh.name);
                Assert.LessOrEqual(b.extents.x, bounds.extents.x + 0.002f, mesh.name);
                Assert.LessOrEqual(b.extents.z, bounds.extents.z + 0.002f, mesh.name);
            }
        }

        [Test]
        public void MaskTexture_IsLinearData()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetValidator.EggMaskPath);
            Assert.IsFalse(importer.sRGBTexture);
            Assert.AreEqual(TextureImporterAlphaSource.FromInput, importer.alphaSource);
            Assert.AreEqual(TextureWrapMode.Clamp, importer.wrapModeV);
            Assert.AreEqual(TextureImporterFormat.ASTC_4x4, importer.GetPlatformTextureSettings("Android").format);
        }
    }
}
