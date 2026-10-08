using System;
using PocketWilds.Simulation;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PocketWilds.Presentation
{
    /// <summary>One pending wish as its bubble shows it (filled by the source from WishService's records).</summary>
    public struct WishBubbleState
    {
        /// <summary>WishService slot as of the source's last change (slots shift when a wish is claimed).</summary>
        public int Slot;

        /// <summary>The animal expressing it (instance id): the bubble's identity (never two pending wishes per animal).</summary>
        public uint Animal;

        public int Progress;
        public int Target;
        public bool Done;
        public bool Tutorial;
        public WishSystem System;

        public float Progress01 => Done ? 1f : Target > 0 ? Mathf.Clamp01((float)Progress / Target) : 0f;

        public bool SameAs(in WishBubbleState other) =>
            Slot == other.Slot && Animal == other.Animal && Progress == other.Progress && Target == other.Target && Done == other.Done
            && Tutorial == other.Tutorial && System == other.System;
    }

    /// <summary>
    /// The pending wishes the bubbles show (implemented by the presenter over WishService: PendingCount, Get(slot)).
    /// <see cref="Version"/> changes whenever a wish is drawn, progresses, comes true, is claimed or swapped: the layer
    /// reads the wishes again only then.
    /// </summary>
    public interface IWishBubbleSource
    {
        int Version { get; }
        int Count { get; }
        bool TryGet(int index, out WishBubbleState state);
    }

    /// <summary>Told when a completed bubble bursts (doc 05 sheet 17, 250 ms after <see cref="WishBubbleLayer.PlayCompleted"/>).</summary>
    public interface IWishBubbleListener
    {
        /// <summary>
        /// The bubble bursts at <paramref name="worldPoint"/> (its center): VFX_WishDone, J-SFX-FEEL-009 and the motes
        /// start here. Also raised at once, at the place the bubble would stand, when the bubble was not showing.
        /// </summary>
        void OnWishBubbleBurst(int slot, uint animal, Vector3 worldPoint);
    }

    /// <summary>
    /// T36 wish bubbles in the world (doc 06 screen 002; doc 01 sections 2.1 and A08 to A10; plan section 7.1). Plain class
    /// ticked by RefugeController.
    /// <list type="bullet">
    /// <item>A pool of <see cref="MaxBubbles"/> bubbles (PF_World_WishBubble) built by <see cref="Prewarm"/> at scene
    /// entry; nothing is instantiated afterwards.</item>
    /// <item>One bubble per visible animal with a pending wish (an animal in the reserve has none until it walks back
    /// in). The wishes are read again only when <see cref="IWishBubbleSource.Version"/> changes.</item>
    /// <item>Placement (<see cref="WishBubblePlacement"/>) at 10 Hz; each frame the bubbles follow their animals, glide
    /// to their new place, and every step is checked before it is taken (rule « aucune collision »): a bubble whose room
    /// was entered takes another free place at once, or disappears at once until a pass finds one.</item>
    /// <item>Hidden while a screen covers the world or a hatch plays (<c>hidden</c> in <see cref="Tick"/>): they fade
    /// out, then cost nothing.</item>
    /// <item>Reduce Motion: fades only.</item>
    /// </list>
    /// No allocation after <see cref="Prewarm"/>.
    /// </summary>
    public sealed class WishBubbleLayer : IMotionSettings, IDisposable
    {
        /// <summary>The stock holds 9 wishes at most (doc 01 section 2.6): one bubble each.</summary>
        public const int MaxBubbles = 9;

        /// <summary>Placement passes: 10 per second (plan section 7.1).</summary>
        public const float PassSeconds = 0.1f;

        /// <summary>The touch area is this much wider than the bubble (about 48 dp at every zoom).</summary>
        public const float HitScale = 1.35f;

        /// <summary>Glide toward a new place (per second, exponential): calm, about 0.3 s.</summary>
        private const float GlideRate = 10f;

        /// <summary>Without a camera rig, the distance the screen-size clamp assumes (the default view, m).</summary>
        private const float DefaultDistance = 13.8f;

        private sealed class Entry
        {
            public WishBubbleView View;
            public bool Active;
            public bool Seen;
            public bool Listed;
            public bool Leaving;
            public bool Completing;
            public WishBubbleState State;
            public int Handle = AnimalScheduler.InvalidHandle;
            public bool Placed;
            public bool HasOffset;
            public int Candidate = -1;
            public Vector3 Offset;
            public Vector3 Target;
            public bool Flip;
            public bool TargetFlip;
            public bool Shown;
            public bool BurstRaised;
        }

        private readonly AnimalScheduler _scheduler;
        private readonly GameObject _prefab;
        private readonly Transform _root;
        private readonly RefugeCameraRig _rig;
        private readonly Camera _fixedCamera;
        private readonly IWishBubbleSource _source;
        private readonly Entry[] _entries = new Entry[MaxBubbles];
        private readonly BubbleRequest[] _requests = new BubbleRequest[MaxBubbles];
        private readonly BubbleSpot[] _spots = new BubbleSpot[MaxBubbles];
        private readonly int[] _requestEntry = new int[MaxBubbles];
        private readonly int[] _order = new int[MaxBubbles];
        // Animals whose bubble burst while their wish was still listed (the claim follows): no new bubble pops up for
        // them until the wish leaves the list.
        private readonly uint[] _burst = new uint[MaxBubbles];
        private readonly bool[] _burstListed = new bool[MaxBubbles];
        private int _burstCount;
        private readonly BubblePlacementBuffers _passBuffers;
        private readonly BubblePlacementBuffers _frameBuffers;
        private int _orderCount;
        private int _version;
        private bool _dirty = true;
        private bool _built;
        private float _nextPass;
        private float _radius = WishBubblePlacement.NominalRadius;
        private float _passRadius = -1f;
        private BubbleCamera _view;
        private bool _hasView;
        private bool _hidden;

        /// <param name="prefab">PF_World_WishBubble (VerticalSliceEntry._wishBubblePrefab).</param>
        /// <param name="camera">Only when there is no rig (tests): the rig's camera wins.</param>
        public WishBubbleLayer(AnimalScheduler scheduler, GameObject prefab, Transform root, RefugeCameraRig rig, IWishBubbleSource source,
            Camera camera = null)
        {
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _prefab = prefab;
            _root = root;
            _rig = rig;
            _fixedCamera = camera;
            _source = source;
            _passBuffers = new BubblePlacementBuffers(MaxBubbles, scheduler.Capacity);
            _frameBuffers = new BubblePlacementBuffers(MaxBubbles, 0);
            for (int i = 0; i < MaxBubbles; i++)
                _entries[i] = new Entry();
        }

        public bool ReduceMotion { get; set; }

        /// <summary>Told when a completed bubble bursts (VFX, sound, motes). May be null.</summary>
        public IWishBubbleListener Listener { get; set; }

        /// <summary>The bubbles' world radius now (screen-size clamp from the camera distance).</summary>
        public float Radius => _radius;

        /// <summary>Bubbles with a wish (shown, fading, bursting or waiting for a place).</summary>
        public int ActiveCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < MaxBubbles; i++)
                    if (_entries[i].Active)
                        n++;
                return n;
            }
        }

        /// <summary>Bubbles drawn this frame.</summary>
        public int ShownCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < MaxBubbles; i++)
                    if (_entries[i].Active && _entries[i].Shown && _entries[i].View != null && _entries[i].View.IsShowing)
                        n++;
                return n;
            }
        }

        public bool Built => _built;

        /// <summary>The camera snapshot of the last tick (tests).</summary>
        public BubbleCamera View => _view;

        /// <summary>
        /// Scene entry: the <see cref="MaxBubbles"/> bubbles are made now, hidden (T28 lesson: the first wish never
        /// hitches). Idempotent.
        /// </summary>
        public void Prewarm()
        {
            if (_built)
                return;
            _built = true;
            if (_prefab == null)
                return;
            for (int i = 0; i < MaxBubbles; i++)
            {
                var instance = Object.Instantiate(_prefab, _root);
                instance.name = "WishBubble" + i;
                var view = instance.GetComponent<WishBubbleView>();
                if (view == null)
                {
                    Object.Destroy(instance);
                    continue;
                }
                view.Prewarm(i);
                _entries[i].View = view;
            }
        }

        private Camera Camera => _rig != null && _rig.Camera != null ? _rig.Camera : _fixedCamera;

        // ------------------------------------------------------------------------------------------- tick

        /// <summary>
        /// One frame. <paramref name="hidden"/>: a screen covers the world or a hatch plays (doc 05: one highlight at a
        /// time); the bubbles fade out and then cost nothing.
        /// </summary>
        /// <summary>
        /// The shown bubbles as spheres of air (the circle and its two trailing dots in one sphere), refreshed each tick:
        /// insects and birds keep out of them (Collision rule: nothing enters anything).
        /// </summary>
        public int AirCount => _airCount;

        public void GetAir(int index, out Vector3 center, out float radius)
        {
            center = _airCenter[index];
            radius = _airRadius;
        }

        private readonly Vector3[] _airCenter = new Vector3[MaxBubbles];
        private int _airCount;
        private float _airRadius;

        public void Tick(float time, float deltaTime, bool hidden)
        {
            _airCount = 0;
            if (!_built)
                Prewarm();
            if (_source != null && (_dirty || _source.Version != _version))
                Read();
            var camera = Camera;
            if (camera == null)
                return;

            _hidden = hidden;
            bool any = false;
            for (int i = 0; i < MaxBubbles; i++)
            {
                var e = _entries[i];
                if (e.Active && e.View != null && (!hidden || e.View.IsShowing || e.Completing || e.Leaving))
                {
                    any = true;
                    break;
                }
            }
            if (!any)
                return;

            _view = BubbleCamera.From(camera);
            _hasView = true;
            _radius = WishBubblePlacement.WorldRadius(_rig != null ? _rig.Distance : DefaultDistance, _view.TanHalfFov);
            // While a pinch changes the size, places are worked out again every frame (they scale with the bubble).
            bool resized = Mathf.Abs(_radius - _passRadius) > _passRadius * 0.005f;
            if (!hidden && (time >= _nextPass || resized))
            {
                _nextPass = time + PassSeconds;
                _passRadius = _radius;
                Pass();
            }
            Follow(deltaTime);
            Animate(deltaTime);
            // The tail hangs under the circle: one sphere a little lower and wider holds both (bob and pop included).
            _airRadius = _radius * 1.6f;
            var down = new Vector3(0f, _radius * 0.35f, 0f);
            for (int i = 0; i < MaxBubbles; i++)
            {
                var e = _entries[i];
                if (e.Active && e.View != null && e.View.IsShowing)
                    _airCenter[_airCount++] = e.View.transform.position - down;
            }
        }

        /// <summary>The wishes changed: bubbles are matched to their animals, new ones wait for a place, gone ones fade.</summary>
        private void Read()
        {
            _version = _source.Version;
            _dirty = false;
            for (int i = 0; i < MaxBubbles; i++)
                _entries[i].Seen = false;
            for (int i = 0; i < _burstCount; i++)
                _burstListed[i] = false;
            int count = _source.Count;
            for (int k = 0; k < count; k++)
            {
                if (!_source.TryGet(k, out var state))
                    continue;
                int burst = BurstIndex(state.Animal);
                if (burst >= 0)
                {
                    _burstListed[burst] = true;
                    continue;
                }
                int found = -1;
                for (int i = 0; i < MaxBubbles; i++)
                {
                    if (_entries[i].Active && _entries[i].State.Animal == state.Animal)
                    {
                        found = i;
                        break;
                    }
                }
                if (found >= 0)
                {
                    var e = _entries[found];
                    e.Seen = true;
                    e.Listed = true;
                    if (e.Completing)
                    {
                        // Its claim is on its way: the bubble finishes its burst; only the slot follows.
                        e.State.Slot = state.Slot;
                        continue;
                    }
                    e.Leaving = false;
                    if (!e.State.SameAs(state))
                    {
                        e.State = state;
                        e.View.SetState(state);
                    }
                    continue;
                }
                int free = -1;
                for (int i = 0; i < MaxBubbles; i++)
                {
                    if (!_entries[i].Active && _entries[i].View != null)
                    {
                        free = i;
                        break;
                    }
                }
                if (free < 0)
                {
                    // Every bubble is busy (one is still bursting): read again next frame.
                    _dirty = true;
                    continue;
                }
                var entry = _entries[free];
                entry.Active = true;
                entry.Seen = true;
                entry.Listed = true;
                entry.Leaving = false;
                entry.Completing = false;
                entry.State = state;
                entry.Handle = AnimalScheduler.InvalidHandle;
                entry.Placed = false;
                entry.HasOffset = false;
                entry.Candidate = -1;
                entry.Shown = false;
                entry.BurstRaised = false;
                entry.View.Begin(state, ReduceMotion);
                // A new wish shows at the next frame's pass, not up to 100 ms later.
                _nextPass = 0f;
            }
            for (int i = _burstCount - 1; i >= 0; i--)
            {
                if (_burstListed[i])
                    continue;
                _burst[i] = _burst[_burstCount - 1];
                _burstListed[i] = _burstListed[_burstCount - 1];
                _burstCount--;
            }
            for (int i = 0; i < MaxBubbles; i++)
            {
                var e = _entries[i];
                if (!e.Active || e.Seen)
                    continue;
                e.Listed = false;
                if (!e.Completing)
                {
                    // Claimed from Today, swapped to another animal: it fades away where it stands.
                    e.Leaving = true;
                    e.Placed = false;
                    e.View.SetVisible(false, ReduceMotion);
                }
            }
        }

        /// <summary>The 10 Hz pass: every bubble's place, bursting and fading ones holding theirs.</summary>
        private void Pass()
        {
            int n = 0;
            for (int i = 0; i < MaxBubbles; i++)
            {
                var e = _entries[i];
                if (!e.Active || e.View == null)
                    continue;
                if (!ResolveHandle(e))
                {
                    e.Placed = false;
                    continue;
                }
                bool holds = e.Completing || e.Leaving;
                if (holds && !(e.View.IsShowing && e.HasOffset))
                    continue;
                _requests[n] = new BubbleRequest
                {
                    Handle = e.Handle,
                    Candidate = e.View.IsShowing ? e.Candidate : -1,
                    Done = e.State.Done || e.Completing,
                    Fixed = holds,
                    Offset = e.Offset,
                    Flip = e.Flip,
                };
                _requestEntry[n] = i;
                n++;
            }
            WishBubblePlacement.Resolve(_scheduler, _view, _radius, _requests, n, _spots, _passBuffers);
            _orderCount = 0;
            for (int k = 0; k < n; k++)
            {
                int r = _passBuffers.Order[k];
                var e = _entries[_requestEntry[r]];
                _order[_orderCount++] = _requestEntry[r];
                if (e.Completing || e.Leaving)
                    continue;
                ref var spot = ref _spots[r];
                e.Placed = spot.Placed;
                if (!spot.Placed)
                    continue;
                e.Candidate = spot.Candidate;
                e.Target = spot.Offset;
                e.TargetFlip = spot.Flip;
                if (!e.HasOffset || !e.View.IsShowing)
                {
                    // Not drawn yet: it appears straight at its place.
                    e.Offset = e.Target;
                    e.Flip = e.TargetFlip;
                    e.HasOffset = true;
                }
            }
        }

        /// <summary>The animal's scheduler handle, found again when it was registered anew (handles are reused).</summary>
        private bool ResolveHandle(Entry e)
        {
            if (e.Handle != AnimalScheduler.InvalidHandle && _scheduler.IsValid(e.Handle) && _scheduler.GetInstanceId(e.Handle) == e.State.Animal)
                return true;
            if (_scheduler.TryGetHandle(e.State.Animal, out int handle))
            {
                e.Handle = handle;
                return true;
            }
            e.Handle = AnimalScheduler.InvalidHandle;
            return false;
        }

        /// <summary>
        /// Each frame: every bubble follows its animal and glides toward its place; each step is checked against the
        /// world and the bubbles checked before it (the pass's order: bursting ones, then nearest first).
        /// </summary>
        private void Follow(float deltaTime)
        {
            _frameBuffers.ClearPlaced();
            float glide = 1f - Mathf.Exp(-GlideRate * deltaTime);
            var rotation = _view.Rotation;
            var right = new Vector3(_view.Right.x, 0f, _view.Right.z);
            float sideDeadZone = 0.02f * _radius / WishBubblePlacement.NominalRadius;
            for (int i = 0; i < MaxBubbles; i++)
                _entries[i].Shown = false;
            // The pass's order first, then any bubble it did not see (new since).
            for (int pass = 0; pass < 2; pass++)
            {
                int count = pass == 0 ? _orderCount : MaxBubbles;
                for (int k = 0; k < count; k++)
                {
                    int index = pass == 0 ? _order[k] : k;
                    var e = _entries[index];
                    if (!e.Active || e.View == null || e.Shown || (pass == 1 && InOrder(index)))
                        continue;
                    FollowOne(e, glide, rotation, right, sideDeadZone);
                }
            }
        }

        private bool InOrder(int index)
        {
            for (int k = 0; k < _orderCount; k++)
                if (_order[k] == index)
                    return true;
            return false;
        }

        private void FollowOne(Entry e, float glide, Quaternion rotation, Vector3 right, float sideDeadZone)
        {
            var view = e.View;
            bool holds = e.Completing || e.Leaving;
            bool wanted = e.Placed && !_hidden && !holds;
            if (!e.HasOffset || !ResolveHandle(e))
            {
                if (view.IsShowing)
                    view.HideNow();
                if (e.Completing)
                    Burst(e, e.HasOffset ? e.Offset : Vector3.zero);
                view.SetVisible(false, ReduceMotion);
                return;
            }
            if (!wanted && !view.IsShowing && !e.Completing)
            {
                view.SetVisible(false, ReduceMotion);
                return;
            }
            bool done = e.State.Done || e.Completing;
            var root = _scheduler.GetPosition(e.Handle);
            var offset = e.Offset;
            if (e.Placed && !holds)
            {
                offset = Vector3.Lerp(e.Offset, e.Target, glide);
                if ((offset - e.Target).sqrMagnitude < 1e-6f)
                    offset = e.Target;
            }
            float side = offset.x * right.x + offset.z * right.z;
            bool flip = side < -sideDeadZone || (side <= sideDeadZone && e.Flip && e.TargetFlip);
            if (!WishBubblePlacement.IsFree(_scheduler, _view, _frameBuffers, root + offset, flip, _radius, done, e.Handle, false))
            {
                if (e.Placed && !holds && WishBubblePlacement.IsFree(_scheduler, _view, _frameBuffers, root + e.Target, e.TargetFlip, _radius, done,
                        e.Handle, false))
                {
                    offset = e.Target;
                    flip = e.TargetFlip;
                }
                else if (!holds && WishBubblePlacement.TryPlace(_scheduler, _view, _radius, e.Handle, e.Candidate, done, _frameBuffers, false,
                             out var spot))
                {
                    // Its room was entered between two passes: it takes the next free place at once.
                    e.Candidate = spot.Candidate;
                    e.Target = offset = spot.Offset;
                    e.TargetFlip = flip = spot.Flip;
                    e.Placed = true;
                }
                else
                {
                    // Nowhere free: gone at once, back when a pass finds a place.
                    view.HideNow();
                    view.SetVisible(false, ReduceMotion);
                    e.Placed = false;
                    if (e.Completing)
                        Burst(e, offset);
                    return;
                }
            }
            e.Offset = offset;
            e.Flip = flip;
            e.Shown = true;
            WishBubblePlacement.AddPlaced(_view, _frameBuffers, root + offset, flip, _radius, done, false);
            view.Place(root + offset, rotation, _radius, flip);
            if (!holds)
                view.SetVisible(wanted, ReduceMotion);
        }

        private void Animate(float deltaTime)
        {
            for (int i = 0; i < MaxBubbles; i++)
            {
                var e = _entries[i];
                if (!e.Active || e.View == null)
                    continue;
                var view = e.View;
                if (e.Completing && _hidden)
                {
                    // A screen opened during the burst: it ends now.
                    Burst(e, e.Offset);
                    continue;
                }
                view.Tick(deltaTime);
                if (view.BurstThisTick)
                    RaiseBurst(e, e.Offset);
                if (e.Completing && view.CompletionFinished)
                    Release(e);
                else if (e.Leaving && !view.IsShowing)
                    Release(e);
            }
        }

        private void RaiseBurst(Entry e, Vector3 offset)
        {
            if (e.BurstRaised)
                return;
            e.BurstRaised = true;
            var point = e.Handle != AnimalScheduler.InvalidHandle && _scheduler.IsValid(e.Handle) ? _scheduler.GetPosition(e.Handle) + offset : offset;
            Listener?.OnWishBubbleBurst(e.State.Slot, e.State.Animal, point);
        }

        /// <summary>The burst without its animation (not showing, hidden): the listener is told once, the bubble freed.</summary>
        private void Burst(Entry e, Vector3 offset)
        {
            RaiseBurst(e, offset);
            Release(e);
        }

        private int BurstIndex(uint animal)
        {
            for (int i = 0; i < _burstCount; i++)
                if (_burst[i] == animal)
                    return i;
            return -1;
        }

        private void Release(Entry e)
        {
            if (e.Completing && e.Listed && _burstCount < MaxBubbles && BurstIndex(e.State.Animal) < 0)
            {
                _burst[_burstCount] = e.State.Animal;
                _burstListed[_burstCount] = true;
                _burstCount++;
            }
            e.View.Release();
            e.Active = false;
            e.Completing = false;
            e.Leaving = false;
            e.Placed = false;
            e.HasOffset = false;
            e.Shown = false;
            e.Handle = AnimalScheduler.InvalidHandle;
            // Its animal may have a new wish waiting for a free bubble.
            _dirty = true;
        }

        // ------------------------------------------------------------------------------------------- touch and anchors

        /// <summary>
        /// A tap ray meets a shown bubble (nearest along the ray; the touch area is <see cref="HitScale"/> × the bubble).
        /// <paramref name="slot"/>: the WishService slot of its wish. Test it before the egg and the animals.
        /// </summary>
        public bool TryHit(Ray ray, out int slot) => TryHit(ray, out slot, out _);

        public bool TryHit(Ray ray, out int slot, out uint animal)
        {
            slot = -1;
            animal = 0;
            float best = float.MaxValue;
            float radius = _radius * HitScale;
            for (int i = 0; i < MaxBubbles; i++)
            {
                var e = _entries[i];
                if (!e.Active || !e.Shown || e.Completing || e.Leaving || !e.Listed || e.View.Alpha < 0.5f || !ResolveHandle(e))
                    continue;
                var center = _scheduler.GetPosition(e.Handle) + e.Offset;
                var toCenter = center - ray.origin;
                float along = Vector3.Dot(toCenter, ray.direction);
                if (along <= 0f || along >= best)
                    continue;
                if ((toCenter - ray.direction * along).sqrMagnitude > radius * radius)
                    continue;
                best = along;
                slot = e.State.Slot;
                animal = e.State.Animal;
            }
            return slot >= 0;
        }

        /// <summary>
        /// Screen point (pixels) of the bubble of this slot's wish: the wish card opens there, the tutorial paw (A08) taps
        /// there. Also while the bubble waits hidden (the place it would take). False when its animal is not visible.
        /// </summary>
        public bool TryGetScreenPoint(int slot, out Vector2 screen) => TryGetScreenPoint(slot, out screen, out _);

        /// <param name="radiusPixels">The bubble's radius on screen.</param>
        public bool TryGetScreenPoint(int slot, out Vector2 screen, out float radiusPixels)
        {
            screen = default;
            radiusPixels = 0f;
            var camera = Camera;
            if (camera == null || !TryGetWorldPoint(slot, out var center))
                return false;
            var point = camera.WorldToScreenPoint(center);
            if (point.z <= 0f)
                return false;
            screen = new Vector2(point.x, point.y);
            radiusPixels = _radius * camera.pixelHeight / (2f * point.z * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad));
            return true;
        }

        /// <summary>World center of the bubble of this slot's wish (or of the place it would take).</summary>
        public bool TryGetWorldPoint(int slot, out Vector3 center)
        {
            center = default;
            var e = BySlot(slot);
            if (e == null || !ResolveHandle(e))
                return false;
            center = _scheduler.GetPosition(e.Handle) + OffsetOf(e);
            return true;
        }

        private Vector3 OffsetOf(Entry e)
        {
            if (e.HasOffset)
                return e.Offset;
            _scheduler.GetBody(e.Handle, out _, out _, out _, out float groundY, out float height);
            var view = _hasView ? _view : Camera != null ? BubbleCamera.From(Camera) : default;
            return WishBubblePlacement.CandidateOffset(view, groundY + height, _scheduler.GetPosition(e.Handle).y, _radius, 0, out _);
        }

        private Entry BySlot(int slot)
        {
            if (slot < 0)
                return null;
            for (int i = 0; i < MaxBubbles; i++)
            {
                var e = _entries[i];
                if (e.Active && e.Listed && !e.Completing && e.State.Slot == slot)
                    return e;
            }
            return null;
        }

        /// <summary>
        /// The wish of this slot was claimed: the ring fills, the bubble bursts and the listener is told at the burst (doc
        /// 05 sheet 17). Call it before WishService's claim, or in the same frame (slots are read as of the last tick).
        /// A bubble that is not showing bursts at once (the listener is still told).
        /// </summary>
        public bool PlayCompleted(int slot) => Complete(BySlot(slot));

        /// <summary>Same, by the animal expressing the wish (whatever its slot now).</summary>
        public bool PlayCompletedForAnimal(uint animal)
        {
            for (int i = 0; i < MaxBubbles; i++)
            {
                var e = _entries[i];
                if (e.Active && !e.Completing && e.State.Animal == animal)
                    return Complete(e);
            }
            return false;
        }

        private bool Complete(Entry e)
        {
            if (e == null)
                return false;
            if (_hidden || !e.Shown || !e.View.IsShowing || e.Leaving || !ResolveHandle(e))
            {
                e.Completing = true;
                e.BurstRaised = false;
                RaiseBurst(e, e.HasOffset ? e.Offset : ResolveHandle(e) ? OffsetOf(e) : Vector3.zero);
                Release(e);
                return true;
            }
            e.Completing = true;
            e.BurstRaised = false;
            e.View.PlayCompleted(ReduceMotion);
            return true;
        }

        // ------------------------------------------------------------------------------------------- tests

        /// <summary>
        /// Smallest free space (m) between the bubbles drawn now, as drawn (their circle and dots at their current scale
        /// and bob), and the world (props, crowns, the egg's room, every animal's body column) or another drawn bubble.
        /// Negative: something entered something. Allocation-free (tests check it every frame).
        /// </summary>
        public float MinClearance()
        {
            float best = AnimalScheduler.ClearanceHorizon;
            for (int i = 0; i < MaxBubbles; i++)
            {
                if (!DrawnSpheres(i, out var a0, out float ra0, out var a1, out float ra1, out var a2, out float ra2))
                    continue;
                best = Mathf.Min(best, _scheduler.AirClearance(a0, ra0, AnimalScheduler.InvalidHandle));
                best = Mathf.Min(best, _scheduler.AirClearance(a1, ra1, AnimalScheduler.InvalidHandle));
                best = Mathf.Min(best, _scheduler.AirClearance(a2, ra2, AnimalScheduler.InvalidHandle));
                for (int j = i + 1; j < MaxBubbles; j++)
                {
                    if (!DrawnSpheres(j, out var b0, out float rb0, out var b1, out float rb1, out var b2, out float rb2))
                        continue;
                    best = Mathf.Min(best, Gap(a0, ra0, b0, rb0, b1, rb1, b2, rb2));
                    best = Mathf.Min(best, Gap(a1, ra1, b0, rb0, b1, rb1, b2, rb2));
                    best = Mathf.Min(best, Gap(a2, ra2, b0, rb0, b1, rb1, b2, rb2));
                }
            }
            return best;
        }

        private static float Gap(Vector3 a, float ra, Vector3 b0, float r0, Vector3 b1, float r1, Vector3 b2, float r2) =>
            Mathf.Min(Mathf.Min(Vector3.Distance(a, b0) - ra - r0, Vector3.Distance(a, b1) - ra - r1), Vector3.Distance(a, b2) - ra - r2);

        /// <summary>The three spheres of bubble <paramref name="index"/> as drawn now (false when not drawn).</summary>
        public bool DrawnSpheres(int index, out Vector3 c0, out float r0, out Vector3 c1, out float r1, out Vector3 c2, out float r2)
        {
            c0 = c1 = c2 = default;
            r0 = r1 = r2 = 0f;
            var e = _entries[index];
            if (!e.Active || e.View == null || !e.View.IsShowing || !_hasView)
                return false;
            float radius = e.View.DrawnRadius;
            WishBubblePlacement.VisualSpheres(_view, e.View.DrawnCenter, e.View.DrawnFlip, radius, out c0, out r0, out c1, out r1, out c2, out r2);
            return true;
        }

        /// <summary>Bubble <paramref name="index"/> (0 to <see cref="MaxBubbles"/> - 1): its animal, its slot, drawn or not.</summary>
        public bool TryGetBubble(int index, out uint animal, out int slot, out bool drawn, out int candidate)
        {
            var e = _entries[index];
            animal = e.State.Animal;
            slot = e.State.Slot;
            drawn = e.Active && e.View != null && e.View.IsShowing;
            candidate = e.Candidate;
            return e.Active;
        }

        /// <summary>The pooled view of bubble <paramref name="index"/> (tests, captures).</summary>
        public WishBubbleView GetView(int index) => _entries[index].View;

        public void Dispose()
        {
            for (int i = 0; i < MaxBubbles; i++)
            {
                var view = _entries[i].View;
                _entries[i].View = null;
                _entries[i].Active = false;
                if (view != null)
                    Object.Destroy(view.gameObject);
            }
        }
    }
}
