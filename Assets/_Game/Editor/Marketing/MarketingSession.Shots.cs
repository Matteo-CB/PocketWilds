using System;
using System.Collections;
using System.Collections.Generic;
using PocketWilds.Bootstrap;
using PocketWilds.Core;
using PocketWilds.Presentation;
using PocketWilds.Services.Feedback;
using PocketWilds.Simulation;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.Marketing
{
    /// <summary>
    /// The T01 test video, shot by shot (storyboard Marketing/Videos/T01_test/storyboard.md; 780 frames, 26 s at 30 fps):
    /// <list type="number">
    /// <item>hatch (0 to 3 s): close on the waiting egg, the real hatch, hearts over the newborn;</item>
    /// <item>meadow (3 to 10.5 s): a slow orbit around the liveliest group of the refuge in the golden light;</item>
    /// <item>hide (10.5 to 16.5 s): the real Hide and Seek (up to six games until an animal hides in tall grass), the
    /// peek, the hint, the tap, « coucou » and the heart (two animals playing together when the activity is not in this
    /// build);</item>
    /// <item>cozy (16.5 to 21 s): the clearest resting animal close up (a hedgehog first), the sheet's treat, hearts;</item>
    /// <item>end (21 to 26 s): the game's own 40° view pulling back over the herd, under the title card.</item>
    /// </list>
    /// Between shots nothing is recorded: the game keeps living (a cut in the edit). Cameras are eased orbits around a
    /// target (yaw, pitch, distance, field of view, and where the target sits on the screen), kept above the ground and out
    /// of every collision volume. A portrait frame is narrow (about ±1.2 m at 8 m), so groups are chosen by projecting the
    /// animals into the 9:16 frame and counting those inside the part free of captions and of TikTok's own interface.
    /// </summary>
    internal sealed partial class MarketingSession
    {
        private const float MinClearance = 0.35f;
        private static readonly ContentId Rabbit = ContentId.FromSource("species.rabbit");
        private static readonly ContentId Squirrel = ContentId.FromSource("species.squirrel");
        private static readonly ContentId Hedgehog = ContentId.FromSource("species.hedgehog");

        private readonly List<Obstacle> _obstacles = new List<Obstacle>();
        private int _safetyMoves;
        private bool _probing;

        private struct Agent
        {
            public int Handle;
            public uint Id;
            public Vector3 Position;
            public float Yaw;
            public AnimalBehaviorState State;
            public ContentId Species;
        }

        private IEnumerator Run()
        {
            float until = Time.realtimeSinceStartup + _settings.bootTimeoutSeconds;
            bool checkedSave = false;
            while (true)
            {
                if (_bootstrap == null)
                    _bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
                if (_bootstrap != null && !checkedSave)
                {
                    // Never the owner's save: stop at once if the hand-over did not reach the bootstrap.
                    if (!CheckScratchSave(out string reason))
                    {
                        Fail(reason);
                        yield break;
                    }
                    checkedSave = true;
                }
                if (_bootstrap != null && _bootstrap.State == BootState.Failed)
                {
                    Fail("the boot failed (see the console)");
                    yield break;
                }
                if (_bootstrap != null && _bootstrap.State == BootState.Ready)
                {
                    _entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
                    if (_entry != null && _entry.Refuge != null && _entry.CameraRig != null)
                        break;
                }
                if (Time.realtimeSinceStartup > until)
                {
                    Fail("the boot did not finish in " + _settings.bootTimeoutSeconds + " s");
                    yield break;
                }
                yield return null;
            }
            _context = _bootstrap.Context;
            _refuge = _entry.Refuge;
            _rig = _entry.CameraRig;
            _camera = _rig.Camera;
            Status("preparing", "closing boot screens");
            for (int i = 0; i < 8; i++)
                yield return null;
            yield return ClearScreens();
            Setup();
            Status("preparing", "loading every chunk");
            yield return WaitForChunks();
            // A second of game time for the forced light, the new quality state and the scatter of every chunk.
            for (int i = 0; i < 30; i++)
                yield return null;

            Status("recording", "hatch");
            yield return ShotHatch(90);
            Status("recording", "meadow");
            yield return ShotMeadow(225);
            Status("recording", "hide");
            yield return ShotHide(180);
            Status("recording", "cozy");
            yield return ShotCozy(135);
            Status("recording", "end");
            if (_settings.endCard == "windmill")
                yield return ShotWindmill(150);
            else
                yield return ShotEnd(150);
            if (_safetyMoves > 0)
                Warn($"{_safetyMoves} camera poses were moved out of the ground or of a collision volume");
        }

        // ---------------- 1. Hatch ----------------

        private IEnumerator ShotHatch(int frames)
        {
            float until = Time.realtimeSinceStartup + 10f;
            while ((_refuge.WaitingEgg == null || _refuge.ModalOpen || _refuge.Hatching) && Time.realtimeSinceStartup < until)
                yield return null;
            var egg = _refuge.WaitingEgg;
            if (egg == null)
            {
                Warn("no egg on the pedestal: the hatch shot shows the meadow instead");
                yield return ShotMeadow(frames);
                yield break;
            }
            CollectObstacles();
            var eggPoint = egg.VfxPoint.position;
            var reveal = egg.RevealPoint.position;
            // The newborn faces the way of its reveal point; the camera looks at its face.
            float camYaw = egg.RevealPoint.eulerAngles.y + 180f;
            for (int f = 0; f < 20; f++)
            {
                SetPose(HatchPose(0f, eggPoint, reveal, camYaw, out var settle));
                SetFocus(settle);
                yield return null;
            }

            int start = _timelineFrame;
            int burst = -1;
            bool requested = false, heart = false, crack1 = false, crack2 = false;
            for (int f = 0; f < frames; f++)
            {
                float u = f / (float)(frames - 1);
                SetPose(HatchPose(u, eggPoint, reveal, camYaw, out var target));
                SetFocus(target);
                if (f == 3)
                {
                    _refuge.RequestHatch();
                    requested = true;
                    Event("hatch_request");
                    if (!_refuge.Hatching)
                        Warn("the hatch did not start (egg cost, screen or pool)");
                }
                if (requested)
                {
                    var phase = _refuge.Hatch.Phase;
                    if (!crack1 && phase >= HatchPhase.Cracked1 && phase < HatchPhase.Done)
                    {
                        crack1 = true;
                        Event("hatch_crack1");
                    }
                    if (!crack2 && phase >= HatchPhase.Cracked2 && phase < HatchPhase.Done)
                    {
                        crack2 = true;
                        Event("hatch_crack2");
                    }
                    if (burst < 0 && phase >= HatchPhase.Revealing)
                    {
                        burst = f;
                        Event("hatch_pop");
                    }
                }
                if (_settings.heartsOnHatch && burst >= 0 && !heart && f >= burst + 11)
                {
                    heart = true;
                    var animal = _refuge.Hatch.Animal;
                    var at = animal != null ? animal.transform.position : reveal;
                    _refuge.Vfx?.Play(VfxId.FavoriteHeart, at + new Vector3(0f, 0.1f, 0f), Time.time);
                    Event("hatch_heart");
                }
                Record();
                yield return null;
            }
            LogShot("hatch", string.Empty, start, _log.hatched ?? "egg", $"egg at {eggPoint}, camera yaw {camYaw:F0}");
        }

        /// <summary>A slow push-in and a small orbit; the target drifts from the egg to where the newborn appears.</summary>
        private CameraPose HatchPose(float u, Vector3 egg, Vector3 reveal, float camYaw, out Vector3 target)
        {
            target = Vector3.Lerp(egg + new Vector3(0f, 0.02f, 0f), Vector3.Lerp(egg, reveal, 0.6f) + new Vector3(0f, 0.06f, 0f), Smoother(u));
            float g = Glide(u);
            return Frame(target, camYaw + Mathf.Lerp(-11f, 5f, g), Mathf.Lerp(17f, 12.5f, g), Mathf.Lerp(2.8f, 1.95f, g), 30f, 0.40f);
        }

        // ---------------- 2. Meadow ----------------

        /// <summary>
        /// Where animals may sit on the screen (viewport -1 to 1): clear of TikTok's right column, of its bottom text and of
        /// our caption band at the top.
        /// </summary>
        private static readonly Rect CaptionFreeBox = Rect.MinMaxRect(-0.85f, -0.55f, 0.72f, 0.45f);

        /// <summary>The end card: the herd under the title card.</summary>
        private static readonly Rect UnderTitleBox = Rect.MinMaxRect(-0.85f, -0.6f, 0.72f, 0.0f);

        private static readonly float[] ClusterYaws = { 0f, 15f, -15f, 30f, -30f, 45f, -45f, 60f, -60f };

        private IEnumerator ShotMeadow(int frames)
        {
            CollectObstacles();
            var agents = Agents();
            NoteAgents("meadow", agents);
            var (center, yaw, score) = ChooseCluster(agents, MeadowPose, CaptionFreeBox);
            for (int f = 0; f < 12; f++)
            {
                SetPose(MeadowPose(0f, center, yaw));
                SetFocus(center);
                yield return null;
            }
            int start = _timelineFrame;
            for (int f = 0; f < frames; f++)
            {
                SetPose(MeadowPose(f / (float)(frames - 1), center, yaw));
                SetFocus(center);
                Record();
                yield return null;
            }
            LogShot("meadow", string.Empty, start, $"{agents.Count} animals", $"orbit around {center}, yaw {yaw:F0}, framing score {score:F1}");
        }

        /// <summary>A slow orbit around the liveliest group, a little above the herd: parallax without leaving it.</summary>
        private CameraPose MeadowPose(float u, Vector3 center, float yaw)
        {
            float g = Glide(u);
            float s = Smoother(u);
            return Frame(center, yaw + Mathf.Lerp(-14f, 14f, s), Mathf.Lerp(34f, 31f, s), Mathf.Lerp(5.2f, 5.9f, g), 30f, 0.40f);
        }

        /// <summary>
        /// The group and the angle a shot frames best: every animal (and the herd's middle) is tried as the center, from a few
        /// yaws around the game's own view; a pose scores the animals inside <paramref name="box"/> at its start, middle and
        /// end, minus the animals cut by the frame's edges and the decor in the sight line.
        /// </summary>
        private (Vector3 center, float yaw, float score) ChooseCluster(List<Agent> agents, Func<float, Vector3, float, CameraPose> pose, Rect box)
        {
            var egg = EggSpawn;
            var centers = new List<Vector3>();
            foreach (var agent in agents)
            {
                var sum = Vector3.zero;
                int count = 0;
                foreach (var other in agents)
                    if (Flat(other.Position - agent.Position).magnitude < 1.8f)
                    {
                        sum += other.Position;
                        count++;
                    }
                centers.Add(agent.Position);
                if (count > 1)
                    centers.Add(sum / count);
            }
            if (agents.Count > 0)
                centers.Add(Centroid(agents));
            if (centers.Count == 0)
                centers.Add(egg);
            var bestCenter = centers[0];
            float bestYaw = 0f, best = float.MinValue;
            _probing = true;
            foreach (var raw in centers)
            {
                var center = raw;
                center.y = GroundAt(center.x, center.z, egg.y) + 0.1f;
                foreach (float yaw in ClusterYaws)
                {
                    var a = pose(0f, center, yaw);
                    var m = pose(0.5f, center, yaw);
                    var e = pose(1f, center, yaw);
                    float score = Mathf.Min(FramingScore(a, agents, box), FramingScore(e, agents, box)) + 0.5f * FramingScore(m, agents, box)
                                  - 2f * (Occlusion(a.Position, center) + Occlusion(e.Position, center)) - Mathf.Abs(yaw) * 0.004f;
                    if (score > best)
                    {
                        best = score;
                        bestCenter = center;
                        bestYaw = yaw;
                    }
                }
            }
            _probing = false;
            return (bestCenter, bestYaw, best);
        }

        private const float Aspect = 9f / 16f;

        /// <summary>Viewport position of a world point for a pose (x and y from -1 to 1 on screen, z the depth).</summary>
        private static Vector3 Project(in CameraPose pose, Vector3 world)
        {
            var local = Quaternion.Inverse(pose.Rotation) * (world - pose.Position);
            if (local.z <= 0.05f)
                return new Vector3(9f, 9f, local.z);
            float tan = Mathf.Tan(pose.Fov * 0.5f * Mathf.Deg2Rad);
            return new Vector3(local.x / (local.z * tan * Aspect), local.y / (local.z * tan), local.z);
        }

        /// <summary>Animals inside the box count one; an animal cut by the frame's edge costs half.</summary>
        private static float FramingScore(in CameraPose pose, List<Agent> agents, Rect box)
        {
            float score = 0f;
            foreach (var agent in agents)
            {
                var p = Project(pose, agent.Position + new Vector3(0f, 0.15f, 0f));
                if (p.z < 0.8f)
                    continue;
                if (box.Contains(new Vector2(p.x, p.y)))
                    score += 1f;
                else if (Mathf.Abs(p.x) <= 1.1f && Mathf.Abs(p.y) <= 1.1f)
                    score -= 0.5f;
            }
            return score;
        }

        // ---------------- 3. Hide and Seek ----------------

        private IEnumerator ShotHide(int frames)
        {
            var presenter = _entry.Activities;
            var services = _context.Activities;
            string reason = "disabled in the settings";
            if (!_settings.hideSeek || presenter == null || services == null || !MarketingHideSeekProbe.EnsureStage(presenter, out reason))
            {
                Warn("Hide and Seek unavailable (" + reason + "): two animals play together instead");
                yield return ShotPlay(frames);
                yield break;
            }
            // As the PlayMode test: the activity opens at refuge level 4 with 8 animals; the save opens it directly.
            int slot = ActivityCatalog.SlotOf(ActivityId.HideAndSeek);
            if (slot >= 0)
                services.Activities.Records[slot].Unlocked = true;
            services.RefreshUnlocks();
            // Up to three games (the first free one, then practice games, which take no ticket) until an animal hides in
            // tall grass: the ears over the blades are the shot. The last game is kept whatever its spots.
            MarketingHideSeekProbe probe = null;
            int animal = -1, played = 0;
            var zoneCamera = Vector3.zero;
            for (int attempt = 0; attempt < 6; attempt++)
            {
                // The stage glides the refuge camera to its zone: let it.
                _poseActive = false;
                presenter.StartGame(attempt == 0 ? ActivityRequest.Free(ActivityId.HideAndSeek) : ActivityRequest.Practice(ActivityId.HideAndSeek));
                float until = Time.realtimeSinceStartup + 15f;
                while (!presenter.Playing && Time.realtimeSinceStartup < until)
                    yield return null;
                probe = null;
                if (presenter.Playing)
                    probe = MarketingHideSeekProbe.Bind(presenter.Stages.Acquire(ActivityId.HideAndSeek, null), out reason);
                else
                    reason = "the game did not start";
                if (probe == null)
                    break;
                probe.PollInput = false;
                // The 0.6 s glide to the zone, then its pose is the axis of the close-up (the decor hides the animal from there).
                for (played = 0; played < 27; played++)
                    yield return null;
                zoneCamera = _camera.transform.position;
                animal = ChooseHidden(probe);
                string decor = animal >= 0 ? probe.SpotDecor(probe.AnimalSpot(animal)) : string.Empty;
                Note($"hide attempt {attempt}: {probe.HiddenCount} hidden, filming {animal} in {decor}");
                if ((animal >= 0 && decor == "TallGrass") || attempt == 5)
                    break;
                yield return LeaveActivity();
            }
            if (probe == null || animal < 0)
            {
                Warn("Hide and Seek could not be staged (" + (probe == null ? reason : "no hidden animal") + "): two animals play together instead");
                yield return LeaveActivity();
                yield return ShotPlay(frames);
                yield break;
            }
            int spot = probe.AnimalSpot(animal);
            var view = probe.View(animal);
            var hidden = probe.AnimalPosition(animal);
            // The ears: the head socket of the animal's own model (what peeks over the decor).
            var head = HeadOf(view, hidden + new Vector3(0f, 0.25f, 0f));
            var axis = (head - zoneCamera).normalized;
            // The closest distance along the zone camera's line where no prop (a tree's canopy, a bush) stands between
            // the camera and the decor, nor around the camera itself.
            // When a prop stands on that line (a tree between the zone camera and the spot), the camera swings around the
            // animal by a few degrees: the clump of the decor still hides most of the body.
            CollectPropBounds();
            float near = 3.0f, swing = 0f;
            float[] distances = { 2.8f, 3.2f, 3.6f, 4.0f };
            float[] swings = { 0f, 12f, -12f, 24f, -24f, 36f, -36f };
            float clearest = float.MaxValue;
            foreach (float yaw in swings)
            {
                var swung = Quaternion.Euler(0f, yaw, 0f) * axis;
                foreach (float d in distances)
                {
                    var position = head - swung * d;
                    float blocked = PropBlocked(position, head, 0.72f) + (InsideProp(position, 0.35f) ? 1f : 0f) + Mathf.Abs(yaw) * 0.002f
                                    + (d - 2.8f) * 0.01f;
                    if (blocked < clearest - 0.001f)
                    {
                        clearest = blocked;
                        near = d;
                        swing = yaw;
                    }
                }
                if (clearest < 0.08f)
                    break;
            }
            axis = Quaternion.Euler(0f, swing, 0f) * axis;
            float far = near + 0.6f, after = near + 0.4f;
            Note($"hide: camera distance {near:F1} m, swing {swing:F0} deg (sight line score {clearest:F2})");
            Note($"hide: animal {animal} ({(view != null ? Describe(view.Species, view.OwnerInstanceId) : "?")}) spot {spot} decor {probe.SpotDecor(spot)}, "
                 + $"zone camera {zoneCamera}, hidden {hidden}, head {head}, hint delay {probe.HintDelay:F1}");
            // Recording starts 0.6 s before the hint is due (it comes up to 1.5 s later), the ears already on screen.
            int startAt = Mathf.RoundToInt(_settings.frameRate * Mathf.Max(1.2f, probe.HintDelay - 0.6f));
            var aim = head;
            const float liftHidden = 0.7f, liftFound = 1.1f;
            float lift = liftHidden, liftVelocity = 0f;
            while (played < startAt)
            {
                SetPose(HidePose(head, axis, far, aim, liftHidden));
                SetFocus(head);
                played++;
                yield return null;
            }

            int start = _timelineFrame;
            int hint = -1, tap = -1, found = -1, taps = 0;
            var velocity = Vector3.zero;
            float distance = far, distanceVelocity = 0f;
            float dt = 1f / _settings.frameRate;
            for (int f = 0; f < frames; f++)
            {
                if (hint < 0 && probe.AnimalHinting(animal))
                {
                    hint = f;
                    Event("hide_hint");
                }
                // The tap comes 0.8 s after the hint (the ears moved, the grass rippled), or at 4 s at the latest.
                bool due = tap < 0 && ((hint >= 0 && f >= hint + 24 && f >= 45) || f >= 120);
                bool retry = tap >= 0 && found < 0 && taps < 4 && f - tap >= 8 * taps;
                if ((due || retry) && spot >= 0)
                {
                    probe.TapAt(probe.SpotScreen(spot));
                    if (tap < 0)
                    {
                        tap = f;
                        Event("hide_tap");
                    }
                    taps++;
                }
                if (found < 0 && probe.AnimalFound(animal))
                {
                    found = f;
                    Event("hide_found");
                }
                if (found >= 0 && f == found + 20)
                    Note($"hide: 0.67 s after the find the animal is at {(view != null ? view.transform.position.ToString() : "?")}, "
                         + $"landing {probe.LandingPoint(animal)}");
                // Before the find the camera rests on the ears; after it, it pans with the hop to where the animal lands.
                var live = HeadOf(view, head);
                var wanted = found < 0 ? Vector3.Lerp(head, live, 0.5f) : live;
                float wantedDistance = found < 0 ? Mathf.Lerp(far, near, Glide(Mathf.Min(1f, f / 90f))) : after;
                aim = Vector3.SmoothDamp(aim, wanted, ref velocity, found < 0 ? 0.45f : 0.22f, Mathf.Infinity, dt);
                distance = Mathf.SmoothDamp(distance, wantedDistance, ref distanceVelocity, 0.5f, Mathf.Infinity, dt);
                // Once found, the camera rises a little: the animal stands out over the grass it hid in.
                lift = Mathf.SmoothDamp(lift, found < 0 ? liftHidden : liftFound, ref liftVelocity, 0.6f, Mathf.Infinity, dt);
                SetPose(HidePose(head, axis, distance, aim, lift));
                SetFocus(aim);
                Record();
                yield return null;
            }
            if (found < 0)
                Warn("the hidden animal was not found by the taps");
            LogShot("hide", "hide", start, view != null ? Describe(view.Species, view.OwnerInstanceId) : "animal " + animal,
                $"spot {spot} ({probe.SpotDecor(spot)}), hint {(hint >= 0 ? hint.ToString() : "none")}, tap {tap}, found {found}");
            yield return LeaveActivity();
        }

        /// <summary>The top of an animal's head (its socket), else a point above its root.</summary>
        private static Vector3 HeadOf(AnimalView view, Vector3 fallback)
        {
            if (view == null)
                return fallback;
            return view.SocketHeadTop != null ? view.SocketHeadTop.position : view.transform.position + new Vector3(0f, 0.25f, 0f);
        }

        /// <summary>The camera on the zone camera's line through <paramref name="anchor"/>, aimed at <paramref name="aim"/>.</summary>
        private CameraPose HidePose(Vector3 anchor, Vector3 axis, float distance, Vector3 aim, float lift)
        {
            var position = anchor - axis * distance + new Vector3(0f, lift, 0f);
            float ground = GroundAt(position.x, position.z, anchor.y);
            if (position.y < ground + MinClearance)
                position.y = ground + MinClearance;
            return LookFrom(position, aim, 18f, 0.50f);
        }

        /// <summary>
        /// The hidden animal to film: tall grass first (the ears over the blades, the shot the owner asked for), a rabbit
        /// next (the ears again), then the most visible one.
        /// </summary>
        private static int ChooseHidden(MarketingHideSeekProbe probe)
        {
            int most = probe.MostVisible();
            int best = -1;
            int bestScore = int.MinValue;
            for (int i = 0; i < probe.HiddenCount; i++)
            {
                int spot = probe.AnimalSpot(i);
                if (!probe.IsHidden(i) || spot < 0)
                    continue;
                var view = probe.View(i);
                int score = (probe.SpotDecor(spot) == "TallGrass" ? 4 : 0) + (view != null && view.Species == Rabbit ? 2 : 0) + (i == most ? 1 : 0);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>Back to the refuge during the cut: the game quits, the hub closes, the camera and the animals come back.</summary>
        private IEnumerator LeaveActivity()
        {
            var presenter = _entry.Activities;
            if (presenter == null)
                yield break;
            if (presenter.Playing)
                presenter.QuitActivity();
            float until = Time.realtimeSinceStartup + 6f;
            while (presenter.IsOpen && Time.realtimeSinceStartup < until)
            {
                if (presenter.HubOpen)
                    presenter.CloseActivities();
                yield return null;
            }
            if (presenter.IsOpen)
                Warn("the activities stayed open after Hide and Seek");
            // The camera glides back and the refuge's animals reappear.
            for (int i = 0; i < 45; i++)
                yield return null;
        }

        /// <summary>Fallback for shot 3: the two closest friends of the refuge, side by side, each with a happy hop and a heart.</summary>
        private IEnumerator ShotPlay(int frames)
        {
            CollectObstacles();
            var agents = Agents();
            if (agents.Count == 0)
            {
                Warn("no animal in the refuge: shot 3 shows the meadow");
                yield return ShotMeadow(frames);
                yield break;
            }
            int best = 0, partner = -1;
            float bestScore = float.MaxValue;
            for (int i = 0; i < agents.Count; i++)
            {
                for (int j = i + 1; j < agents.Count; j++)
                {
                    float d = Flat(agents[i].Position - agents[j].Position).magnitude;
                    if (d < 0.45f || d > 1.8f)
                        continue;
                    float score = d - (agents[i].Species != agents[j].Species ? 0.4f : 0f);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = i;
                        partner = j;
                    }
                }
            }
            if (partner < 0)
            {
                best = PreferSpecies(agents, Rabbit);
                partner = -1;
            }
            var a = agents[best];
            var b = partner >= 0 ? agents[partner] : a;
            var pairAxis = Flat(b.Position - a.Position);
            float baseYaw = pairAxis.sqrMagnitude > 0.01f ? Mathf.Atan2(pairAxis.x, pairAxis.z) * Mathf.Rad2Deg + 90f : a.Yaw + 180f;
            if (Mathf.Abs(Mathf.DeltaAngle(baseYaw, 0f)) > 90f)
                baseYaw += 180f;
            var target = (a.Position + b.Position) * 0.5f + new Vector3(0f, 0.12f, 0f);
            float offset = ChooseYawOffset((u, extra) => PlayPose(u, target, baseYaw + extra), new[] { 0f, 20f, -20f, 40f, -40f });
            var velocity = Vector3.zero;
            float dt = 1f / _settings.frameRate;
            for (int f = 0; f < 12; f++)
            {
                SetPose(PlayPose(0f, target, baseYaw + offset));
                SetFocus(target);
                yield return null;
            }
            int start = _timelineFrame;
            for (int f = 0; f < frames; f++)
            {
                var pa = _refuge.Scheduler.GetPosition(a.Handle);
                var pb = _refuge.Scheduler.GetPosition(b.Handle);
                target = Vector3.SmoothDamp(target, (pa + pb) * 0.5f + new Vector3(0f, 0.12f, 0f), ref velocity, 0.45f, Mathf.Infinity, dt);
                SetPose(PlayPose(f / (float)(frames - 1), target, baseYaw + offset));
                SetFocus(target);
                if (f == 40)
                    Cheer(a, "play_happy_a");
                if (f == 64 && partner >= 0)
                    Cheer(b, "play_happy_b");
                Record();
                yield return null;
            }
            LogShot("hide", "play", start, Describe(a.Species, a.Id) + (partner >= 0 ? " + " + Describe(b.Species, b.Id) : string.Empty),
                $"yaw {baseYaw + offset:F0}");
        }

        private CameraPose PlayPose(float u, Vector3 target, float yaw)
        {
            float g = Glide(u);
            return Frame(target, yaw + Mathf.Lerp(-6f, 6f, Smoother(u)), Mathf.Lerp(17f, 13f, g), Mathf.Lerp(2.6f, 2.1f, g), 30f, 0.42f);
        }

        /// <summary>A touch as the player gives one (a happy hop and its voice), with the favorite hearts.</summary>
        private void Cheer(in Agent agent, string name)
        {
            var position = _refuge.Scheduler.GetPosition(agent.Handle);
            _refuge.Scheduler.Poke(agent.Handle, Time.time);
            _refuge.Vfx?.Play(VfxId.FavoriteHeart, position + new Vector3(0f, 0.2f, 0f), Time.time);
            _context.Audio.Play(AudioEventId.AnimalHappy, position + new Vector3(0f, 0.2f, 0f), agent.Species);
            Event(name);
        }

        // ---------------- 4. Cozy close-up ----------------

        private IEnumerator ShotCozy(int frames)
        {
            CollectObstacles();
            // The subject and the angle are scored together: a clear sight line through the decor first, a hedgehog then
            // a squirrel then a rabbit, nibbling or resting (never walking off), seen from the front.
            float[] offsets = { 0f, 30f, -30f, 55f, -55f, 80f, -80f, 120f, -120f };
            Agent subject = default;
            float yawOffset = 0f, best = float.MaxValue;
            bool found = false;
            for (int wait = 0; wait < 75 && !found; wait++)
            {
                var agents = Agents();
                if (wait == 0)
                    NoteAgents("cozy", agents);
                best = float.MaxValue;
                foreach (var agent in agents)
                {
                    float species = agent.Species == Hedgehog ? 0f : agent.Species == Squirrel ? 0.35f : 0.5f;
                    float state = agent.State == AnimalBehaviorState.Eat ? 0f
                        : agent.State == AnimalBehaviorState.Idle || agent.State == AnimalBehaviorState.IdleLook || agent.State == AnimalBehaviorState.Inspect
                          ? 0.2f : -1f;
                    if (state < 0f)
                        continue;
                    var target = agent.Position + new Vector3(0f, 0.12f, 0f);
                    foreach (float offset in offsets)
                    {
                        float score = species + state + Mathf.Abs(offset) / 400f + ViewScore(target, agent.Yaw + 180f + offset);
                        if (score < best)
                        {
                            best = score;
                            subject = agent;
                            yawOffset = offset;
                        }
                    }
                }
                // A good pick at once; a fair one after a moment; anything resting at the end of the wait.
                found = best < 0.45f || (wait > 30 && best < 1.2f) || (wait == 74 && best < float.MaxValue);
                if (!found)
                    yield return null;
            }
            if (!found)
            {
                Warn("no animal for the close-up: shot 4 shows the meadow");
                yield return ShotMeadow(frames);
                yield break;
            }
            var scheduler = _refuge.Scheduler;
            var aim = scheduler.GetPosition(subject.Handle) + new Vector3(0f, 0.12f, 0f);
            float yaw = subject.Yaw + 180f + yawOffset;
            Note($"cozy: {Describe(subject.Species, subject.Id)} {subject.State} at {subject.Position}, yaw {yaw:F0}, score {best:F2}");
            var velocity = Vector3.zero;
            float dt = 1f / _settings.frameRate;
            for (int f = 0; f < 10; f++)
            {
                SetPose(CozyPose(0f, aim, yaw));
                SetFocus(aim);
                yield return null;
            }
            int start = _timelineFrame;
            int moves = _safetyMoves;
            bool treated = false;
            for (int f = 0; f < frames; f++)
            {
                if (scheduler.IsValid(subject.Handle))
                    aim = Vector3.SmoothDamp(aim, scheduler.GetPosition(subject.Handle) + new Vector3(0f, 0.12f, 0f), ref velocity, 0.4f,
                        Mathf.Infinity, dt);
                SetPose(CozyPose(f / (float)(frames - 1), aim, yaw));
                SetFocus(aim);
                if (f == 36)
                    treated = GiveTreat(subject);
                Record();
                yield return null;
            }
            LogShot("cozy", treated ? "treat" : "cheer", start, Describe(subject.Species, subject.Id) + " " + subject.State,
                $"yaw {yaw:F0}, score {best:F2}, safety moves {_safetyMoves - moves}");
        }

        /// <summary>How badly a close-up from this yaw would see its target: decor in the sight line, the camera in a volume.</summary>
        private float ViewScore(Vector3 target, float yaw)
        {
            _probing = true;
            float score = 0f;
            for (int k = 0; k <= 1; k++)
            {
                var pose = CozyPose(k, target, yaw);
                score += 3f * Occlusion(pose.Position, target) + (Inside(pose.Position, 0.08f) ? 2f : 0f);
            }
            _probing = false;
            return score;
        }

        private CameraPose CozyPose(float u, Vector3 target, float yaw)
        {
            float g = Glide(u);
            return Frame(target, yaw + Mathf.Lerp(0f, -6f, Smoother(u)), Mathf.Lerp(41f, 37f, g), Mathf.Lerp(2.9f, 2.5f, g), 30f, 0.27f);
        }

        /// <summary>The sheet's treat, given as the player gives it (hearts, the eat sound, a happy hop); a cheer if none is owned.</summary>
        private bool GiveTreat(in Agent subject)
        {
            var daily = _context.Daily;
            if (daily != null && daily.Bond.BestTreatFor(subject.Id).IsValid)
            {
                _refuge.GiveTreat(subject.Id);
                Event("cozy_treat");
                return true;
            }
            Cheer(subject, "cozy_cheer");
            return false;
        }

        // ---------------- 5. End card ----------------

        private IEnumerator ShotEnd(int frames)
        {
            CollectObstacles();
            var agents = Agents();
            NoteAgents("end", agents);
            var (center, yaw, score) = ChooseCluster(agents, EndPose, UnderTitleBox);
            for (int f = 0; f < 12; f++)
            {
                SetPose(EndPose(0f, center, yaw));
                SetFocus(center);
                yield return null;
            }
            int start = _timelineFrame;
            for (int f = 0; f < frames; f++)
            {
                SetPose(EndPose(f / (float)(frames - 1), center, yaw));
                SetFocus(center);
                Record();
                yield return null;
            }
            LogShot("end", "refuge", start, $"{agents.Count} animals", $"center {center}, yaw {yaw:F0}, framing score {score:F1}");
        }

        /// <summary>The game's own diorama view (40°), slowly pulling back: the herd in the lower half, the title card above.</summary>
        private CameraPose EndPose(float u, Vector3 center, float yaw)
        {
            float g = Glide(u);
            return Frame(center, yaw + Mathf.Lerp(4f, -4f, Smoother(u)), Mathf.Lerp(38f, 42f, g), Mathf.Lerp(7.0f, 9.5f, g), 30f, 0.30f);
        }

        private IEnumerator ShotWindmill(int frames)
        {
            Transform windmill = null;
            var root = _entry.Streamer != null ? _entry.Streamer.Root : null;
            if (root != null)
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name.StartsWith("PF_GL_Landmark_Windmill", StringComparison.Ordinal))
                    {
                        windmill = t;
                        break;
                    }
            if (windmill == null)
            {
                Warn("no windmill in the streamed chunks: the end card rises over the refuge");
                yield return ShotEnd(frames);
                yield break;
            }
            var target = windmill.position + new Vector3(0f, 2.0f, 0f);
            for (int f = 0; f < 24; f++)
            {
                SetPose(WindmillPose(0f, target));
                SetFocus(windmill.position);
                yield return null;
            }
            CollectObstacles();
            int start = _timelineFrame;
            for (int f = 0; f < frames; f++)
            {
                SetPose(WindmillPose(f / (float)(frames - 1), target));
                SetFocus(windmill.position);
                Record();
                yield return null;
            }
            LogShot("end", "windmill", start, windmill.name, $"windmill at {windmill.position}");
        }

        private CameraPose WindmillPose(float u, Vector3 target)
        {
            float g = Glide(u);
            return Frame(target, Mathf.Lerp(18f, 10f, Smoother(u)), Mathf.Lerp(8f, 6f, g), Mathf.Lerp(13f, 11f, g), 34f, 0.38f);
        }

        // ---------------- Helpers ----------------

        private Vector3 EggSpawn => _entry.StartChunk != null && _entry.StartChunk.EggSpawn != null ? _entry.StartChunk.EggSpawn.position : Vector3.zero;

        private void LogShot(string name, string variant, int start, string subject, string notes)
        {
            var shot = new ShotLog { name = name, variant = variant, start = start, end = _timelineFrame - 1, subject = subject, notes = notes };
            _log.shots.Add(shot);
            WriteLog();
            Debug.Log($"[MarketingCapture] Shot {name} ({variant}) frames {shot.start}-{shot.end}: {subject}; {notes}");
        }

        private void NoteAgents(string shot, List<Agent> agents)
        {
            var text = new System.Text.StringBuilder(shot + " animals:");
            foreach (var agent in agents)
                text.Append($" [{MarketingCapture.Name(_context.Content, agent.Species).Replace("species.", string.Empty).Replace(".name", string.Empty)} "
                            + $"{agent.State} ({agent.Position.x:F1}, {agent.Position.z:F1})]");
            Note(text.ToString());
        }

        private string Describe(ContentId species, uint instance)
        {
            string name = MarketingCapture.Name(_context.Content, species);
            if (_context.State.Collection.TryGet(instance, out var animal))
                name += " / " + MarketingCapture.VariantName(_context.Content, animal.Variant);
            return name + " #" + instance;
        }

        private List<Agent> Agents()
        {
            var list = new List<Agent>();
            var scheduler = _refuge.Scheduler;
            var collection = _context.State.Collection;
            for (int i = 0; i < scheduler.Count; i++)
            {
                int handle = scheduler.GetActiveHandle(i);
                if (!scheduler.IsValid(handle) || scheduler.IsEmerging(handle))
                    continue;
                uint id = scheduler.GetInstanceId(handle);
                list.Add(new Agent
                {
                    Handle = handle,
                    Id = id,
                    Position = scheduler.GetPosition(handle),
                    Yaw = scheduler.GetYaw(handle),
                    State = scheduler.GetState(handle),
                    Species = collection.TryGet(id, out var animal) ? animal.Species : ContentId.None,
                });
            }
            return list;
        }

        /// <summary>An animal of the species resting in view: eating (or, when not required, idle or looking around).</summary>
        private bool TryPick(List<Agent> agents, ContentId species, bool eating, out Agent picked)
        {
            picked = default;
            var egg = EggSpawn;
            float best = float.MaxValue;
            bool any = false;
            foreach (var agent in agents)
            {
                if (agent.Species != species)
                    continue;
                bool ok = eating ? agent.State == AnimalBehaviorState.Eat
                    : agent.State == AnimalBehaviorState.Eat || agent.State == AnimalBehaviorState.Idle || agent.State == AnimalBehaviorState.IdleLook
                      || agent.State == AnimalBehaviorState.Inspect;
                if (!ok)
                    continue;
                float score = Flat(agent.Position - egg).magnitude;
                if (score < best)
                {
                    best = score;
                    picked = agent;
                    any = true;
                }
            }
            return any;
        }

        private static int PreferSpecies(List<Agent> agents, ContentId species)
        {
            for (int i = 0; i < agents.Count; i++)
                if (agents[i].Species == species)
                    return i;
            return 0;
        }

        private static Vector3 Centroid(List<Agent> agents)
        {
            var sum = Vector3.zero;
            foreach (var agent in agents)
                sum += agent.Position;
            return agents.Count > 0 ? sum / agents.Count : Vector3.zero;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private static float Smoother(float u)
        {
            u = Mathf.Clamp01(u);
            return u * u * u * (u * (6f * u - 15f) + 10f);
        }

        /// <summary>Eased, but already moving at the cut (a glide, not a start from rest).</summary>
        private static float Glide(float u) => Mathf.Lerp(Mathf.Clamp01(u), Smoother(u), 0.7f);

        private static Vector3 CatmullRom(Vector3 a, Vector3 b, Vector3 c, float u)
        {
            if (u <= 0.5f)
                return CatmullRom(a, a, b, c, u * 2f);
            return CatmullRom(a, b, c, c, (u - 0.5f) * 2f);
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3);
        }

        /// <summary>
        /// The camera on an orbit around <paramref name="target"/>, then tilted so the target sits at <paramref name="frameV"/>
        /// of the screen height (0 bottom, 1 top). Kept above the ground and out of the collision volumes (rule « aucune
        /// collision »: the camera never enters a decor either).
        /// </summary>
        private CameraPose Frame(Vector3 target, float yaw, float pitch, float distance, float fov, float frameV)
        {
            var orbit = Quaternion.Euler(pitch, yaw, 0f);
            var position = target - orbit * Vector3.forward * distance;
            bool moved = false;
            float ground = GroundAt(position.x, position.z, target.y);
            if (position.y < ground + MinClearance)
            {
                position.y = ground + MinClearance;
                moved = true;
            }
            for (int k = 0; k < 16 && Inside(position, 0.06f); k++)
            {
                position = Vector3.Lerp(position, target, 0.07f);
                moved = true;
            }
            if (moved)
            {
                if (!_probing)
                    _safetyMoves++;
                return LookFrom(position, target, fov, frameV);
            }
            return new CameraPose(position, Quaternion.Euler(pitch - FrameTilt(fov, frameV), yaw, 0f), fov);
        }

        private static CameraPose LookFrom(Vector3 position, Vector3 target, float fov, float frameV)
        {
            var direction = target - position;
            if (direction.sqrMagnitude < 1e-6f)
                direction = Vector3.forward;
            var angles = Quaternion.LookRotation(direction.normalized, Vector3.up).eulerAngles;
            return new CameraPose(position, Quaternion.Euler(angles.x - FrameTilt(fov, frameV), angles.y, 0f), fov);
        }

        /// <summary>Degrees to tilt up so a point on the view axis shows at <paramref name="frameV"/> of the screen height.</summary>
        private static float FrameTilt(float fov, float frameV) =>
            Mathf.Atan((0.5f - frameV) * 2f * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad)) * Mathf.Rad2Deg;

        /// <summary>The yaw offset whose path (start, middle, end) sees its target best through the decor.</summary>
        private float ChooseYawOffset(Func<float, float, CameraPose> pose, float[] offsets)
        {
            float best = offsets[0];
            float bestScore = float.MaxValue;
            _probing = true;
            foreach (var offset in offsets)
            {
                float score = 0f;
                for (int k = 0; k <= 2; k++)
                {
                    float u = k * 0.5f;
                    var p = pose(u, offset);
                    var forward = p.Rotation * Vector3.forward;
                    // The aimed point: the view axis at the orbit distance is close enough for a visibility test.
                    var aim = p.Position + forward * 2.5f;
                    score += Occlusion(p.Position, aim) + (Inside(p.Position, 0.06f) ? 1f : 0f);
                }
                score += Mathf.Abs(offset) * 0.001f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = offset;
                }
            }
            _probing = false;
            return best;
        }

        private readonly List<Bounds> _props = new List<Bounds>();

        /// <summary>
        /// The drawn props of the loaded chunks (their renderer boxes): canopies and bushes reach higher than the walkers'
        /// collision volumes. The ground, the water and the paths are left out (their boxes cover everything).
        /// </summary>
        private void CollectPropBounds()
        {
            _props.Clear();
            var root = _entry.Streamer != null ? _entry.Streamer.Root : null;
            if (root == null)
                return;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(false))
            {
                if (!renderer.enabled || renderer is ParticleSystemRenderer)
                    continue;
                string name = renderer.name;
                if (name.Contains("Ground") || name.Contains("Water") || name.Contains("Stream") || name.Contains("Path") || name.Contains("Pond"))
                    continue;
                var bounds = renderer.bounds;
                if (bounds.size.x > 8f || bounds.size.z > 8f)
                    continue;
                _props.Add(bounds);
            }
        }

        private bool InsideProp(Vector3 point, float margin)
        {
            foreach (var bounds in _props)
            {
                var b = bounds;
                b.Expand(margin * 2f);
                if (b.Contains(point))
                    return true;
            }
            return false;
        }

        /// <summary>Share of the first <paramref name="upTo"/> of a sight line that runs through a prop's box.</summary>
        private float PropBlocked(Vector3 from, Vector3 to, float upTo)
        {
            const int samples = 24;
            int hits = 0;
            for (int k = 1; k <= samples; k++)
                if (InsideProp(Vector3.Lerp(from, to, k / (float)samples * upTo), 0.02f))
                    hits++;
            return hits / (float)samples;
        }

        private void CollectObstacles()
        {
            _obstacles.Clear();
            var streamer = _entry.Streamer;
            if (streamer == null)
                return;
            for (int i = 0; i < streamer.Count; i++)
            {
                if (!streamer.IsLoaded(i))
                    continue;
                var chunk = streamer.GetChunk(i);
                if (chunk != null)
                    _obstacles.AddRange(chunk.GetWorldObstacles());
            }
        }

        private bool Inside(Vector3 point, float margin)
        {
            var q = new Vector2(point.x, point.z);
            foreach (var o in _obstacles)
            {
                if (point.y < o.WorldBottom - margin || point.y > o.WorldTop + margin)
                    continue;
                if (DistanceToSegment(q, o.a, o.b) < o.radius + margin)
                    return true;
            }
            return false;
        }

        /// <summary>Share of the sight line (its first 82 %, short of the subject) that runs through a collision volume.</summary>
        private float Occlusion(Vector3 from, Vector3 to)
        {
            const int samples = 20;
            int hits = 0;
            for (int k = 1; k <= samples; k++)
                if (Inside(Vector3.Lerp(from, to, k / (float)samples * 0.82f), 0f))
                    hits++;
            return hits / (float)samples;
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return (p - (a + ab * t)).magnitude;
        }

        private float GroundAt(float x, float z, float fallback)
        {
            var streamer = _entry.Streamer;
            if (streamer == null)
                return fallback;
            var point = new Vector2(x, z);
            for (int i = 0; i < streamer.Count; i++)
            {
                if (!streamer.IsLoaded(i) || !streamer.GetArea(i).Contains(point))
                    continue;
                var chunk = streamer.GetChunk(i);
                if (chunk != null)
                    return chunk.Ground.HeightAt(x, z);
            }
            return fallback;
        }
    }
}
