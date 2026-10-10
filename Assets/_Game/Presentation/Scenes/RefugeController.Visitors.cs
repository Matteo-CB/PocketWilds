using System;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Services.Feedback;
using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T44C « Visiteurs rares », the refuge's side (doc « 01 Rétention », rare visitors; doc 05 sheet 25 « Arrivée d'un
    /// visiteur »). When <see cref="IVisitors.VisitChanged"/> says a visitor came, it appears at the edge of the view in its
    /// species' look with its seasonal variant and walks to a free spot on tested paths (the scheduler checks every step and the
    /// other animals step around it: nothing enters anything), 8 s at most, leaving a round soft dot behind it every 250 ms
    /// (VisitorTrail, 8 visible at most); on arrival a warm ring on the grass and its sound when it is in the frame, then it
    /// stays there and looks at the refuge (idle and looking only, never wandering off). Reduce Motion: it appears softly at its
    /// spot, without a trail. A touch on it greets it (<see cref="IVisitors.TryGreet"/>): the gift in a pop, its sound and a
    /// Confirm vibration, a small visitor badge over its head; when it joins the collection the « Nouveau visiteur ! » banner
    /// and hearts, and it stays as a normal animal; else it walks back out after the greeting. It walks out when the visit
    /// ends. Hidden with the other animals during an activity; it waits for a hatch to end before coming in. No allocation
    /// per frame (the profile copy, the gift text and the badge are made once per visit or greeting).
    /// </summary>
    public sealed partial class RefugeController
    {
        /// <summary>The visitor's instance id in the scheduler (never an id of the collection).</summary>
        public const uint VisitorInstanceId = 0xFFFFFF01u;

        /// <summary>Doc 05 sheet 25: 8 s of walking at most, a dot every 250 ms.</summary>
        public const float VisitorWalkSeconds = 8f;

        public const float VisitorTrailSeconds = 0.25f;

        /// <summary>The soft appearance (Reduce Motion) and disappearance, and how long the badge stays after a touch.</summary>
        private const float VisitorFadeSeconds = 0.4f;

        private const float VisitorBadgeSeconds = 2.5f;

        /// <summary>After a greeting that did not make it join, the visitor lingers this long, then walks out.</summary>
        private const float VisitorLingerSeconds = 1.6f;

        private const float VisitorBodyRadius = 0.35f;

        private enum VisitorPhase : byte
        {
            None = 0,
            WalkingIn = 1,
            Seated = 2,
            Greeted = 3,
            WalkingOut = 4,
            Leaving = 5,
        }

        private IVisitors _visitors;
        private Action _onVisitChanged;
        private bool _visitDirty;
        private VisitorPhase _visitorPhase;
        private AnimalView _visitorView;
        private int _visitorHandle = AnimalScheduler.InvalidHandle;
        private float _visitorUntil;
        private float _visitorTrailNext;
        private float _visitorFadeT = -1f;
        private float _visitorScale = 1f;
        private bool _visitorFadingOut;
        private bool _visitorHidden;
        private float _visitorSide = 1f;
        private string _visitorKey;
        private ContentId _visitorProfileSpecies;
        private BehaviorProfile _visitorProfile;
        private SpriteRenderer _visitorBadge;
        private bool _visitorBadgeSearched;
        private float _visitorBadgeUntil = -1f;
        private int _visitorBadgeHandle = AnimalScheduler.InvalidHandle;

        /// <summary>T44C: the rare visitors (set by the scene entry; null in scenes without them). Set it to null before the refuge goes.</summary>
        public IVisitors Visitors
        {
            get => _visitors;
            set
            {
                if (ReferenceEquals(_visitors, value))
                    return;
                if (_visitors != null && _onVisitChanged != null)
                    _visitors.VisitChanged -= _onVisitChanged;
                _visitors = value;
                _onVisitChanged ??= () => _visitDirty = true;
                if (_visitors != null)
                    _visitors.VisitChanged += _onVisitChanged;
                _visitDirty = true;
                if (_visitors == null)
                    RemoveVisitor();
            }
        }

        /// <summary>Tests and diagnostics: the visitor is in the refuge (walking, seated or leaving), where, and its phase.</summary>
        public bool VisitorShown => _visitorView != null;

        public bool VisitorSeated => _visitorPhase == VisitorPhase.Seated;

        public bool VisitorLeaving => _visitorPhase == VisitorPhase.WalkingOut || _visitorPhase == VisitorPhase.Leaving;

        public Vector3 VisitorPosition => _scheduler.IsValid(_visitorHandle) ? _scheduler.GetPosition(_visitorHandle) : Vector3.zero;

        public int VisitorHandle => _visitorHandle;

        /// <summary>The visitor in the refuge (visitor.meadow_rabbit…), null when none.</summary>
        public string VisitorKey => _visitorKey;

        public bool VisitorBadgeShown => _visitorBadge != null && _visitorBadge.enabled;

        /// <summary>Per frame from <see cref="Tick"/>.</summary>
        private void TickVisitors(float time, float deltaTime)
        {
            if (_visitors == null && _visitorView == null)
                return;
            bool present = _visitors != null && _visitors.IsOpen && _visitors.Present;
            if (_visitDirty || (present && _visitorView == null) || (!present && _visitorPhase is VisitorPhase.WalkingIn or VisitorPhase.Seated))
            {
                if (present && _visitorView == null && !Hatching && !_actorsHidden)
                {
                    _visitDirty = false;
                    SpawnVisitor(time);
                }
                else if (!present && _visitorView != null && _visitorPhase is VisitorPhase.WalkingIn or VisitorPhase.Seated)
                {
                    _visitDirty = false;
                    WalkVisitorOut(time);
                }
                else if (!present || _visitorView != null)
                {
                    _visitDirty = false;
                }
            }
            if (_visitorView == null)
                return;
            if (_visitorHidden != _actorsHidden)
            {
                _visitorHidden = _actorsHidden;
                ShowActor(_visitorView, !_actorsHidden);
            }
            bool valid = _scheduler.IsValid(_visitorHandle);
            switch (_visitorPhase)
            {
                case VisitorPhase.WalkingIn:
                    if (valid && _scheduler.GetGoalLegs(_visitorHandle) > 0 && time < _visitorUntil)
                        Trail(time);
                    else
                        SeatVisitor(time);
                    break;
                case VisitorPhase.Greeted:
                    if (time >= _visitorUntil)
                        WalkVisitorOut(time);
                    break;
                case VisitorPhase.WalkingOut:
                    if (valid && _scheduler.GetGoalLegs(_visitorHandle) > 0 && time < _visitorUntil)
                        Trail(time);
                    else
                        StartLeaving();
                    break;
            }
            TickVisitorFade(deltaTime);
            TickVisitorBadge(time);
        }

        private void Trail(float time)
        {
            if (time < _visitorTrailNext || _actorsHidden || _context.State.Settings.ReduceMotion)
                return;
            _visitorTrailNext = time + VisitorTrailSeconds;
            _vfx?.Play(VfxId.VisitorTrail, _scheduler.GetPosition(_visitorHandle) + new Vector3(0f, 0.03f, 0f), time);
        }

        // ---------------- arriving ----------------

        private void SpawnVisitor(float time)
        {
            var visit = _visitors.Current;
            if (visit.Species == ContentId.None)
                return;
            _visitorKey = visit.Key;
            uint seed = SeedHash.Mix((uint)visit.ArriveUnixMs ^ (uint)(visit.ArriveUnixMs >> 32), visit.Species.Value);
            var animal = new AnimalInstance
            {
                InstanceId = VisitorInstanceId, Species = visit.Species, Variant = visit.Variant, Seed = seed, Rarity = RarityTier.Common,
            };
            bool reduce = _context.State.Settings.ReduceMotion;
            var profile = VisitorProfileOf(visit.Species);
            var right = ViewRight();
            var home = VisitorHome(seed, right);
            _visitorSide = (seed & 1u) == 0u ? -1f : 1f;
            var start = reduce ? home : VisitorEntry(home, right * _visitorSide, profile.moveSpeed);
            float yaw = Quaternion.LookRotation(home - start == Vector3.zero ? -right * _visitorSide : home - start).eulerAngles.y;
            if (!_context.Visuals.TryResolve(animal, start, yaw, out var snapshot) || !TryAcquire(snapshot, out var view))
            {
                GameLog.Warning(LogCategory.Presentation, "Visitor " + visit.Key + ": no view of its species in the refuge (it visits unseen).");
                return;
            }
            int handle = _scheduler.Register(view, VisitorInstanceId, seed, start, yaw, profile, time, view.transform.localScale.x, false);
            if (handle == AnimalScheduler.InvalidHandle)
            {
                _animals.Release(view);
                return;
            }
            _visitorView = view;
            _visitorHandle = handle;
            _visitorScale = view.transform.localScale.x;
            _visitorHidden = false;
            if (_actorsHidden)
            {
                _visitorHidden = true;
                ShowActor(view, false);
            }
            _visitorTrailNext = time;
            if (!reduce && _scheduler.TryWalkTo(handle, home, time))
            {
                _visitorPhase = VisitorPhase.WalkingIn;
                _visitorUntil = time + VisitorWalkSeconds;
            }
            else
            {
                // Reduce Motion (or no way in): it appears softly where it will stay.
                StartFade(false);
                SeatVisitor(time);
            }
        }

        /// <summary>The right of the view along the ground.</summary>
        private Vector3 ViewRight()
        {
            var camera = _rig != null ? _rig.Camera : null;
            if (camera == null)
                return Vector3.right;
            var right = camera.transform.right;
            right.y = 0f;
            return right.sqrMagnitude > 1e-4f ? right.normalized : Vector3.right;
        }

        /// <summary>A free spot near the view's centre where the visitor will sit (a few seeded tries, the clearest kept).</summary>
        private Vector3 VisitorHome(uint seed, Vector3 right)
        {
            var focus = ViewFocus;
            var forward = new Vector3(-right.z, 0f, right.x);
            for (int k = 0; k < 6; k++)
            {
                float across = (SeedHash.Unit(seed, 0x7151A000u + (uint)k) - 0.5f) * 2.4f;
                float depth = (SeedHash.Unit(seed, 0x7151B000u + (uint)k) - 0.5f) * 1.2f;
                var point = Grounded(_walkArea.Clamp(focus + right * across + forward * depth));
                if (_scheduler.IsDiskClear(point, VisitorBodyRadius, 0.5f, true) && !_scheduler.InEggApron(point))
                    return point;
            }
            return Grounded(_walkArea.Clamp(focus));
        }

        /// <summary>
        /// Where it comes in: the edge of the visible ground on its side, no farther than it walks in 7 s, on a free place;
        /// nearer when the edge is taken.
        /// </summary>
        private Vector3 VisitorEntry(Vector3 home, Vector3 outward, float speed)
        {
            float maxRun = Mathf.Max(1f, speed * (VisitorWalkSeconds - 1f));
            float run = maxRun;
            var camera = _rig != null ? _rig.Camera : null;
            if (camera != null)
            {
                float edgeX = Vector3.Dot(outward, ViewRight()) > 0f ? 0.97f : 0.03f;
                var ray = camera.ViewportPointToRay(new Vector3(edgeX, 0.42f, 0f));
                if (ray.direction.y < -0.01f)
                {
                    float t = (home.y - ray.origin.y) / ray.direction.y;
                    var edge = ray.origin + ray.direction * t;
                    edge.y = home.y;
                    run = Mathf.Clamp(Vector3.Distance(edge, home), 1f, maxRun);
                }
            }
            for (int step = 0; step < 5; step++)
            {
                var point = Grounded(_walkArea.Clamp(home + outward * (run * (1f - step * 0.18f))));
                if (_scheduler.IsDiskClear(point, VisitorBodyRadius, 0.5f, true))
                    return point;
            }
            return FreeArrivalPoint(home, outward);
        }

        private void SeatVisitor(float time)
        {
            _visitorPhase = VisitorPhase.Seated;
            if (!_scheduler.IsValid(_visitorHandle))
                return;
            var position = _scheduler.GetPosition(_visitorHandle);
            _vfx?.Play(VfxId.VisitorArrive, position + new Vector3(0f, 0.02f, 0f), time);
            if (InFrame(position))
                _context.Audio.Play(AudioEventId.VisitorArrive, position + new Vector3(0f, 0.2f, 0f), _visitorView != null ? _visitorView.Species : default);
        }

        /// <summary>
        /// A copy of its species' behaviour that only idles and looks around (it stays on its spot and watches the refuge);
        /// made once per species visiting.
        /// </summary>
        private BehaviorProfile VisitorProfileOf(ContentId species)
        {
            if (_visitorProfile != null && _visitorProfileSpecies == species)
                return _visitorProfile;
            var profile = JsonUtility.FromJson<BehaviorProfile>(JsonUtility.ToJson(ProfileOf(species)));
            profile.wanderWeight = 0f;
            profile.eatWeight = 0f;
            profile.inspectWeight = 0f;
            profile.sleepWeight = 0f;
            profile.socialWeight = 0f;
            profile.idleWeight = 0.45f;
            profile.idleLookWeight = 0.55f;
            profile.idleSeconds = new Vector2(4f, 9f);
            _visitorProfile = profile;
            _visitorProfileSpecies = species;
            return profile;
        }

        // ---------------- the touch ----------------

        /// <summary>From <see cref="OnTap"/>: a touch on the visitor greets it. True when the touch was the visitor's.</summary>
        private bool TryTapVisitor(Ray ray)
        {
            if (_visitorView == null || _visitors == null || !_scheduler.IsValid(_visitorHandle) || _actorsHidden)
                return false;
            if (_visitorPhase != VisitorPhase.WalkingIn && _visitorPhase != VisitorPhase.Seated)
                return false;
            float scale = Mathf.Max(0.5f, _visitorScale);
            var center = _scheduler.GetPosition(_visitorHandle) + new Vector3(0f, 0.25f * scale, 0f);
            if (!HitsSphere(ray, center, AnimalTapRadius * scale))
                return false;
            GreetVisitor();
            return true;
        }

        private void GreetVisitor()
        {
            var position = _scheduler.GetPosition(_visitorHandle);
            if (!_visitors.TryGreet(out var greeting))
            {
                WalkVisitorOut(_time);
                return;
            }
            _scheduler.Poke(_visitorHandle, _time);
            _context.Audio.Play(AudioEventId.GiftOpen, position + new Vector3(0f, 0.2f, 0f), _visitorView.Species);
            _context.Haptics.Play(HapticEventId.Confirm);
            _hud?.ShowToast(ToastKind.Reward, GiftText(greeting.Gift));
            ShowBadge(_visitorHandle);
            if (greeting.Joined)
            {
                _hud?.ShowToast(ToastKind.Visitor, _context.Localization.Get(JournalText.NewVisitor));
                _vfx?.Play(VfxId.FavoriteHeart, position + new Vector3(0f, 0.3f * Mathf.Max(0.5f, _visitorScale), 0f), _time);
                JoinVisitor(greeting.JoinedInstanceId);
                return;
            }
            _visitorPhase = VisitorPhase.Greeted;
            _visitorUntil = _time + VisitorLingerSeconds;
        }

        /// <summary>It joins: its place goes to the collection's new animal, at the same pose, living as the others do.</summary>
        private void JoinVisitor(uint instanceId)
        {
            var position = _scheduler.GetPosition(_visitorHandle);
            float yaw = _scheduler.GetYaw(_visitorHandle);
            RemoveVisitor();
            if (instanceId == 0u || IsVisible(instanceId) || !_context.State.Collection.TryGet(instanceId, out var animal))
                return;
            if (!_context.Visuals.TryResolve(animal, position, yaw, out var snapshot) || !TryAcquire(snapshot, out var view))
                return;
            AddToMeadow(view, animal.Species, instanceId, animal.Seed, RareGlow.KindOf(animal.Rarity, animal.TraitBits), emerging: false);
            int index = ViewIndexOf(instanceId);
            if (index >= 0)
                ShowBadge(_agentByView[index]);
        }

        private string GiftText(in RewardBundle gift)
        {
            var text = _context.Localization;
            if (gift.Gems > 0)
                return Counted(text, JournalText.GiftGems, gift.Gems);
            if (gift.LightMinutes > 0.0)
                return Counted(text, JournalText.GiftLight, (long)Math.Round(gift.LightMinutes));
            if (gift.Treats > 0)
                return Counted(text, JournalText.GiftTreats, gift.Treats);
            if (gift.Seeds > 0)
                return Counted(text, JournalText.GiftSeeds, gift.Seeds);
            if (gift.Tickets > 0)
                return Counted(text, JournalText.GiftTickets, gift.Tickets);
            if (gift.Stickers > 0)
                return Counted(text, JournalText.GiftStickers, gift.Stickers);
            return text.Get(JournalText.GiftSome);
        }

        private static string Counted(ILocalization text, string key, long n) =>
            text.GetPlural(key, n).Replace("{n}", n.ToString(System.Globalization.CultureInfo.InvariantCulture));

        // ---------------- leaving ----------------

        private void WalkVisitorOut(float time)
        {
            if (_visitorView == null || !_scheduler.IsValid(_visitorHandle))
            {
                RemoveVisitor();
                return;
            }
            var position = _scheduler.GetPosition(_visitorHandle);
            if (_context.State.Settings.ReduceMotion || _actorsHidden)
            {
                StartLeaving();
                return;
            }
            var exit = VisitorEntry(position, ViewRight() * _visitorSide, _visitorProfile != null ? _visitorProfile.moveSpeed : 0.38f);
            if (_scheduler.TryWalkTo(_visitorHandle, exit, time))
            {
                _visitorPhase = VisitorPhase.WalkingOut;
                _visitorUntil = time + VisitorWalkSeconds;
                _visitorTrailNext = time;
                return;
            }
            StartLeaving();
        }

        /// <summary>At the edge (or now): it shrinks away softly, then its view goes back to the pool.</summary>
        private void StartLeaving()
        {
            _visitorPhase = VisitorPhase.Leaving;
            StartFade(true);
        }

        private void StartFade(bool fadingOut)
        {
            _visitorFadingOut = fadingOut;
            _visitorFadeT = 0f;
            if (_visitorView != null)
                _visitorView.SetScale(fadingOut ? _visitorScale : _visitorScale * 0.6f);
        }

        private void TickVisitorFade(float deltaTime)
        {
            if (_visitorFadeT < 0f || _visitorView == null)
                return;
            _visitorFadeT += deltaTime;
            float k = Mathf.Clamp01(_visitorFadeT / VisitorFadeSeconds);
            float eased = k * k * (3f - 2f * k);
            float scale = _visitorFadingOut ? Mathf.Lerp(_visitorScale, 0.01f, eased) : Mathf.Lerp(_visitorScale * 0.6f, _visitorScale, eased);
            _visitorView.SetScale(scale);
            if (k < 1f)
                return;
            _visitorFadeT = -1f;
            if (_visitorFadingOut)
                RemoveVisitor();
        }

        /// <summary>The visitor's view back to its pool, its place in the scheduler freed.</summary>
        private void RemoveVisitor()
        {
            if (_visitorHandle != AnimalScheduler.InvalidHandle && _scheduler.IsValid(_visitorHandle))
                _scheduler.Unregister(_visitorHandle);
            if (_visitorBadgeHandle == _visitorHandle)
                HideBadge();
            _visitorHandle = AnimalScheduler.InvalidHandle;
            if (_visitorView != null)
            {
                if (_visitorHidden)
                    ShowActor(_visitorView, true);
                _visitorView.SetScale(_visitorScale);
                _animals.Release(_visitorView);
            }
            _visitorView = null;
            _visitorHidden = false;
            _visitorPhase = VisitorPhase.None;
            _visitorFadeT = -1f;
            _visitorKey = null;
        }

        // ---------------- the badge ----------------

        /// <summary>The small visitor badge over the head of the animal at <paramref name="handle"/> for a moment (made once, the paw as its picture).</summary>
        private void ShowBadge(int handle)
        {
            if (!EnsureBadge())
                return;
            _visitorBadgeHandle = handle;
            _visitorBadgeUntil = _time + VisitorBadgeSeconds;
            _visitorBadge.enabled = true;
            TickVisitorBadge(_time);
        }

        private void HideBadge()
        {
            _visitorBadgeHandle = AnimalScheduler.InvalidHandle;
            _visitorBadgeUntil = -1f;
            if (_visitorBadge != null)
                _visitorBadge.enabled = false;
        }

        private bool EnsureBadge()
        {
            if (_visitorBadge != null)
                return true;
            if (_visitorBadgeSearched)
                return false;
            _visitorBadgeSearched = true;
            Sprite picture = null;
            foreach (var sprite in Resources.FindObjectsOfTypeAll<Sprite>())
            {
                if (sprite == null)
                    continue;
                if (sprite.name == "ICO_Visitor")
                {
                    picture = sprite;
                    break;
                }
                if (sprite.name == "ICO_Paw")
                    picture = sprite;
            }
            if (picture == null)
                return false;
            var badge = new GameObject("VisitorBadge");
            badge.transform.SetParent(_root, false);
            _visitorBadge = badge.AddComponent<SpriteRenderer>();
            _visitorBadge.sprite = picture;
            _visitorBadge.color = new Color(1f, 0.83f, 0.55f, 1f);
            _visitorBadge.sortingOrder = 10;
            var size = picture.bounds.size;
            float s = 0.16f / Mathf.Max(0.001f, Mathf.Max(size.x, size.y));
            badge.transform.localScale = new Vector3(s, s, s);
            _visitorBadge.enabled = false;
            return true;
        }

        private void TickVisitorBadge(float time)
        {
            if (_visitorBadge == null || !_visitorBadge.enabled)
                return;
            if (time >= _visitorBadgeUntil || !_scheduler.IsValid(_visitorBadgeHandle) || _actorsHidden)
            {
                HideBadge();
                return;
            }
            _scheduler.GetBody(_visitorBadgeHandle, out _, out _, out _, out float groundY, out float height);
            var position = _scheduler.GetPosition(_visitorBadgeHandle);
            position.y = groundY + height + 0.12f;
            var camera = _rig != null ? _rig.Camera : null;
            var transform = _visitorBadge.transform;
            if (camera != null)
                transform.SetPositionAndRotation(position, camera.transform.rotation);
            else
                transform.position = position;
        }

        private bool InFrame(Vector3 position)
        {
            var camera = _rig != null ? _rig.Camera : null;
            if (camera == null)
                return false;
            var viewport = camera.WorldToViewportPoint(position);
            return viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
        }
    }
}
