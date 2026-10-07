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
        public IEnumerator StreamBend_StreamsInBeforeTheCamera_AndIsReleasedAfterIt_Twice()
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
            // T35A: the open zones and the next one (the meadow) stay loaded; StreamBend, two zones away, streams.
            int meadow = streamer.IndexOf("chunk.grassland.streambend");
            Assert.IsTrue(streamer.IsResident(streamer.IndexOf("chunk.grassland.meadow")), "The next zone stays loaded: its hedge meets the start chunk.");
            Assert.IsFalse(streamer.IsResident(meadow));
            Assert.AreEqual(ChunkStreamState.Active, streamer.GetState(start), "The start chunk is ready before Enter.");
            Assert.AreSame(streamer.GetChunk(start), entry.StartChunk);
            Assert.IsNotNull(entry.Refuge, "The refuge starts on the streamed chunk.");
            Assert.AreEqual(1, CountChunks("PF_Chunk_Grassland_Start"), "The chunk is not in the scene file any more.");

            var rig = entry.CameraRig;
            // The game is portrait (the Windows benchmark player runs 480 x 1040): the window follows that frame,
            // not whatever size the test runner's game view has.
            rig.Camera.aspect = 480f / 1040f;
            var egg = entry.StartChunk.EggSpawn.position;
            // T35A: the pan limits stop past the next zone's hedge (seam at 7 m, a 2.2 m glimpse beyond it), once the
            // next zone has loaded and its hedge stands.
            float hedge = Time.realtimeSinceStartup + 10f;
            while (!entry.Zones.Boundary.IsPlaced)
            {
                Assert.Less(Time.realtimeSinceStartup, hedge, "The next zone's hedge never stood.");
                yield return null;
            }
            rig.SnapTo(new Vector3(400f, 0f, egg.z));
            Assert.AreEqual(7f + RefugeZones.CameraPastSeam, rig.Focus.x, 0.01f);
            // The streaming itself is tested along the whole strip, past the zones' limits.
            rig.SetBounds(new Vector3(50f, egg.y, egg.z), new Vector2(60f, 4f));
            rig.SnapTo(new Vector3(-3.5f, 0f, egg.z));
            yield return null;
            // Counted from the portrait frame on: a landscape test view sees the meadow from the egg at boot.
            int lateBefore = streamer.Stats.lateActivations;

            for (int cycle = 0; cycle < 2; cycle++)
            {
                // From the west end of the start chunk StreamBend is out of the window.
                float until = Time.realtimeSinceStartup + 8f;
                while (streamer.GetState(meadow) != ChunkStreamState.Unloaded)
                {
                    Assert.Less(Time.realtimeSinceStartup, until, "StreamBend was never released.");
                    yield return null;
                }
                yield return null; // Destroy runs at the end of the release frame
                Assert.AreEqual(0, CountChunks("PF_Chunk_Grassland_StreamBend"), "Released chunks leave no instance behind.");
                int loads = streamer.Stats.loads;

                yield return Travel(rig, 28f);
                until = Time.realtimeSinceStartup + 5f;
                while (streamer.GetState(meadow) != ChunkStreamState.Active)
                {
                    Assert.Less(Time.realtimeSinceStartup, until, $"StreamBend never became active (focus {rig.Focus.x:F1}, state {streamer.GetState(meadow)}).");
                    yield return null;
                }
                Assert.AreEqual(loads + 1, streamer.Stats.loads);
                Assert.AreEqual(1, CountChunks("PF_Chunk_Grassland_StreamBend"));
                var chunk = streamer.GetChunk(meadow);
                Assert.AreEqual(new Vector3(28f, 0f, 0f), chunk.transform.position);
                Assert.IsTrue(chunk.transform.Find("Ground").GetComponent<MeshRenderer>().GetShaderUserValue() != 0u, "Palettes are bound before activation.");

                yield return Travel(rig, -3.5f);
                Assert.AreEqual(ChunkStreamState.Active, streamer.GetState(start));
            }
            float end = Time.realtimeSinceStartup + 8f;
            while (streamer.GetState(meadow) != ChunkStreamState.Unloaded)
            {
                Assert.Less(Time.realtimeSinceStartup, end, "StreamBend was never released.");
                yield return null;
            }
            yield return null;
            Assert.AreEqual(0, CountChunks("PF_Chunk_Grassland_StreamBend"));
            Assert.GreaterOrEqual(streamer.Stats.releases, 2);
            Assert.AreEqual(lateBefore, streamer.Stats.lateActivations, "StreamBend was always active before entering the frame.");
            Assert.AreEqual(0, streamer.Stats.failures);
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
        }
    }
}
