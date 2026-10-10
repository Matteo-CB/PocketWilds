#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Simulation;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T42: the postcard of a welcome (doc 01 Contenu, section 7.1; doc 05 sheet 23), rendered once at run time. The
    /// destination's set (PF_Activity_Postcard_FlowerMeadows, Addressables group Activity_Expeditions) is loaded the first
    /// time and built far below the refuge; the team is posed on its seats through the same visual resolver as the activity
    /// stages (each animal's own look and size, Idle at its own phase, one of them Happy), measured so nobody enters anybody
    /// nor any prop; the set's camera draws one frame into a pooled 512 × 768 render texture with the set's light and sky
    /// (the refuge's are given back in the same frame); the frame is read back without stalling (AsyncGPUReadback), the
    /// picture shows as soon as its pixels are in, the PNG is encoded and written off the main thread, then
    /// <see cref="IExpeditions.PostcardRendered"/> is called and the pictures no longer kept are deleted. The set stays
    /// hidden between postcards and goes with <see cref="Release"/>. Nothing heavy on the main thread: one frame drawn, one
    /// texture upload; no allocation per frame.
    /// </summary>
    public sealed class PostcardRenderer : IPostcardRenderer, IDisposable
    {
        private enum Phase : byte
        {
            Idle = 0,
            Loading = 1,
            Posing = 2,
            Reading = 3,
            Writing = 4,
        }

        private readonly GameContext? _context;
        private readonly IExpeditions? _expeditions;
        private readonly Transform? _parent;
        private readonly PostcardStore _store;
        private readonly GameObject? _givenPrefab;

        private Phase _phase;
        private ExpeditionReturn _welcome;
        private bool _disposed;

        // The set.
        private AsyncOperationHandle<GameObject> _handle;
        private bool _ownsHandle;
        private string? _setKey;
        private GameObject? _setObject;
        private PostcardSetView? _set;
        private PostcardSetData? _data;
        private ActivityAnimalViews? _views;
        private bool _setFailed;

        // The team (pooled arrays).
        private readonly AnimalInstance[] _team = new AnimalInstance[PostcardSetLayout.MaxTeam];
        private readonly BodyShape[] _bodies = new BodyShape[PostcardSetLayout.MaxTeam];
        private readonly float[] _scales = new float[PostcardSetLayout.MaxTeam];
        private readonly Vector3[] _positions = new Vector3[PostcardSetLayout.MaxTeam];
        private readonly float[] _yaws = new float[PostcardSetLayout.MaxTeam];
        private readonly AnimalView?[] _shown = new AnimalView?[PostcardSetLayout.MaxTeam];
        private readonly AnimatorCullingMode[] _culling = new AnimatorCullingMode[PostcardSetLayout.MaxTeam];
        private int _teamCount;

        // The frame.
        private RenderTexture? _target;
        private AsyncGPUReadbackRequest _request;
        private byte[]? _pixels;
        private Texture2D? _picture;
        private bool _pictureReady;
        private Task? _write;
        private int _pixelsIn;
        private int[] _kept = Array.Empty<int>();

        // Lighting while the set renders (saved and given back in the same frame).
        private readonly PostcardLighting _lighting = new PostcardLighting();

        /// <summary>
        /// The shipped renderer: the sets come from Addressables, the pictures go to the persistent data path. Tests and
        /// tools may give a store (another folder) and a set prefab (built at the first postcard, no Addressables).
        /// </summary>
        public PostcardRenderer(GameContext? context, IExpeditions? expeditions, Transform? parent, PostcardStore? store = null, GameObject? setPrefab = null)
        {
            _context = context;
            _expeditions = expeditions;
            _parent = parent;
            _store = store ?? new PostcardStore();
            _givenPrefab = setPrefab;
        }

        // ---------------- IPostcardRenderer ----------------

        public bool Busy => _phase != Phase.Idle;

        public Texture2D? Picture => _pictureReady ? _picture : null;

        /// <summary>Where the pictures are written.</summary>
        public PostcardStore Store => _store;

        /// <summary>The postcard being made or the last one made (tests).</summary>
        public int PostcardId => _welcome.PostcardId;

        /// <summary>The set's Addressables handle is held (tests).</summary>
        public bool SetLoaded => _handle.IsValid();

        public PostcardSetView? Set => _set;

        /// <summary>The animals posed on the last postcard (origin frame), their bodies and scales, for the tests' no-collision check.</summary>
        public int PosedCount => _teamCount;

        public Vector3 PosedPosition(int index) => _positions[index];

        public float PosedYaw(int index) => _yaws[index];

        public BodyShape PosedBody(int index) => _bodies[index];

        public float PosedScale(int index) => _scales[index];

        /// <summary>The smallest gap between two posed animals or an animal and a prop on the last postcard (m).</summary>
        public float PosedGap { get; private set; } = float.MaxValue;

        public void Begin(in ExpeditionReturn welcome)
        {
            if (Busy || _disposed)
                return;
            _welcome = welcome;
            _pictureReady = false;
            _teamCount = 0;
            PosedGap = float.MaxValue;
            _phase = Phase.Loading;
            StartSet(DestinationKey(welcome.Destination));
            Advance();
        }

        public void Tick()
        {
            if (_phase != Phase.Idle)
                Advance();
        }

        /// <summary>
        /// The screen closed: a postcard already drawn is finished now (its read-back waited for, its file written, the
        /// expeditions told), one not drawn yet is dropped; then the picture, the set and its animals go.
        /// </summary>
        public void Release()
        {
            if (_phase == Phase.Reading || _phase == Phase.Writing)
                Finish();
            else if (_phase != Phase.Idle)
                GameLog.Warning(LogCategory.Presentation, $"Postcard {_welcome.PostcardId}: released before it was drawn, no picture kept.");
            _phase = Phase.Idle;
            _pictureReady = false;
            ReleaseTarget();
            if (_picture != null)
                DestroyObject(_picture);
            _picture = null;
            _pixels = null;
            HideTeam();
            _views?.Dispose();
            _views = null;
            if (_setObject != null)
                DestroyObject(_setObject);
            _setObject = null;
            _set = null;
            _data = null;
            _setKey = null;
            _setFailed = false;
            if (_ownsHandle && _handle.IsValid())
                Addressables.Release(_handle);
            _handle = default;
            _ownsHandle = false;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            Release();
            _disposed = true;
        }

        // ---------------- the steps ----------------

        private void Advance()
        {
            switch (_phase)
            {
                case Phase.Loading:
                    if (!SetReady())
                        return;
                    if (_set == null)
                    {
                        Fail("no postcard set");
                        return;
                    }
                    // The team's views are made now; the frame is drawn on the next tick (two light frames, not one heavy).
                    if (!PrepareTeam())
                    {
                        Fail("no animal to pose");
                        return;
                    }
                    _phase = Phase.Posing;
                    return;
                case Phase.Posing:
                    if (!DrawFrame())
                    {
                        Fail("the frame could not be drawn");
                        return;
                    }
                    _phase = Phase.Reading;
                    if (!SystemInfo.supportsAsyncGPUReadback && ReadBack())
                        _phase = Phase.Writing;
                    return;
                case Phase.Reading:
                    if (!_request.done)
                    {
                        _request.Update();
                        return;
                    }
                    if (!ReadBack())
                        return;
                    _phase = Phase.Writing;
                    return;
                case Phase.Writing:
                    if (!_pictureReady && Volatile.Read(ref _pixelsIn) == 1)
                        UploadPicture();
                    if (_write == null || !_write.IsCompleted)
                        return;
                    Written();
                    return;
            }
        }

        /// <summary>Waits for every step already started (the screen closed while a postcard was being read or written).</summary>
        private void Finish()
        {
            if (_phase == Phase.Reading)
            {
                if (SystemInfo.supportsAsyncGPUReadback)
                    _request.WaitForCompletion();
                if (!ReadBack())
                    return;
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
                Written();
            }
        }

        private void Fail(string why)
        {
            GameLog.Warning(LogCategory.Presentation, $"Postcard {_welcome.PostcardId}: {why}; the welcome goes on without its picture.");
            HideTeam();
            ReleaseTarget();
            _phase = Phase.Idle;
        }

        // ---------------- the set ----------------

        private string DestinationKey(ContentId destination)
        {
            if (_expeditions != null && _expeditions.TryGetDestination(destination, out var found) && !string.IsNullOrEmpty(found.Key))
                return found.Key;
            return PostcardSetLayout.FlowerMeadows;
        }

        private void StartSet(string destinationKey)
        {
            // One set this build: every destination draws in the Prés fleuris set until its own exists.
            string key = _givenPrefab != null || AddressExists(PostcardSetLayout.SetAddress(destinationKey)) ? destinationKey : PostcardSetLayout.FlowerMeadows;
            if (_setKey == key && (_set != null || _handle.IsValid() || _setFailed))
                return;
            if (_setKey != null && _setKey != key)
            {
                // Another destination's set: the previous one goes first.
                var welcome = _welcome;
                _phase = Phase.Idle;
                Release();
                _welcome = welcome;
                _phase = Phase.Loading;
            }
            _setKey = key;
            _setFailed = false;
            if (_givenPrefab != null)
            {
                BuildSet(_givenPrefab);
                return;
            }
            string address = PostcardSetLayout.SetAddress(key);
            if (!AddressExists(address))
            {
                _setFailed = true;
                GameLog.Error(LogCategory.Presentation, $"Postcard: no Addressables entry {address} (run PostcardSetPipeline.BuildAll).");
                return;
            }
            _handle = Addressables.LoadAssetAsync<GameObject>(address);
            _ownsHandle = true;
        }

        private static bool AddressExists(string address)
        {
            foreach (var locator in Addressables.ResourceLocators)
                if (locator.Locate(address, typeof(GameObject), out _))
                    return true;
            return false;
        }

        /// <summary>The set is built (or failed for good).</summary>
        private bool SetReady()
        {
            if (_set != null || _setFailed)
                return true;
            if (!_handle.IsValid())
            {
                _setFailed = true;
                return true;
            }
            if (!_handle.IsDone)
                return false;
            if (_handle.Status != AsyncOperationStatus.Succeeded || _handle.Result == null)
            {
                _setFailed = true;
                GameLog.Error(LogCategory.Presentation, "Postcard: the set did not load (" + PostcardSetLayout.SetAddress(_setKey ?? PostcardSetLayout.FlowerMeadows) + ").");
                return true;
            }
            BuildSet(_handle.Result);
            return true;
        }

        private void BuildSet(GameObject prefab)
        {
            _setObject = Object.Instantiate(prefab, PostcardSetLayout.SetPosition, Quaternion.identity, _parent);
            _setObject.name = prefab.name;
            _set = _setObject.GetComponent<PostcardSetView>();
            if (_set == null || _set.Data == null || _set.Data.origin == null || _set.Data.camera == null)
            {
                GameLog.Error(LogCategory.Presentation, "Postcard: the set prefab has no PostcardSetView (rebuild it with PostcardSetPipeline).");
                DestroyObject(_setObject);
                _setObject = null;
                _set = null;
                _setFailed = true;
                return;
            }
            _data = _set.Data;
            _data.camera.enabled = false;
            _data.camera.fieldOfView = PostcardSetLayout.CameraFov;
            _data.camera.nearClipPlane = PostcardSetLayout.CameraNear;
            _data.camera.farClipPlane = PostcardSetLayout.CameraFar;
            if (_data.light != null)
                _data.light.enabled = false;
            int missing = _set.Bind(_context?.Palettes);
            if (missing > 0)
                GameLog.Warning(LogCategory.Presentation, $"Postcard: {missing} set palettes unknown (DEF_Palettes_Postcards in the content database?).");
            _views = new ActivityAnimalViews(_context, _data.origin, "Team", PostcardSetLayout.MaxTeam);
            _setObject.SetActive(false);
        }

        // ---------------- the team ----------------

        /// <summary>The team at departure (species and variants from the welcome), their views warmed, their seats solved.</summary>
        private bool PrepareTeam()
        {
            var data = _data!;
            int count = Mathf.Clamp(_welcome.TeamSize, 0, PostcardSetLayout.MaxTeam);
            _teamCount = 0;
            for (int i = 0; i < count; i++)
            {
                uint id = _welcome.AnimalAt(i);
                var species = _welcome.SpeciesAt(i);
                AnimalInstance animal = default;
                var collection = _context != null && _context.Save != null ? _context.State.Collection : null;
                if (collection == null || !collection.TryGet(id, out animal))
                    animal = new AnimalInstance { InstanceId = id, Seed = _welcome.Seed + (uint)i * 0x9E3779B9u, Rarity = RarityTier.Common };
                // The postcard poses the team as it left, even if an animal changed since.
                if (species != ContentId.None)
                    animal.Species = species;
                if (_welcome.VariantAt(i) != ContentId.None)
                    animal.Variant = _welcome.VariantAt(i);
                if (animal.Species == ContentId.None || _views == null || !_views.TryGetEntry(animal.Species, out var entry))
                    continue;
                _team[_teamCount] = animal;
                _bodies[_teamCount] = entry.behavior != null ? entry.behavior.body : new BodyShape(0.25f, -0.4f, 0.17f, 0.66f);
                _scales[_teamCount] = Mathf.Max(0.1f, AnimalGenome.Scale(animal.TraitBits));
                _teamCount++;
            }
            if (_teamCount == 0)
                return false;
            for (int i = 0; i < _teamCount; i++)
                _views!.Warm(_team[i].Species, CountSpecies(_team[i].Species));
            PosedGap = PostcardTeamLayout.Solve(data, _teamCount, _bodies, _scales, _positions, _yaws);
            return true;
        }

        private int CountSpecies(ContentId species)
        {
            int n = 0;
            for (int i = 0; i < _teamCount; i++)
                if (_team[i].Species == species)
                    n++;
            return n;
        }

        private void ShowTeam()
        {
            var data = _data!;
            var origin = data.origin;
            // One of them happy, chosen by the expedition's seed; the others at rest, each at its own Idle phase.
            int happy = _teamCount > 0 ? (int)(_welcome.Seed % (uint)_teamCount) : -1;
            for (int i = 0; i < _teamCount; i++)
            {
                var world = origin.TransformPoint(_positions[i]);
                float yaw = _yaws[i] + origin.eulerAngles.y;
                var view = _views!.Show(in _team[i], world, yaw, true);
                _shown[i] = view;
                if (view == null)
                    continue;
                view.SetPose(world, yaw);
                var animator = view.Animator;
                if (animator == null || animator.runtimeAnimatorController == null)
                    continue;
                _culling[i] = animator.cullingMode;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (!animator.isInitialized)
                    animator.Rebind();
                if (i == happy)
                    animator.Play(AnimalStateHashes.Happy, 0, PostcardSetLayout.HappyTime);
                else
                    animator.Play(AnimalStateHashes.Idle, 0, AnimalVisualResolver.IdlePhase(_team[i].Seed));
                animator.Update(0f);
            }
        }

        private void HideTeam()
        {
            for (int i = 0; i < _shown.Length; i++)
            {
                var view = _shown[i];
                _shown[i] = null;
                if (view != null && view.Animator != null)
                    view.Animator.cullingMode = _culling[i];
            }
            _views?.HideAll();
        }

        // ---------------- the frame ----------------

        private bool DrawFrame()
        {
            var data = _data!;
            var camera = data.camera;
            var cameraTransform = camera.transform;
            var localPosition = cameraTransform.localPosition;
            var localRotation = cameraTransform.localRotation;
            bool drawn = false;
            _setObject!.SetActive(true);
            ShowButterflies(data);
            ShowTeam();
            FrameTeam(data, camera);
            _lighting.Apply(data);
            _target = RenderTexture.GetTemporary(PostcardLayout.Width, PostcardLayout.Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 1);
            try
            {
                camera.targetTexture = _target;
                camera.aspect = PostcardLayout.Width / (float)PostcardLayout.Height;
                camera.Render();
                drawn = true;
            }
            catch (Exception e)
            {
                GameLog.Warning(LogCategory.Presentation, "Postcard: the frame failed: " + e.Message);
            }
            finally
            {
                camera.targetTexture = null;
                camera.ResetAspect();
                cameraTransform.SetLocalPositionAndRotation(localPosition, localRotation);
                _lighting.Restore(data);
                HideTeam();
                _setObject.SetActive(false);
            }
            if (!drawn)
                return false;
            EnsurePixels();
            if (SystemInfo.supportsAsyncGPUReadback)
            {
                _request = AsyncGPUReadback.Request(_target, 0, TextureFormat.RGBA32);
                return true;
            }
            // No asynchronous read-back on this device: read it now (once, at the welcome).
            var previous = RenderTexture.active;
            RenderTexture.active = _target;
            _picture!.ReadPixels(new Rect(0, 0, PostcardLayout.Width, PostcardLayout.Height), 0, 0, false);
            RenderTexture.active = previous;
            _picture.GetPixelData<byte>(0).CopyTo(_pixels);
            return true;
        }

        /// <summary>A wider team than the set's framing (a Giant pushed aside): the camera steps back until everyone is in.</summary>
        private void FrameTeam(PostcardSetData data, Camera camera)
        {
            var transform = camera.transform;
            float center = data.origin.InverseTransformPoint(transform.position).x;
            // The bodies' width across the picture (their capsules' ends and half width), not their reach: a long tail lies
            // along the camera's axis and never widens the picture (T42c: the reach pushed the camera back 0.76 m).
            float needed = 0f;
            for (int i = 0; i < _teamCount; i++)
            {
                _bodies[i].Segment(_positions[i], _yaws[i], _scales[i], out var a, out var b);
                float half = _bodies[i].halfWidth * _scales[i];
                needed = Mathf.Max(needed, Mathf.Max(Mathf.Abs(a.x - center), Mathf.Abs(b.x - center)) + half);
            }
            float extra = needed + 0.05f - data.frameHalfWidth;
            if (extra <= 0f)
                return;
            float tanH = Mathf.Tan(PostcardSetLayout.CameraFov * 0.5f * Mathf.Deg2Rad) * (PostcardLayout.Width / (float)PostcardLayout.Height);
            transform.position -= transform.forward * (extra / Mathf.Max(0.05f, tanH));
        }

        /// <summary>
        /// The near butterflies (by the team, at head height) whenever no posed animal reaches them, then far ones over the
        /// hill, picked by the seed, up to <see cref="PostcardSetLayout.ButterfliesShown"/>. Nothing enters anything.
        /// </summary>
        private void ShowButterflies(PostcardSetData data)
        {
            var butterflies = data.butterflies;
            if (butterflies == null || butterflies.Length == 0)
                return;
            int near = Mathf.Clamp(data.nearButterflies, 0, butterflies.Length);
            int shown = 0;
            for (int i = 0; i < near; i++)
            {
                bool show = butterflies[i] != null && Clear(data, i) && shown < PostcardSetLayout.ButterfliesShown;
                if (butterflies[i] != null)
                    butterflies[i].gameObject.SetActive(show);
                if (show)
                    shown++;
            }
            int far = butterflies.Length - near;
            int start = far > 0 ? (int)(_welcome.Seed % (uint)far) : 0;
            for (int k = 0; k < far; k++)
            {
                int i = near + (start + k) % far;
                if (butterflies[i] == null)
                    continue;
                bool show = shown < PostcardSetLayout.ButterfliesShown && Clear(data, i);
                butterflies[i].gameObject.SetActive(show);
                if (show)
                    shown++;
            }
        }

        /// <summary>The butterfly's sphere stays out of every posed animal (its body seen from above, up to its height).</summary>
        private bool Clear(PostcardSetData data, int butterfly)
        {
            var at = data.origin.InverseTransformPoint(butterfly < data.butterflies.Length ? data.butterflies[butterfly].position : Vector3.zero);
            float radius = data.butterflyRadii != null && butterfly < data.butterflyRadii.Length ? data.butterflyRadii[butterfly] : 0.15f;
            var flat = new Vector2(at.x, at.z);
            for (int i = 0; i < _teamCount; i++)
            {
                float top = _positions[i].y + _bodies[i].height * _scales[i];
                if (at.y - radius >= top + PostcardSetLayout.SeatGap)
                    continue;
                _bodies[i].Segment(_positions[i], _yaws[i], _scales[i], out var a, out var b);
                if (CollisionMath.Gap(flat, flat, radius, a, b, _bodies[i].halfWidth * _scales[i]) < PostcardSetLayout.SeatGap)
                    return false;
            }
            return true;
        }

        private void EnsurePixels()
        {
            int size = PostcardLayout.Width * PostcardLayout.Height * 4;
            if (_pixels == null || _pixels.Length != size)
                _pixels = new byte[size];
            if (_picture == null)
            {
                _picture = new Texture2D(PostcardLayout.Width, PostcardLayout.Height, TextureFormat.RGBA32, false, false)
                {
                    name = "Postcard", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
                };
            }
        }

        /// <summary>The frame's pixels are in: the render texture goes back to the pool, the PNG is encoded and written off the main thread.</summary>
        private bool ReadBack()
        {
            if (SystemInfo.supportsAsyncGPUReadback)
            {
                if (_request.hasError)
                {
                    Fail("the read-back failed");
                    return false;
                }
                _request.GetData<byte>().CopyTo(_pixels);
            }
            ReleaseTarget();
            var pixels = _pixels!;
            var store = _store;
            int id = _welcome.PostcardId;
            Volatile.Write(ref _pixelsIn, 0);
            _write = Task.Run(() =>
            {
                // A postcard is opaque (the camera may leave alpha anywhere).
                for (int i = 3; i < pixels.Length; i += 4)
                    pixels[i] = 255;
                Volatile.Write(ref _pixelsIn, 1);
                var png = ImageConversion.EncodeArrayToPNG(pixels, GraphicsFormat.R8G8B8A8_SRGB, (uint)PostcardLayout.Width, (uint)PostcardLayout.Height);
                store.Write(id, png);
            });
            return true;
        }

        private void UploadPicture()
        {
            if (_picture == null || _pixels == null)
                return;
            _picture.SetPixelData(_pixels, 0);
            _picture.Apply(false, false);
            _pictureReady = true;
        }

        private void Written()
        {
            var write = _write;
            _write = null;
            if (!_pictureReady)
                UploadPicture();
            _phase = Phase.Idle;
            if (write == null || write.IsFaulted || write.IsCanceled)
            {
                var error = write?.Exception?.GetBaseException();
                GameLog.Warning(LogCategory.Presentation, $"Postcard {_welcome.PostcardId}: not written ({error?.Message ?? "cancelled"}).");
                return;
            }
            _expeditions?.PostcardRendered(_welcome.PostcardId);
            PruneInBackground();
        }

        /// <summary>The pictures no longer kept go, off the main thread (the kept ids read here).</summary>
        private void PruneInBackground()
        {
            if (_expeditions == null)
                return;
            int count = Math.Max(0, _expeditions.PostcardCount);
            if (_kept.Length < count)
                _kept = new int[Math.Max(count, 16)];
            for (int i = 0; i < count; i++)
                _kept[i] = _expeditions.PostcardAt(i);
            var kept = (int[])_kept.Clone();
            var store = _store;
            Task.Run(() =>
            {
                try
                {
                    store.Prune(kept, count);
                }
                catch (Exception)
                {
                    // A busy file stays until the next welcome.
                }
            });
        }

        private void ReleaseTarget()
        {
            if (_target != null)
                RenderTexture.ReleaseTemporary(_target);
            _target = null;
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
