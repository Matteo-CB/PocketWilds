using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Editor.Cosmetics;
using PocketWilds.Editor.Environment;
using PocketWilds.Editor.Events;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEngine;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T49 decorations of L'Éveil des Terriers (BurrowsDecorPipeline): every decoration, gold version and the monument has its
    /// prefab on the shared environment material, sits on the ground (lowest vertex within 1 mm below to 10 mm above the pivot),
    /// stays in budget (decorations under 1,500 triangles, Coffret ones under 2,000, the monument under 2,500) and has a measured
    /// footprint in DEF_CosmeticVisualCatalog (height bands and a ground outline, nothing enters anything); the gold versions share
    /// the model with the gold row; the icon keys match the event catalog.
    /// </summary>
    public class BurrowsDecorTests
    {
        [Test]
        public void EveryBurrowsDecoration_HasPrefab_OnTheGround_InBudget_WithFootprint()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CosmeticVisualCatalog>(AccessoryVisualPipeline.CatalogPath);
            var palettes = AssetDatabase.LoadAssetAtPath<PaletteSetAsset>(BurrowsDecorPipeline.PalettesPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(GrasslandPipeline.EnvironmentMaterialPath);
            Assert.IsNotNull(catalog, "DEF_CosmeticVisualCatalog missing.");
            Assert.IsNotNull(palettes, "DEF_Palettes_EventBurrows missing: run BurrowsDecorPipeline.BuildAll.");
            var problems = new List<string>();
            var pieces = BurrowsDecorPipeline.Pieces().ToList();
            Assert.AreEqual(18, pieces.Count, "13 decorations, 4 gold versions, 1 monument.");
            foreach (var piece in pieces)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BurrowsDecorPipeline.PrefabPath(piece.Prefab));
                if (prefab == null)
                {
                    problems.Add($"{piece.ItemId}: no {piece.Prefab}");
                    continue;
                }
                int tris = 0;
                float bottom = float.MaxValue;
                var toRoot = prefab.transform.worldToLocalMatrix;
                foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null)
                        continue;
                    tris += filter.sharedMesh.triangles.Length / 3;
                    var matrix = toRoot * filter.transform.localToWorldMatrix;
                    foreach (var v in filter.sharedMesh.vertices)
                        bottom = Mathf.Min(bottom, matrix.MultiplyPoint3x4(v).y);
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (renderer == null || renderer.sharedMaterial != material)
                        problems.Add($"{piece.Prefab}/{filter.name}: not on MAT_Environment_Common");
                }
                if (tris == 0)
                    problems.Add($"{piece.Prefab}: no mesh");
                else if (tris >= piece.Budget)
                    problems.Add($"{piece.Prefab}: {tris} tris, budget under {piece.Budget}");
                if (tris > 0 && (bottom < -0.001f || bottom > 0.01f))
                    problems.Add($"{piece.Prefab}: lowest point {bottom * 1000f:0.0} mm, not on the ground");

                if (!palettes.palettes.Any(p => p.sourceId == piece.Row))
                    problems.Add($"{piece.ItemId}: palette row {piece.Row} missing");
                if (piece.Gold && piece.Row != BurrowsDecorPipeline.GoldRow)
                    problems.Add($"{piece.ItemId}: a gold version uses {BurrowsDecorPipeline.GoldRow}");

                var entry = catalog.FindDecor(piece.ItemId);
                if (entry == null)
                {
                    problems.Add($"{piece.ItemId}: no decoration entry (run BurrowsDecorPipeline.BuildAll)");
                    continue;
                }
                if (entry.prefabAddress != piece.Prefab || entry.paletteRow != piece.Row)
                    problems.Add($"{piece.ItemId}: entry says {entry.prefabAddress} / {entry.paletteRow}");
                if (entry.bands == null || entry.bands.Length == 0 || entry.bands[0].bottom > 0.05f)
                    problems.Add($"{piece.ItemId}: no ground band in its footprint");
                else if (entry.bands.Any(b => b.radius <= 0f || b.top <= b.bottom || b.kind != ObstacleKind.Solid))
                    problems.Add($"{piece.ItemId}: a degenerate band");
                if (entry.groundHull == null || entry.groundHull.Length < 3 || CollisionProxies.HullArea(entry.groundHull) < 1e-4f)
                    problems.Add($"{piece.ItemId}: no ground outline");
                if (entry.reach <= 0f || entry.height <= 0f)
                    problems.Add($"{piece.ItemId}: no reach or height");
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void IconKeys_MatchTheEventCatalog()
        {
            var catalog = ScriptableObject.CreateInstance<ShopCatalogAsset>();
            try
            {
                BurrowsEventPipeline.FillCatalog(catalog);
                var problems = new List<string>();
                foreach (var piece in BurrowsDecorPipeline.Pieces())
                {
                    var item = catalog.entitlements.FirstOrDefault(e => e.sourceId == piece.ItemId);
                    if (item == null)
                        problems.Add(piece.ItemId + ": not an item of the event catalog");
                    else if (item.iconKey != piece.Icon)
                        problems.Add($"{piece.ItemId}: catalog icon {item.iconKey}, rendered {piece.Icon}");
                }
                foreach (var accessory in catalog.accessories)
                {
                    var visual = AccessoryVisualPipeline.FindPiece(accessory.sourceId);
                    if (visual == null || visual.Icon != accessory.iconKey || visual.Visual != accessory.visualKey)
                        problems.Add($"{accessory.sourceId}: icon or visual key differs from AccessoryVisualPipeline");
                }
                for (int i = 0; i < catalog.outfits.Count; i++)
                    if (catalog.outfits[i].iconKey != AccessoryVisualPipeline.OutfitIcon(i + 1))
                        problems.Add($"{catalog.outfits[i].sourceId}: icon {catalog.outfits[i].iconKey}");
                Assert.IsEmpty(problems, string.Join("\n", problems));
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }
    }
}
