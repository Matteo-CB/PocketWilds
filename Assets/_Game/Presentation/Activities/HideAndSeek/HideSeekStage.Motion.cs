using PocketWilds.Core;
using PocketWilds.Simulation;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace PocketWilds.Presentation
{
    // The hidden animals and the stage's pooled effects (doc 01 Contenu S-ACT-002 « Sons et effets »; collision rule: an
    // animal stands beside its decor, never inside it or another animal; its « coucou » arc goes around the decor to free
    // ground; the leaves fly away from the decor, never through it). Every effect is a renderer built while the game loads.
    public sealed partial class HideSeekStage
    {
        private const byte Off = 0;
        private const byte Hidden = 1;
        private const byte Hopping = 2;
        private const byte Out = 3;
        private const byte Moving = 4;
        private const byte Gathering = 5;

        private const float ContactTolerance = 0.002f;
        private const float MaxTiltDegrees = 14f;

        private int _hiddenCount;
        private readonly AnimalView[] _view = new AnimalView[HideSeekLayout.MaxHidden];
        private readonly byte[] _phase = new byte[HideSeekLayout.MaxHidden];
        private readonly int[] _spot = new int[HideSeekLayout.MaxHidden];
        private readonly int[] _nextSpot = new int[HideSeekLayout.MaxHidden];
        private readonly int[] _speciesRow = new int[HideSeekLayout.MaxHidden];
        private readonly float[] _scale = new float[HideSeekLayout.MaxHidden];
        private readonly Vector3[] _position = new Vector3[HideSeekLayout.MaxHidden];
        private readonly float[] _yaw = new float[HideSeekLayout.MaxHidden];
        private readonly float[] _t = new float[HideSeekLayout.MaxHidden];
        private readonly float[] _duration = new float[HideSeekLayout.MaxHidden];
        private readonly Vector3[] _from = new Vector3[HideSeekLayout.MaxHidden];
        private readonly Vector3[] _control = new Vector3[HideSeekLayout.MaxHidden];
        private readonly Vector3[] _to = new Vector3[HideSeekLayout.MaxHidden];
        private readonly float[] _arc = new float[HideSeekLayout.MaxHidden];
        private readonly bool[] _pop = new bool[HideSeekLayout.MaxHidden];
        private readonly float[] _fromYaw = new float[HideSeekLayout.MaxHidden];
        private readonly float[] _toYaw = new float[HideSeekLayout.MaxHidden];
        private readonly bool[] _hinting = new bool[HideSeekLayout.MaxHidden];
        private readonly float[] _hintNext = new float[HideSeekLayout.MaxHidden];
        private readonly float[] _wiggleAt = new float[HideSeekLayout.MaxHidden];
        private readonly float[] _nibbleAt = new float[HideSeekLayout.MaxHidden];
        private readonly Vector3[] _landing = new Vector3[HideSeekLayout.MaxHidden];
        private readonly Vector3[] _gatherSlot = new Vector3[HideSeekLayout.MaxHidden];
        private readonly bool[] _gatherOk = new bool[HideSeekLayout.MaxHidden];
        private readonly int[] _order = new int[HideSeekLayout.MaxHidden];
        private float _gatherAt = -1f;
        private bool _planningGather;

        // Local gusts on the foliage (FoliageGusts, 4 slots): hints, decoys, the whistle.
        private readonly int[] _gustSpot = new int[FoliageGusts.Slots];
        private readonly float[] _gustStart = new float[FoliageGusts.Slots];
        private readonly float[] _gustLength = new float[FoliageGusts.Slots];
        private readonly float[] _gustPeak = new float[FoliageGusts.Slots];
        private readonly Vector3[] _gustPosition = new Vector3[FoliageGusts.Slots];
        private readonly float[] _gustRadius = new float[FoliageGusts.Slots];

        // Rings (H-VFX-ACT-002-01 discovery, the magnifier's glass ring, the paused touch's cue).
        private const byte RingOff = 0;
        private const byte RingDiscovery = 1;
        private const byte RingMagnifier = 2;
        private const byte RingCue = 3;
        private const int MagnifierRing = HideSeekLayout.RingCount - 1;
        private MeshRenderer[] _rings = System.Array.Empty<MeshRenderer>();
        private Mesh[] _ringMeshes = System.Array.Empty<Mesh>();
        private readonly byte[] _ringKind = new byte[HideSeekLayout.RingCount];
        private readonly float[] _ringT = new float[HideSeekLayout.RingCount];
        private readonly float[] _ringSize = new float[HideSeekLayout.RingCount];
        private readonly Vector3[] _ringPosition = new Vector3[HideSeekLayout.RingCount];
        private readonly byte[] _ringAlpha = new byte[HideSeekLayout.RingCount];
        private readonly Color32[] _ringColors = new Color32[4];
        private int _ringNext;
        private float _magnifierShow;

        // Leaves (H-VFX-ACT-002-02), the kit's GL_Leaf_A in the leaf palette.
        private MeshRenderer[] _leaves = System.Array.Empty<MeshRenderer>();
        private readonly float[] _leafT = new float[HideSeekLayout.LeafCount];
        private readonly Vector3[] _leafFrom = new Vector3[HideSeekLayout.LeafCount];
        private readonly Vector3[] _leafVelocity = new Vector3[HideSeekLayout.LeafCount];
        private readonly Vector3[] _leafAxis = new Vector3[HideSeekLayout.LeafCount];
        private readonly float[] _leafFloor = new float[HideSeekLayout.LeafCount];
        private int _leafNext;
        private float _leafScale = 1f;
        private uint _seed = 0x48534B31u;

        // ---------------- read by tests and captures ----------------

        public int HiddenShown => _hiddenCount;

        public AnimalView View(int animal) => (uint)animal < (uint)_hiddenCount ? _view[animal] : null;

        /// <summary>The animal is hidden in a spot (not hopping, not out).</summary>
        public bool IsHidden(int animal) => (uint)animal < (uint)_hiddenCount && _phase[animal] == Hidden;

        public bool IsHopping(int animal) => (uint)animal < (uint)_hiddenCount && (_phase[animal] == Hopping || _phase[animal] == Gathering);

        public bool IsOut(int animal) => (uint)animal < (uint)_hiddenCount && _phase[animal] == Out;

        public Vector3 AnimalPosition(int animal) => _position[animal];

        public float AnimalYaw(int animal) => _yaw[animal];

        public float AnimalScale(int animal) => _scale[animal];

        public BodyShape AnimalBody(int animal) => _pickBody[animal % Mathf.Max(1, _pickCount)];

        /// <summary>Where the animal lands at its « coucou » (world), once found.</summary>
        public Vector3 LandingPoint(int animal) => _landing[animal];

        /// <summary>The obstacles of the zone's chunk (world space), as the stage checks them.</summary>
        public int ObstacleCount => _obstacleCount;

        public Obstacle GetObstacle(int index) => _obstacles[index];

        /// <summary>A foliage gust is running on a spot (a hint, a decoy, the whistle).</summary>
        public bool Rippling(int spot)
        {
            for (int i = 0; i < _gustSpot.Length; i++)
                if (_gustSpot[i] == spot)
                    return true;
            return false;
        }

        /// <summary>The whistle's wiggles played this session, each with its decor's ripple (tests: hints ripple the same decor).</summary>
        public int WhistleWiggles { get; private set; }

        public bool RingShown(int ring) => (uint)ring < (uint)_rings.Length && _rings[ring] != null && _rings[ring].gameObject.activeSelf;

        public bool MagnifierRingShown => RingShown(MagnifierRing);

        public int LeavesFlying
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _leaves.Length; i++)
                    if (_leaves[i] != null && _leaves[i].gameObject.activeSelf)
                        n++;
                return n;
            }
        }

        /// <summary>
        /// The share of the animal's silhouette the pipeline measured at the place it stands (interpolated like the
        /// placement); −1 for a species the set does not know.
        /// </summary>
        public float ExpectedShare(int animal)
        {
            if (_set == null || (uint)animal >= (uint)_hiddenCount || _view[animal] == null || _speciesRow[animal] < 0
                || (uint)_spot[animal] >= (uint)_spotCount)
                return -1f;
            int spot = _firstSpot + _spot[animal];
            float share = _game.Visibility;
            int row = _speciesRow[animal];
            if (share <= HideSpotSet.ShareAt(0))
                return _set.MeasuredShare(spot, row, 0);
            for (int level = 1; level < HideSpotSet.ShareLevels; level++)
            {
                float hi = HideSpotSet.ShareAt(level);
                if (share <= hi)
                {
                    float lo = HideSpotSet.ShareAt(level - 1);
                    return Mathf.Lerp(_set.MeasuredShare(spot, row, level - 1), _set.MeasuredShare(spot, row, level), (share - lo) / (hi - lo));
                }
            }
            return _set.MeasuredShare(spot, row, HideSpotSet.ShareLevels - 1);
        }

        /// <summary>The smallest gap between an animal's body and the zone's volumes or the other animals (negative: inside).</summary>
        public float AnimalClearance(int animal) =>
            (uint)animal < (uint)_hiddenCount && _phase[animal] != Off ? Clearance(animal, _position[animal], _yaw[animal], true) : float.MaxValue;

        // ---------------- building (at load) ----------------

        private void BuildProps()
        {
            _propsBuilt = true;
            var parent = _root.transform;
            if (_vfxMaterial != null)
            {
                _rings = new MeshRenderer[HideSeekLayout.RingCount];
                _ringMeshes = new Mesh[HideSeekLayout.RingCount];
                for (int i = 0; i < _rings.Length; i++)
                {
                    _ringMeshes[i] = RingQuad("HideSeek_Ring_" + i);
                    _rings[i] = Renderer("Ring_" + i, parent, _ringMeshes[i], _vfxMaterial);
                }
            }
            if (_leafMesh != null && _leafMaterial != null)
            {
                _leaves = new MeshRenderer[HideSeekLayout.LeafCount];
                uint value = EnvironmentVisualPacking.Pack(0, WorldChunk.NeutralBrightness);
                var palettes = _context != null ? _context.Palettes : null;
                if (palettes != null && palettes.TryGetRow(ContentId.FromSource(HideSeekLayout.LeafPalette), out int row))
                    value = EnvironmentVisualPacking.Pack(row, WorldChunk.NeutralBrightness);
                var size = _leafMesh.bounds.size;
                _leafScale = HideSeekLayout.LeafSize / Mathf.Max(0.005f, Mathf.Max(size.x, Mathf.Max(size.y, size.z)));
                for (int i = 0; i < _leaves.Length; i++)
                {
                    _leaves[i] = Renderer("Leaf_" + i, parent, _leafMesh, _leafMaterial);
                    _leaves[i].SetShaderUserValue(value);
                }
            }
        }

        private static MeshRenderer Renderer(string name, Transform parent, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.allowOcclusionWhenDynamic = false;
            go.SetActive(false);
            return renderer;
        }

        /// <summary>A unit quad (XY plane, 1 m across) showing the feedback atlas' ring tile; its vertex colors are the ring's tint.</summary>
        private static Mesh RingQuad(string name)
        {
            float u0 = HideSeekLayout.RingTile / (float)HideSeekLayout.AtlasTiles;
            float u1 = (HideSeekLayout.RingTile + 1) / (float)HideSeekLayout.AtlasTiles;
            var mesh = new Mesh { name = name };
            mesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) };
            mesh.uv = new[] { new Vector2(u0, 0f), new Vector2(u1, 0f), new Vector2(u1, 1f), new Vector2(u0, 1f) };
            mesh.colors32 = new[] { new Color32(255, 255, 255, 0), new Color32(255, 255, 255, 0), new Color32(255, 255, 255, 0), new Color32(255, 255, 255, 0) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(1f, 1f, 0.1f));
            mesh.MarkDynamic();
            return mesh;
        }

        private void DestroyProps()
        {
            for (int i = 0; i < _ringMeshes.Length; i++)
                DestroyObject(_ringMeshes[i]);
            _ringMeshes = System.Array.Empty<Mesh>();
            _rings = System.Array.Empty<MeshRenderer>();
            _leaves = System.Array.Empty<MeshRenderer>();
        }

        // ---------------- a game starts / ends ----------------

        /// <summary>
        /// The game's animals in their spots (its visibility, each at its own size), nibbling the grass head down (the pose the
        /// shares are measured in), their temperaments told.
        /// </summary>
        private void PlaceAnimals()
        {
            _views.HideAll();
            _hiddenCount = Mathf.Min(_game.HiddenCount, HideSeekLayout.MaxHidden);
            for (int i = 0; i < HideSeekLayout.MaxHidden; i++)
            {
                _phase[i] = Off;
                _view[i] = null;
                _hinting[i] = false;
                _wiggleAt[i] = -1f;
                _nibbleAt[i] = -1f;
                _spot[i] = -1;
            }
            if (_controller is HideSeekController played && _zone >= 0)
            {
                if (_zone >= s_lastLayouts.Length)
                    System.Array.Resize(ref s_lastLayouts, _zone + 8);
                s_lastLayouts[_zone] = played.LayoutMask;
            }
            for (int i = 0; i < _hiddenCount; i++)
            {
                int spot = _game.AnimalSpot(i);
                _spot[i] = spot;
                if (_pickCount == 0)
                    continue;
                ref readonly var animal = ref _picks[i % _pickCount];
                // Doc 01: a Timid animal waits 2 s more before its hint (the refuge's Shy temperament).
                _game.SetTimid(i, TemperamentOf(animal.Seed) == Temperament.Shy);
                if ((uint)spot >= (uint)_spotCount)
                    continue;
                _speciesRow[i] = _set.SpeciesIndex(animal.Species);
                _scale[i] = AnimalGenome.Scale(animal.TraitBits);
                HidingPose(i, spot, out var position, out float yaw);
                var view = _views.Show(in animal, position, yaw, true);
                if (view == null)
                    continue;
                _view[i] = view;
                _phase[i] = Hidden;
                SetPose(i, position, yaw);
                _landing[i] = position;
                view.PlayState(AnimalStateHashes.Eat, 0f);
            }
        }

        /// <summary>Every prop back in its pool (inactive), the gusts calm, the views returned.</summary>
        private void HideVisuals()
        {
            _views.HideAll();
            for (int i = 0; i < HideSeekLayout.MaxHidden; i++)
            {
                _phase[i] = Off;
                _view[i] = null;
            }
            _hiddenCount = 0;
            _gatherAt = -1f;
            for (int i = 0; i < _rings.Length; i++)
            {
                _ringKind[i] = RingOff;
                if (_rings[i] != null)
                    _rings[i].gameObject.SetActive(false);
            }
            _magnifierShow = 0f;
            for (int i = 0; i < _leaves.Length; i++)
            {
                _leafT[i] = -1f;
                if (_leaves[i] != null)
                    _leaves[i].gameObject.SetActive(false);
            }
            for (int i = 0; i < _gustSpot.Length; i++)
                _gustSpot[i] = -1;
            FoliageGusts.ClearAll();
        }

        // ---------------- placement (collision rule) ----------------

        /// <summary>
        /// Where an animal hides in a spot: the measured point for its species at the game's visibility (chunk origin plus the
        /// set's point), on the ground, facing as measured. A bigger individual than the measured one never enters its decor:
        /// it steps back from it until its body is free (1 cm at a time).
        /// </summary>
        private void HidingPose(int animal, int spot, out Vector3 position, out float yaw)
        {
            var s = _set.GetSpot(_firstSpot + spot);
            position = _origin + _set.Placement(_firstSpot + spot, _speciesRow[animal], _game.Visibility);
            position.y = GroundY(position);
            yaw = s.facing;
            var away = position - (_origin + s.decorCenter);
            away.y = 0f;
            if (away.sqrMagnitude < 1e-6f)
                away = CameraForward();
            away.Normalize();
            for (int step = 0; step < 40 && Clearance(animal, position, yaw, true) < -ContactTolerance; step++)
            {
                position += away * 0.01f;
                position.y = GroundY(position);
                PlacementSteps++;
            }
        }

        /// <summary>Centimeters stepped back this session so that no body entered anything (0 when the measured points fit as they are).</summary>
        public int PlacementSteps { get; private set; }

        private float GroundY(Vector3 p) => _ground.HeightAt(p.x, p.z);

        /// <summary>
        /// The smallest gap (m) between this animal's body at a pose and the zone's volumes at its height, and (with
        /// <paramref name="animals"/>) the other animals standing in the game. Negative: it would enter something.
        /// </summary>
        private float Clearance(int animal, Vector3 position, float yaw, bool animals, float lift = 0f)
        {
            var body = AnimalBody(animal);
            float scale = _scale[animal];
            body.Segment(position, yaw, scale, out var a0, out var a1);
            float radius = body.halfWidth * scale;
            float height = body.height * scale;
            float best = float.MaxValue;
            for (int i = 0; i < _obstacleCount; i++)
            {
                ref readonly var o = ref _obstacles[i];
                if (!o.BlocksWalker(position.y + lift, height))
                    continue;
                float gap = CollisionMath.Gap(a0, a1, radius, in o);
                if (gap < best)
                    best = gap;
            }
            if (!animals)
                return best;
            for (int j = 0; j < _hiddenCount; j++)
            {
                // Planning the gather: the others going to the row travel with it in screen order (their tracks never cross).
                if (j == animal || _phase[j] == Off || (_planningGather && _phase[j] == Gathering))
                    continue;
                var other = AnimalBody(j);
                float otherScale = _scale[j];
                // An animal on its way counts where it lands: two « coucou » never land in each other.
                bool travelling = _phase[j] == Hopping || _phase[j] == Gathering;
                other.Segment(travelling ? _to[j] : _position[j], travelling ? _toYaw[j] : _yaw[j], otherScale, out var b0, out var b1);
                float gap = CollisionMath.Gap(a0, a1, radius, b0, b1, other.halfWidth * otherScale);
                if (gap < best)
                    best = gap;
            }
            return best;
        }

        /// <summary>The animal's pose on the ground: its yaw, tilted with the slope like the refuge's walkers (14° at most).</summary>
        private void SetPose(int animal, Vector3 position, float yaw)
        {
            _position[animal] = position;
            _yaw[animal] = yaw;
            var view = _view[animal];
            if (view == null)
                return;
            var normal = _ground.NormalAt(position.x, position.z);
            float angle = Vector3.Angle(Vector3.up, normal);
            if (angle > MaxTiltDegrees)
                normal = Vector3.Slerp(Vector3.up, normal, MaxTiltDegrees / angle);
            view.SetPose(position, Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0f, yaw, 0f));
        }

        private Vector3 CameraForward()
        {
            var camera = Camera;
            var forward = camera != null ? camera.transform.forward : _set != null && _zone >= 0 ? _set.GetZone(_zone).cameraRotation * Vector3.forward : Vector3.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
        }

        /// <summary>The yaw (degrees) of an animal turned toward the camera, a little to the side (a 3/4 « coucou »).</summary>
        private float FacingCamera(int animal)
        {
            var toward = -CameraForward();
            float yaw = Mathf.Atan2(toward.x, toward.z) * Mathf.Rad2Deg;
            return yaw + ((animal & 1) == 0 ? 22f : -22f);
        }

        // ---------------- « coucou »: the hop out (doc 01: l'animal sort en « coucou ») ----------------

        /// <summary>A found animal hops out to the free ground in front of its spot, around its decor; it turns to the camera.</summary>
        private void StartCoucou(int animal, float delay)
        {
            if ((uint)animal >= (uint)_hiddenCount || _view[animal] == null)
                return;
            if (_phase[animal] == Moving)
                FinishMove(animal);
            if (_phase[animal] != Hidden)
                return;
            if (TryFreeLanding(animal, out var landing))
            {
                _landing[animal] = landing;
                PlanHop(animal, _position[animal], landing, HideSeekLayout.HopSeconds, true);
            }
            else
            {
                // No free ground in front (another animal out there, a crowded corner): it pops up where it hides, a hop in
                // place without turning (its body stays where it was measured free).
                _landing[animal] = _position[animal];
                PlanHop(animal, _position[animal], _position[animal], HideSeekLayout.HopSeconds, true);
                _control[animal] = _position[animal];
                _toYaw[animal] = _yaw[animal];
                _pop[animal] = false;
            }
            _t[animal] = -delay / HideSeekLayout.HopSeconds;
            _phase[animal] = Hopping;
            _hinting[animal] = false;
            _wiggleAt[animal] = -1f;
            _nibbleAt[animal] = -1f;
            _view[animal].PlayState(AnimalStateHashes.React, 0.05f);
        }

        /// <summary>
        /// The spot's measured landing point when this animal's body fits there (no volume, no other animal, none landing);
        /// else the nearest free point around it (rings of 8 cm, <see cref="HideSeekLayout.LandingRings"/>). False when none is
        /// free: never a landing inside anything (collision rule).
        /// </summary>
        private bool TryFreeLanding(int animal, out Vector3 landing)
        {
            int spot = _spot[animal];
            var measured = _origin + _set.GetSpot(_firstSpot + spot).landing;
            measured.y = GroundY(measured);
            landing = measured;
            float yaw = FacingCamera(animal);
            if (Clearance(animal, measured, yaw, true) >= HideSeekLayout.BodyGap)
                return true;
            for (int ring = 1; ring <= HideSeekLayout.LandingRings; ring++)
            {
                for (int k = 0; k < 8; k++)
                {
                    float a = k * Mathf.PI * 0.25f;
                    var p = measured + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (0.08f * ring);
                    p.y = GroundY(p);
                    if (Clearance(animal, p, yaw, true) >= HideSeekLayout.BodyGap)
                    {
                        landing = p;
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// An arc from one ground point to another (a quadratic curve in plan, a hop's height on top). When the straight track
        /// would cross the decor of the spot or anything else, it bends around beside the decor (either side, a little wider
        /// each try); with no free track at all the animal ducks down where it is and pops up there instead (never crossing).
        /// </summary>
        private void PlanHop(int animal, Vector3 from, Vector3 to, float seconds, bool animals)
        {
            _from[animal] = from;
            _to[animal] = to;
            _duration[animal] = seconds;
            _arc[animal] = HideSeekLayout.HopHeight * Mathf.Max(0.6f, _scale[animal]);
            _fromYaw[animal] = _yaw[animal];
            _toYaw[animal] = FacingCamera(animal);
            _pop[animal] = false;
            var middle = (from + to) * 0.5f;
            _control[animal] = middle;
            if (TrackClear(animal, animals))
                return;
            int spot = _spot[animal];
            var s = (uint)spot < (uint)_spotCount ? _set.GetSpot(_firstSpot + spot) : default;
            var decor = _origin + s.decorCenter;
            var body = AnimalBody(animal);
            float reach = s.decorRadius + body.Reach * _scale[animal] + HideSeekLayout.BodyGap;
            var along = to - from;
            along.y = 0f;
            var side = along.sqrMagnitude > 1e-6f ? new Vector3(-along.z, 0f, along.x).normalized : Vector3.right;
            float first = Vector3.Dot(middle - decor, side) >= 0f ? 1f : -1f;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                for (int pass = 0; pass < 2; pass++)
                {
                    float sign = pass == 0 ? first : -first;
                    var waypoint = decor + side * (sign * (reach + 0.05f + 0.08f * attempt));
                    waypoint.y = GroundY(waypoint);
                    // The curve passes through the waypoint at its middle: control = 2 W - (P + L) / 2.
                    _control[animal] = 2f * waypoint - 0.5f * (from + to);
                    if (TrackClear(animal, animals))
                        return;
                }
            }
            _control[animal] = middle;
            _pop[animal] = true;
        }

        /// <summary>Samples of the planned track: the body at each, in the air by the hop's height, enters nothing.</summary>
        private bool TrackClear(int animal, bool animals)
        {
            for (int k = 2; k <= 9; k++)
            {
                float t = k / 10f;
                var q = Curve(animal, t, out var tangent);
                float yaw = tangent.sqrMagnitude > 1e-6f ? Mathf.Atan2(tangent.x, tangent.z) * Mathf.Rad2Deg : _yaw[animal];
                float lift = 4f * t * (1f - t) * _arc[animal];
                q.y = GroundY(q);
                if (Clearance(animal, q, yaw, animals, lift) < -ContactTolerance)
                    return false;
            }
            return true;
        }

        private Vector3 Curve(int animal, float t, out Vector3 tangent)
        {
            var p = _from[animal];
            var c = _control[animal];
            var l = _to[animal];
            float u = 1f - t;
            tangent = 2f * u * (c - p) + 2f * t * (l - c);
            tangent.y = 0f;
            return u * u * p + 2f * u * t * c + t * t * l;
        }

        // ---------------- challenge 9: an animal slips to another spot, unseen ----------------

        private void StartMove(int animal, int spot)
        {
            if ((uint)animal >= (uint)_hiddenCount || _view[animal] == null || (uint)spot >= (uint)_spotCount)
                return;
            if (_phase[animal] == Moving)
                FinishMove(animal);
            if (_phase[animal] != Hidden)
                return;
            _nextSpot[animal] = spot;
            _t[animal] = 0f;
            _phase[animal] = Moving;
        }

        /// <summary>Lands the move at once (a find or a second move while it was still slipping).</summary>
        private void FinishMove(int animal)
        {
            _spot[animal] = _nextSpot[animal];
            HidingPose(animal, _spot[animal], out var position, out float yaw);
            SetPose(animal, position, yaw);
            _view[animal].SetScale(_scale[animal]);
            _phase[animal] = Hidden;
        }

        // ---------------- all found: they gather in front (T39 default) ----------------

        /// <summary>
        /// Every animal out of its spot gathers on a row at the zone's gather point, facing the camera, in their order on the
        /// screen (no track crosses another); one whose place or track is not free stays where it landed, and the others'
        /// places and tracks keep clear of it (collision rule: a hop never passes through an animal that stays).
        /// </summary>
        private void StartGather()
        {
            _gatherAt = -1f;
            if (_set == null || _zone < 0)
                return;
            var center = _origin + _set.GetZone(_zone).gatherPoint;
            center.y = GroundY(center);
            var right = Vector3.Cross(Vector3.up, CameraForward());
            int n = 0;
            for (int i = 0; i < _hiddenCount; i++)
                if (_phase[i] == Out && _view[i] != null)
                    _order[n++] = i;
            if (n == 0)
                return;
            // Left to right as they stand on the screen.
            for (int a = 1; a < n; a++)
            {
                int key = _order[a];
                float x = Vector3.Dot(_position[key], right);
                int b = a - 1;
                while (b >= 0 && Vector3.Dot(_position[_order[b]], right) > x)
                {
                    _order[b + 1] = _order[b];
                    b--;
                }
                _order[b + 1] = key;
            }
            float width = 0f;
            for (int k = 0; k < n; k++)
                width += 2f * AnimalBody(_order[k]).halfWidth * _scale[_order[k]] + (k > 0 ? 0.06f : 0f);
            float x0 = -0.5f * width;
            for (int k = 0; k < n; k++)
            {
                int i = _order[k];
                float half = AnimalBody(i).halfWidth * _scale[i];
                var slot = center + right * (x0 + half);
                slot.y = GroundY(slot);
                x0 += 2f * half + 0.06f;
                _gatherSlot[i] = slot;
                _gatherOk[i] = Clearance(i, slot, FacingCamera(i), false) >= HideSeekLayout.BodyGap;
            }
            // Until it settles: a slot taken by an animal that stays (its own place or track not free) is given up too, and
            // every track is planned around the volumes and the animals that stay (the others go to the row with it).
            _planningGather = true;
            try
            {
                for (int round = 0; round <= n; round++)
                {
                    bool changed = false;
                    for (int k = 0; k < n; k++)
                    {
                        int i = _order[k];
                        if (!_gatherOk[i])
                            continue;
                        for (int m = 0; m < n && _gatherOk[i]; m++)
                        {
                            int j = _order[m];
                            if (j == i || _gatherOk[j])
                                continue;
                            if (BodyGap(i, _gatherSlot[i], FacingCamera(i), j, _position[j], _yaw[j]) < HideSeekLayout.BodyGap)
                            {
                                _gatherOk[i] = false;
                                changed = true;
                            }
                        }
                    }
                    for (int k = 0; k < n; k++)
                    {
                        int i = _order[k];
                        _phase[i] = _gatherOk[i] ? Gathering : Out;
                    }
                    for (int k = 0; k < n; k++)
                    {
                        int i = _order[k];
                        if (!_gatherOk[i])
                            continue;
                        PlanHop(i, _position[i], _gatherSlot[i], HideSeekLayout.GatherSeconds, true);
                        if (_pop[i])
                        {
                            // No free track: it stays, and the others plan again around it.
                            _gatherOk[i] = false;
                            _phase[i] = Out;
                            changed = true;
                        }
                    }
                    if (!changed)
                        break;
                }
            }
            finally
            {
                _planningGather = false;
            }
            int moving = 0;
            for (int k = 0; k < n; k++)
            {
                int i = _order[k];
                if (!_gatherOk[i])
                {
                    _phase[i] = Out;
                    _view[i].PlayState(AnimalStateHashes.Happy, 0.15f);
                    continue;
                }
                _t[i] = -0.08f * moving++ / HideSeekLayout.GatherSeconds;
                _phase[i] = Gathering;
            }
        }

        private float BodyGap(int a, Vector3 pa, float yawA, int b, Vector3 pb, float yawB)
        {
            var bodyA = AnimalBody(a);
            var bodyB = AnimalBody(b);
            bodyA.Segment(pa, yawA, _scale[a], out var a0, out var a1);
            bodyB.Segment(pb, yawB, _scale[b], out var b0, out var b1);
            return CollisionMath.Gap(a0, a1, bodyA.halfWidth * _scale[a], b0, b1, bodyB.halfWidth * _scale[b]);
        }

        /// <summary>The game ended with animals still hidden: they peek out to their landing points, one after the other.</summary>
        private void RevealRest()
        {
            int k = 0;
            for (int i = 0; i < _hiddenCount; i++)
                if (_phase[i] == Hidden || _phase[i] == Moving)
                    StartCoucou(i, 0.25f + 0.12f * k++);
        }

        // ---------------- per frame ----------------

        private void UpdateAnimals(float dt)
        {
            for (int i = 0; i < _hiddenCount; i++)
            {
                var view = _view[i];
                if (view == null)
                    continue;
                switch (_phase[i])
                {
                    case Hidden:
                        if (_wiggleAt[i] >= 0f && _time >= _wiggleAt[i])
                        {
                            // The whistle: a small wiggle, its decor ripples once.
                            _wiggleAt[i] = -1f;
                            WhistleWiggles++;
                            view.PlayState(AnimalStateHashes.React, 0.08f);
                            _nibbleAt[i] = _time + HideSeekLayout.BackToNibbleSeconds;
                            Gust(_spot[i], HideSeekLayout.WhistleRippleSeconds, 0.9f);
                        }
                        if (_hinting[i] && _time >= _hintNext[i])
                        {
                            // H-VFX-ACT-002-03: the ear twitches softly in place, the grass of its spot ripples; again every 3 s.
                            _hintNext[i] = _time + HideSeekLayout.HintRepeatSeconds;
                            view.PlayState(AnimalStateHashes.React, HideSeekLayout.SoftReactFade);
                            _nibbleAt[i] = _time + HideSeekLayout.BackToNibbleSeconds;
                            Gust(_spot[i], HideSeekLayout.HintRippleSeconds, 1f);
                        }
                        if (_nibbleAt[i] >= 0f && _time >= _nibbleAt[i])
                        {
                            // Back to nibbling after the peek: the head goes down behind the decor again.
                            _nibbleAt[i] = -1f;
                            view.PlayState(AnimalStateHashes.Eat, HideSeekLayout.NibbleFade);
                        }
                        break;
                    case Hopping:
                    case Gathering:
                        UpdateHop(i, dt);
                        break;
                    case Moving:
                        UpdateMove(i, dt);
                        break;
                }
            }
            if (_gatherAt >= 0f && _time >= _gatherAt)
                StartGather();
        }

        private void UpdateHop(int animal, float dt)
        {
            _t[animal] += dt / Mathf.Max(0.01f, _duration[animal]);
            if (_t[animal] < 0f)
                return;
            float t = Mathf.Min(1f, _t[animal]);
            var view = _view[animal];
            if (_pop[animal])
            {
                // No free track: down into the grass where it stood, up at the landing (never seen crossing anything).
                float s = t < 0.5f ? 1f - 0.95f * Smooth(t / 0.5f) : 0.05f + 0.95f * Smooth((t - 0.5f) / 0.5f);
                view.SetScale(_scale[animal] * s);
                if (t >= 0.5f && _position[animal] != _to[animal])
                    SetPose(animal, _to[animal], _toYaw[animal]);
            }
            else
            {
                var q = Curve(animal, t, out var tangent);
                q.y = GroundY(q) + 4f * t * (1f - t) * _arc[animal];
                // Facing along its way, then turning to the camera as it lands.
                float travel = tangent.sqrMagnitude > 1e-6f ? Mathf.Atan2(tangent.x, tangent.z) * Mathf.Rad2Deg : _fromYaw[animal];
                float yaw = t < 0.6f ? Mathf.LerpAngle(_fromYaw[animal], travel, Smooth(t / 0.25f)) : Mathf.LerpAngle(travel, _toYaw[animal], Smooth((t - 0.6f) / 0.4f));
                _position[animal] = q;
                _yaw[animal] = yaw;
                view.SetPose(q, Quaternion.Euler(0f, yaw, 0f));
            }
            if (t < 1f)
                return;
            view.SetScale(_scale[animal]);
            SetPose(animal, _to[animal], _toYaw[animal]);
            bool gathered = _phase[animal] == Gathering;
            _phase[animal] = Out;
            view.PlayState(AnimalStateHashes.Happy, 0.12f);
            if (gathered && (animal & 1) == 0)
                Heart(animal);
        }

        private void UpdateMove(int animal, float dt)
        {
            float before = _t[animal];
            _t[animal] += dt / HideSeekLayout.MovedSeconds;
            float t = Mathf.Min(1f, _t[animal]);
            var view = _view[animal];
            if (before < 0.5f && t >= 0.5f)
            {
                // Out of sight under the grass: it reappears behind its new decor.
                _spot[animal] = _nextSpot[animal];
                HidingPose(animal, _spot[animal], out var position, out float yaw);
                SetPose(animal, position, yaw);
            }
            float s = t < 0.5f ? 1f - 0.95f * Smooth(t / 0.5f) : 0.05f + 0.95f * Smooth((t - 0.5f) / 0.5f);
            view.SetScale(_scale[animal] * s);
            if (t >= 1f)
            {
                view.SetScale(_scale[animal]);
                _phase[animal] = Hidden;
            }
        }

        // ---------------- gusts (H-VFX-ACT-002-03, FoliageGusts) ----------------

        /// <summary>A local gust on a spot's decor for a while: the same spot gusting again only extends it; else a free slot, else the oldest.</summary>
        private void Gust(int spot, float seconds, float peak)
        {
            if ((uint)spot >= (uint)_spotCount || _set == null)
                return;
            int slot = -1;
            for (int i = 0; i < _gustSpot.Length && slot < 0; i++)
                if (_gustSpot[i] == spot)
                    slot = i;
            if (slot >= 0 && _time - _gustStart[slot] < _gustLength[slot])
            {
                _gustLength[slot] = Mathf.Max(_gustLength[slot], _time - _gustStart[slot] + seconds * 0.6f);
                _gustPeak[slot] = Mathf.Max(_gustPeak[slot], peak);
                return;
            }
            for (int i = 0; i < _gustSpot.Length && slot < 0; i++)
                if (_gustSpot[i] < 0)
                    slot = i;
            if (slot < 0)
            {
                float latest = float.MaxValue;
                for (int i = 0; i < _gustSpot.Length; i++)
                {
                    float end = _gustStart[i] + _gustLength[i];
                    if (end < latest)
                    {
                        latest = end;
                        slot = i;
                    }
                }
            }
            var s = _set.GetSpot(_firstSpot + spot);
            _gustSpot[slot] = spot;
            _gustStart[slot] = _time;
            _gustLength[slot] = seconds;
            _gustPeak[slot] = peak;
            _gustPosition[slot] = _origin + s.decorCenter + Vector3.up * (0.5f * s.decorHeight);
            _gustRadius[slot] = Mathf.Max(0.25f, s.decorRadius * 1.15f + 0.05f);
        }

        private void UpdateGusts()
        {
            for (int i = 0; i < _gustSpot.Length; i++)
            {
                if (_gustSpot[i] < 0)
                    continue;
                float t = _time - _gustStart[i];
                float length = _gustLength[i];
                if (t >= length)
                {
                    _gustSpot[i] = -1;
                    FoliageGusts.Clear(i);
                    continue;
                }
                // A soft rise (0.2 s), a hold, a long fall: a ripple, never a jolt.
                float rise = Smooth(t / 0.2f);
                float fall = 1f - Smooth((t - 0.45f * length) / (0.55f * length));
                FoliageGusts.Set(i, _gustPosition[i], _gustRadius[i], _gustPeak[i] * Mathf.Min(rise, fall));
            }
        }

        // ---------------- rings ----------------

        private void Ring(byte kind, Vector3 position, float size)
        {
            if (_rings.Length == 0)
                return;
            int slot;
            if (kind == RingMagnifier)
            {
                slot = MagnifierRing;
            }
            else
            {
                slot = _ringNext;
                _ringNext = (_ringNext + 1) % MagnifierRing;
            }
            _ringKind[slot] = kind;
            _ringT[slot] = 0f;
            _ringSize[slot] = size;
            _ringPosition[slot] = position;
            _ringAlpha[slot] = 255;
            if (_rings[slot] != null)
            {
                SetRingColor(slot, kind == RingCue ? new Color32(214, 220, 226, 0) : new Color32(255, 242, 212, 0));
                _rings[slot].gameObject.SetActive(true);
            }
        }

        private void UpdateRings(float dt)
        {
            var camera = Camera;
            var facing = camera != null ? camera.transform.rotation : Quaternion.identity;
            var ground = Quaternion.Euler(90f, 0f, 0f);
            for (int i = 0; i < _rings.Length; i++)
            {
                var renderer = _rings[i];
                if (renderer == null || _ringKind[i] == RingOff || i == MagnifierRing)
                    continue;
                _ringT[i] += dt;
                float t = _ringT[i];
                float length = _ringKind[i] == RingCue ? 0.45f : HideSeekLayout.RingSeconds;
                if (t >= length)
                {
                    _ringKind[i] = RingOff;
                    renderer.gameObject.SetActive(false);
                    continue;
                }
                float k = t / length;
                float size = _ringSize[i] * (_ringKind[i] == RingCue ? 0.8f + 0.3f * k : 0.45f + 0.85f * (1f - (1f - k) * (1f - k)));
                float alpha = _ringKind[i] == RingCue ? 0.5f * (1f - k) : 0.9f * Mathf.Pow(1f - k, 1.4f) * Smooth(k / 0.12f);
                renderer.transform.SetPositionAndRotation(_ringPosition[i], ground);
                renderer.transform.localScale = new Vector3(size, size, size);
                SetRingAlpha(i, alpha);
            }
            UpdateMagnifier(dt, facing);
        }

        /// <summary>The magnifier's soft glass ring around the spot it shows, pulsing gently while the game counts it (2 s).</summary>
        private void UpdateMagnifier(float dt, Quaternion facing)
        {
            if (_rings.Length == 0)
                return;
            var renderer = _rings[MagnifierRing];
            int spot = _game.MagnifierSpot;
            bool on = spot >= 0 && spot < _spotCount && _game.MagnifierLeft > 0f && !_ended;
            _magnifierShow = Mathf.MoveTowards(_magnifierShow, on ? 1f : 0f, dt / 0.15f);
            if (_magnifierShow <= 0f)
            {
                if (_ringKind[MagnifierRing] != RingOff)
                {
                    _ringKind[MagnifierRing] = RingOff;
                    renderer.gameObject.SetActive(false);
                }
                return;
            }
            if (on)
            {
                var s = _set.GetSpot(_firstSpot + spot);
                _ringPosition[MagnifierRing] = _origin + s.decorCenter + Vector3.up * (0.5f * Mathf.Max(0.25f, s.decorHeight));
                _ringSize[MagnifierRing] = 2.3f * Mathf.Max(s.decorRadius, 0.5f * s.decorHeight) + 0.25f;
            }
            if (_ringKind[MagnifierRing] == RingOff)
                Ring(RingMagnifier, _ringPosition[MagnifierRing], _ringSize[MagnifierRing]);
            float pulse = 1f + 0.05f * Mathf.Sin(_time * 7f);
            float size = _ringSize[MagnifierRing] * pulse;
            renderer.transform.SetPositionAndRotation(_ringPosition[MagnifierRing], facing);
            renderer.transform.localScale = new Vector3(size, size, size);
            SetRingAlpha(MagnifierRing, (0.6f + 0.2f * Mathf.Sin(_time * 7f)) * _magnifierShow);
        }

        private void SetRingColor(int ring, Color32 color)
        {
            for (int v = 0; v < 4; v++)
                _ringColors[v] = color;
            _ringMeshes[ring].colors32 = _ringColors;
            _ringAlpha[ring] = color.a;
        }

        /// <summary>The ring's alpha in its vertex colors (the shared VFX shader's tint), written only when it moved.</summary>
        private void SetRingAlpha(int ring, float alpha)
        {
            byte a = (byte)Mathf.Clamp(Mathf.RoundToInt(alpha * 255f), 0, 255);
            if (a == _ringAlpha[ring])
                return;
            var mesh = _ringMeshes[ring];
            var color = _ringKind[ring] == RingCue ? new Color32(214, 220, 226, a) : new Color32(255, 242, 212, a);
            for (int v = 0; v < 4; v++)
                _ringColors[v] = color;
            mesh.colors32 = _ringColors;
            _ringAlpha[ring] = a;
        }

        // ---------------- leaves (H-VFX-ACT-002-02) ----------------

        /// <summary>Three leaves fly off the decor's front toward the camera and up, away from it (never through it).</summary>
        private void Leaves(int spot)
        {
            if (_leaves.Length == 0 || (uint)spot >= (uint)_spotCount)
                return;
            var s = _set.GetSpot(_firstSpot + spot);
            var toward = -CameraForward();
            var side = Vector3.Cross(Vector3.up, toward);
            var center = _origin + s.decorCenter;
            float floor = GroundY(center);
            for (int k = 0; k < HideSeekLayout.LeavesPerTouch; k++)
            {
                int i = _leafNext;
                _leafNext = (_leafNext + 1) % _leaves.Length;
                var renderer = _leaves[i];
                if (renderer == null)
                    continue;
                float lateral = (k - 1) * 0.55f + (Next01() - 0.5f) * 0.3f;
                _leafFrom[i] = center + toward * (s.decorRadius * 1.08f + 0.03f) + side * (lateral * s.decorRadius * 0.6f)
                               + Vector3.up * (Mathf.Max(0.12f, s.decorHeight) * (0.55f + 0.25f * Next01()));
                _leafVelocity[i] = toward * (0.55f + 0.35f * Next01()) + Vector3.up * (1.05f + 0.35f * Next01()) + side * (lateral * 0.9f);
                _leafAxis[i] = new Vector3(Next01() - 0.5f, Next01() - 0.5f, Next01() + 0.2f).normalized;
                _leafFloor[i] = floor;
                _leafT[i] = -0.04f * k;
                renderer.transform.localScale = Vector3.zero;
                renderer.gameObject.SetActive(true);
            }
        }

        private void UpdateLeaves(float dt)
        {
            for (int i = 0; i < _leaves.Length; i++)
            {
                if (_leafT[i] < -0.5f || _leaves[i] == null)
                    continue;
                _leafT[i] += dt;
                float t = _leafT[i];
                var target = _leaves[i].transform;
                if (t < 0f)
                    continue;
                if (t >= HideSeekLayout.LeafSeconds)
                {
                    _leafT[i] = -1f;
                    _leaves[i].gameObject.SetActive(false);
                    continue;
                }
                var p = _leafFrom[i] + _leafVelocity[i] * t + Vector3.up * (-1.1f * t * t);
                // A leaf drifting down fades before it touches the grass.
                float fade = 1f - Smooth((t - 0.55f * HideSeekLayout.LeafSeconds) / (0.45f * HideSeekLayout.LeafSeconds));
                if (p.y < _leafFloor[i] + 0.06f)
                    fade = Mathf.Min(fade, Mathf.Max(0f, (p.y - _leafFloor[i]) / 0.06f));
                float s = _leafScale * fade * Smooth(t / 0.08f);
                target.SetPositionAndRotation(p, Quaternion.AngleAxis(620f * t, _leafAxis[i]));
                target.localScale = new Vector3(s, s, s);
            }
        }

        // ---------------- hearts ----------------

        /// <summary>H-VFX-012: the refuge's own heart burst (pooled there) above the animal's head.</summary>
        private void Heart(int animal)
        {
            var view = _view[animal];
            if (view == null)
                return;
            var head = view.SocketHeadTop != null ? view.SocketHeadTop.position : _position[animal] + Vector3.up * (AnimalBody(animal).height * _scale[animal]);
            var point = head + Vector3.up * (0.06f * _scale[animal]);
            var refuge = _host != null ? _host.Refuge : null;
            if (refuge != null && refuge.Vfx != null)
                refuge.Vfx.Play(VfxId.FavoriteHeart, point, refuge.SimulationTime);
            else
                Vfx?.Play(VfxId.FavoriteHeart, point, _time);
        }

        /// <summary>Captures without a refuge: the feedback effects the hearts come from.</summary>
        public FeedbackVfx Vfx { get; set; }

        private float Next01()
        {
            _seed ^= _seed << 13;
            _seed ^= _seed >> 17;
            _seed ^= _seed << 5;
            return (_seed & 0xFFFFFFu) / 16777216f;
        }

        private static float Smooth(float x)
        {
            float t = Mathf.Clamp01(x);
            return t * t * (3f - 2f * t);
        }
    }
}
