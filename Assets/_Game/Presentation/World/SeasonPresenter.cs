using System;
using System.Collections.Generic;
using System.Diagnostics;
using PocketWilds.Core;
using PocketWilds.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T48B the season's palette rows: the base colors of every row a season touches are read once from the palette texture's
    /// CPU copy, each season's colors prepared once; a change writes only the rows whose colors differ and uploads them in one
    /// Apply. No allocation after construction.
    /// </summary>
    public sealed class SeasonPalettes
    {
        private readonly PaletteTable _table;
        private readonly int[] _rows;
        private readonly Color32[] _base;
        private readonly Color32[][] _seasons = new Color32[5][];

        public SeasonPalettes(PaletteTable table, SeasonLookSet looks)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            var rows = new List<int>();
            var slot = new Dictionary<int, int>();
            int missing = 0;
            if (looks != null)
            {
                foreach (var look in looks.looks)
                {
                    if (look == null)
                        continue;
                    foreach (var palette in look.palettes)
                    {
                        if (palette == null || string.IsNullOrEmpty(palette.paletteId) || palette.colors == null
                            || palette.colors.Length < PaletteTable.Columns || !table.TryGetRow(ContentId.FromSource(palette.paletteId), out int row))
                        {
                            missing++;
                            continue;
                        }
                        if (slot.ContainsKey(row))
                            continue;
                        slot.Add(row, rows.Count);
                        rows.Add(row);
                    }
                }
            }
            _rows = rows.ToArray();
            MissingPalettes = missing;
            _base = new Color32[_rows.Length * PaletteTable.Columns];
            var read = new Color32[PaletteTable.Columns];
            for (int i = 0; i < _rows.Length; i++)
            {
                table.ReadRow(_rows[i], read);
                Array.Copy(read, 0, _base, i * PaletteTable.Columns, PaletteTable.Columns);
            }
            _seasons[0] = _base;
            for (int s = 1; s < _seasons.Length; s++)
            {
                var colors = (Color32[])_base.Clone();
                var look = looks != null ? looks.Get((RefugeSeason)s) : null;
                if (look != null)
                {
                    foreach (var palette in look.palettes)
                    {
                        if (palette == null || string.IsNullOrEmpty(palette.paletteId) || palette.colors == null || palette.colors.Length < PaletteTable.Columns
                            || !table.TryGetRow(ContentId.FromSource(palette.paletteId), out int row) || !slot.TryGetValue(row, out int at))
                            continue;
                        Array.Copy(palette.colors, 0, colors, at * PaletteTable.Columns, PaletteTable.Columns);
                    }
                }
                _seasons[s] = colors;
            }
        }

        /// <summary>Rows any season touches.</summary>
        public int RowCount => _rows.Length;

        /// <summary>Authored rows whose palette is not in the table (another biome not loaded, a renamed palette).</summary>
        public int MissingPalettes { get; }

        /// <summary>Writes the season's rows (base colors for None and Soleil) and uploads them. Returns the rows written.</summary>
        public int Apply(RefugeSeason season)
        {
            int s = (int)season;
            var colors = s >= 1 && s < _seasons.Length ? _seasons[s] : _base;
            for (int i = 0; i < _rows.Length; i++)
                _table.WriteRow(_rows[i], colors, i * PaletteTable.Columns);
            return _table.ApplyRows();
        }

        /// <summary>The colors a row wears in a season (tests). False when no season touches it.</summary>
        public bool TryGetSeasonColors(int row, RefugeSeason season, Color32[] into)
        {
            int at = Array.IndexOf(_rows, row);
            if (at < 0)
                return false;
            int s = (int)season;
            var colors = s >= 1 && s < _seasons.Length ? _seasons[s] : _base;
            Array.Copy(colors, at * PaletteTable.Columns, into, 0, PaletteTable.Columns);
            return true;
        }
    }

    /// <summary>
    /// T48B the season's drift: round soft petals (Éveil), rounded falling leaves (Récolte), fine round snow (Givre). ONE instanced
    /// draw of a quad whose instances only carry a position in a unit box: the vertex shader wraps it through the drift box
    /// around the view, makes it fall, sway and tumble, and fades it out above the ground (nothing rests inside anything).
    /// Capped at 160 instances on Magnifique, -30 % Équilibrée, -60 % Fluide. No allocation per frame; no draw without a drift.
    /// </summary>
    public sealed class SeasonDrift
    {
        public const int MaxInstances = 160;

        private static readonly int BoxId = Shader.PropertyToID("_PW_DriftBox");
        private static readonly int SizeId = Shader.PropertyToID("_PW_DriftSize");
        private static readonly int ColorAId = Shader.PropertyToID("_ColorA");
        private static readonly int ColorBId = Shader.PropertyToID("_ColorB");
        private static readonly int ShapeId = Shader.PropertyToID("_Shape");
        private static readonly int ParticleSizeId = Shader.PropertyToID("_Size");
        private static readonly int FallId = Shader.PropertyToID("_Fall");
        private static readonly int SwayId = Shader.PropertyToID("_Sway");

        private readonly Matrix4x4[] _instances;
        private readonly MaterialPropertyBlock _properties = new MaterialPropertyBlock();
        private readonly Mesh _mesh;
        private readonly Material _material;
        private readonly Vector3 _box;
        private RenderParams _params;
        private float _amount;
        private float _intensity = 1f;
        private uint _random = 0xB5297A4Du;

        public SeasonDrift(SeasonLookSet looks, in WorldQualityProfile.Tier quality)
        {
            int max = looks != null ? Mathf.Clamp(looks.maxDrift, 0, MaxInstances) : 0;
            _instances = new Matrix4x4[max];
            for (int i = 0; i < max; i++)
                _instances[i] = Matrix4x4.Translate(new Vector3(Next(), Next(), Next()));
            _mesh = looks != null ? looks.quadMesh : null;
            _material = looks != null ? looks.driftMaterial : null;
            _box = looks != null ? looks.driftBox : new Vector3(14f, 5f, 14f);
            if (_material != null)
                _params = new RenderParams(_material) { matProps = _properties, shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
            SetQuality(quality);
        }

        public SeasonDriftKind Kind { get; private set; }

        /// <summary>Instances drawn now (season amount, tier share).</summary>
        public int Count => Kind == SeasonDriftKind.None ? 0 : Mathf.CeilToInt(_instances.Length * _amount * _intensity - 0.0001f);

        public int Capacity => _instances.Length;
        public int LastInstances { get; private set; }
        public int LastDraws { get; private set; }

        /// <summary>Share of the instances kept by the tier (40 / 70 / 100 %, like the rain).</summary>
        public void SetQuality(in WorldQualityProfile.Tier quality) => _intensity = Mathf.Clamp01(quality.weather);

        /// <summary>The season's drift: shape, colors, size, fall and sway, set once on the property block.</summary>
        public void SetLook(SeasonLookSet.Look look)
        {
            Kind = look != null && look.driftAmount > 0f ? look.drift : SeasonDriftKind.None;
            _amount = Kind == SeasonDriftKind.None ? 0f : Mathf.Clamp01(look.driftAmount);
            if (Kind == SeasonDriftKind.None)
                return;
            _properties.SetColor(ColorAId, look.driftColorA);
            _properties.SetColor(ColorBId, look.driftColorB);
            _properties.SetFloat(ShapeId, Kind == SeasonDriftKind.Petals ? 0f : Kind == SeasonDriftKind.Leaves ? 1f : 2f);
            _properties.SetFloat(ParticleSizeId, look.driftSize);
            _properties.SetFloat(FallId, look.driftFall);
            _properties.SetFloat(SwayId, look.driftSway);
        }

        /// <summary>One instanced draw between the focus and the camera, above the ground. No allocation.</summary>
        public void Render(Camera camera, Vector3 focus)
        {
            LastInstances = 0;
            LastDraws = 0;
            int count = Count;
            if (count <= 0 || camera == null || _mesh == null || _material == null)
                return;
            // Like the rain: the box leans toward the camera, so the drift passes in the open air in front of the scene; its floor
            // sits 0.5 m above the focus, where every petal, leaf and flake has faded out.
            var center = Vector3.Lerp(focus, camera.transform.position, 0.35f);
            center.y = focus.y + 0.5f + _box.y * 0.5f;
            _properties.SetVector(BoxId, new Vector4(center.x, center.y, center.z, _box.y));
            _properties.SetVector(SizeId, new Vector4(_box.x, 0f, _box.z, 0f));
            _params.worldBounds = new Bounds(center, _box + Vector3.one);
            _params.camera = camera;
            Graphics.RenderMeshInstanced(_params, _mesh, 0, _instances, count);
            LastInstances = count;
            LastDraws = 1;
        }

        private float Next()
        {
            _random ^= _random << 13;
            _random ^= _random >> 17;
            _random ^= _random << 5;
            return (_random & 0xFFFFFFu) / 16777216f;
        }
    }

    /// <summary>
    /// T48B the refuge's season on screen: palettes (only the changed vegetation and ground rows), the atmosphere tint, the
    /// weather rules (Éveil showers more often, Givre no rain nor rainbow), the drift and the fireflies of Soleil evenings.
    /// Listens to <see cref="ISeasonView.Changed"/>: a new Remote Config month map switches everything at once, without a
    /// reload. Idle cost: one check a second (fireflies' evening weight) and the drift draw.
    /// </summary>
    public sealed class SeasonPresenter : IDisposable
    {
        private const float CheckSeconds = 1f;

        private readonly ISeasonView _view;
        private readonly SeasonLookSet _looks;
        private readonly SeasonPalettes _palettes;
        private readonly WorldAtmosphere _atmosphere;
        private readonly WeatherSystem _weather;
        private readonly AmbientLife _life;
        private readonly SeasonDrift _drift;
        private readonly Action<RefugeSeason> _onChanged;
        private RefugeSeason _forced;
        private bool _fireflies;
        private float _nextCheck = float.NegativeInfinity;

        public SeasonPresenter(ISeasonView view, PaletteTable palettes, SeasonLookSet looks, WorldAtmosphere atmosphere, WeatherSystem weather,
            AmbientLife life, in WorldQualityProfile.Tier quality)
        {
            _view = view;
            _looks = looks;
            _palettes = palettes != null && looks != null ? new SeasonPalettes(palettes, looks) : null;
            _atmosphere = atmosphere;
            _weather = weather;
            _life = life;
            _drift = looks != null && looks.quadMesh != null && looks.driftMaterial != null ? new SeasonDrift(looks, quality) : null;
            _onChanged = OnChanged;
            if (_view != null)
                _view.Changed += _onChanged;
            Apply(Target);
        }

        /// <summary>The season on screen.</summary>
        public RefugeSeason Shown { get; private set; }
        public SeasonPalettes Palettes => _palettes;
        public SeasonDrift Drift => _drift;
        public bool Fireflies => _fireflies;

        /// <summary>Cost of the last switch: wall time (ms) and palette rows uploaded.</summary>
        public double LastSwitchMilliseconds { get; private set; }
        public int LastRowsWritten { get; private set; }

        /// <summary>The service's season; Soleil (the validated look) when none runs (headless scenes).</summary>
        private RefugeSeason Target =>
            _forced != RefugeSeason.None ? _forced : _view != null && _view.Current != RefugeSeason.None ? _view.Current : RefugeSeason.Sun;

        /// <summary>Captures and tests: shows a season whatever the date. None returns to the service's season.</summary>
        public void ForceSeason(RefugeSeason season)
        {
            _forced = season;
            Apply(Target);
        }

        private void OnChanged(RefugeSeason season) => Apply(Target);

        public void Apply(RefugeSeason season)
        {
            long start = Stopwatch.GetTimestamp();
            var look = _looks != null ? _looks.Get(season) : null;
            int rows = _palettes != null ? _palettes.Apply(season) : 0;
            _atmosphere?.SetSeason(look != null ? look.Tint : default);
            if (look != null)
                _weather?.SetSeasonRules(look.showerFrequency, look.rain, look.rainbow);
            else
                _weather?.SetSeasonRules(1f, true, true);
            _drift?.SetLook(look);
            _fireflies = look != null && look.fireflies;
            _nextCheck = float.NegativeInfinity;
            if (!_fireflies)
                _life?.SetFireflies(0f);
            Shown = season;
            LastRowsWritten = rows;
            LastSwitchMilliseconds = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
        }

        /// <summary>Once a second: the fireflies follow the evening of the hour shown. No allocation.</summary>
        public void Tick(float time)
        {
            if (time < _nextCheck)
                return;
            _nextCheck = time + CheckSeconds;
            if (_life == null || !_fireflies)
                return;
            float weight = _atmosphere == null ? 0f : _atmosphere.Night ? 1f : FireflyWeight(_atmosphere.Hour);
            _life.SetFireflies(weight);
        }

        public void Render(Camera camera, Vector3 focus) => _drift?.Render(camera, focus);

        public void SetQuality(in WorldQualityProfile.Tier quality) => _drift?.SetQuality(quality);

        /// <summary>Fireflies come out from 18:30 (all out at 20:00) and go home from 5:30 (gone at 7:00).</summary>
        public static float FireflyWeight(float hour)
        {
            hour = Mathf.Repeat(hour, 24f);
            if (hour >= 20f || hour <= 5.5f)
                return 1f;
            if (hour > 18.5f)
                return Mathf.SmoothStep(0f, 1f, (hour - 18.5f) / 1.5f);
            if (hour < 7f)
                return Mathf.SmoothStep(1f, 0f, (hour - 5.5f) / 1.5f);
            return 0f;
        }

        /// <summary>Unsubscribes and gives the palette rows their base colors back (the table outlives the scene).</summary>
        public void Dispose()
        {
            if (_view != null)
                _view.Changed -= _onChanged;
            _palettes?.Apply(RefugeSeason.None);
            _life?.SetFireflies(0f);
        }
    }
}
