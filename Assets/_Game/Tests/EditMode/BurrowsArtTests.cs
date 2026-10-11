using System.Linq;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Albums;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.Profile;
using PocketWilds.Presentation;
using PocketWilds.Services.Save;
using PocketWilds.Simulation;
using PocketWilds.UI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using B = PocketWilds.Editor.Events.BurrowsEventPipeline;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>T49 hatch effects: the newest owned one plays by default, the player's choice and « off » are kept and saved.</summary>
    public class HatchEffectTests
    {
        private static readonly ContentId Sprout = ContentId.FromSource(HatchEffects.SproutBurst);

        [Test]
        public void TheBurrowsEffect_IsTheEventsHatchEffect()
        {
            Assert.AreEqual(B.HatchEffect, HatchEffects.SproutBurst);
            Assert.AreEqual(0, HatchEffects.IndexOf(Sprout));
            Assert.AreEqual(-1, HatchEffects.IndexOf(ContentId.FromSource("accessory.burrows.sprout_hat")));
            Assert.AreEqual(-1, HatchEffects.IndexOf(default));
            Assert.AreEqual(VfxId.SproutBurst, HatchSequencePlayer.EffectVfx(0));
            Assert.AreEqual(VfxId.Count, HatchSequencePlayer.EffectVfx(-1));
        }

        [Test]
        public void NothingOwned_PlaysNoEffect()
        {
            var e = new EntitlementState();
            Assert.AreEqual(-1, HatchEffects.Active(e));
            Assert.AreEqual(-1, HatchEffects.Active(null));
            Assert.IsFalse(HatchEffects.Select(e, Sprout), "An effect not owned cannot be chosen.");
            Assert.IsFalse(e.HatchEffect.IsValid);
        }

        [Test]
        public void TheNewestOwned_PlaysByDefault_AndOffIsKept()
        {
            var e = new EntitlementState();
            e.Owned.Add(Sprout);
            Assert.AreEqual(0, HatchEffects.Active(e), "Owned without a choice: the newest owned plays.");
            HatchEffects.OnGranted(e, Sprout);
            Assert.AreEqual(Sprout, e.HatchEffect, "Receiving an effect selects it.");

            HatchEffects.SetOff(e, true);
            Assert.AreEqual(-1, HatchEffects.Active(e), "Turned off: no effect.");
            HatchEffects.OnGranted(e, Sprout);
            Assert.AreEqual(-1, HatchEffects.Active(e), "A new effect never turns effects back on.");
            HatchEffects.SetOff(e, false);
            Assert.AreEqual(0, HatchEffects.Active(e));

            HatchEffects.SetOff(e, true);
            Assert.IsTrue(HatchEffects.Select(e, Sprout), "Choosing an owned effect turns effects back on.");
            Assert.AreEqual(0, HatchEffects.Active(e));

            e.Owned.Remove(Sprout);
            Assert.AreEqual(-1, HatchEffects.Active(e), "A chosen effect no longer owned never plays.");
        }

        [Test]
        public void TheChoice_IsSaved()
        {
            var state = new PlayerState();
            state.Entitlements.Owned.Add(Sprout);
            state.Entitlements.HatchEffect = Sprout;
            state.Entitlements.HatchEffectOff = true;
            var serializer = new BinarySaveSerializer();
            var writer = new ByteBufferWriter();
            serializer.Serialize(state, writer);
            var bytes = writer.WrittenSpan.ToArray();
            var loaded = serializer.Deserialize(serializer.ReadDocument(bytes, bytes.Length));
            Assert.AreEqual(Sprout, loaded.Entitlements.HatchEffect);
            Assert.IsTrue(loaded.Entitlements.HatchEffectOff);
            Assert.AreEqual(-1, HatchEffects.Active(loaded.Entitlements));
        }
    }

    /// <summary>
    /// T49: every art key the Burrows event waited for from the effects and art lane resolves: the currency, effect and gold
    /// frame icons and the banner (pw_ui_sprites_t49.py), both « Racines » frames (profile rows and FrameArt), the Sprout Burst
    /// in the VFX catalog, the Peek Out pose in both family libraries and controllers, the hub's theme art and currency icon,
    /// and the 12 stickers of page 103 in the captured set. Run after the pipelines (VFX, family controllers, event screens).
    /// </summary>
    public class BurrowsArtKeysTests
    {
        private const string Icons = "Assets/_Game/Art/UI/Icons/";

        [TestCase(B.CurrencyIcon)]
        [TestCase("ICO_Effect_SproutBurst")]
        [TestCase("ICO_Frame_EventBurrowsGold")]
        public void Icon_IsAUiSprite(string key)
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Sprite>(Icons + key + ".png"), key + " (run pw_ui_sprites_t49.py)");
        }

        [Test]
        public void TheCatalogIcons_OfThisLane_Resolve()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ShopCatalogAsset>(B.CatalogPath);
            Assert.IsNotNull(catalog, "Run BurrowsEventPipeline first.");
            foreach (string id in new[] { B.HatchEffect, B.GoldFrame })
            {
                var row = catalog.entitlements.FirstOrDefault(e => e.sourceId == id);
                Assert.IsNotNull(row, id);
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Sprite>(Icons + row.iconKey + ".png"), id + ": " + row.iconKey);
            }
        }

        [Test]
        public void Banner_IsThemedInTheHub()
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/UI/Events/UI_EventBanner_Burrows.png"));
            var hub = AssetDatabase.LoadAssetAtPath<GameObject>(PocketWilds.Editor.UI.EventUiPipeline.HubPrefabPath);
            Assert.IsNotNull(hub, "Run the event screens pipeline.");
            var ui = hub.GetComponent<EventHubScreen>().Ui;
            Assert.IsNotNull(ui.themeArt, "The banner has its art image.");
            int theme = System.Array.IndexOf(ui.themeKeys, B.ThemeKey);
            Assert.GreaterOrEqual(theme, 0, B.ThemeKey);
            Assert.IsNotNull(ui.themeSprites[theme]);
            int currency = System.Array.IndexOf(ui.currencyKeys, B.CurrencyIcon);
            Assert.GreaterOrEqual(currency, 0, B.CurrencyIcon);
            Assert.IsNotNull(ui.currencySprites[currency]);
            Assert.Greater(ui.currencyImages.Length, 1, "The chip, the egg cost and the prices follow the event's currency.");
        }

        [TestCase(B.Frame)]
        [TestCase(B.GoldFrame)]
        public void Frame_IsAProfileRowWithArt(string key)
        {
            int index = ProfileData.IndexOf(ProfileItemKind.Frame, key);
            Assert.GreaterOrEqual(index, 0, key);
            var art = FrameArt.Find(key);
            Assert.IsNotNull(art, key + " (pw_ui_sprites_t44b.py writes FrameArt.cs)");
            CollectionAssert.IsSubsetOf(new[] { "Roots", "Sprout" }, art.Motifs.Select(m => m.Sprite).Distinct().ToArray(), "Rounded roots and tender sprouts.");
            foreach (var motif in art.Motifs)
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/UI/Profile/Frames/M_" + motif.Sprite + ".png"), motif.Sprite);
        }

        [Test]
        public void GoldFrame_IsGivenOnlyByTheGoldLine()
        {
            ref readonly var row = ref ProfileData.Row(ProfileItemKind.Frame, ProfileData.IndexOf(ProfileItemKind.Frame, B.GoldFrame));
            Assert.AreEqual(ProfileSource.EventGold, row.Source);
            Assert.AreEqual(B.AlbumNumber, row.Subject);
            Assert.AreEqual(20, row.Value);
            var gold = FrameArt.Find(B.GoldFrame);
            Assert.Greater(gold.RingColor.r, gold.RingColor.b + 100, "A gold ring.");
        }

        [Test]
        public void SproutBurst_IsInTheVfxCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<VfxCatalog>(PocketWilds.Editor.Vfx.VfxPipeline.CatalogPath);
            Assert.IsNotNull(catalog);
            Assert.IsTrue(catalog.TryGet(VfxId.SproutBurst, out var effect), "Run the VFX pipeline.");
            Assert.IsNotNull(effect.prefab);
            Assert.LessOrEqual(effect.hardCapacity, 3, "Budgeted like the other hatch effects.");
            int particles = effect.prefab.GetComponentsInChildren<ParticleSystem>(true).Sum(p =>
            {
                var bursts = new ParticleSystem.Burst[p.emission.burstCount];
                p.emission.GetBursts(bursts);
                return bursts.Sum(b => (int)b.count.constant);
            });
            Assert.LessOrEqual(particles * catalog.highCountScale, 40f, "High stays under 40 particles.");
            Assert.GreaterOrEqual(particles * catalog.lowCountScale, 10f, "Low still reads.");
            Assert.AreEqual(VfxId.SproutBurst, HatchSequencePlayer.EffectVfx(HatchEffects.IndexOf(ContentId.FromSource(B.HatchEffect))));
        }

        [TestCase("SmallQuadruped", RigFamily.SmallQuadruped)]
        [TestCase("MediumQuadruped", RigFamily.MediumQuadruped)]
        public void PeekOut_PlaysInEveryGroundFamily(string family, RigFamily rig)
        {
            Assert.IsTrue(PoseClips.TryGet(B.Pose, rig, out int hash));
            var entry = PoseClips.All.First(p => p.Key == B.Pose);
            string clip = PoseClips.ClipName(entry, rig);
            Assert.IsTrue(AssetDatabase.LoadAllAssetsAtPath(AnimalPipeline.ClipsPath(family)).OfType<AnimationClip>().Any(c => c.name == clip), clip);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimalPipeline.ControllerPath(family));
            Assert.IsNotNull(controller);
            var state = controller.layers[0].stateMachine.states.Select(s => s.state).FirstOrDefault(s => s.name == entry.State);
            Assert.IsNotNull(state, family + ": run AnimalPipeline.EnsureFamilyController.");
            Assert.AreEqual(clip, state.motion.name);
            Assert.AreEqual(hash, Animator.StringToHash(state.name));
            Assert.AreEqual("Idle", state.transitions.Single().destinationState.name, "A one-shot back to Idle.");
        }

        [Test]
        public void Page103_IsCaptured()
        {
            var page = StickerCaptureDefinitions.All.Where(d => d.page == "page.event.103").ToList();
            Assert.AreEqual(12, page.Count);
            CollectionAssert.AreEqual(Enumerable.Range(1, 12).Select(i => "STK-EVT-103-" + i.ToString("00")).ToArray(), page.Select(d => d.id).ToArray());
            CollectionAssert.Contains(StickerCaptureDefinitions.Pages, "page.event.103");
            foreach (var d in page)
                foreach (var s in d.subjects.Where(s => s.kind == SubjectKind.Prefab && !s.source.StartsWith("Assets/")))
                    Assert.IsTrue(s.optional, d.id + ": " + s.source + " is made by another pipeline, captured without it while missing.");
        }
    }
}
