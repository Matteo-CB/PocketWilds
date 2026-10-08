using System;
using PocketWilds.Core;
using PocketWilds.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>The interface bursts of the rendez-vous (doc 05 Sensations, section 5.2). Stable values.</summary>
    public enum UiBurstKind : byte
    {
        /// <summary>H-VFX-071 VFX_UI_DailyGift: 12 round glints and a ring, 0.8 s; a gold ring more for an egg or Gems.</summary>
        Gift = 0,

        /// <summary>H-VFX-072 VFX_UI_CalendarStamp: a ring and 4 ink dots, 0.4 s.</summary>
        Stamp = 1,

        /// <summary>H-VFX-072 at boxes 7, 14, 21, 28: the stamp's ring and dots, plus 12 round five-point stars, 0.9 s.</summary>
        StampBig = 2,

        /// <summary>A wish taken in Today (the screen echo of H-VFX-070): soft hearts, round bubbles and a ring, 0.9 s.</summary>
        Hearts = 3,

        /// <summary>A claim (welcome gift, journal goal): a ring and 6 round dots, 0.6 s.</summary>
        Claim = 4,
    }

    /// <summary>
    /// T36 interface burst (doc 05 Sensations, section 0.4: « un seul composant poolé UiBurst », on the principle of
    /// CollectMotes, since a particle system cannot draw in an overlay canvas): a fixed pool of 24 images (round dots, soft
    /// rings, round five-point stars, hearts; never a four-point star), quantities by quality tier (×0.5 Fluide, ×1.25
    /// Magnifique), no instantiation after the prefab is built. Reduce Motion plays the calm profile: half the quantity,
    /// 0.8 of the duration, no spin, rings that brighten in place. Passive, own canvas, ticked by its screen.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiBurst : MonoBehaviour
    {
        public const int MaxImages = 24;

        private enum Shape : byte
        {
            Dot,
            Ring,
            Star,
            Heart,
        }

        [SerializeField] private RectTransform _root;
        [SerializeField] private Image[] _images = Array.Empty<Image>();
        [SerializeField] private Sprite _dot;
        [SerializeField] private Sprite _ring;
        [SerializeField] private Sprite _star;
        [SerializeField] private Sprite _heart;

        private readonly Vector2[] _origin = new Vector2[MaxImages];
        private readonly Vector2[] _direction = new Vector2[MaxImages];
        private readonly float[] _distance = new float[MaxImages];
        private readonly float[] _start = new float[MaxImages];
        private readonly float[] _life = new float[MaxImages];
        private readonly float[] _size = new float[MaxImages];
        private readonly float[] _spin = new float[MaxImages];
        private readonly float[] _age = new float[MaxImages];
        private readonly Shape[] _shape = new Shape[MaxImages];
        private readonly Color[] _color = new Color[MaxImages];
        private readonly bool[] _active = new bool[MaxImages];
        private int _activeCount;
        private uint _seed = 0x9E3779B9u;

        public bool ReduceMotion { get; set; }
        public bool Playing => _activeCount > 0;
        public int ActiveImages => _activeCount;

        /// <summary>Plays <paramref name="kind"/> centered on <paramref name="at"/> (any rect of the same screen).</summary>
        public void Play(UiBurstKind kind, RectTransform at, Color main, Color accent, bool extraRing = false, float delay = 0f)
        {
            if (at == null || _root == null)
                return;
            var world = at.TransformPoint(at.rect.center);
            PlayLocal(kind, _root.InverseTransformPoint(world), main, accent, extraRing, delay);
        }

        /// <summary>Plays at a point of the burst's own space (relative to its pivot).</summary>
        public void PlayLocal(UiBurstKind kind, Vector2 local, Color main, Color accent, bool extraRing = false, float delay = 0f)
        {
            if (_root == null || _images == null || _images.Length == 0)
                return;
            float tier = PresentationQuality.Tier == QualityTier.Low ? 0.5f : PresentationQuality.Tier == QualityTier.High ? 1.25f : 1f;
            float calm = ReduceMotion ? 0.5f : 1f;
            float time = ReduceMotion ? 0.8f : 1f;
            switch (kind)
            {
                case UiBurstKind.Gift:
                {
                    Ring(local, 300f, delay, 0.8f * time, main);
                    if (extraRing)
                        Ring(local, 380f, delay + 0.06f, 0.8f * time, accent);
                    Radial(Shape.Dot, local, Count(12, tier, calm), 150f, 250f, 18f, 30f, delay, 0.8f * time, main, Color.white);
                    break;
                }
                case UiBurstKind.Stamp:
                {
                    Ring(local, 150f, delay, 0.4f * time, main);
                    Radial(Shape.Dot, local, Count(4, tier, calm), 60f, 90f, 12f, 16f, delay, 0.4f * time, main, main);
                    break;
                }
                case UiBurstKind.StampBig:
                {
                    Ring(local, 170f, delay, 0.5f * time, main);
                    Radial(Shape.Dot, local, Count(4, tier, calm), 60f, 90f, 12f, 16f, delay, 0.45f * time, main, main);
                    Radial(Shape.Star, local, Count(12, tier, calm), 110f, 200f, 26f, 38f, delay + 0.05f, 0.9f * time, accent, Color.white);
                    break;
                }
                case UiBurstKind.Hearts:
                {
                    Ring(local, 200f, delay, 0.6f * time, main);
                    Radial(Shape.Heart, local, Count(4, tier, calm), 60f, 130f, 34f, 44f, delay, 0.9f * time, main, accent);
                    Radial(Shape.Dot, local, Count(4, tier, calm), 70f, 140f, 14f, 22f, delay + 0.04f, 0.7f * time, Color.white, accent);
                    break;
                }
                case UiBurstKind.Claim:
                {
                    Ring(local, 220f, delay, 0.6f * time, main);
                    Radial(Shape.Dot, local, Count(6, tier, calm), 90f, 160f, 14f, 24f, delay, 0.6f * time, main, accent);
                    break;
                }
            }
            if (_activeCount > 0 && !_root.gameObject.activeSelf)
                _root.gameObject.SetActive(true);
        }

        public void Tick(float deltaTime)
        {
            if (_activeCount == 0)
                return;
            for (int i = 0; i < _images.Length && i < MaxImages; i++)
            {
                if (!_active[i])
                    continue;
                _age[i] += deltaTime;
                float t = _age[i] - _start[i];
                var image = _images[i];
                var rect = image.rectTransform;
                if (t < 0f)
                    continue;
                float k = t / _life[i];
                if (k >= 1f)
                {
                    Release(i);
                    continue;
                }
                if (!image.enabled)
                    image.enabled = true;
                float alpha;
                float scale;
                Vector2 position = _origin[i];
                switch (_shape[i])
                {
                    case Shape.Ring:
                        if (ReduceMotion)
                        {
                            // Brightens in place instead of spreading.
                            scale = 1f;
                            alpha = 0.75f * Mathf.Sin(k * Mathf.PI);
                        }
                        else
                        {
                            scale = Mathf.Lerp(0.35f, 1.15f, UiCurves.EaseOut(k));
                            alpha = 0.85f * (1f - k) * (1f - k);
                        }
                        break;
                    case Shape.Heart:
                    {
                        float rise = UiCurves.EaseOut(k);
                        position += new Vector2(_direction[i].x * _distance[i] * 0.45f * rise + Mathf.Sin(k * 6.2832f) * 8f,
                            _distance[i] * rise);
                        scale = ReduceMotion ? 1f : Mathf.Min(1f, k / 0.18f) * Mathf.Lerp(1f, 0.8f, k);
                        alpha = k < 0.6f ? Mathf.Min(1f, k / 0.1f) : 1f - (k - 0.6f) / 0.4f;
                        break;
                    }
                    default:
                    {
                        position += _direction[i] * (_distance[i] * UiCurves.EaseOut(k));
                        scale = Mathf.Lerp(1f, 0.45f, k);
                        alpha = k < 0.12f ? k / 0.12f : k < 0.55f ? 1f : 1f - (k - 0.55f) / 0.45f;
                        break;
                    }
                }
                rect.anchoredPosition = position;
                float size = _size[i] * scale;
                rect.sizeDelta = new Vector2(size, size);
                if (_shape[i] == Shape.Star && !ReduceMotion)
                    rect.localEulerAngles = new Vector3(0f, 0f, _spin[i] * t);
                var color = _color[i];
                color.a *= Mathf.Clamp01(alpha);
                image.color = color;
            }
            if (_activeCount == 0 && _root != null && _root.gameObject.activeSelf)
                _root.gameObject.SetActive(false);
        }

        /// <summary>Every image off at once (the screen closed).</summary>
        public void Clear()
        {
            for (int i = 0; i < _images.Length && i < MaxImages; i++)
            {
                if (_active[i])
                    Release(i);
            }
            if (_root != null && _root.gameObject.activeSelf)
                _root.gameObject.SetActive(false);
        }

        private static int Count(int balanced, float tier, float calm) => Mathf.Max(1, Mathf.RoundToInt(balanced * tier * calm));

        private void Ring(Vector2 origin, float size, float delay, float life, Color color) =>
            Spawn(Shape.Ring, origin, Vector2.zero, 0f, size, delay, life, color, 0f);

        private void Radial(Shape shape, Vector2 origin, int count, float minDistance, float maxDistance, float minSize, float maxSize,
            float delay, float life, Color color, Color mix)
        {
            float turn = Next() * 6.2832f;
            for (int n = 0; n < count; n++)
            {
                // Evenly around the point, a little uneven so it never looks drawn with a compass; a touch more upward.
                float angle = turn + n * 6.2832f / count + (Next() - 0.5f) * 0.5f;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.9f + 0.15f);
                float distance = Mathf.Lerp(minDistance, maxDistance, Next());
                if (ReduceMotion)
                    distance *= 0.4f;
                float size = Mathf.Lerp(minSize, maxSize, Next());
                var tint = Color.Lerp(color, mix, Next() * 0.35f);
                float spin = (Next() - 0.5f) * 140f;
                Spawn(shape, origin, direction, distance, size, delay + n * 0.012f, life * Mathf.Lerp(0.85f, 1f, Next()), tint, spin);
            }
        }

        private void Spawn(Shape shape, Vector2 origin, Vector2 direction, float distance, float size, float delay, float life, Color color,
            float spin)
        {
            int slot = -1;
            for (int i = 0; i < _images.Length && i < MaxImages; i++)
            {
                if (!_active[i])
                {
                    slot = i;
                    break;
                }
            }
            // Doc 05 section 0.4: beyond the pool the image is skipped, without a hitch.
            if (slot < 0)
                return;
            var image = _images[slot];
            image.sprite = shape == Shape.Ring ? _ring : shape == Shape.Star ? _star : shape == Shape.Heart ? _heart : _dot;
            var rect = image.rectTransform;
            rect.anchoredPosition = origin;
            rect.sizeDelta = new Vector2(size, size);
            rect.localEulerAngles = Vector3.zero;
            var hidden = color;
            hidden.a = 0f;
            image.color = hidden;
            image.enabled = delay <= 0f;
            _origin[slot] = origin;
            _direction[slot] = direction;
            _distance[slot] = distance;
            _size[slot] = size;
            _start[slot] = Mathf.Max(0f, delay);
            _life[slot] = Mathf.Max(0.05f, life);
            _spin[slot] = spin;
            _age[slot] = 0f;
            _shape[slot] = shape;
            _color[slot] = color;
            _active[slot] = true;
            _activeCount++;
            if (!image.gameObject.activeSelf)
                image.gameObject.SetActive(true);
        }

        private void Release(int i)
        {
            _active[i] = false;
            _activeCount--;
            if (_images[i].gameObject.activeSelf)
                _images[i].gameObject.SetActive(false);
        }

        /// <summary>A small xorshift: no System.Random, nothing allocated.</summary>
        private float Next()
        {
            _seed ^= _seed << 13;
            _seed ^= _seed >> 17;
            _seed ^= _seed << 5;
            return (_seed & 0xFFFFFF) / 16777216f;
        }

        /// <summary>Prefab builder hook (Editor DailyVisitUiPipeline).</summary>
        public void SetReferences(RectTransform root, Image[] images, Sprite dot, Sprite ring, Sprite star, Sprite heart)
        {
            _root = root;
            _images = images;
            _dot = dot;
            _ring = ring;
            _star = star;
            _heart = heart;
        }
    }
}
