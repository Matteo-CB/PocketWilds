using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using PocketWilds.Bootstrap;
using PocketWilds.Content;
using PocketWilds.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace PocketWilds.Tests.PlayMode
{
    /// <summary>
    /// T27 in the real scene, through the bootstrap and Addressables (editor play mode database): the start
    /// chunk is streamed before Enter, the meadow is preloaded and active before the camera reaches it, and it is
    /// released (instance destroyed, handle released) after the camera leaves, twice in a row.
    /// </summary>
    public class StreamingPlayTests
    {
        private string _saveDir;

        [SetUp]
        public void SetUp()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "PocketWildsPlayTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveDir);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var bootstrap in Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None))
                Object.Destroy(bootstrap.gameObject);
            try
            {
                Directory.Delete(_saveDir, true);
            }
            catch (IOException)
            {
            }
        }

        /// <summary>Moves the camera focus in small steps, like a finger drag (about 6 m/s).</summary>
        private static IEnumerator Travel(RefugeCameraRig rig, float toX)
        {
            var focus = rig.Focus;
            while (Mathf.Abs(focus.x - toX) > 0.01f)
            {
                focus.x = Mathf.MoveTowards(focus.x, toX, 0.1f);
                rig.SnapTo(focus);
                yield return null;
            }
        }

        private static int CountChunks(string name)
        {
            int count = 0;
            foreach (var chunk in Object.FindObjectsByType<WorldChunk>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (chunk.name.StartsWith(name, StringComparison.Ordinal))
                    count++;
            return count;
        }

        [UnityTest]
        public IEnumerator Meadow_StreamsInBeforeTheCamera_AndIsReleasedAfterIt_Twice()
        {
#if UNITY_EDITOR
            var database = UnityEditor.AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>("Assets/_Game/Content/DEF_ContentDatabase.asset");
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>("Assets/_Game/Content/Visuals/DEF_AnimalVisualCatalog.asset");
            GameBootstrap.PendingOptions = new BootOptions
            {
                SaveDirectory = _saveDir,
                ContentOverride = database.CreateBuilder().Build(),
                VisualCatalogOverride = catalog,
                LocalizationOverride = database.localization.ToArray(),
                LoadTargetScene = true,
            };
            var bootstrap = new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
            float timeout = Time.realtimeSinceStartup + 20f;
            while (bootstrap.State == BootState.NotStarted || bootstrap.State == BootState.Running)
            {
                Assert.Less(Time.realtimeSinceStartup, timeout, "Boot timed out.");
                yield return null;
            }
            Assert.AreEqual(BootState.Ready, bootstrap.State);
            yield return null;

            var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
            var streamer = entry.Streamer;
            Assert.IsNotNull(streamer, "The scene entry streams its chunks.");
            int start = streamer.IndexOf("chunk.grassland.start");
            int meadow = streamer.IndexOf("chunk.grassland.meadow");
            Assert.AreEqual(ChunkStreamState.Active, streamer.GetState(start), "The start chunk is ready before Enter.");
            Assert.AreSame(streamer.GetChunk(start), entry.StartChunk);
            Assert.IsNotNull(entry.Refuge, "The refuge starts on the streamed chunk.");
            Assert.AreEqual(1, CountChunks("PF_Chunk_Grassland_Start"), "The chunk is not in the scene file any more.");

            var rig = entry.CameraRig;
            // The game is portrait (the Windows benchmark player runs 480 x 1040): the window follows that frame,
            // not whatever size the test runner's game view has.
            rig.Camera.aspect = 480f / 1040f;
            var egg = entry.StartChunk.EggSpawn.position;
            // T33B: the pan limits reach the far east of the strip (ForestEdge at 98 m, 3.5 m short of its rim).
            rig.SnapTo(new Vector3(400f, 0f, egg.z));
            Assert.AreEqual(101.5f, rig.Focus.x, 0.01f);
            rig.SnapTo(new Vector3(-3.5f, 0f, egg.z));
            yield return null;
            // Counted from the portrait frame on: a landscape test view sees the meadow from the egg at boot.
            int lateBefore = streamer.Stats.lateActivations;

            for (int cycle = 0; cycle < 2; cycle++)
            {
                // From the west end of the start chunk the meadow is out of the window.
                float until = Time.realtimeSinceStartup + 8f;
                while (streamer.GetState(meadow) != ChunkStreamState.Unloaded)
                {
                    Assert.Less(Time.realtimeSinceStartup, until, "The meadow was never released.");
                    yield return null;
                }
                yield return null; // Destroy runs at the end of the release frame
                Assert.AreEqual(0, CountChunks("PF_Chunk_Grassland_Meadow"), "Released chunks leave no instance behind.");
                int loads = streamer.Stats.loads;

                yield return Travel(rig, 14f);
                until = Time.realtimeSinceStartup + 5f;
                while (streamer.GetState(meadow) != ChunkStreamState.Active)
                {
                    Assert.Less(Time.realtimeSinceStartup, until, "The meadow never became active.");
                    yield return null;
                }
                Assert.AreEqual(loads + 1, streamer.Stats.loads);
                Assert.AreEqual(1, CountChunks("PF_Chunk_Grassland_Meadow"));
                var chunk = streamer.GetChunk(meadow);
                Assert.AreEqual(new Vector3(14f, 0f, 0f), chunk.transform.position);
                Assert.IsTrue(chunk.transform.Find("Ground").GetComponent<MeshRenderer>().GetShaderUserValue() != 0u, "Palettes are bound before activation.");

                yield return Travel(rig, -3.5f);
                Assert.AreEqual(ChunkStreamState.Active, streamer.GetState(start));
            }
            float end = Time.realtimeSinceStartup + 8f;
            while (streamer.GetState(meadow) != ChunkStreamState.Unloaded)
            {
                Assert.Less(Time.realtimeSinceStartup, end, "The meadow was never released.");
                yield return null;
            }
            yield return null;
            Assert.AreEqual(0, CountChunks("PF_Chunk_Grassland_Meadow"));
            Assert.GreaterOrEqual(streamer.Stats.releases, 2);
            Assert.AreEqual(lateBefore, streamer.Stats.lateActivations, "The meadow was always active before entering the frame.");
            Assert.AreEqual(0, streamer.Stats.failures);
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
        }
    }
}
