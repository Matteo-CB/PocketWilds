using System.Collections;
using System.Collections.Generic;
using PocketWilds.Core;
using PocketWilds.Presentation;
using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.Editor.Marketing
{
    /// <summary>
    /// T02 (storyboard Marketing/Videos/T02/storyboard.md): many short real moments, recorded a little longer than the edit
    /// keeps (the edit cuts them on the beat of Hurried Paws). In the order the game state allows: the single Rare hatch
    /// first (the save's first egg), the meadow's own life (a greeting, a treat, a nap in the evening light, a shower and its
    /// rainbow), Hide and Seek, Grooming on its own set, the × 10 hatch, the next zone opening, and a last push down to the
    /// new egg for a clean loop back to the first frame.
    /// </summary>
    internal sealed partial class MarketingSession
    {
        private static readonly ContentId Grassland = ContentId.FromSource("biome.grassland");

        /// <summary>Weather held by PoseFix every frame (a shot may ask for rain or the rainbow; clear otherwise).</summary>
        private float _rain, _rainbow;

        private IEnumerator RunT02()
        {
            Status("recording", "hatch");
            yield return ShotHatch(96);
            Status("recording", "social");
            yield return ShotSocial(130);
            Status("recording", "meadow");
            yield return ShotMeadow(100);
            Status("recording", "cozy");
            yield return ShotCozy(110);
            Status("recording", "sleepy");
            yield return ShotSleepy(100);
            Status("recording", "hide");
            yield return ShotHide(180);
            Status("recording", "grooming");
            yield return ShotGrooming(110, 110);
            Status("recording", "burst");
            yield return ShotBurst(210);
            Status("recording", "zone");
            yield return ShotZone(170);
            Status("recording", "end");
            yield return ShotEndLoop(170);
            if (_safetyMoves > 0)
                Warn($"{_safetyMoves} camera poses were moved out of the ground or of a collision volume");
        }

        // ---------------- A greeting, nose to nose ----------------

        private IEnumerator ShotSocial(int frames)
        {
            CollectObstacles();
            var scheduler = _refuge.Scheduler;
            var agents = Agents();
            NoteAgents("social", agents);
            int partner = -1;
            Agent first = default;
            // Two awake animals a little apart, different species first: the one the scheduler pairs is filmed.
            var pairs = new List<(float score, int a)>();
            for (int i = 0; i < agents.Count; i++)
            {
                if (agents[i].State == AnimalBehaviorState.Sleep)
                    continue;
                float best = float.MaxValue;
                for (int j = 0; j < agents.Count; j++)
                {
                    if (i == j || agents[j].State == AnimalBehaviorState.Sleep)
                        continue;
                    float d = Flat(agents[i].Position - agents[j].Position).magnitude;
                    if (d < 0.8f || d > 2.8f)
                        continue;
                    best = Mathf.Min(best, Mathf.Abs(d - 1.6f) - (agents[i].Species != agents[j].Species ? 0.5f : 0f));
                }
                if (best < float.MaxValue)
                    pairs.Add((best, i));
            }
            pairs.Sort((x, y) => x.score.CompareTo(y.score));
            foreach (var (_, index) in pairs)
            {
                if (!scheduler.TryStartSocial(agents[index].Handle, Time.time))
                    continue;
                partner = scheduler.GetPartner(agents[index].Handle);
                if (partner >= 0)
                {
                    first = agents[index];
                    break;
                }
            }
            if (partner < 0)
            {
                Warn("no greeting could start: the meadow instead");
                yield return ShotMeadow(frames);
                yield break;
            }
            Event("social_start");
            var pa = scheduler.GetPosition(first.Handle);
            var pb = scheduler.GetPosition(partner);
            var axis = Flat(pb - pa);
            float yaw = axis.sqrMagnitude > 0.01f ? Mathf.Atan2(axis.x, axis.z) * Mathf.Rad2Deg + 90f : 0f;
            if (Mathf.Abs(Mathf.DeltaAngle(yaw, 0f)) > 90f)
                yaw += 180f;
            var target = (pa + pb) * 0.5f + new Vector3(0f, 0.12f, 0f);
            CollectPropBounds();
            float bestSide = float.MaxValue, chosen = yaw;
            foreach (float side in new[] { yaw, yaw + 180f, yaw + 25f, yaw - 25f, yaw + 155f, yaw + 205f })
            {
                float blocked = ViewScore(target, side) + Mathf.Abs(Mathf.DeltaAngle(side, 0f)) * 0.002f;
                if (blocked < bestSide)
                {
                    bestSide = blocked;
                    chosen = side;
                }
            }
            yaw = chosen;
            var velocity = Vector3.zero;
            float dt = 1f / _settings.frameRate;
            int start = _timelineFrame;
            bool greeted = false;
            for (int f = 0; f < frames; f++)
            {
                if (scheduler.IsValid(first.Handle) && scheduler.IsValid(partner))
                    target = Vector3.SmoothDamp(target, (scheduler.GetPosition(first.Handle) + scheduler.GetPosition(partner)) * 0.5f
                        + new Vector3(0f, 0.12f, 0f), ref velocity, 0.45f, Mathf.Infinity, dt);
                if (!greeted && scheduler.IsValid(first.Handle) && scheduler.GetState(first.Handle) == AnimalBehaviorState.Social)
                {
                    greeted = true;
                    Event("social_greet");
                }
                float u = f / (float)(frames - 1);
                float g = Glide(u);
                SetPose(Frame(target, yaw + Mathf.Lerp(-8f, 8f, Smoother(u)), Mathf.Lerp(30f, 26f, g), Mathf.Lerp(3.0f, 2.4f, g), 30f, 0.36f));
                SetFocus(target);
                Record();
                yield return null;
            }
            LogShot("social", greeted ? "greet" : "walk", start, Describe(first.Species, first.Id), $"partner handle {partner}, yaw {yaw:F0}");
        }

        // ---------------- A nap in the evening light ----------------

        private IEnumerator ShotSleepy(int frames)
        {
            _entry.Atmosphere?.ForceHour(19.4f);
            for (int i = 0; i < 6; i++)
                yield return null;
            CollectObstacles();
            CollectPropBounds();
            var agents = Agents();
            Agent subject = default;
            float best = float.MaxValue, yawOffset = 0f;
            float[] offsets = { 0f, 30f, -30f, 60f, -60f, 100f, -100f };
            foreach (var agent in agents)
            {
                float state = agent.State == AnimalBehaviorState.Sleep ? 0f : -1f;
                if (state < 0f)
                    continue;
                var target = agent.Position + new Vector3(0f, 0.12f, 0f);
                foreach (float offset in offsets)
                {
                    float score = state + Mathf.Abs(offset) / 400f + ViewScore(target, agent.Yaw + 180f + offset);
                    if (score < best)
                    {
                        best = score;
                        subject = agent;
                        yawOffset = offset;
                    }
                }
            }
            if (best == float.MaxValue)
            {
                Warn("no sleeper: the meadow in the evening instead");
                yield return ShotMeadow(frames);
                _entry.Atmosphere?.ForceHour(_settings.hour);
                yield break;
            }
            var aim = subject.Position + new Vector3(0f, 0.1f, 0f);
            float yaw = subject.Yaw + 180f + yawOffset;
            int start = _timelineFrame;
            for (int f = 0; f < frames; f++)
            {
                float u = f / (float)(frames - 1);
                float g = Glide(u);
                SetPose(Frame(aim, yaw + Mathf.Lerp(6f, -6f, Smoother(u)), Mathf.Lerp(34f, 30f, g), Mathf.Lerp(2.3f, 1.9f, g), 30f, 0.33f));
                SetFocus(aim);
                Record();
                yield return null;
            }
            LogShot("sleepy", subject.State.ToString(), start, Describe(subject.Species, subject.Id), $"yaw {yaw:F0}, score {best:F2}");
            _entry.Atmosphere?.ForceHour(_settings.hour);
            for (int i = 0; i < 6; i++)
                yield return null;
        }

        // ---------------- A shower, then its rainbow ----------------

        private IEnumerator ShotRain(int rainFrames, int rainbowFrames)
        {
            CollectObstacles();
            var agents = Agents();
            _rain = 1f;
            _rainbow = 0f;
            for (int i = 0; i < 20; i++)
                yield return null;
            var (center, yaw, score) = ChooseCluster(agents, MeadowPose, CaptionFreeBox);
            int start = _timelineFrame;
            for (int f = 0; f < rainFrames; f++)
            {
                SetPose(MeadowPose(f / (float)(rainFrames - 1), center, yaw));
                SetFocus(center);
                Record();
                yield return null;
            }
            LogShot("rain", string.Empty, start, $"{agents.Count} animals", $"center {center}, yaw {yaw:F0}, score {score:F1}");

            _rain = 0f;
            _rainbow = 1f;
            for (int i = 0; i < 20; i++)
                yield return null;
            start = _timelineFrame;
            var egg = EggSpawn;
            for (int f = 0; f < rainbowFrames; f++)
            {
                float u = f / (float)(rainbowFrames - 1);
                float g = Glide(u);
                // Low over the meadow, looking up a little: the arc in the sky above the herd.
                SetPose(Frame(center, yaw + Mathf.Lerp(-6f, 6f, Smoother(u)), Mathf.Lerp(14f, 18f, g), Mathf.Lerp(7.5f, 6.5f, g), 34f, 0.22f));
                SetFocus(center);
                Record();
                yield return null;
            }
            LogShot("rainbow", string.Empty, start, "sky", $"egg {egg}");
            _rainbow = 0f;
        }

        // ---------------- Grooming, on its own set ----------------

        private IEnumerator ShotGrooming(int brushFrames, int bowFrames)
        {
            var presenter = _entry.Activities;
            var services = _context.Activities;
            if (presenter == null || services == null || presenter.Stages == null)
            {
                Warn("no activities: grooming skipped");
                yield break;
            }
            if (presenter.Stages is ActivityStageRegistry registry && !registry.Has(ActivityId.Grooming))
                GroomingStage.Register(registry);
            int slot = ActivityCatalog.SlotOf(ActivityId.Grooming);
            if (slot >= 0)
                services.Activities.Records[slot].Unlocked = true;
            services.RefreshUnlocks();
            _poseActive = false;
            presenter.StartGame(ActivityRequest.Free(ActivityId.Grooming));
            float until = Time.realtimeSinceStartup + 15f;
            while (!presenter.Playing && Time.realtimeSinceStartup < until)
                yield return null;
            var stage = presenter.Playing ? presenter.Stages.Acquire(ActivityId.Grooming, null) as GroomingStage : null;
            if (stage == null || stage.Camera == null || stage.Animal == null)
            {
                Warn("grooming could not start");
                yield return LeaveActivity();
                yield break;
            }
            stage.PollInput = false;
            var game = stage.Game;
            // The set has its own camera: the frames come from it while the refuge's camera rests.
            _recorder.Detach();
            _recorder.Attach(stage.Camera);
            for (int i = 0; i < 20; i++)
                yield return null;

            // Brush strokes over the dirty coat: the brush follows the finger, the foam rises, the dust fades.
            var zones = new List<int>();
            for (int z = 0; z < game.ZoneCount; z++)
                if (game.IsDirty(z))
                    zones.Add(z);
            zones.Sort((a, b) => stage.Animal.Screen[b].y.CompareTo(stage.Animal.Screen[a].y));
            int start = _timelineFrame;
            int zoneIndex = -1, strokeFrame = 0;
            for (int f = 0; f < brushFrames; f++)
            {
                if (zones.Count > 0 && (zoneIndex < 0 || strokeFrame >= 26))
                {
                    if (zoneIndex >= 0)
                        stage.PointerUp();
                    zoneIndex = (zoneIndex + 1) % zones.Count;
                    strokeFrame = 0;
                    int z = zones[zoneIndex];
                    stage.PointerDown(stage.Animal.Screen[z] - new Vector2(0.8f * stage.Animal.ScreenRadii[z], 0f));
                    if (f == 0)
                        Event("groom_brush");
                }
                if (zoneIndex >= 0)
                {
                    int z = zones[zoneIndex];
                    float swing = Mathf.Sin(strokeFrame / 26f * Mathf.PI * 4f);
                    stage.PointerMove(stage.Animal.Screen[z] + new Vector2(0.8f * swing * stage.Animal.ScreenRadii[z], 0.25f * stage.Animal.ScreenRadii[z] * Mathf.Cos(strokeFrame * 0.5f)));
                    strokeFrame++;
                }
                Record();
                yield return null;
            }
            stage.PointerUp();
            LogShot("groom_brush", string.Empty, start, "grooming", $"{zones.Count} dirty zones");

            // During the cut: every zone and every leaf, as a quick player would.
            until = Time.realtimeSinceStartup + 25f;
            while (game.Phase == GroomingPhase.Brushing && presenter.Playing && Time.realtimeSinceStartup < until)
            {
                for (int z = 0; z < game.ZoneCount; z++)
                {
                    if (!game.IsDirty(z))
                        continue;
                    var c = stage.Animal.Screen[z];
                    float r = stage.Animal.ScreenRadii[z];
                    stage.PointerDown(c - new Vector2(0.75f * r, 0f));
                    for (int k = 0; k < 4; k++)
                        stage.PointerMove(c + new Vector2((k & 1) == 0 ? 0.75f * r : -0.75f * r, 0f));
                    stage.PointerUp();
                    break;
                }
                for (int i = 0; i < game.DebrisCount; i++)
                    if (game.DebrisActive(i) && stage.DebrisShown(i))
                    {
                        stage.TapAt(stage.DebrisScreen(i));
                        break;
                    }
                yield return null;
            }
            // The bows wait: the pink one flies onto the head.
            until = Time.realtimeSinceStartup + 5f;
            while (!stage.PickerShown && presenter.Playing && Time.realtimeSinceStartup < until)
                yield return null;
            start = _timelineFrame;
            bool picked = false;
            for (int f = 0; f < bowFrames; f++)
            {
                if (f == 12 && stage.PickerShown)
                {
                    stage.Pick((int)GroomingBow.Pink);
                    picked = true;
                    Event("groom_bow");
                }
                Record();
                yield return null;
            }
            LogShot("groom_bow", picked ? "bow" : "none", start, "grooming", $"bow on head {stage.BowOnHead}");
            _recorder.Detach();
            _recorder.Attach(_camera);
            until = Time.realtimeSinceStartup + 6f;
            while (presenter.InGame && !presenter.Playing && Time.realtimeSinceStartup < until)
            {
                presenter.BackToHub();
                yield return null;
            }
            yield return LeaveActivity();
            if (_entry.Activities != null && _entry.Activities.HubOpen)
                _entry.Activities.CloseActivities();
            for (int i = 0; i < 20; i++)
                yield return null;
        }

        // ---------------- × 10 ----------------

        private IEnumerator ShotBurst(int frames)
        {
            for (int k = 0; k < 3 && _refuge.Choice.Quantity != 10; k++)
            {
                _refuge.CycleHatchQuantity();
                yield return null;
            }
            Note($"burst: quantity {_refuge.Choice.Quantity} (max {_refuge.Choice.MaxQuantity})");
            float until = Time.realtimeSinceStartup + 12f;
            while ((_refuge.WaitingEgg == null || _refuge.ModalOpen || _refuge.Hatching) && Time.realtimeSinceStartup < until)
            {
                if (_refuge.SummaryOpen)
                    _refuge.CloseSummary();
                yield return null;
            }
            CollectObstacles();
            var egg = EggSpawn + new Vector3(0f, 0.15f, 0f);
            for (int f = 0; f < 10; f++)
            {
                SetPose(BurstPose(0f, egg, false));
                SetFocus(egg);
                yield return null;
            }
            _refuge.RequestHatch();
            Event("burst_request");
            // The group waits for its circle to be free (animals step off the spots), then the eggs appear.
            until = Time.realtimeSinceStartup + 10f;
            int waited = 0;
            while (!_refuge.Multi.IsPlaying && !_refuge.Hatch.IsPlaying && Time.realtimeSinceStartup < until && waited < 240)
            {
                SetPose(BurstPose(0f, egg, false));
                SetFocus(egg);
                waited++;
                yield return null;
            }
            int start = _timelineFrame;
            bool burst = false;
            float burstU = 1f;
            for (int f = 0; f < frames; f++)
            {
                if (!burst && _refuge.Multi.Burst)
                {
                    burst = true;
                    burstU = f / (float)(frames - 1);
                    Event("burst_pop");
                }
                float u = f / (float)(frames - 1);
                // Push in while the eggs crack; once they burst, rise gently to show the newborns walking out.
                SetPose(burst ? BurstPose(Mathf.InverseLerp(burstU, 1f, u), egg, true) : BurstPose(u, egg, false));
                SetFocus(egg);
                Record();
                yield return null;
            }
            LogShot("burst", _refuge.Choice.Quantity.ToString(), start, $"{_refuge.Multi.Count} eggs", $"waited {waited} frames");
            until = Time.realtimeSinceStartup + 8f;
            while ((_refuge.Multi.IsPlaying || _refuge.SummaryOpen) && Time.realtimeSinceStartup < until)
            {
                if (_refuge.SummaryOpen)
                    _refuge.CloseSummary();
                yield return null;
            }
        }

        private CameraPose BurstPose(float u, Vector3 egg, bool rising)
        {
            float g = Glide(Mathf.Clamp01(u));
            if (!rising)
                return Frame(egg, Mathf.Lerp(-8f, -2f, g), Mathf.Lerp(30f, 27f, g), Mathf.Lerp(4.4f, 3.6f, g), 30f, 0.42f);
            return Frame(egg, Mathf.Lerp(-2f, 6f, g), Mathf.Lerp(27f, 36f, g), Mathf.Lerp(3.6f, 4.8f, g), 30f, 0.40f);
        }

        // ---------------- The next zone opens ----------------

        private IEnumerator ShotZone(int frames)
        {
            var zones = _entry.Zones;
            var progression = _context.Progression;
            if (zones == null || progression == null || !progression.Zones.TryGetNext(Grassland, out var zone))
            {
                Warn("no next zone to open");
                yield break;
            }
            CollectObstacles();
            var egg = EggSpawn;
            float seam = zones.SeamX;
            var hedge = new Vector3(seam, GroundAt(seam, egg.z, egg.y) + 0.3f, egg.z);
            var beyond = hedge + new Vector3(3.2f, 0f, 0f);
            beyond.y = GroundAt(beyond.x, beyond.z, egg.y) + 0.2f;
            for (int f = 0; f < 15; f++)
            {
                SetPose(ZonePose(0f, hedge, beyond));
                SetFocus(hedge);
                yield return null;
            }
            int start = _timelineFrame;
            bool opened = false;
            for (int f = 0; f < frames; f++)
            {
                if (f == 12)
                {
                    opened = progression.Zones.TryOpen(zone.Id, out _);
                    Event(opened ? "zone_open" : "zone_refused");
                }
                SetPose(ZonePose(f / (float)(frames - 1), hedge, beyond));
                SetFocus(Vector3.Lerp(hedge, beyond, Smoother(f / (float)(frames - 1))));
                Record();
                yield return null;
            }
            LogShot("zone", opened ? "open" : "refused", start, zone.Id.ToString(), $"seam x {seam:F1}");
        }

        /// <summary>Facing the hedge from the open side, then a crane over it into the new zone.</summary>
        private CameraPose ZonePose(float u, Vector3 hedge, Vector3 beyond)
        {
            float g = Glide(u);
            var target = Vector3.Lerp(hedge, beyond, Smoother(Mathf.InverseLerp(0.25f, 1f, u)));
            return Frame(target, 90f + Mathf.Lerp(-14f, 4f, g), Mathf.Lerp(13f, 30f, g), Mathf.Lerp(3.6f, 6.2f, g), 30f, 0.40f);
        }

        // ---------------- The loop: down to the new egg ----------------

        private IEnumerator ShotEndLoop(int frames)
        {
            float until = Time.realtimeSinceStartup + 10f;
            while (_refuge.WaitingEgg == null && Time.realtimeSinceStartup < until)
                yield return null;
            CollectObstacles();
            var agents = Agents();
            NoteAgents("end", agents);
            var egg = _refuge.WaitingEgg != null ? _refuge.WaitingEgg.VfxPoint.position : EggSpawn;
            int start = _timelineFrame;
            for (int f = 0; f < frames; f++)
            {
                float u = f / (float)(frames - 1);
                float g = Glide(u);
                // The herd under the title, then down to the egg waiting on its pedestal: the first frame of the video again.
                SetPose(Frame(egg, Mathf.Lerp(-6f, 0f, Smoother(u)), Mathf.Lerp(40f, 22f, g), Mathf.Lerp(8.5f, 3.2f, g), 30f, Mathf.Lerp(0.30f, 0.40f, g)));
                SetFocus(egg);
                Record();
                yield return null;
            }
            LogShot("end", "loop", start, $"{agents.Count} animals", $"egg {egg}");
        }
    }
}
