using System;
using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.Presentation
{
    // The pooled items, the basket on the head, the animal's hop and lean, the effects (collision rule: nothing enters
    // anything; a control before each placement, no physics).
    public sealed partial class NutDropStage
    {
        private const byte VFree = 0;
        private const byte VFalling = 1;
        private const byte VToPile = 2;
        private const byte VPile = 3;
        private const byte VAirborne = 4;
        private const byte VGround = 5;
        private const byte VPop = 6;

        private const float AppearSeconds = 0.16f;
        private const float ToPileSeconds = 0.2f;
        private const float PopSeconds = 0.28f;
        private const float RecycleSeconds = 0.14f;
        private const float GroundRestSeconds = 0.2f;
        private const float GroundFadeSeconds = 0.35f;
        private const float Gravity = -6.5f;
        private const float TrailInterval = 0.045f;
        private const float TrailLifetime = 0.27f;
        private const float RingSeconds = 0.75f;
        private const float LeafRestSeconds = 0.3f;
        private const float LeafFadeSeconds = 0.3f;

        // Game slots -> pooled visuals.
        private int[] _slotVisual = Array.Empty<int>();

        // The pooled visuals (the set's item renderers).
        private Transform[] _vTransform = Array.Empty<Transform>();
        private MeshFilter[] _vFilter = Array.Empty<MeshFilter>();
        private MeshRenderer[] _vRenderer = Array.Empty<MeshRenderer>();
        private byte[] _vState = Array.Empty<byte>();
        private NutDropItemKind[] _vKind = Array.Empty<NutDropItemKind>();
        private int[] _vSlot = Array.Empty<int>();
        private int[] _vSpot = Array.Empty<int>();
        private Vector3[] _vPos = Array.Empty<Vector3>();
        private Vector3[] _vVel = Array.Empty<Vector3>();
        private Vector3[] _vFrom = Array.Empty<Vector3>();
        private Vector3[] _vAxis = Array.Empty<Vector3>();
        private Quaternion[] _vRest = Array.Empty<Quaternion>();
        private float[] _vT = Array.Empty<float>();
        private float[] _vDuration = Array.Empty<float>();
        private float[] _vSpin = Array.Empty<float>();
        private float[] _vSpinRate = Array.Empty<float>();
        private float[] _vRadius = Array.Empty<float>();
        private float[] _vScale = Array.Empty<float>();
        private float[] _vTrail = Array.Empty<float>();
        private float[] _vShown = Array.Empty<float>();
        private readonly uint[] _kindValues = new uint[NutDropLayout.KindCount];
        private uint _spawnSeed = 0x4E555453u;

        // The pile in the basket: spots in arrival order (a ring), their offsets from the basket's pivot (world metres).
        private readonly int[] _pile = new int[NutDropLayout.PileMax];
        private readonly Vector3[] _pileOffset = new Vector3[NutDropLayout.PileMax];
        private int _pileCount;
        private float _pileWidth = -1f;

        /// <summary>The pile's balls: the largest catchable item's radius as it settles (m).</summary>
        private float _pileRadius = 0.06f;

        // The basket.
        private Transform _basket;
        private uint _basketValue;
        private float _basketWidthShown = 1f;
        private float _vs = 1f;
        private float _pivotLift = NutDropLayout.DefaultSeat;
        private float _restPivotY = 0.4f;
        private float _rimRest = 0.5f;
        private float _sy = 0.45f;
        private float _basketY;
        private float _basketYVelocity;
        private bool _basketSnap;
        private Vector3 _basketPos;
        private Quaternion _basketRot = Quaternion.identity;
        private float _rimTopNow;
        private float _basketBottomNow;
        private float _basketHalf;

        // The animal's motion.
        private float _turn;
        private float _lean;
        private float _hopPhase;
        private float _hopY;
        private float _moveBlend;
        private bool _moving;
        private int _baseShown;
        private float _reactionUntil = -1f;
        private float _socketY = 0.3f;
        private float _socketZ = 0.1f;
        private float _bodyX0, _bodyX1, _bodyZ0, _bodyZ1, _bodyTop;

        // Effects.
        private int _glintNext;
        private ParticleSystem.EmitParams _glintEmit;
        private ParticleSystem.EmitParams _trailEmit;
        private ParticleSystem.EmitParams _ringEmit;
        private ParticleSystem.EmitParams _heartEmit;
        private float _ringT = -1f;
        private byte[] _leafState = Array.Empty<byte>();
        private float[] _leafT = Array.Empty<float>();
        private Vector3[] _leafPos = Array.Empty<Vector3>();
        private Vector3[] _leafVel = Array.Empty<Vector3>();
        private float[] _leafPhase = Array.Empty<float>();
        private int _leafNext;

        // ---------------- what the tests and captures read ----------------

        /// <summary>Pooled item renderers shown now (falling, in the pile, bouncing, fading).</summary>
        public int VisibleItems
        {
            get
            {
                int n = 0;
                for (int v = 0; v < _vState.Length; v++)
                    if (_vState[v] != VFree)
                        n++;
                return n;
            }
        }

        /// <summary>The game slot's item is shown falling.</summary>
        public bool ItemShown(int slot) => (uint)slot < (uint)_slotVisual.Length && _slotVisual[slot] >= 0;

        public Vector3 ItemPosition(int slot) => ItemShown(slot) ? _vPos[_slotVisual[slot]] : default;

        public int PileCount => _pileCount;

        public Transform Basket => _basket;

        /// <summary>The basket's rim (world y) and its half width (m) this frame.</summary>
        public float RimTop => _rimTopNow;

        public float BasketHalfWidth => _basketHalf;

        /// <summary>The catch line at rest (world y): where the game's y = 0 is drawn.</summary>
        public float CatchLineY => _data != null ? _data.origin.position.y + _rimRest : 0f;

        public float FloorY => _data != null ? _data.origin.position.y : 0f;

        /// <summary>The basket's pivot over the live head socket (m) and the seat it must keep at least (its underside over the head and the ears).</summary>
        public float BasketLift => _socket != null ? _basketY - _socket.position.y : float.PositiveInfinity;

        public float SeatLift => _pivotLift;

        /// <summary>H-VFX-ACT-003-04's ring is playing around the basket.</summary>
        public bool RingShown => _ringT >= 0f;

        /// <summary>The animal plays its walk (Move) rather than its Idle.</summary>
        public bool Walking => _moving;

        /// <summary>
        /// Collision rule (tests): every shown item clear of the animal and of the basket's weave, the pile's items above the
        /// weave and apart, nothing under the floor. Null when all is clear, else what overlaps.
        /// </summary>
        public string CheckOverlaps()
        {
            if (_data == null || !_entered)
                return null;
            float floor = FloorY - 0.001f;
            for (int v = 0; v < _vState.Length; v++)
            {
                byte state = _vState[v];
                if (state == VFree)
                    continue;
                var p = _vPos[v];
                float r = _vRadius[v] * _vShown[v];
                if (p.y - r < floor)
                    return $"item {v} ({_vKind[v]}) under the floor ({p.y - r - FloorY:F3} m)";
                if (state == VPile || state == VToPile || state == VPop)
                    continue;
                if (state == VFalling && p.y - r >= _rimTopNow - 0.001f)
                    continue;
                if (InsideBody(p, r * 0.98f))
                    return $"item {v} ({_vKind[v]}) in the animal at {p}";
                if (InsideBasket(p, r * 0.98f))
                    return $"item {v} ({_vKind[v]}) in the basket's weave at {p}";
            }
            for (int a = 0; a < _pileCount; a++)
            {
                int va = _pile[a];
                if (va < 0 || _vState[va] != VPile)
                    continue;
                for (int b = a + 1; b < _pileCount; b++)
                {
                    int vb = _pile[b];
                    if (vb < 0 || _vState[vb] != VPile)
                        continue;
                    float d = Vector3.Distance(_vPos[va], _vPos[vb]);
                    if (d < 2f * _pileRadius - 0.002f)
                        return $"pile items {va} and {vb} overlap ({d:F3} m)";
                }
            }
            return null;
        }

        private bool InsideBody(Vector3 p, float r)
        {
            if (_view == null)
                return false;
            float floor = FloorY;
            return p.y - r < floor + _bodyTop && p.x + r > _bodyX0 && p.x - r < _bodyX1 && p.z + r > _bodyZ0 && p.z - r < _bodyZ1;
        }

        private bool InsideBasket(Vector3 p, float r) =>
            _basket != null && _basket.gameObject.activeSelf && p.y - r < _rimTopNow && p.y + r > _basketBottomNow
            && Mathf.Abs(p.x - _basketPos.x) < _basketHalf + r;

        // ---------------- building (at load) ----------------

        private void AllocateItemState(int capacity)
        {
            _slotVisual = new int[Mathf.Max(0, capacity)];
            for (int i = 0; i < _slotVisual.Length; i++)
                _slotVisual[i] = -1;
        }

        private void BuildProps()
        {
            var items = _data.items ?? Array.Empty<MeshRenderer>();
            int n = items.Length;
            _vTransform = new Transform[n];
            _vFilter = new MeshFilter[n];
            _vRenderer = new MeshRenderer[n];
            _vState = new byte[n];
            _vKind = new NutDropItemKind[n];
            _vSlot = new int[n];
            _vSpot = new int[n];
            _vPos = new Vector3[n];
            _vVel = new Vector3[n];
            _vFrom = new Vector3[n];
            _vAxis = new Vector3[n];
            _vRest = new Quaternion[n];
            _vT = new float[n];
            _vDuration = new float[n];
            _vSpin = new float[n];
            _vSpinRate = new float[n];
            _vRadius = new float[n];
            _vScale = new float[n];
            _vTrail = new float[n];
            _vShown = new float[n];
            for (int v = 0; v < n; v++)
            {
                _vSlot[v] = -1;
                _vSpot[v] = -1;
                if (items[v] == null)
                    continue;
                _vRenderer[v] = items[v];
                _vTransform[v] = items[v].transform;
                _vFilter[v] = items[v].GetComponent<MeshFilter>();
            }
            var palettes = _context != null ? _context.Palettes : null;
            for (int k = 0; k < _kindValues.Length; k++)
                _kindValues[k] = _data.kindPalettes != null && k < _data.kindPalettes.Length ? GroomingSetView.Packed(palettes, _data.kindPalettes[k]) : 0u;
            _basket = _data.basket != null ? _data.basket.transform : null;
            _basketValue = GroomingSetView.Packed(palettes, _data.basketPalette);
            if (_data.basket != null)
                _data.basket.SetShaderUserValue(_basketValue);
            int leaves = _data.leaves != null ? _data.leaves.Length : 0;
            _leafState = new byte[leaves];
            _leafT = new float[leaves];
            _leafPos = new Vector3[leaves];
            _leafVel = new Vector3[leaves];
            _leafPhase = new float[leaves];
            uint leafValue = GroomingSetView.Packed(palettes, _data.leafPalette);
            for (int i = 0; i < leaves; i++)
                if (_data.leaves[i] != null)
                    _data.leaves[i].SetShaderUserValue(leafValue);
            for (int i = 0; i < _pile.Length; i++)
                _pile[i] = -1;
            _pileRadius = 0f;
            for (int k = (int)NutDropItemKind.Hazelnut; k <= (int)NutDropItemKind.GoldenNut; k++)
                _pileRadius = Mathf.Max(_pileRadius, KindRadius((NutDropItemKind)k) * NutDropLayout.PileScale);
            _glintEmit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            _trailEmit = new ParticleSystem.EmitParams { applyShapeToPosition = false, velocity = Vector3.zero };
            _ringEmit = new ParticleSystem.EmitParams { applyShapeToPosition = false, position = Vector3.zero, velocity = Vector3.zero };
            _heartEmit = new ParticleSystem.EmitParams { applyShapeToPosition = false, velocity = new Vector3(0f, 0.3f, 0f) };
            HideVisuals();
        }

        // ---------------- a game starts / ends ----------------

        private void BeginVisuals()
        {
            _time = 0f;
            _dt = 0f;
            _celebrated = false;
            _reactionUntil = -1f;
            _baseShown = 0;
            _moving = false;
            _turn = _lean = _hopPhase = _hopY = _moveBlend = 0f;
            _fingerX = _game.BasketX;
            HideVisuals();
            MeasureSeat();
            _basketWidthShown = Mathf.Max(0.1f, _game.BasketWidth);
            _pileWidth = -1f;
            LayoutPile(_basketWidthShown);
            _basketSnap = true;
            if (_basket != null)
                _basket.gameObject.SetActive(true);
            BorrowIcons();
            StartTutorial(_activeHost != null && _activeHost.Tutorial);
            SyncItems();
            // The first frame already right (the presenter draws before the first LateUpdate on a slow device).
            UpdateAnimal(0f);
            UpdateBasket(0f);
            UpdateItems(0f);
        }

        /// <summary>The basket's seat on this animal and the field's vertical scale (the spawn line under the canopy, the rim on the head).</summary>
        private void MeasureSeat()
        {
            _vs = NutDropLayout.FieldScale * 0.5f / Mathf.Max(1e-4f, _data.basketRimRadius);
            float scale = _animalScale;
            if (_seat != null)
            {
                _socketY = _seat.socketY * scale;
                _socketZ = _seat.socketZ * scale;
                _pivotLift = _seat.seat + Mathf.Max(0f, scale - 1f) * _seat.above + NutDropLayout.SeatMargin;
            }
            else if (_view != null && _socket != null)
            {
                var local = _view.transform.InverseTransformPoint(_socket.position);
                _socketY = local.y * scale;
                _socketZ = local.z * scale;
                _pivotLift = NutDropLayout.DefaultSeat - _data.basketBottom * _vs;
            }
            else
            {
                _socketY = 0.3f * scale;
                _socketZ = 0.1f * scale;
                _pivotLift = NutDropLayout.DefaultSeat;
            }
            _restPivotY = _socketY + _pivotLift;
            _rimRest = _restPivotY + _data.basketRimTop * _vs;
            _sy = Mathf.Max(0.05f, (_data.spawnY - _rimRest) / Mathf.Max(0.5f, _game.SpawnHeight));
        }

        /// <summary>Every item back in its pool (inactive), the pile emptied, the effects stopped.</summary>
        private void HideVisuals()
        {
            if (_data == null)
                return;
            for (int v = 0; v < _vState.Length; v++)
                Free(v);
            for (int i = 0; i < _slotVisual.Length; i++)
                _slotVisual[i] = -1;
            for (int i = 0; i < _pile.Length; i++)
                _pile[i] = -1;
            _pileCount = 0;
            for (int i = 0; i < _leafState.Length; i++)
            {
                _leafState[i] = 0;
                if (_data.leaves[i] != null)
                    _data.leaves[i].gameObject.SetActive(false);
            }
            _ringT = -1f;
            if (_basket != null)
                _basket.gameObject.SetActive(false);
            StopSystem(_data.trail);
            StopSystem(_data.ring);
            StopSystem(_data.hearts);
            if (_data.glints != null)
                for (int i = 0; i < _data.glints.Length; i++)
                    StopSystem(_data.glints[i]);
        }

        private static void StopSystem(ParticleSystem system)
        {
            if (system != null)
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void PauseEffects(bool paused)
        {
            if (_data == null)
                return;
            PauseSystem(_data.trail, paused);
            PauseSystem(_data.ring, paused);
            PauseSystem(_data.hearts, paused);
            if (_data.glints != null)
                for (int i = 0; i < _data.glints.Length; i++)
                    PauseSystem(_data.glints[i], paused);
        }

        private static void PauseSystem(ParticleSystem system, bool paused)
        {
            if (system == null)
                return;
            if (paused && system.isPlaying)
                system.Pause(true);
            else if (!paused && system.isPaused)
                system.Play(true);
        }

        /// <summary>The warm-up frame: the basket, one item of each kind, a leaf and every effect in view of the camera, then hidden.</summary>
        private void WarmProps(bool on)
        {
            if (!on)
            {
                HideVisuals();
                return;
            }
            var origin = _data.origin.position;
            var basePoint = origin + Vector3.up * (_data.spawnY * 0.5f);
            if (_basket != null)
            {
                _basket.gameObject.SetActive(true);
                _basket.SetPositionAndRotation(origin + Vector3.up * 0.6f, Quaternion.identity);
            }
            int v = 0;
            for (int k = 1; k < NutDropLayout.KindCount && v < _vState.Length; k++)
            {
                if (_data.kindMeshes == null || k >= _data.kindMeshes.Length || _data.kindMeshes[k] == null || _vTransform[v] == null)
                    continue;
                SetupVisual(v, (NutDropItemKind)k);
                _vTransform[v].position = basePoint + Vector3.right * (0.18f * (k - 4));
                _vRenderer[v].gameObject.SetActive(true);
                v++;
            }
            if (_data.leaves != null && _data.leaves.Length > 0 && _data.leaves[0] != null)
            {
                _data.leaves[0].transform.position = basePoint + Vector3.up * 0.3f;
                _data.leaves[0].gameObject.SetActive(true);
            }
            EmitTrail(basePoint);
            if (_data.glints != null && _data.glints.Length > 0 && _data.glints[0] != null)
            {
                _glintEmit.position = basePoint;
                _data.glints[0].Emit(_glintEmit, 1);
            }
            if (_data.ring != null)
            {
                _data.ring.transform.position = basePoint;
                _data.ring.Emit(_ringEmit, 1);
            }
            if (_data.hearts != null)
            {
                _heartEmit.position = basePoint;
                _data.hearts.Emit(_heartEmit, 1);
            }
        }

        // ---------------- the game's items (events and the per frame sync) ----------------

        /// <summary>Shows what the game drops and lets go what it took away without an event (the events do the same).</summary>
        private void SyncItems()
        {
            int n = Mathf.Min(_slotVisual.Length, _game.ItemCapacity);
            for (int s = 0; s < n; s++)
            {
                bool active = _game.ItemActive(s);
                int v = _slotVisual[s];
                if (active && v < 0)
                    OnSpawned(s, _game.ItemKind(s));
                else if (active && _vKind[v] != _game.ItemKind(s))
                    SetupVisual(v, _game.ItemKind(s));
                else if (!active && v >= 0)
                    Miss(s);
            }
        }

        private void OnSpawned(int slot, NutDropItemKind kind)
        {
            if ((uint)slot >= (uint)_slotVisual.Length || kind == NutDropItemKind.None)
                return;
            if (_slotVisual[slot] >= 0)
                Free(_slotVisual[slot]);
            int v = AcquireVisual();
            if (v < 0)
                return;
            SetupVisual(v, kind);
            _slotVisual[slot] = v;
            _vSlot[v] = slot;
            _vState[v] = VFalling;
            _vT[v] = 0f;
            _vShown[v] = 0f;
            _vTrail[v] = 0f;
            _vPos[v] = ItemWorld(_game.ItemX(slot), _game.ItemY(slot), _vRadius[v]);
            _vTransform[v].position = _vPos[v];
            _vTransform[v].localScale = Vector3.zero;
            _vRenderer[v].gameObject.SetActive(true);
        }

        /// <summary>A free renderer, else the oldest one fading on the ground or in the pile (never a falling one).</summary>
        private int AcquireVisual()
        {
            int fallback = -1;
            float oldest = -1f;
            for (int v = 0; v < _vState.Length; v++)
            {
                if (_vRenderer[v] == null)
                    continue;
                if (_vState[v] == VFree)
                    return v;
                if ((_vState[v] == VGround || _vState[v] == VPop) && _vT[v] > oldest)
                {
                    oldest = _vT[v];
                    fallback = v;
                }
            }
            if (fallback >= 0)
                Free(fallback);
            return fallback;
        }

        private void SetupVisual(int v, NutDropItemKind kind)
        {
            int k = (int)NutDropLayout.MeshKind(kind);
            _vKind[v] = kind;
            var meshes = _data.kindMeshes;
            var mesh = meshes != null && k < meshes.Length ? meshes[k] : null;
            if (_vFilter[v] != null && mesh != null && _vFilter[v].sharedMesh != mesh)
                _vFilter[v].sharedMesh = mesh;
            bool gold = _data.kindGold != null && k < _data.kindGold.Length && _data.kindGold[k];
            var material = gold && _data.goldMaterial != null ? _data.goldMaterial : _data.propsMaterial;
            if (material != null && _vRenderer[v].sharedMaterial != material)
                _vRenderer[v].sharedMaterial = material;
            _vRenderer[v].SetShaderUserValue(_kindValues[k < _kindValues.Length ? k : 0]);
            _vScale[v] = NutDropLayout.ItemScale(kind);
            _vRadius[v] = KindRadius(kind) * _vScale[v];
            uint h = Hash(v, _spawnSeed++);
            _vAxis[v] = new Vector3(((h & 255u) / 255f) - 0.5f, 0.6f, (((h >> 8) & 255u) / 255f) - 0.5f).normalized;
            _vSpin[v] = (h >> 16) & 359u;
            _vSpinRate[v] = (Rocks(kind) ? 0f : 70f + 80f * (((h >> 24) & 255u) / 255f)) * ((h & 1u) == 0 ? 1f : -1f);
            _vSpot[v] = -1;
        }

        private static bool Rocks(NutDropItemKind kind) => kind == NutDropItemKind.Feather || kind == NutDropItemKind.WideBasket;

        /// <summary>A kind's mesh radius around its pivot (m, measured by the pipeline; 7 cm without it).</summary>
        private float KindRadius(NutDropItemKind kind)
        {
            int k = (int)NutDropLayout.MeshKind(kind);
            return _data.kindRadii != null && k < _data.kindRadii.Length && _data.kindRadii[k] > 1e-4f ? _data.kindRadii[k] : 0.07f;
        }

        private void Free(int v)
        {
            if ((uint)v >= (uint)_vState.Length)
                return;
            int slot = _vSlot[v];
            if (slot >= 0 && slot < _slotVisual.Length && _slotVisual[slot] == v)
                _slotVisual[slot] = -1;
            int spot = _vSpot[v];
            if (spot >= 0 && _pile[spot] == v)
            {
                _pile[spot] = -1;
                _pileCount = Mathf.Max(0, _pileCount - 1);
            }
            _vSlot[v] = -1;
            _vSpot[v] = -1;
            _vState[v] = VFree;
            _vShown[v] = 0f;
            if (_vRenderer[v] != null && _vRenderer[v].gameObject.activeSelf)
                _vRenderer[v].gameObject.SetActive(false);
        }

        private int Detach(int slot)
        {
            if ((uint)slot >= (uint)_slotVisual.Length)
                return -1;
            int v = _slotVisual[slot];
            if (v < 0)
                return -1;
            _slotVisual[slot] = -1;
            _vSlot[v] = -1;
            return v;
        }

        /// <summary>A catch: the item drops into the basket (over the rim, then down) and settles on the pile; 4 round glints on the rim.</summary>
        private void Catch(int slot, NutDropItemKind kind)
        {
            Glints();
            int v = Detach(slot);
            if (v < 0)
                return;
            // The pile fills from the weave up (each ball rests on the weave or on the ones under it). Six at most: past six,
            // the ball on top goes softly and the new one takes its place (nothing rests on it, nothing is left floating).
            int spot = -1;
            for (int i = 0; i < _pile.Length && spot < 0; i++)
                if (_pile[i] < 0)
                    spot = i;
            if (spot < 0)
            {
                spot = _pile.Length - 1;
                int old = _pile[spot];
                _pile[spot] = -1;
                _pileCount = Mathf.Max(0, _pileCount - 1);
                _vSpot[old] = -1;
                StartPop(old, RecycleSeconds);
            }
            _pile[spot] = v;
            _pileCount = Mathf.Min(_pile.Length, _pileCount + 1);
            _vSpot[v] = spot;
            _vState[v] = VToPile;
            _vT[v] = 0f;
            _vShown[v] = 1f;
            _vFrom[v] = _vPos[v] - _basketPos;
            _vRest[v] = _vTransform[v].rotation;
        }

        /// <summary>A missed item reached the meadow: it bounces once, rolls toward the grass and fades.</summary>
        private void Miss(int slot)
        {
            int v = Detach(slot);
            if (v < 0)
                return;
            _vShown[v] = 1f;
            float side = _vPos[v].x >= _basketPos.x ? 1f : -1f;
            _vState[v] = VAirborne;
            _vVel[v] = new Vector3(side * 0.22f, 0.85f, -0.35f);
            _vPos[v].y = Mathf.Max(_vPos[v].y, FloorY + _vRadius[v]);
            _vT[v] = 0f;
        }

        /// <summary>A pinecone in the basket: a soft bonk, it hops off the rim to the side, two small leaves fall, the animal starts.</summary>
        private void Bonk(int slot)
        {
            PlayReaction(AnimalStateHashes.React);
            int v = Detach(slot);
            if (v < 0)
                return;
            _vShown[v] = 1f;
            float side = _vPos[v].x > _basketPos.x + 0.01f ? 1f : _vPos[v].x < _basketPos.x - 0.01f ? -1f : ((_spawnSeed & 1u) == 0 ? 1f : -1f);
            // Off the rim, outside the weave first.
            _vPos[v].y = Mathf.Max(_vPos[v].y, _rimTopNow + _vRadius[v]);
            _vState[v] = VAirborne;
            _vVel[v] = new Vector3(side * 0.95f, 1.35f, -0.2f);
            _vT[v] = 0f;
            Leaves(side);
        }

        /// <summary>A special caught: it swells and melts away at the rim (its effect starts with the ring).</summary>
        private void Pop(int slot)
        {
            int v = Detach(slot);
            if (v < 0)
                return;
            _vFrom[v] = _vPos[v] - _basketPos;
            StartPop(v, PopSeconds);
        }

        private void StartPop(int v, float seconds)
        {
            // From the size it has now (a pile item is smaller).
            if (_vShown[v] > 0f)
                _vScale[v] *= _vShown[v];
            _vShown[v] = 1f;
            _vState[v] = VPop;
            _vT[v] = 0f;
            _vDuration[v] = seconds;
            _vFrom[v] = _vPos[v] - _basketPos;
        }

        // ---------------- the field on the set ----------------

        /// <summary>
        /// A playfield point on the set: x times <see cref="NutDropLayout.FieldScale"/>; y from the rim on the head (y = 0) up
        /// to the spawn line under the leaves, and below the rim down to the meadow (the item resting on it at GroundY).
        /// </summary>
        private Vector3 ItemWorld(float x, float y, float radius)
        {
            var o = _data.origin.position;
            float wy;
            if (y >= 0f)
            {
                wy = _rimRest + y * _sy;
            }
            else
            {
                float ground = _game.GroundY;
                float t = ground < -0.001f ? Mathf.Clamp01(y / ground) : 1f;
                wy = Mathf.Lerp(_rimRest, radius, t);
            }
            return new Vector3(o.x + x * NutDropLayout.FieldScale, o.y + Mathf.Max(radius, wy), o.z);
        }

        /// <summary>The item keeps clear of the basket's weave and of the animal: pushed out sideways (a control before each step).</summary>
        private void Deflect(ref Vector3 p, float r)
        {
            if (InsideBasket(p, r))
                p.x = p.x < _basketPos.x ? _basketPos.x - _basketHalf - r : _basketPos.x + _basketHalf + r;
            if (InsideBody(p, r))
            {
                float middle = 0.5f * (_bodyX0 + _bodyX1);
                p.x = p.x < middle ? _bodyX0 - r : _bodyX1 + r;
                if (InsideBasket(p, r))
                    p.x = p.x < _basketPos.x ? _basketPos.x - _basketHalf - r : _basketPos.x + _basketHalf + r;
            }
        }

        // ---------------- per frame (LateTick) ----------------

        /// <summary>
        /// The animal under the basket: facing the camera, turned up to 35° toward its motion, Move while the basket runs and
        /// Idle when it rests, a small hop and a lean pivoting on the paws of that side (no paw ever sinks into the meadow);
        /// its head stays right under the basket's x.
        /// </summary>
        private void UpdateAnimal(float dt)
        {
            if (_view == null)
                return;
            float v = _ended ? 0f : _game.BasketVelocity;
            float speed = Mathf.Abs(v);
            if (!_moving && speed > NutDropLayout.MoveOn)
                _moving = true;
            else if (_moving && speed < NutDropLayout.MoveOff)
                _moving = false;
            int wanted = _moving ? AnimalStateHashes.Move : AnimalStateHashes.Idle;
            if (_time >= _reactionUntil && _baseShown != wanted && !_paused)
            {
                _view.PlayState(wanted, 0.15f);
                _baseShown = wanted;
            }
            float turnTarget = Mathf.Clamp(v / NutDropLayout.TurnSpeed, -1f, 1f);
            _turn = Mathf.MoveTowards(_turn, turnTarget, dt * 3f);
            _moveBlend = Mathf.MoveTowards(_moveBlend, _moving ? 1f : 0f, dt * 4f);
            if (_moveBlend > 0f)
                _hopPhase += dt * NutDropLayout.HopRate * Mathf.PI;
            else
                _hopPhase = 0f;
            _hopY = NutDropLayout.HopHeight * _animalScale * Mathf.Abs(Mathf.Sin(_hopPhase)) * _moveBlend;
            _lean = Mathf.MoveTowards(_lean, turnTarget * NutDropLayout.MaxLean, dt * 20f);

            var origin = _data.origin.position;
            float yaw = NutDropLayout.Yaw(_turn);
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            var socketOffset = rotation * new Vector3(0f, _socketY, _socketZ);
            float headX = origin.x + _game.BasketX * NutDropLayout.FieldScale;
            var position = new Vector3(headX - socketOffset.x, origin.y + _hopY, origin.z - socketOffset.z);
            if (Mathf.Abs(_lean) > 0.01f)
            {
                // Pivot on the outer paws of the leaning side: the other paws lift a little, none goes down.
                var pivot = position + Vector3.right * (Mathf.Sign(_lean) * _halfWidth * _animalScale);
                var q = Quaternion.AngleAxis(-_lean, Vector3.forward);
                position = pivot + q * (position - pivot);
                rotation = q * rotation;
                // The head back under the basket.
                position.x += headX - (position + q * socketOffset).x;
            }
            _view.SetPose(position, rotation);
            // The head as drawn this frame (its idle sway): the body follows a few centimetres so the head stays right under
            // the basket, whose x is the game's.
            if (_socket != null)
            {
                float drift = Mathf.Clamp(headX - _socket.position.x, -0.03f, 0.03f);
                if (Mathf.Abs(drift) > 0.0005f)
                {
                    position.x += drift;
                    _view.SetPose(position, rotation);
                }
            }

            // The body seen from above (its yaw), for the items' clearance.
            float rad = yaw * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            float hw = _halfWidth * _animalScale, f = _front * _animalScale, b = _back * _animalScale;
            _bodyX0 = _bodyZ0 = float.MaxValue;
            _bodyX1 = _bodyZ1 = float.MinValue;
            for (int c = 0; c < 4; c++)
            {
                float lx = (c & 1) == 0 ? -hw : hw;
                float lz = (c & 2) == 0 ? b : f;
                float x = lx * cos + lz * sin;
                float z = -lx * sin + lz * cos;
                _bodyX0 = Mathf.Min(_bodyX0, position.x + x);
                _bodyX1 = Mathf.Max(_bodyX1, position.x + x);
                _bodyZ0 = Mathf.Min(_bodyZ0, position.z + z);
                _bodyZ1 = Mathf.Max(_bodyZ1, position.z + z);
            }
            // The lean lifts the far side by up to sin(lean) × width.
            float leanLift = Mathf.Abs(Mathf.Sin(_lean * Mathf.Deg2Rad)) * 2f * hw;
            _bodyTop = _height * _animalScale + _hopY + leanLift;
        }

        /// <summary>
        /// The basket on the head: its x is the game's basket, its width the game's catch width (it widens softly with the wide
        /// basket), its underside always over the head and the ears (the measured seat over the live head socket), with a
        /// gentle bob and half the animal's lean.
        /// </summary>
        private void UpdateBasket(float dt)
        {
            if (_basket == null)
                return;
            var origin = _data.origin.position;
            float width = Mathf.Max(0.1f, _game.BasketWidth);
            _basketWidthShown = dt > 0f ? Mathf.MoveTowards(_basketWidthShown, width, dt * 2.5f) : width;
            if (Mathf.Abs(_basketWidthShown - _pileWidth) > 0.01f)
                LayoutPile(_basketWidthShown);
            float hs = NutDropLayout.FieldScale * _basketWidthShown * 0.5f / Mathf.Max(1e-4f, _data.basketRimRadius);
            // On the live head (its walk, its hop, a happy jump), softened by a spring, never lower than the seat over it.
            float headPivot = _socket != null && _view != null ? _socket.position.y + _pivotLift : origin.y + _restPivotY + _hopY;
            float bob = NutDropLayout.BasketBob * _moveBlend * (0.5f + 0.5f * Mathf.Sin(_hopPhase * 2f + 0.6f));
            float target = headPivot + bob;
            if (_basketSnap || dt <= 0f)
            {
                if (_basketSnap || _basketY < headPivot)
                    _basketY = target;
                _basketYVelocity = 0f;
                _basketSnap = false;
            }
            else
            {
                _basketY = Mathf.SmoothDamp(_basketY, target, ref _basketYVelocity, 0.06f, 4f, dt);
            }
            // Never into the head, whatever the spring does.
            _basketY = Mathf.Max(_basketY, headPivot);
            _basketPos = new Vector3(origin.x + _game.BasketX * NutDropLayout.FieldScale, _basketY, origin.z);
            _basketRot = Quaternion.AngleAxis(-0.5f * _lean, Vector3.forward);
            _basket.SetPositionAndRotation(_basketPos, _basketRot);
            _basket.localScale = new Vector3(hs, _vs, hs);
            _rimTopNow = _basketY + _data.basketRimTop * _vs;
            _basketBottomNow = _basketY + _data.basketBottom * _vs;
            _basketHalf = NutDropLayout.FieldScale * _basketWidthShown * 0.5f;
        }

        /// <summary>
        /// The pile's spots for a basket width: one in the middle, four around it, one on top, each resting on the weave (the
        /// measured inside of the basket) or on the items under it, never in them.
        /// </summary>
        private void LayoutPile(float width)
        {
            _pileWidth = width;
            float hs = NutDropLayout.FieldScale * width * 0.5f / Mathf.Max(1e-4f, _data.basketRimRadius);
            float rp = _pileRadius;
            float inner = Mathf.Max(0.01f, _data.basketInnerRadius * hs);
            float ring = Mathf.Clamp(inner - rp * 0.8f, 0f, 1.9f * rp);
            for (int k = 0; k < _pileOffset.Length; k++)
            {
                float a = k == 1 ? 225f : k == 2 ? 315f : k == 3 ? 135f : k == 4 ? 45f : 0f;
                float rho = k == 0 || k >= 5 ? 0f : ring;
                float px = rho * Mathf.Cos(a * Mathf.Deg2Rad);
                float pz = rho * Mathf.Sin(a * Mathf.Deg2Rad);
                float y = Support(rho, rp, hs) + 0.003f;
                for (int j = 0; j < k; j++)
                {
                    float dx = _pileOffset[j].x - px, dz = _pileOffset[j].z - pz;
                    float d2 = dx * dx + dz * dz;
                    if (d2 < 4f * rp * rp)
                        y = Mathf.Max(y, _pileOffset[j].y + Mathf.Sqrt(4f * rp * rp - d2) + 0.002f);
                }
                _pileOffset[k] = new Vector3(px, y, pz);
            }
        }

        /// <summary>The lowest height (from the pivot) a ball of radius r at this distance from the axis may sit on the weave.</summary>
        private float Support(float rho, float r, float hs)
        {
            float best = float.MinValue;
            for (int s = -2; s <= 2; s++)
            {
                float offset = s * 0.5f * r;
                float at = Mathf.Abs(rho + offset);
                float h = InnerHeight(at / Mathf.Max(1e-4f, hs)) * _vs + Mathf.Sqrt(Mathf.Max(0f, r * r - offset * offset));
                best = Mathf.Max(best, h);
            }
            return best;
        }

        /// <summary>The weave's inside floor at a distance from the axis (mesh units): measured, the rim beyond the inside.</summary>
        private float InnerHeight(float rho)
        {
            var inner = _data.basketInner;
            float radius = Mathf.Max(1e-4f, _data.basketInnerRadius);
            if (rho >= radius)
                return _data.basketRimTop;
            if (inner == null || inner.Length < 2)
            {
                float u = rho / radius;
                return Mathf.Lerp(_data.basketBottom, _data.basketRimTop, 0.15f + 0.85f * u * u);
            }
            float t = rho / radius * (inner.Length - 1);
            int i = Mathf.Min(inner.Length - 2, (int)t);
            return Mathf.Lerp(inner[i], inner[i + 1], t - i);
        }

        private void UpdateItems(float dt)
        {
            float floor = FloorY;
            for (int v = 0; v < _vState.Length; v++)
            {
                byte state = _vState[v];
                if (state == VFree)
                    continue;
                var transform = _vTransform[v];
                float r = _vRadius[v];
                switch (state)
                {
                    case VFalling:
                    {
                        int slot = _vSlot[v];
                        if (slot < 0 || !_game.ItemActive(slot))
                            break;
                        float gameY = _game.ItemY(slot);
                        var p = ItemWorld(_game.ItemX(slot), gameY, r);
                        // Over the catch line and over the basket: it waits on the rim for its catch (never in the weave);
                        // past the line it has missed and keeps clear of the basket and the animal.
                        if (gameY >= 0f && Mathf.Abs(p.x - _basketPos.x) <= _basketHalf + r && p.y < _rimTopNow + r)
                            p.y = _rimTopNow + r;
                        else if (p.y - r < _rimTopNow)
                            Deflect(ref p, r);
                        _vPos[v] = p;
                        _vT[v] += dt;
                        _vShown[v] = Smooth(_vT[v] / AppearSeconds);
                        _vSpin[v] += _vSpinRate[v] * dt;
                        // The feather and the little basket rock as they float down; the others turn over.
                        var rotation = Rocks(_vKind[v])
                            ? Quaternion.Euler(0f, 20f * Mathf.Sin(_vT[v] * 1.7f), (_vKind[v] == NutDropItemKind.Feather ? 28f : 12f) * Mathf.Sin(_vT[v] * 3.1f))
                            : Quaternion.AngleAxis(_vSpin[v], _vAxis[v]);
                        transform.SetPositionAndRotation(p, rotation);
                        transform.localScale = Vector3.one * (_vScale[v] * _vShown[v]);
                        if (_vKind[v] == NutDropItemKind.GoldenNut || _vKind[v] == NutDropItemKind.GoldenShower)
                        {
                            _vTrail[v] -= dt;
                            if (_vTrail[v] <= 0f && dt > 0f)
                            {
                                _vTrail[v] += TrailInterval;
                                EmitTrail(p);
                            }
                        }
                        break;
                    }
                    case VToPile:
                    {
                        _vT[v] += dt / ToPileSeconds;
                        float t = Mathf.Min(1f, _vT[v]);
                        var from = _basketPos + _vFrom[v];
                        var to = _basketPos + _basketRot * _pileOffset[Mathf.Max(0, _vSpot[v])];
                        float above = Mathf.Max(from.y, _rimTopNow + r);
                        Vector3 p;
                        if (t < 0.45f)
                        {
                            float u = Smooth(t / 0.45f);
                            p = new Vector3(Mathf.Lerp(from.x, to.x, u), Mathf.Lerp(from.y, above, u), Mathf.Lerp(from.z, to.z, u));
                        }
                        else
                        {
                            float u = Smooth((t - 0.45f) / 0.55f);
                            p = new Vector3(to.x, Mathf.Lerp(above, to.y, u), to.z);
                        }
                        _vPos[v] = p;
                        transform.SetPositionAndRotation(p, _vRest[v]);
                        _vShown[v] = Mathf.Lerp(1f, NutDropLayout.PileScale, Smooth(t));
                        transform.localScale = Vector3.one * (_vScale[v] * _vShown[v]);
                        if (t >= 1f)
                            _vState[v] = VPile;
                        break;
                    }
                    case VPile:
                    {
                        var p = _basketPos + _basketRot * _pileOffset[Mathf.Max(0, _vSpot[v])];
                        _vPos[v] = p;
                        transform.SetPositionAndRotation(p, _basketRot * _vRest[v]);
                        break;
                    }
                    case VPop:
                    {
                        _vT[v] += dt / Mathf.Max(0.01f, _vDuration[v]);
                        float t = _vT[v];
                        var p = _basketPos + _vFrom[v];
                        float s = t < 0.3f ? 1f + 0.25f * (t / 0.3f) : 1.25f * Mathf.Max(0f, 1f - (t - 0.3f) / 0.7f);
                        if (_vDuration[v] <= RecycleSeconds + 1e-4f)
                            s = Mathf.Max(0f, 1f - t);
                        _vShown[v] = Mathf.Min(1f, s);
                        _vPos[v] = p;
                        transform.position = p;
                        transform.localScale = Vector3.one * (_vScale[v] * s);
                        if (t >= 1f)
                            Free(v);
                        break;
                    }
                    case VAirborne:
                    {
                        var vel = _vVel[v];
                        vel.y += Gravity * dt;
                        var p = _vPos[v] + vel * dt;
                        Deflect(ref p, r);
                        if (p.y <= floor + r && vel.y <= 0f)
                        {
                            p.y = floor + r;
                            _vState[v] = VGround;
                            _vT[v] = 0f;
                            vel = new Vector3(vel.x * 0.4f, 0f, -0.55f);
                        }
                        _vVel[v] = vel;
                        _vPos[v] = p;
                        _vSpin[v] += _vSpinRate[v] * dt;
                        transform.SetPositionAndRotation(p, Quaternion.AngleAxis(_vSpin[v], _vAxis[v]));
                        transform.localScale = Vector3.one * (_vScale[v] * _vShown[v]);
                        break;
                    }
                    case VGround:
                    {
                        _vT[v] += dt;
                        float t = _vT[v];
                        // Rolls toward the grass, slowing; it shrinks into it after a short rest (never into the floor).
                        var vel = _vVel[v];
                        vel = Vector3.MoveTowards(vel, Vector3.zero, dt * 1.4f);
                        _vVel[v] = vel;
                        var p = _vPos[v] + new Vector3(vel.x, 0f, vel.z) * dt;
                        float s = t < GroundRestSeconds ? 1f : Mathf.Max(0f, 1f - (t - GroundRestSeconds) / GroundFadeSeconds);
                        _vShown[v] = s;
                        p.y = floor + r * s;
                        Deflect(ref p, r * s);
                        _vPos[v] = p;
                        _vSpin[v] += -vel.z / Mathf.Max(0.01f, r) * Mathf.Rad2Deg * dt;
                        transform.SetPositionAndRotation(p, Quaternion.AngleAxis(_vSpin[v], Vector3.right));
                        transform.localScale = Vector3.one * (_vScale[v] * s);
                        if (t >= GroundRestSeconds + GroundFadeSeconds)
                            Free(v);
                        break;
                    }
                }
            }
            UpdateLeaves(dt);
        }

        // ---------------- effects ----------------

        /// <summary>H-VFX-ACT-003-01: four round glints spread along the rim.</summary>
        private void Glints()
        {
            var systems = _data.glints;
            if (systems == null || systems.Length == 0)
                return;
            var system = systems[_glintNext];
            _glintNext = (_glintNext + 1) % systems.Length;
            if (system == null)
                return;
            // Four glints spread along the rim (as seen from the camera: left, front, right, back), drifting up and out.
            float r = _basketHalf * 0.92f;
            float turn = (_spawnSeed & 3u) * 22.5f;
            for (int k = 0; k < 4; k++)
            {
                float a = (turn + 90f * k) * Mathf.Deg2Rad;
                var out2 = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                _glintEmit.position = new Vector3(_basketPos.x, _rimTopNow + 0.01f, _basketPos.z) + out2 * r;
                _glintEmit.velocity = out2 * 0.12f + Vector3.up * 0.22f;
                system.Emit(_glintEmit, 1);
            }
        }

        /// <summary>H-VFX-ACT-003-02: one soft dot of a golden nut's trail (6 alive at once: 0.27 s every 0.045 s).</summary>
        private void EmitTrail(Vector3 position)
        {
            if (_data.trail == null)
                return;
            _trailEmit.position = position;
            _data.trail.Emit(_trailEmit, 1);
        }

        /// <summary>H-VFX-ACT-003-04: the soft ring around the basket when a special starts (it follows the basket).</summary>
        private void Ring()
        {
            if (_data.ring == null)
                return;
            _ringT = 0f;
            _data.ring.transform.position = new Vector3(_basketPos.x, (_rimTopNow + _basketBottomNow) * 0.5f, _basketPos.z);
            _ringEmit.startSize = Mathf.Max(0.1f, _basketHalf * 2.7f);
            _data.ring.Emit(_ringEmit, 1);
        }

        /// <summary>A heart over the head (every 5 catches in a row, and at the end).</summary>
        private void Heart()
        {
            if (_data.hearts == null || _view == null)
                return;
            float top = Mathf.Max(_rimTopNow, _socket != null ? _socket.position.y : FloorY + _height * _animalScale);
            _heartEmit.position = new Vector3(_basketPos.x + 0.6f * _basketHalf, top + 0.12f, _basketPos.z - 0.05f);
            _data.hearts.Emit(_heartEmit, 1);
        }

        private void UpdateEffects(float dt)
        {
            if (_ringT >= 0f && _data.ring != null)
            {
                _ringT += dt;
                _data.ring.transform.position = new Vector3(_basketPos.x, (_rimTopNow + _basketBottomNow) * 0.5f, _basketPos.z);
                if (_ringT > RingSeconds)
                    _ringT = -1f;
            }
        }

        /// <summary>H-VFX-ACT-003-03: two small leaves flutter down beside the basket (never through the animal), rest and fade.</summary>
        private void Leaves(float side)
        {
            var leaves = _data.leaves;
            if (leaves == null || leaves.Length == 0)
                return;
            for (int k = 0; k < 2; k++)
            {
                int i = _leafNext;
                _leafNext = (_leafNext + 1) % leaves.Length;
                if (leaves[i] == null)
                    continue;
                float s = k == 0 ? side : -side;
                _leafState[i] = 1;
                _leafT[i] = 0f;
                _leafPhase[i] = k * 1.7f + (_spawnSeed & 7u);
                _leafPos[i] = new Vector3(_basketPos.x + s * (_basketHalf + _data.leafRadius + 0.02f), _rimTopNow + 0.03f + 0.04f * k, _basketPos.z - 0.03f);
                _leafVel[i] = new Vector3(s * 0.3f, 0.35f, -0.05f);
                leaves[i].transform.position = _leafPos[i];
                leaves[i].gameObject.SetActive(true);
            }
        }

        private void UpdateLeaves(float dt)
        {
            var leaves = _data.leaves;
            if (leaves == null)
                return;
            float floor = FloorY;
            for (int i = 0; i < _leafState.Length; i++)
            {
                if (_leafState[i] == 0 || leaves[i] == null)
                    continue;
                _leafT[i] += dt;
                float t = _leafT[i];
                var p = _leafPos[i];
                var vel = _leafVel[i];
                float s = 1f;
                if (_leafState[i] == 1)
                {
                    // Falls slowly, swaying (a leaf, not a stone).
                    vel.y = Mathf.MoveTowards(vel.y, -0.5f, dt * 2.6f);
                    vel.x = Mathf.MoveTowards(vel.x, 0f, dt * 0.4f);
                    p += new Vector3(vel.x + 0.22f * Mathf.Cos(t * 4.2f + _leafPhase[i]), vel.y, vel.z) * dt;
                    Deflect(ref p, _data.leafRadius);
                    if (p.y <= floor + _data.leafRadius)
                    {
                        // Down: it lies flat on the meadow, its underside on it.
                        p.y = floor + _data.leafLift;
                        _leafState[i] = 2;
                        _leafT[i] = 0f;
                        leaves[i].transform.SetPositionAndRotation(p, Quaternion.Euler(0f, 40f * t, 0f));
                    }
                    else
                    {
                        leaves[i].transform.SetPositionAndRotation(p, Quaternion.Euler(30f * Mathf.Sin(t * 4.2f + _leafPhase[i]), 40f * t, 20f * Mathf.Cos(t * 3.3f)));
                    }
                }
                else
                {
                    s = t < LeafRestSeconds ? 1f : Mathf.Max(0f, 1f - (t - LeafRestSeconds) / LeafFadeSeconds);
                    if (s <= 0f)
                    {
                        _leafState[i] = 0;
                        leaves[i].gameObject.SetActive(false);
                        continue;
                    }
                }
                _leafPos[i] = p;
                _leafVel[i] = vel;
                leaves[i].transform.localScale = Vector3.one * (_data.leafScale * s);
            }
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private static uint Hash(int i, uint salt)
        {
            uint h = (uint)i * 2654435761u ^ salt;
            h ^= h >> 15;
            h *= 0x2C1B3C6Du;
            h ^= h >> 12;
            return h;
        }
    }
}
