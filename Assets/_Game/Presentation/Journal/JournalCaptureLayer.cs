using System;
using System.Threading.Tasks;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Simulation;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T44C: the journal's captures (doc « 01 Succès, titres, cadres, poses et journal », section 6 « Images »), made once at
    /// run time like T42's postcards. The pages whose capture is pending (<see cref="IJournal.PendingCaptureAt"/>) are taken
    /// one at a time at a calm moment: never while <see cref="Busy"/> (a hatch reveal, an activity, a purchase, a level up, a
    /// screen over the refuge), at most one a second. An animal's portrait is the animal alone on a small stage far below the
    /// refuge (its own look and size through the activity views, Idle at its own phase), framed by its body and drawn by the
    /// stage's own camera over a soft colour of its biome into a pooled 512 × 512 render texture; a refuge view is the game
    /// camera drawn once into the same texture (the screen-space interface is not in it). The frame is read back without
    /// stalling (AsyncGPUReadback), encoded as JPEG and written (Journal/journal_{id}.jpg) off the main thread, then
    /// <see cref="IJournal.CaptureStored"/> is called and the pictures of pages no longer captured are deleted. A page that
    /// cannot be captured (its animal gone, no visual) keeps its thumbnail. Nothing allocates per frame while idle.
    /// </summary>
    public sealed class JournalCaptureLayer : IDisposable
    {
        private enum Phase : byte
        {
            Idle = 0,
            Posing = 1,
            Reading = 2,
            Writing = 3,
        }

        private const float PortraitFov = 28f;
        private const float PortraitYaw = 32f;

        /// <summary>Soft backgrounds, one picked per biome when no <see cref="Backdrop"/> is given.</summary>
        private static readonly Color[] SoftBiomeColors =
        {
            new Color(0.86f, 0.92f, 0.80f, 1f), new Color(0.84f, 0.90f, 0.95f, 1f), new Color(0.96f, 0.90f, 0.82f, 1f),
            new Color(0.93f, 0.88f, 0.93f, 1f), new Color(0.82f, 0.92f, 0.90f, 1f), new Color(0.96f, 0.94f, 0.84f, 1f),
            new Color(0.90f, 0.93f, 0.97f, 1f), new Color(0.95f, 0.87f, 0.85f, 1f),
        };

        private readonly GameContext _context;
        private readonly IJournal _journal;
        private readonly Transform _parent;
        private readonly JournalImageStore _store;

        private Phase _phase;
        private JournalPage _page;
        private float _next;
        private bool _disposed;

        // The portrait stage (built at the first animal portrait).
        private GameObject _stage;
        private Camera _stageCamera;
        private ActivityAnimalViews _views;
        private AnimalView _shown;
        private AnimatorCullingMode _culling;

        // The frame.
        private RenderTexture _target;
        private AsyncGPUReadbackRequest _request;
        private byte[] _pixels;
        private Texture2D _fallback;
        private Task _write;
        private int[] _kept = Array.Empty<int>();

        // Pages given up on (their animal gone, no visual): skipped until the journal drops them.
        private readonly int[] _failed = new int[16];
        private int _failedCount;

        /// <summary>The shipped layer: the refuge's camera for refuge views, calm when no hatch, screen, activity or purchase is on.</summary>
        public static JournalCaptureLayer Create(GameContext context, IJournal journal, RefugeController refuge, RefugeCameraRig rig, Transform parent)
        {
            if (journal == null)
                return null;
            var layer = new JournalCaptureLayer(context, journal, parent);
            if (refuge != null)
                layer.Busy = () => refuge.Hatching || refuge.ModalOpen || refuge.CameraHeld || !refuge.ActorsVisible;
            if (rig != null)
                layer.GameCamera = () => rig.Camera;
            return layer;
        }

        public JournalCaptureLayer(GameContext context, IJournal journal, Transform parent, JournalImageStore store = null)
        {
            _context = context;
            _journal = journal ?? throw new ArgumentNullException(nameof(journal));
            _parent = parent;
            _store = store ?? new JournalImageStore();
        }

        /// <summary>Not a calm moment (a hatch reveal, an activity, a purchase, a level up, a screen over the refuge): no capture starts.</summary>
        public Func<bool> Busy { get; set; }

        /// <summary>The game camera a refuge view is drawn with (null: refuge views wait).</summary>
        public Func<Camera> GameCamera { get; set; }

        /// <summary>The soft background of an animal's portrait by its species (default: a soft colour picked by its biome).</summary>
        public Func<ContentId, Color> Backdrop { get; set; }

        public JournalImageStore Store => _store;

        public bool Capturing => _phase != Phase.Idle;

        /// <summary>The captures stored since the layer started (tests), and the page being captured or last captured.</summary>
        public int Stored { get; private set; }

        public int PageId => _page.Id;

        /// <summary>Per frame (unscaled time).</summary>
        public void Tick(float time)
        {
            if (_disposed)
                return;
            switch (_phase)
            {
                case Phase.Idle:
                    TryStart(time);
                    return;
                case Phase.Posing:
                    if (Busy != null && Busy())
                    {
                        // Something started meanwhile: the portrait waits for the next calm moment.
                        HideAnimal();
                        _phase = Phase.Idle;
                        _next = time + JournalLayout.CaptureGapSeconds;
                        return;
                    }
                    if (!DrawAnimal())
                    {
                        Fail("the portrait could not be drawn", true, time);
                        return;
                    }
                    StartRead(time);
                    return;
                case Phase.Reading:
                    if (!_request.done)
                    {
                        _request.Update();
                        return;
                    }
                    if (ReadBack(time))
                        _phase = Phase.Writing;
                    return;
                case Phase.Writing:
                    if (_write == null || !_write.IsCompleted)
                        return;
                    Written(time);
                    return;
            }
        }

        private void TryStart(float time)
        {
            if (time < _next || _journal.PendingCaptureCount <= 0 || (Busy != null && Busy()))
                return;
            int count = _journal.PendingCaptureCount;
            bool found = false;
            for (int i = 0; i < count && !found; i++)
            {
                var page = _journal.PendingCaptureAt(i);
                if (IsFailed(page.Id))
                    continue;
                _page = page;
                found = true;
            }
            if (!found)
                return;
            _next = time + JournalLayout.CaptureGapSeconds;
            switch (_page.Image)
            {
                case JournalImageKind.AnimalCapture:
                    if (!PrepareAnimal())
                    {
                        Fail("its animal has no portrait", true, time);
                        return;
                    }
                    // The view is posed now; its frame is drawn on the next tick (two light frames, not one heavy).
                    _phase = Phase.Posing;
                    return;
                case JournalImageKind.RefugeCapture:
                    var camera = GameCamera?.Invoke();
                    if (camera == null)
                        return;
                    if (!DrawRefuge(camera))
                    {
                        Fail("the refuge view could not be drawn", false, time);
                        return;
                    }
                    StartRead(time);
                    return;
                default:
                    Fail("its picture is not a capture", true, time);
                    return;
            }
        }

        // ---------------- the portrait ----------------

        private void EnsureStage()
        {
            if (_stage != null)
                return;
            _stage = new GameObject("JournalPortraitStage");
            if (_parent != null)
                _stage.transform.SetParent(_parent, false);
            _stage.transform.position = JournalLayout.StagePosition;
            var cameraObject = new GameObject("PortraitCamera");
            cameraObject.transform.SetParent(_stage.transform, false);
            _stageCamera = cameraObject.AddComponent<Camera>();
            _stageCamera.enabled = false;
            _stageCamera.clearFlags = CameraClearFlags.SolidColor;
            _stageCamera.fieldOfView = PortraitFov;
            _stageCamera.nearClipPlane = 0.05f;
            _stageCamera.farClipPlane = 12f;
            _stageCamera.allowHDR = false;
            _stageCamera.allowMSAA = false;
            _views = new ActivityAnimalViews(_context, _stage.transform, "Portrait", 1);
        }

        /// <summary>The page's animal (from the collection) shown on the stage at Idle, its view made now (once per species).</summary>
        private bool PrepareAnimal()
        {
            var collection = _context != null && _context.Save != null ? _context.State.Collection : null;
            if (collection == null || !collection.TryGet(_page.ImageRef, out var animal) || animal.Species == ContentId.None)
                return false;
            EnsureStage();
            if (!_views.TryGetEntry(animal.Species, out _) || _views.Warm(animal.Species, 1) <= 0)
                return false;
            var origin = _stage.transform.position;
            _shown = _views.Show(in animal, origin, PortraitYaw, true);
            if (_shown == null)
                return false;
            _shown.SetPose(origin, PortraitYaw);
            var animator = _shown.Animator;
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                _culling = animator.cullingMode;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (!animator.isInitialized)
                    animator.Rebind();
                animator.Play(AnimalStateHashes.Idle, 0, AnimalVisualResolver.IdlePhase(animal.Seed));
                animator.Update(0f);
            }
            FrameAnimal(animal);
            return true;
        }

        /// <summary>The camera steps back until the whole body fits (its height and its length seen three quarters).</summary>
        private void FrameAnimal(in AnimalInstance animal)
        {
            float scale = Mathf.Max(0.1f, AnimalGenome.Scale(animal.TraitBits));
            var body = new BodyShape(0.25f, -0.4f, 0.17f, 0.66f);
            if (_views.TryGetEntry(animal.Species, out var entry) && entry.behavior != null)
                body = entry.behavior.body;
            float height = Mathf.Max(0.15f, body.height * scale);
            body.Segment(Vector3.zero, PortraitYaw, scale, out var a, out var b);
            float length = Vector2.Distance(a, b) + body.halfWidth * scale * 2f;
            float extent = Mathf.Max(height * 1.15f, length * 0.85f);
            float distance = extent * 0.5f * 1.3f / Mathf.Tan(PortraitFov * 0.5f * Mathf.Deg2Rad);
            var origin = _stage.transform.position;
            var look = origin + new Vector3(0f, height * 0.5f, 0f);
            var position = origin + new Vector3(0f, height * 0.62f + distance * 0.18f, distance);
            _stageCamera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(look - position, Vector3.up));
            _stageCamera.backgroundColor = BackdropOf(animal.Species);
        }

        private Color BackdropOf(ContentId species)
        {
            if (Backdrop != null)
                return Backdrop(species);
            uint biome = 0u;
            if (_context != null && _context.Content != null && _context.Content.Species.TryGet(species, out var definition))
                biome = definition.Biome.Value;
            return SoftBiomeColors[(int)(SeedHash.Mix(biome, 0x6A0C0001u) % (uint)SoftBiomeColors.Length)];
        }

        private bool DrawAnimal()
        {
            if (_shown == null || _stageCamera == null)
                return false;
            bool drawn = false;
            _target = RenderTexture.GetTemporary(JournalLayout.PictureSize, JournalLayout.PictureSize, 24, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB, 1);
            try
            {
                _stageCamera.targetTexture = _target;
                _stageCamera.aspect = 1f;
                _stageCamera.Render();
                drawn = true;
            }
            catch (Exception e)
            {
                GameLog.Warning(LogCategory.Presentation, "Journal: the portrait failed: " + e.Message);
            }
            finally
            {
                _stageCamera.targetTexture = null;
                HideAnimal();
            }
            return drawn;
        }

        private void HideAnimal()
        {
            if (_shown != null && _shown.Animator != null)
                _shown.Animator.cullingMode = _culling;
            _shown = null;
            _views?.HideAll();
        }

        // ---------------- the refuge view ----------------

        private bool DrawRefuge(Camera camera)
        {
            var previous = camera.targetTexture;
            _target = RenderTexture.GetTemporary(JournalLayout.PictureSize, JournalLayout.PictureSize, 24, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB, 1);
            bool drawn = false;
            try
            {
                camera.targetTexture = _target;
                camera.aspect = 1f;
                camera.Render();
                drawn = true;
            }
            catch (Exception e)
            {
                GameLog.Warning(LogCategory.Presentation, "Journal: the refuge view failed: " + e.Message);
            }
            finally
            {
                camera.targetTexture = previous;
                camera.ResetAspect();
            }
            return drawn;
        }

        // ---------------- read, encode, write ----------------

        private void StartRead(float time)
        {
            int size = JournalLayout.PictureSize * JournalLayout.PictureSize * 4;
            if (_pixels == null || _pixels.Length != size)
                _pixels = new byte[size];
            if (SystemInfo.supportsAsyncGPUReadback)
            {
                _request = AsyncGPUReadback.Request(_target, 0, TextureFormat.RGBA32);
                _phase = Phase.Reading;
                return;
            }
            // No asynchronous read-back on this device: read it now (once per page).
            if (_fallback == null)
                _fallback = new Texture2D(JournalLayout.PictureSize, JournalLayout.PictureSize, TextureFormat.RGBA32, false, false) { name = "JournalCapture" };
            var active = RenderTexture.active;
            RenderTexture.active = _target;
            _fallback.ReadPixels(new Rect(0, 0, JournalLayout.PictureSize, JournalLayout.PictureSize), 0, 0, false);
            RenderTexture.active = active;
            _fallback.GetPixelData<byte>(0).CopyTo(_pixels);
            _phase = Phase.Reading;
            if (ReadBack(time))
                _phase = Phase.Writing;
        }

        /// <summary>The pixels are in: the render texture goes back to the pool, the JPEG is encoded and written off the main thread.</summary>
        private bool ReadBack(float time)
        {
            if (SystemInfo.supportsAsyncGPUReadback)
            {
                if (_request.hasError)
                {
                    Fail("the read-back failed", false, time);
                    return false;
                }
                _request.GetData<byte>().CopyTo(_pixels);
            }
            ReleaseTarget();
            var pixels = _pixels;
            var store = _store;
            int id = _page.Id;
            _write = Task.Run(() =>
            {
                for (int i = 3; i < pixels.Length; i += 4)
                    pixels[i] = 255;
                var jpg = ImageConversion.EncodeArrayToJPG(pixels, GraphicsFormat.R8G8B8A8_SRGB, (uint)JournalLayout.PictureSize,
                    (uint)JournalLayout.PictureSize, 0u, JournalLayout.JpegQuality);
                store.Write(id, jpg);
            });
            return true;
        }

        private void Written(float time)
        {
            var write = _write;
            _write = null;
            _phase = Phase.Idle;
            _next = time + JournalLayout.CaptureGapSeconds;
            if (write == null || write.IsFaulted || write.IsCanceled)
            {
                var error = write?.Exception?.GetBaseException();
                GameLog.Warning(LogCategory.Presentation, $"Journal page {_page.Id}: capture not written ({error?.Message ?? "cancelled"}).");
                return;
            }
            Stored++;
            _journal.CaptureStored(_page.Id);
            PruneInBackground();
        }

        /// <summary>The pictures of pages no longer captured go (the 300 kept, doc), off the main thread.</summary>
        private void PruneInBackground()
        {
            int pages = Mathf.Max(0, _journal.PageCount);
            if (_kept.Length < pages)
                _kept = new int[Mathf.Max(pages, 64)];
            int count = 0;
            for (int i = 0; i < pages; i++)
            {
                var page = _journal.PageAt(i);
                if (page.Captured && (page.Image == JournalImageKind.AnimalCapture || page.Image == JournalImageKind.RefugeCapture))
                    _kept[count++] = page.Id;
            }
            var kept = new int[count];
            Array.Copy(_kept, kept, count);
            var store = _store;
            Task.Run(() =>
            {
                try
                {
                    store.Prune(kept, kept.Length);
                }
                catch (Exception)
                {
                    // A busy file stays until the next capture.
                }
            });
        }

        private void Fail(string why, bool forGood, float time)
        {
            GameLog.Warning(LogCategory.Presentation, $"Journal page {_page.Id}: {why}; its thumbnail stands in.");
            HideAnimal();
            ReleaseTarget();
            _phase = Phase.Idle;
            _next = time + JournalLayout.CaptureGapSeconds;
            if (forGood && !IsFailed(_page.Id))
            {
                _failed[_failedCount % _failed.Length] = _page.Id;
                _failedCount++;
            }
        }

        private bool IsFailed(int pageId)
        {
            int n = Mathf.Min(_failedCount, _failed.Length);
            for (int i = 0; i < n; i++)
                if (_failed[i] == pageId)
                    return true;
            return false;
        }

        private void ReleaseTarget()
        {
            if (_target != null)
                RenderTexture.ReleaseTemporary(_target);
            _target = null;
        }

        /// <summary>A capture already drawn is finished now (read back, written, stored); then the stage goes.</summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            try
            {
                if (_phase == Phase.Reading)
                {
                    if (SystemInfo.supportsAsyncGPUReadback)
                        _request.WaitForCompletion();
                    if (ReadBack(Time.unscaledTime))
                        _phase = Phase.Writing;
                }
                if (_phase == Phase.Writing && _write != null)
                {
                    try
                    {
                        _write.Wait();
                    }
                    catch (AggregateException)
                    {
                    }
                    Written(Time.unscaledTime);
                }
            }
            finally
            {
                _disposed = true;
                HideAnimal();
                ReleaseTarget();
                _views?.Dispose();
                _views = null;
                if (_stage != null)
                    DestroyObject(_stage);
                _stage = null;
                if (_fallback != null)
                    DestroyObject(_fallback);
                _fallback = null;
                _phase = Phase.Idle;
            }
        }

        private static void DestroyObject(Object target)
        {
            if (target == null)
                return;
            if (Application.isPlaying)
                Object.Destroy(target);
            else
                Object.DestroyImmediate(target);
        }
    }
}
