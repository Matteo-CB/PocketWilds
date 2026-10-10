using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using PocketWilds.Bootstrap;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Presentation;
using PocketWilds.Services.Save;
using PocketWilds.Simulation;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace PocketWilds.Tests.PlayMode
{
    /// <summary>
    /// T25 in the real scene: two animals greet nose to nose with the family Social clip on their own
    /// Animators, and the start chunk feeds its points of interest to the scheduler.
    /// </summary>
    public class SocialPlayTests
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

        private void PrepareSave()
        {
            var service = new SaveService(new SaveFileStore(_saveDir), new BinarySaveSerializer(), SaveMigrations.CreatePipeline(),
                new RuntimeGameClock(), new SaveServiceOptions { AppVersion = Application.version });
            service.LoadOrCreate();
            var state = service.State;
            var animals = new[] { ("species.rabbit", "variant.rabbit.brown"), ("species.hedgehog", "variant.hedgehog.brown"),
                ("species.squirrel", "variant.squirrel.red"), ("species.rabbit", "variant.rabbit.white") };
            // A small refuge (T35A: the whole arrival zone): ten animals, so neighbours meet like in a real game.
            for (int i = 0; i < 10; i++)
            {
                var species = ContentId.FromSource(animals[i % animals.Length].Item1);
                var variant = ContentId.FromSource(animals[i % animals.Length].Item2);
                state.Collection.Add(new AnimalInstance { Species = species, Variant = variant, Rarity = RarityTier.Common, Seed = SeedHash.Mix((uint)i + 7u, 0x50C1A1u) });
                state.Collection.DiscoveredSpecies.Add(species);
                state.Collection.DiscoveredVariants.Add(variant);
            }
            state.Codex.SeenSpecies.UnionWith(state.Collection.DiscoveredSpecies);
            state.Codex.SeenVariants.UnionWith(state.Collection.DiscoveredVariants);
            Assert.IsTrue(service.FlushNow());
            service.Dispose();
        }

        private static AnimalView ViewOf(AnimalScheduler scheduler, int handle)
        {
            uint id = scheduler.GetInstanceId(handle);
            foreach (var view in Object.FindObjectsByType<AnimalView>(FindObjectsSortMode.None))
                if (view.OwnerInstanceId == id)
                    return view;
            return null;
        }


        // [SOCDIAG] temporary instrumentation
        private static readonly System.Text.StringBuilder Diag = new System.Text.StringBuilder();
        private static readonly System.Reflection.BindingFlags NP = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        private static object AgentOf(AnimalScheduler s, int h) => ((Array)typeof(AnimalScheduler).GetField("_agents", NP).GetValue(s)).GetValue(h);
        private static T Fld<T>(object o, string n) => (T)o.GetType().GetField(n).GetValue(o);
        private static string Dump(AnimalScheduler s, int h, string label)
        {
            var ag = AgentOf(s, h);
            var pos = Fld<Vector3>(ag, "Position");
            var face = Fld<Vector3>(ag, "Facing");
            var tgt = Fld<Vector3>(ag, "Target");
            float yaw = Fld<float>(ag, "Yaw");
            var to = face - pos; to.y = 0f;
            float desired = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            return $"{label}[{h}] st={Fld<AnimalBehaviorState>(ag, "State")} part={Fld<int>(ag, "Partner")} tier={Fld<SimulationTier>(ag, "Tier")} pos=({pos.x:F3},{pos.z:F3}) yaw={yaw:F1} " +
                   $"tgt=({tgt.x:F3},{tgt.z:F3}) face=({face.x:F3},{face.z:F3}) want={desired:F1} turn={Fld<bool>(ag, "Turn")} arrive={Fld<AnimalBehaviorState>(ag, "Arrive")} " +
                   $"wp={Fld<bool>(ag, "HasWaypoint")} end={Fld<float>(ag, "StateEnd"):F2} clr={s.Clearance(h):F3}";
        }
        private static string Blockers(AnimalScheduler s, int h, float yaw)
        {
            var ag = AgentOf(s, h);
            var body = Fld<BodyShape>(ag, "Body");
            float scale = Fld<float>(ag, "Scale");
            float radius = Fld<float>(ag, "Radius") - AnimalScheduler.ContactTolerance;
            var pos = Fld<Vector3>(ag, "Position");
            body.Segment(pos, yaw, scale, out var s0, out var s1);
            var sb = new System.Text.StringBuilder();
            sb.Append($"   blockers at yaw {yaw:F1}:");
            for (int i = 0; i < s.Count; i++)
            {
                int o = s.GetActiveHandle(i);
                if (o == h) continue;
                s.GetBody(o, out var a0, out var a1, out float r, out _, out _);
                float d = Mathf.Sqrt(CollisionMath.SegmentSegmentDistanceSq(s0, s1, a0, a1)) - radius - r;
                if (d < 0.05f) sb.Append($" animal[{o}] gap={d:F3} st={s.GetState(o)} pos=({s.GetPosition(o).x:F3},{s.GetPosition(o).z:F3}) yaw={s.GetYaw(o):F1};");
            }
            var obs = (Obstacle[])typeof(AnimalScheduler).GetField("_obstacles", NP).GetValue(s);
            int count = (int)typeof(AnimalScheduler).GetField("_obstacleCount", NP).GetValue(s);
            for (int i = 0; i < count; i++)
            {
                if (!obs[i].BlocksWalker(pos.y, Fld<float>(ag, "Height"))) continue;
                float d = Mathf.Sqrt(CollisionMath.SegmentSegmentDistanceSq(s0, s1, obs[i].a, obs[i].b)) - radius - obs[i].radius;
                if (d < 0.05f) sb.Append($" obstacle[{i}] gap={d:F3} kind={obs[i].kind} a={obs[i].a} b={obs[i].b} r={obs[i].radius:F3};");
            }
            bool hasEgg = (bool)typeof(AnimalScheduler).GetField("_hasEggSpot", NP).GetValue(s);
            if (hasEgg)
            {
                var egg = (Vector2)typeof(AnimalScheduler).GetField("_eggSpot", NP).GetValue(s);
                float er = (float)typeof(AnimalScheduler).GetField("_eggRadius", NP).GetValue(s);
                float d = Mathf.Sqrt(CollisionMath.PointSegmentDistanceSq(egg, s0, s1)) - radius - er;
                sb.Append($" egg gap={d:F3};");
            }
            return sb.ToString();
        }
        private static void Log(string line) => Diag.AppendLine($"{Time.frameCount} t={Time.time:F3} {line}");

        private static bool PlaysSocial(AnimalView view)
        {
            var animator = view.Animator;
            return animator.GetCurrentAnimatorStateInfo(0).shortNameHash == AnimalStateHashes.Social
                   || animator.GetNextAnimatorStateInfo(0).shortNameHash == AnimalStateHashes.Social;
        }

        [UnityTest]
        public IEnumerator TwoAnimals_GreetNoseToNose_WithTheFamilyClip()
        {
#if UNITY_EDITOR
            PrepareSave();
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
            float timeout = Time.realtimeSinceStartup + 60f; // a cold first boot compiles shaders and loads every bundle
            while (bootstrap.State == BootState.NotStarted || bootstrap.State == BootState.Running)
            {
                Assert.Less(Time.realtimeSinceStartup, timeout, "Boot timed out.");
                yield return null;
            }
            yield return null;
            yield return null;
            var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
            var refuge = entry.Refuge;
            var scheduler = refuge.Scheduler;
            Assert.AreEqual(entry.StartChunk.InterestCount, scheduler.InterestCount, "The chunk's points of interest reach the scheduler.");
            Assert.Greater(scheduler.InterestCount, 0);

            // Animals are spread over the meadow: any of them that has a free neighbour within reach invites it. A pair whose
            // approach finds no free track gives up (nothing walks through a prop, Collision_Rule.md): then another pair forms.
            int a = -1, b = -1, pairs = 0;
            float until = Time.realtimeSinceStartup + 40f;
            string lastPair = "";
            Diag.Clear();
            AnimalBehaviorState lastStA = AnimalBehaviorState.Idle, lastStB = AnimalBehaviorState.Idle;
            while (a < 0 || scheduler.GetState(a) != AnimalBehaviorState.Social)
            {
                if (a >= 0 && scheduler.GetPartner(a) < 0)
                {
                    lastPair = $"pair {pairs} gave up: A {scheduler.GetState(a)} at {scheduler.GetPosition(a)}, B {scheduler.GetState(b)} at {scheduler.GetPosition(b)}";
                    a = -1;
                }
                if (a < 0)
                {
                    for (int h = 0; h < scheduler.Capacity && a < 0; h++)
                        if (scheduler.IsValid(h) && scheduler.GetPartner(h) < 0 && scheduler.TryStartSocial(h, refuge.SimulationTime))
                            a = h;
                    if (a >= 0)
                    {
                        b = scheduler.GetPartner(a);
                        pairs++;
                        Log($"PAIR {pairs} formed a={a} b={b} dist={Vector3.Distance(scheduler.GetPosition(a), scheduler.GetPosition(b)):F3}");
                        Log(Dump(scheduler, a, "A"));
                        Log(Dump(scheduler, b, "B"));
                        Log("A" + Blockers(scheduler, a, scheduler.GetYaw(a)));
                        Log("B" + Blockers(scheduler, b, scheduler.GetYaw(b)));
                        Log("B+6" + Blockers(scheduler, b, scheduler.GetYaw(b) + 6f));
                        Log("B-6" + Blockers(scheduler, b, scheduler.GetYaw(b) - 6f));
                    }
                }
                if (a >= 0)
                {
                    if (Time.frameCount % 10 == 0 || scheduler.GetState(a) != lastStA || (b >= 0 && scheduler.GetState(b) != lastStB))
                    {
                        Log(Dump(scheduler, a, "A"));
                        if (b >= 0) Log(Dump(scheduler, b, "B"));
                        if (scheduler.GetPartner(a) >= 0 && scheduler.GetPartner(a) != b) Log(Dump(scheduler, scheduler.GetPartner(a), "A's partner"));
                    }
                    lastStA = scheduler.GetState(a);
                    if (b >= 0) lastStB = scheduler.GetState(b);
                }
                if (Time.realtimeSinceStartup >= until)
                    Assert.Fail($"No pair met in 40 s ({pairs} pairs formed; {lastPair}).");
                yield return null;
            }
            Log("A SOCIAL NOW");
            Log(Dump(scheduler, a, "A"));
            if (scheduler.GetPartner(a) >= 0) Log(Dump(scheduler, scheduler.GetPartner(a), "A's partner"));
            Log(Dump(scheduler, b, "B(orig)"));
            Assert.Less(pairs, 6, "Most pairs meet: " + lastPair);
            // The meadow may have paired A anew on its own meanwhile: the partner is the one it greets now.
            b = scheduler.GetPartner(a);
            Assert.GreaterOrEqual(b, 0, "A greets a partner.");
            Assert.AreEqual(AnimalBehaviorState.Social, scheduler.GetState(b));
            // The pair may meet anywhere in the arrival zone: look at it (animators of animals off screen do not run).
            entry.CameraRig.SnapTo((scheduler.GetPosition(a) + scheduler.GetPosition(b)) * 0.5f);
            // Let the crossfade and the last few degrees of turning finish (time, not frames: a fast run has short frames).
            var viewA = ViewOf(scheduler, a);
            var viewB = ViewOf(scheduler, b);
            // Watch the greeting for up to 3 s (time, not frames): each Animator must be seen in the family Social clip, and at
            // some moment both must face each other nose to nose. A short greeting may end before the wait does.
            float settle = Time.realtimeSinceStartup + 3f;
            bool socialA = false, socialB = false;
            float bestA = 180f, bestB = 180f, bestGap = float.MaxValue;
            while (Time.realtimeSinceStartup < settle)
            {
                entry.CameraRig.SnapTo((viewA.transform.position + viewB.transform.position) * 0.5f);
                socialA |= PlaysSocial(viewA);
                socialB |= PlaysSocial(viewB);
                var gap = viewB.transform.position - viewA.transform.position;
                gap.y = 0f;
                float angleA = Vector3.Angle(viewA.transform.forward, gap);
                float angleB = Vector3.Angle(viewB.transform.forward, -gap);
                if (Mathf.Max(angleA, angleB) < Mathf.Max(bestA, bestB))
                {
                    bestA = angleA;
                    bestB = angleB;
                    bestGap = gap.magnitude;
                }
                {
                    Log(Dump(scheduler, a, "A") + $" viewAngle={angleA:F1} viewYaw={viewA.transform.eulerAngles.y:F1} socialAnim={PlaysSocial(viewA)}");
                    Log(Dump(scheduler, b, "B") + $" viewAngle={angleB:F1} viewYaw={viewB.transform.eulerAngles.y:F1} socialAnim={PlaysSocial(viewB)}");
                    float ya = scheduler.GetYaw(a);
                    Log(Blockers(scheduler, a, ya));
                    Log(Blockers(scheduler, a, ya + 6f));
                    Log(Blockers(scheduler, a, ya - 6f));
                    float yb = scheduler.GetYaw(b);
                    Log("B" + Blockers(scheduler, b, yb));
                    Log("B" + Blockers(scheduler, b, yb + 6f));
                    Log("B" + Blockers(scheduler, b, yb - 6f));
                }
                if (socialA && socialB && bestA < 10f && bestB < 10f)
                    break;
                yield return null;
            }
            File.WriteAllText(@"C:\Users\matte\AppData\Local\Temp\claude\C--Users-matte-Desktop-PocketWilds--claude-worktrees-great-leavitt-eeabe9\5b0986b2-9b5d-4643-a577-1966cea33c03\scratchpad\socdiag.txt", Diag.ToString());
            Assert.IsTrue(socialA && socialB, "Both Animators play the family Social clip.");
            Assert.Less(bestA, 10f, "Facing the partner.");
            Assert.Less(bestB, 10f, "Facing the partner.");
            Assert.Less(bestGap, 0.55f, "Noses close.");
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
        }
    }
}
