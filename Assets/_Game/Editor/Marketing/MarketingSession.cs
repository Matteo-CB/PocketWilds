using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using PocketWilds.Bootstrap;
using PocketWilds.Core;
using PocketWilds.Presentation;
using PocketWilds.Simulation;
using PocketWilds.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.LowLevel;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.Marketing
{
    /// <summary>
    /// One capture in Play Mode. Three systems join the player loop: <c>Pre</c> at the start of Update (the shot script
    /// advances one frame, then the camera pose is set before any game code culls or streams with it), <c>PoseFix</c>
    /// after LateUpdate (the pose again, in case a rig or a stage moved the camera, and the UI hidden, just before the
    /// frame renders) and <c>Post</c> after rendering (the frame and its audio recorded). The script is a coroutine
    /// stepped by hand once a frame; game time runs at a fixed 1/30 s per frame whatever the real speed.
    /// </summary>
    internal sealed partial class MarketingSession
    {
        private struct MarketingPre
        {
        }

        private struct MarketingPoseFix
        {
        }

        private struct MarketingPost
        {
        }

        [Serializable]
        private sealed class CaptureStatus
        {
            public string state;
            public string detail;
            public int frames;
            public string updated;
        }

        [Serializable]
        internal sealed class ShotLog
        {
            public string name;
            public string variant;
            public int start;
            public int end;
            public string subject;
            public string notes;
        }

        [Serializable]
        internal sealed class EventLog
        {
            public string name;
            public int frame;
            public float time;
        }

        [Serializable]
        internal sealed class CaptureLog
        {
            public string video;
            public int fps;
            public int stride;
            public int width;
            public int height;
            public int supersample;
            public int msaa;
            public int frames;
            public int written;
            public string audioFile;
            public int audioSampleRate;
            public int audioChannels;
            public long audioSampleFrames;
            public string profile;
            public string prediction;
            public string hatched;
            public float hour;
            public string realSeconds;
            public List<ShotLog> shots = new List<ShotLog>();
            public List<EventLog> events = new List<EventLog>();
            public List<string> warnings = new List<string>();
            public List<string> notes = new List<string>();
        }

        public static MarketingSession Current { get; private set; }

        private readonly MarketingCaptureSettings _settings;
        private readonly string _videoDirectory;
        private readonly Stack<IEnumerator> _script = new Stack<IEnumerator>();
        private readonly CaptureLog _log = new CaptureLog();
        private readonly float _realStart;
        private MarketingRecorder _recorder;
        private bool _finished;

        // Game objects, once booted.
        private GameBootstrap _bootstrap;
        private GameContext _context;
        private VerticalSliceEntry _entry;
        private RefugeController _refuge;
        private RefugeCameraRig _rig;
        private Camera _camera;

        // The frame being made.
        private bool _poseActive;
        private Vector3 _posePosition;
        private Quaternion _poseRotation = Quaternion.identity;
        private float _poseFov = 30f;
        private Vector3 _focus;
        private bool _hasFocus;
        private bool _recordThisFrame;
        private int _timelineFrame;
        private int _written;
        private bool _uiHidden;
        private Action<HatchResult> _onHatched;

        private MarketingSession(MarketingCaptureSettings settings)
        {
            _settings = settings;
            _videoDirectory = MarketingCapture.VideoDirectory(settings);
            _realStart = Time.realtimeSinceStartup;
            _log.video = settings.video;
            _log.fps = settings.frameRate;
            _log.stride = settings.stride;
            _log.width = settings.width;
            _log.height = settings.height;
            _log.supersample = settings.supersample;
            _log.hour = settings.hour;
            _log.profile = MarketingCapture.ProfileText;
            _log.prediction = MarketingCapture.Prediction;
            _script.Push(Run());
        }

        /// <summary>Entered Play Mode with the hand-over armed: the session joins the player loop.</summary>
        public static void Start(MarketingCaptureSettings settings)
        {
            Current?.Stop();
            Current = new MarketingSession(settings);
            Application.runInBackground = true;
            InstallLoop();
            WriteStatus(settings, "booting", "waiting for the bootstrap", 0);
            Debug.Log("[MarketingCapture] Session started for " + settings.video + ".");
        }

        public static void WriteStatus(MarketingCaptureSettings settings, string state, string detail, int frames)
        {
            try
            {
                Directory.CreateDirectory(MarketingCapture.VideoDirectory(settings));
                var status = new CaptureStatus { state = state, detail = detail, frames = frames, updated = DateTime.Now.ToString("s") };
                File.WriteAllText(MarketingCapture.StatusPath(settings), JsonUtility.ToJson(status, true));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MarketingCapture] Status not written: " + ex.Message);
            }
        }

        private void Status(string state, string detail) => WriteStatus(_settings, state, detail, _written);

        private void Warn(string message)
        {
            _log.warnings.Add($"[frame {_timelineFrame}] {message}");
            Debug.LogWarning("[MarketingCapture] " + message);
        }

        /// <summary>A diagnostic line of the log (where the animals were, what a shot chose).</summary>
        private void Note(string message) => _log.notes.Add($"[frame {_timelineFrame}] {message}");

        /// <summary>An event of the timeline (frame of the recorded video at full rate), for the edit's ducking and checks.</summary>
        private void Event(string name)
        {
            _log.events.Add(new EventLog { name = name, frame = _timelineFrame, time = _timelineFrame / (float)_settings.frameRate });
            Debug.Log($"[MarketingCapture] {name} at frame {_timelineFrame}");
        }

        public void Abort(string reason)
        {
            if (_finished)
                return;
            Fail("aborted: " + reason);
        }

        /// <summary>Leaving Play Mode before the end (the user pressed Stop): what was recorded is closed cleanly.</summary>
        public void OnExitingPlayMode()
        {
            if (!_finished)
                Fail("Play Mode ended before the capture finished");
            Stop();
            // Outside the player loop here: the systems can leave at once.
            UninstallLoop();
        }

        // ---------------- Player loop ----------------

        private static void InstallLoop()
        {
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            RemoveOurs(ref loop);
            Insert(ref loop, typeof(UnityEngine.PlayerLoop.Update), new PlayerLoopSystem { type = typeof(MarketingPre), updateDelegate = OnPre }, true);
            Insert(ref loop, typeof(UnityEngine.PlayerLoop.PreLateUpdate), new PlayerLoopSystem { type = typeof(MarketingPoseFix), updateDelegate = OnPoseFix }, false);
            Insert(ref loop, typeof(UnityEngine.PlayerLoop.PostLateUpdate), new PlayerLoopSystem { type = typeof(MarketingPost), updateDelegate = OnPost }, false);
            PlayerLoop.SetPlayerLoop(loop);
        }

        private static void UninstallLoop()
        {
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            if (RemoveOurs(ref loop))
                PlayerLoop.SetPlayerLoop(loop);
        }

        private static void Insert(ref PlayerLoopSystem loop, Type phase, PlayerLoopSystem system, bool atStart)
        {
            var phases = loop.subSystemList;
            for (int i = 0; i < phases.Length; i++)
            {
                if (phases[i].type != phase)
                    continue;
                var list = new List<PlayerLoopSystem>(phases[i].subSystemList ?? Array.Empty<PlayerLoopSystem>());
                if (atStart)
                    list.Insert(0, system);
                else
                    list.Add(system);
                phases[i].subSystemList = list.ToArray();
                return;
            }
            throw new InvalidOperationException("Player loop phase not found: " + phase.Name);
        }

        private static bool RemoveOurs(ref PlayerLoopSystem system)
        {
            if (system.subSystemList == null)
                return false;
            bool removed = false;
            var list = new List<PlayerLoopSystem>(system.subSystemList.Length);
            foreach (var child in system.subSystemList)
            {
                if (child.type == typeof(MarketingPre) || child.type == typeof(MarketingPoseFix) || child.type == typeof(MarketingPost))
                {
                    removed = true;
                    continue;
                }
                var copy = child;
                removed |= RemoveOurs(ref copy);
                list.Add(copy);
            }
            system.subSystemList = list.ToArray();
            return removed;
        }

        private static void OnPre() => Current?.Pre();

        private static void OnPoseFix() => Current?.PoseFix();

        private static void OnPost() => Current?.Post();

        private void Pre()
        {
            if (_finished)
                return;
            try
            {
                _recordThisFrame = false;
                if (!Step())
                {
                    Finish();
                    return;
                }
                ApplyControl();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                Fail(ex.GetType().Name + ": " + ex.Message);
            }
        }

        private void PoseFix()
        {
            if (_finished || _camera == null)
                return;
            try
            {
                if (_poseActive)
                {
                    if (_rig != null)
                        _rig.enabled = false;
                    ApplyPose();
                }
                if (_uiHidden)
                    HideUi();
                // No shower during a video (Force keeps the sky clear every frame, whatever the schedule says).
                _entry?.Weather?.Force(0f, 0f);
                if (_refuge != null && _refuge.ShopOpen)
                {
                    _refuge.Shop.CloseOffer();
                    _refuge.Shop.CloseShop();
                    Warn("a shop screen opened and was closed");
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                Fail(ex.GetType().Name + ": " + ex.Message);
            }
        }

        private void Post()
        {
            if (_finished || _recorder == null)
                return;
            try
            {
                if (_settings.recordAudio)
                    _recorder.RenderAudio(_recordThisFrame);
                if (_recordThisFrame)
                {
                    if (_timelineFrame % _settings.stride == 0)
                        _recorder.CaptureFrame(_written++);
                    _timelineFrame++;
                }
                _recorder.Poll();
                if (_recorder.Error != null)
                    Fail(_recorder.Error);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                Fail(ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>One frame of the script: nested coroutines run inline, a yield ends the frame.</summary>
        private bool Step()
        {
            while (_script.Count > 0)
            {
                var top = _script.Peek();
                if (!top.MoveNext())
                {
                    _script.Pop();
                    continue;
                }
                if (top.Current is IEnumerator nested)
                {
                    _script.Push(nested);
                    continue;
                }
                return true;
            }
            return false;
        }

        // ---------------- Camera control ----------------

        private void SetPose(in CameraPose pose)
        {
            _poseActive = true;
            _posePosition = pose.Position;
            _poseRotation = pose.Rotation;
            _poseFov = pose.Fov;
        }

        private void SetFocus(Vector3 focus)
        {
            _focus = focus;
            _hasFocus = true;
        }

        /// <summary>
        /// The rig stays off while the session holds the camera (no pan, zoom or fling). Its focus still follows the shot:
        /// the scheduler and the animal voices measure from it, so what the camera looks at is what is heard.
        /// </summary>
        private void ApplyControl()
        {
            if (_camera == null || !_poseActive)
                return;
            if (_rig != null)
            {
                _rig.enabled = false;
                if (_hasFocus)
                    _rig.SnapTo(_focus, Vector3.Distance(_posePosition, _focus));
            }
            ApplyPose();
        }

        private void ApplyPose()
        {
            _camera.transform.SetPositionAndRotation(_posePosition, _poseRotation);
            _camera.fieldOfView = _poseFov;
        }

        /// <summary>A recorded frame: this frame's picture and audio go to the video.</summary>
        private void Record() => _recordThisFrame = true;

        // ---------------- Setup ----------------

        private bool CheckScratchSave(out string reason)
        {
            reason = null;
            var field = typeof(GameBootstrap).GetField("_options", BindingFlags.Instance | BindingFlags.NonPublic);
            var options = field?.GetValue(_bootstrap) as BootOptions;
            string expected = Path.GetFullPath(MarketingCapture.SaveDirectory(_settings));
            if (options == null || string.IsNullOrEmpty(options.SaveDirectory))
            {
                reason = "the bootstrap booted without the scratch save options";
                return false;
            }
            if (!string.Equals(Path.GetFullPath(options.SaveDirectory), expected, StringComparison.OrdinalIgnoreCase))
            {
                reason = "the bootstrap uses another save folder: " + options.SaveDirectory;
                return false;
            }
            return true;
        }

        /// <summary>
        /// The picture of a High phone with nothing adaptive (a slow capture frame must not lower the quality), the light
        /// at the chosen hour, a clear sky, every Prairie chunk kept loaded, no wish bubble, no UI; then 30 fps game time,
        /// the capture target and the offline audio.
        /// </summary>
        private void Setup()
        {
            var quality = _context.Quality;
            quality.SetAdaptiveEnabled(false);
            quality.SetThermalSource(new NoThermalSource());
            quality.ForceState(QualityState.Uniform(QualityTier.High));
            _entry.Atmosphere?.ForceHour(_settings.hour);
            _entry.Weather?.Force(0f, 0f);
            var streamer = _entry.Streamer;
            if (streamer != null)
                for (int i = 0; i < streamer.Count; i++)
                    streamer.SetResident(i, true);
            _refuge.Bubbles = null;
            if (_entry.DailyVisit != null)
                _entry.DailyVisit.Bubbles = null;
            _entry.Life?.SetBubbles(null);
            foreach (var bubble in Object.FindObjectsByType<WishBubbleView>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                bubble.gameObject.SetActive(false);
            _uiHidden = true;
            HideUi();
            _onHatched = OnHatched;
            _context.Hatch.Hatched += _onHatched;

            Time.captureFramerate = _settings.frameRate;
            _recorder = new MarketingRecorder(Path.Combine(_videoDirectory, "frames"), _settings.width, _settings.height, _settings.supersample,
                _settings.flipY);
            _recorder.Attach(_camera);
            _log.msaa = _recorder.MsaaSamples;
            if (_settings.recordAudio && !_recorder.StartAudio(Path.Combine(_videoDirectory, "game_audio.wav")))
                Warn("AudioRenderer did not start: no game audio");
            _log.audioFile = _settings.recordAudio ? "game_audio.wav" : string.Empty;
            _log.audioSampleRate = _recorder.AudioSampleRate;
            _log.audioChannels = _recorder.AudioChannels;
        }

        private void OnHatched(HatchResult result)
        {
            var content = _context.Content;
            _log.hatched = $"{MarketingCapture.Name(content, result.Animal.Species)} / {MarketingCapture.VariantName(content, result.Animal.Variant)} ({result.Animal.Rarity})";
            Debug.Log("[MarketingCapture] Hatched " + _log.hatched);
        }

        /// <summary>Every canvas off (a screen-space-camera or world canvas would render into the frames).</summary>
        private static void HideUi()
        {
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (canvas.isRootCanvas && canvas.enabled)
                    canvas.enabled = false;
        }

        /// <summary>What a boot may open over the refuge (from the PlayMode tests): the offline card, a level reward, Today, the hub.</summary>
        private IEnumerator ClearScreens()
        {
            float until = Time.realtimeSinceStartup + 10f;
            float collectAt = -1f;
            bool collected = false;
            while (Time.realtimeSinceStartup < until && (_refuge.ModalOpen || (_entry.Activities != null && _entry.Activities.IsOpen)))
            {
                if (_refuge.OfflineScreenOpen && !collected)
                {
                    if (collectAt < 0f)
                        collectAt = Time.realtimeSinceStartup + 0.35f;
                    else if (Time.realtimeSinceStartup >= collectAt)
                    {
                        var offline = Object.FindAnyObjectByType<OfflineReturnScreen>();
                        var button = offline != null ? offline.GetComponentInChildren<UnityEngine.UI.Button>(true) : null;
                        if (button != null)
                            button.onClick.Invoke();
                        collected = true;
                    }
                }
                var progression = _entry.ProgressionScreens;
                if (progression != null && progression.RewardWaiting && !progression.IsOpen)
                    progression.ShowPendingRewards();
                var card = Object.FindAnyObjectByType<LevelRewardScreen>();
                if (card != null && card.IsOpen)
                    card.ClaimButton.onClick.Invoke();
                if (_entry.DailyVisit != null && _entry.DailyVisit.ScreenOpen)
                    _entry.DailyVisit.CloseToday();
                if (_entry.Activities != null && _entry.Activities.HubOpen)
                    _entry.Activities.CloseActivities();
                if (_refuge.SummaryOpen)
                    _refuge.CloseSummary();
                yield return null;
            }
            if (_refuge.ModalOpen)
                Warn("a screen stayed open over the refuge after boot");
        }

        /// <summary>Every chunk loaded (staged or drawn), so a camera move never waits for a load mid-shot.</summary>
        private IEnumerator WaitForChunks()
        {
            var streamer = _entry.Streamer;
            if (streamer == null)
                yield break;
            float until = Time.realtimeSinceStartup + 15f;
            while (Time.realtimeSinceStartup < until)
            {
                // Closed zones stay unloaded (the zones keep only open ones resident): only loads in flight are awaited.
                bool loading = false;
                for (int i = 0; i < streamer.Count; i++)
                    if (streamer.GetState(i) == ChunkStreamState.Loading)
                        loading = true;
                if (!loading)
                    yield break;
                yield return null;
            }
            var states = new System.Text.StringBuilder();
            for (int i = 0; i < streamer.Count; i++)
                states.Append(streamer.GetEntry(i).id).Append('=').Append(streamer.GetState(i)).Append(' ');
            Warn("some chunks were still loading after 15 s: " + states);
        }

        // ---------------- End ----------------

        private void Finish()
        {
            if (_finished)
                return;
            Status("encoding", "writing the last frames");
            bool drained = _recorder == null || _recorder.Drain(120f);
            _log.audioSampleFrames = _recorder != null ? _recorder.AudioFrames : 0;
            _recorder?.StopAudio();
            _log.frames = _timelineFrame;
            _log.written = _written;
            _log.realSeconds = (Time.realtimeSinceStartup - _realStart).ToString("F0");
            if (!drained)
                Warn("some PNG encodes did not finish in time");
            if (_recorder?.Error != null)
            {
                Fail(_recorder.Error);
                return;
            }
            WriteLog();
            _finished = true;
            Status("done", $"{_written} frames ({_timelineFrame} at full rate), {_log.shots.Count} shots, {_log.warnings.Count} warnings, {_log.realSeconds} s");
            Debug.Log($"[MarketingCapture] Done: {_written} frames in {_log.realSeconds} s. Log: {Path.Combine(_videoDirectory, "capture_log.json")}");
            Stop();
            if (_settings.exitPlayModeWhenDone && EditorApplication.isPlaying)
                EditorApplication.ExitPlaymode();
        }

        private void Fail(string reason)
        {
            if (_finished)
                return;
            _finished = true;
            _log.warnings.Add("FAILED: " + reason);
            _log.frames = _timelineFrame;
            _log.written = _written;
            try
            {
                _recorder?.Drain(30f);
                _recorder?.StopAudio();
                WriteLog();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            Status("failed", reason);
            Debug.LogError("[MarketingCapture] Failed: " + reason);
            Stop();
            if (EditorApplication.isPlaying)
                EditorApplication.ExitPlaymode();
        }

        private void WriteLog()
        {
            try
            {
                File.WriteAllText(Path.Combine(_videoDirectory, "capture_log.json"), JsonUtility.ToJson(_log, true));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MarketingCapture] Log not written: " + ex.Message);
            }
        }

        /// <summary>Leaves the player loop and gives the game back what was borrowed (rig, camera target, time step).</summary>
        private void Stop()
        {
            _finished = true;
            // Stop usually runs inside a player loop callback: the systems leave on the next editor tick, idle until then.
            EditorApplication.delayCall += UninstallLoop;
            Time.captureFramerate = 0;
            if (_context != null && _onHatched != null)
                _context.Hatch.Hatched -= _onHatched;
            _onHatched = null;
            _recorder?.Dispose();
            _recorder = null;
            if (_rig != null)
                _rig.enabled = true;
            if (Current == this)
                Current = null;
        }
    }

    /// <summary>A camera pose: world position, rotation and vertical field of view.</summary>
    internal readonly struct CameraPose
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly float Fov;

        public CameraPose(Vector3 position, Quaternion rotation, float fov)
        {
            Position = position;
            Rotation = rotation;
            Fov = fov;
        }
    }
}
