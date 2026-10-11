using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using PocketWilds.Bootstrap;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Presentation;
using PocketWilds.Services.Save;
using PocketWilds.Services.Store;
using PocketWilds.Simulation;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace PocketWilds.Tests.PlayMode
{
    /// <summary>
    /// T47 in the real scene through the bootstrap, with the sandbox store: the Meadow Pack brings the Field Hamster into the
    /// refuge (staged arrival) and the Cozy Burrow; the Flower Crown hangs under a rabbit's SOCK_HeadTop and comes off; a restore
    /// on a fresh profile brings the hamster and the burrow back, not the Gems.
    /// </summary>
    public class CompanionRefugePlayTests
    {
        private const string MeadowPackOffer = "offer.starter.001";
        private string _saveDir;
        private string _freshDir;
        private SandboxStoreProvider _store;

        [SetUp]
        public void SetUp()
        {
            string root = Path.Combine(Path.GetTempPath(), "PocketWildsPlayTests");
            _saveDir = Path.Combine(root, Guid.NewGuid().ToString("N"));
            _freshDir = Path.Combine(root, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveDir);
            Directory.CreateDirectory(_freshDir);
            _store = new SandboxStoreProvider("en", 0);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var bootstrap in Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None))
                Object.Destroy(bootstrap.gameObject);
            try
            {
                TestSaveDir.Release(_saveDir);
                TestSaveDir.Release(_freshDir);
            }
            catch (IOException)
            {
            }
        }

        /// <summary>Past onboarding: rabbits and squirrels, Light to hatch, nothing bought.</summary>
        private static void PrepareSave(string directory)
        {
            var service = new SaveService(new SaveFileStore(directory), new BinarySaveSerializer(), SaveMigrations.CreatePipeline(),
                new RuntimeGameClock(), new SaveServiceOptions { AppVersion = Application.version });
            service.LoadOrCreate();
            var state = service.State;
            string[] animals = { "rabbit.brown", "squirrel.red", "rabbit.white", "squirrel.grey", "rabbit.grey", "rabbit.spotted" };
            for (int i = 0; i < animals.Length; i++)
            {
                var species = ContentId.FromSource("species." + animals[i].Substring(0, animals[i].IndexOf('.')));
                var variant = ContentId.FromSource("variant." + animals[i]);
                state.Collection.Add(new AnimalInstance { Species = species, Variant = variant, Rarity = RarityTier.Common, Seed = (uint)i + 11u });
                state.Collection.DiscoveredSpecies.Add(species);
                state.Collection.DiscoveredVariants.Add(variant);
            }
            state.Codex.SeenSpecies.UnionWith(state.Collection.DiscoveredSpecies);
            state.Codex.SeenVariants.UnionWith(state.Collection.DiscoveredVariants);
            state.Wallet.Light = BigValue.FromMantissaExponent(1.0, 9);
            Assert.IsTrue(service.FlushNow());
            service.Dispose();
        }

        private IEnumerator Boot(string directory, Action<GameBootstrap> done)
        {
#if UNITY_EDITOR
            var database = UnityEditor.AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>("Assets/_Game/Content/DEF_ContentDatabase.asset");
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>("Assets/_Game/Content/Visuals/DEF_AnimalVisualCatalog.asset");
            GameBootstrap.PendingOptions = new BootOptions
            {
                SaveDirectory = directory,
                ContentOverride = database.CreateBuilder().Build(),
                VisualCatalogOverride = catalog,
                LocalizationOverride = database.localization.ToArray(),
                StoreProviderOverride = _store,
                LoadTargetScene = true,
            };
#endif
            var bootstrap = new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
            float timeout = Time.realtimeSinceStartup + 20f;
            while (bootstrap.State == BootState.NotStarted || bootstrap.State == BootState.Running)
            {
                Assert.Less(Time.realtimeSinceStartup, timeout, "Boot timed out.");
                yield return null;
            }
            Assert.AreEqual(BootState.Ready, bootstrap.State);
            yield return null;
            yield return null;
            done(bootstrap);
        }

        private static IEnumerator Wait(Task task, string what)
        {
            float until = Time.realtimeSinceStartup + 10f;
            while (!task.IsCompleted)
            {
                Assert.Less(Time.realtimeSinceStartup, until, what + " timed out.");
                yield return null;
            }
            Assert.IsFalse(task.IsFaulted, what + " failed: " + task.Exception);
        }

        private static IEnumerator WaitFor(Func<bool> condition, float seconds, string what)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                Assert.Less(Time.realtimeSinceStartup, until, what);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator MeadowPack_BringsHamsterAndBurrow_CrownOnAndOff_RestoreWithoutGems()
        {
#if UNITY_EDITOR
            PrepareSave(_saveDir);
            GameBootstrap bootstrap = null;
            yield return Boot(_saveDir, b => bootstrap = b);
            var context = bootstrap.Context;
            var refuge = Object.FindAnyObjectByType<VerticalSliceEntry>().Refuge;
            Assert.IsNotNull(context.Companions, "Companion service missing.");
            Assert.IsNotNull(context.Cosmetics, "Cosmetic service missing.");
            Assert.IsNotNull(refuge.Wardrobe, "No accessory visuals: run AccessoryVisualPipeline.BuildAll.");
            Assert.IsNotNull(refuge.Wardrobe, "Wardrobe missing.");
            yield return WaitFor(() => context.Store.IsReady, 10f, "Sandbox store not ready.");

            var purchase = context.Store.PurchaseAsync(ContentId.FromSource(MeadowPackOffer), default);
            yield return Wait(purchase, "Meadow Pack purchase");
            Assert.AreEqual(PurchaseFlowStatus.Granted, purchase.Result.Status);
            var species = CompanionCatalog.FieldHamster.Species;
            uint hamster = context.Companions.AnimalOf(species);
            Assert.AreNotEqual(0u, hamster, "The pack places the companion in the collection.");
            long gems = context.State.Wallet.Gems;
            Assert.AreEqual(500, gems, "The pack's 500 Gems.");

            // The staged arrival: it starts, lasts about 2 s, then the arrival is seen and the hamster lives in the refuge.
            yield return WaitFor(() => refuge.CompanionArrivalPlaying, 10f, "The hamster's arrival was not staged.");
            Assert.IsNotNull(refuge.VisibleViewOf(hamster), "The hamster appears at the start of its arrival.");
            float started = Time.realtimeSinceStartup;
            yield return WaitFor(() => !refuge.CompanionArrivalPlaying, 6f, "The arrival never ended.");
            Assert.GreaterOrEqual(Time.realtimeSinceStartup - started, 1.5f, "The arrival is a 2 s staging.");
            Assert.IsFalse(context.Companions.TryGetPendingArrival(out _, out _), "The arrival is marked seen.");
            Assert.IsNotNull(refuge.VisibleViewOf(hamster), "The hamster stays in the refuge.");
            yield return WaitFor(() => refuge.Burrow != null, 12f, "The Cozy Burrow did not appear.");

            // The Flower Crown (in the pack) on a visible rabbit: a child of its SOCK_HeadTop, removed on unequip.
            uint rabbit = 0;
            var rabbitSpecies = ContentId.FromSource("species.rabbit");
            for (int i = 0; i < context.State.Collection.Count && rabbit == 0; i++)
            {
                var animal = context.State.Collection[i];
                if (animal.Species == rabbitSpecies && refuge.VisibleViewOf(animal.InstanceId) != null)
                    rabbit = animal.InstanceId;
            }
            Assert.AreNotEqual(0u, rabbit, "A rabbit is visible.");
            var view = refuge.VisibleViewOf(rabbit);
            var crown = ContentId.FromSource("cosmetic.flower_crown");
            Assert.AreEqual(EquipResult.Ok, context.Cosmetics.Equip(rabbit, crown, AccessorySocket.HeadTop, 0));
            yield return null;
            var piece = refuge.AccessoryOn(rabbit, AccessorySocket.HeadTop);
            Assert.IsNotNull(piece, "The crown is hung on the rabbit.");
            Assert.AreSame(view.SocketHeadTop, piece.parent, "The crown is a rigid child of SOCK_HeadTop.");
            Assert.IsTrue(piece.gameObject.activeInHierarchy);
            Assert.IsTrue(context.Cosmetics.Unequip(rabbit, AccessorySocket.HeadTop));
            yield return null;
            Assert.IsNull(refuge.AccessoryOn(rabbit, AccessorySocket.HeadTop), "Unequipped: nothing on the socket.");
            for (int c = 0; c < view.SocketHeadTop.childCount; c++)
                Assert.IsFalse(view.SocketHeadTop.GetChild(c).gameObject.activeInHierarchy && view.SocketHeadTop.GetChild(c).name.StartsWith("PF_Acc_"),
                    "No crown left under SOCK_HeadTop.");

            // Let the background save hold the purchase, then a new device: a fresh profile and a restore.
            yield return WaitFor(() => context.Store.AwaitingSave == 0, 6f, "The purchase was never saved.");
            Object.Destroy(bootstrap.gameObject);
            yield return null;
            yield return null;
            PrepareSave(_freshDir);
            yield return Boot(_freshDir, b => bootstrap = b);
            context = bootstrap.Context;
            refuge = Object.FindAnyObjectByType<VerticalSliceEntry>().Refuge;
            yield return WaitFor(() => context.Store.IsReady, 10f, "Sandbox store not ready after the restart.");
            Assert.IsFalse(context.Companions.Owns(species), "A fresh profile has no companion before the restore.");
            var restore = context.Store.RestoreAsync(default);
            yield return Wait(restore, "Restore");
            Assert.GreaterOrEqual(restore.Result, 1, "The Meadow Pack is restored.");
            Assert.AreEqual(0, context.State.Wallet.Gems, "A restore never gives the Gems again.");
            Assert.IsTrue(context.Companions.Owns(species), "The companion comes back.");
            hamster = context.Companions.AnimalOf(species);
            yield return WaitFor(() => refuge.VisibleViewOf(hamster) != null && !refuge.CompanionArrivalPlaying, 12f, "The hamster is not back in the refuge.");
            yield return WaitFor(() => refuge.Burrow != null, 12f, "The Cozy Burrow is not back.");
#else
            yield break;
#endif
        }
    }
}
