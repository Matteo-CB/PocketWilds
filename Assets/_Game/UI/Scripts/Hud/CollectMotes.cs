using PocketWilds.Core;
using PocketWilds.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>
    /// T34A collect feedback (doc 07 H-VFX-008, VFX_CollectLight): Light motes fly from where the Light was
    /// collected into the Light chip, which pops a little as each lands. UI motes on the HUD's overlay canvas
    /// (a particle system cannot draw in an overlay canvas): a fixed pool of 12 images, aggregated (never one per
    /// unit), 8 on Low. Passive, own canvas, ticked by the HUD. Reduce Motion: no flight, one soft pop.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CollectMotes : MonoBehaviour
    {
        public const int MaxMotes = 12;
        public const int LowMotes = 8;
        private const float Flight = 0.62f;
        private const float Stagger = 0.045f;
        private const float PopSeconds = 0.35f;

        [SerializeField] private RectTransform _root;
        [SerializeField] private RectTransform[] _motes;
        [SerializeField] private RectTransform _target;
        [SerializeField] private RectTransform _chip;
        [SerializeField] private Image _ring;

        private readonly Vector2[] _from = new Vector2[MaxMotes];
        private readonly Vector2[] _bend = new Vector2[MaxMotes];
        private float _time = -1f;
        private int _active;
        private float _pop = -1f;

        public bool ReduceMotion { get; set; }
        public bool Playing => _time >= 0f;
        public int ActiveMotes => _active;
        public int Landed { get; private set; }

        public void Play(Vector2 fromScreenPosition)
        {
            if (_root == null || _motes == null)
                return;
            Landed = 0;
            if (ReduceMotion)
            {
                _pop = 0f;
                return;
            }
            var rect = (RectTransform)_root;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, fromScreenPosition, null, out var from);
            PlayFromLocal(from);
        }

        /// <summary>Starts the flight from a point of the motes' own space (relative to its pivot). Captures use it directly.</summary>
        public void PlayFromLocal(Vector2 from)
        {
            if (_root == null || _motes == null)
                return;
            Landed = 0;
            if (ReduceMotion)
            {
                _pop = 0f;
                return;
            }
            _active = Mathf.Min(_motes.Length, PresentationQuality.Tier == QualityTier.Low ? LowMotes : MaxMotes);
            for (int i = 0; i < _motes.Length; i++)
            {
                bool on = i < _active;
                if (_motes[i].gameObject.activeSelf != on)
                    _motes[i].gameObject.SetActive(on);
                if (!on)
                    continue;
                // A small burst ring around the start, then each mote curves its own way to the chip.
                float angle = (i / (float)_active) * Mathf.PI * 2f + 0.4f;
                float radius = 70f + 30f * ((i * 7) % 5) / 4f;
                _from[i] = from + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                _bend[i] = new Vector2(Mathf.Cos(angle) * 220f, 160f + 60f * ((i * 3) % 4));
                _motes[i].anchoredPosition = _from[i];
                _motes[i].localScale = Vector3.zero;
            }
            _time = 0f;
            _root.gameObject.SetActive(true);
        }

        public void Tick(float deltaTime)
        {
            TickPop(deltaTime);
            if (_time < 0f)
                return;
            _time += deltaTime;
            var rect = (RectTransform)_root;
            Vector2 target = ToLocal(rect, _target);
            bool any = false;
            for (int i = 0; i < _active; i++)
            {
                float t = (_time - i * Stagger) / Flight;
                var mote = _motes[i];
                if (t < 0f)
                {
                    any = true;
                    continue;
                }
                if (t >= 1f)
                {
                    if (mote.gameObject.activeSelf)
                    {
                        mote.gameObject.SetActive(false);
                        Landed++;
                        _pop = 0f;
                    }
                    continue;
                }
                any = true;
                // Quadratic curve, slow start then a quick pull into the chip.
                float e = t * t * (1.6f - 0.6f * t);
                var control = Vector2.Lerp(_from[i], target, 0.35f) + _bend[i];
                var a = Vector2.Lerp(_from[i], control, e);
                var b = Vector2.Lerp(control, target, e);
                mote.anchoredPosition = Vector2.Lerp(a, b, e);
                float grow = Mathf.Clamp01(t / 0.15f);
                float s = Mathf.Lerp(0.2f, 1f, grow) * Mathf.Lerp(1f, 0.55f, e);
                mote.localScale = new Vector3(s, s, 1f);
            }
            if (!any)
            {
                _time = -1f;
                _root.gameObject.SetActive(false);
            }
        }

        private void TickPop(float deltaTime)
        {
            if (_pop < 0f || _chip == null)
                return;
            _pop += deltaTime;
            float k = Mathf.Clamp01(1f - _pop / PopSeconds);
            float s = 1f + 0.1f * k * k;
            _chip.localScale = new Vector3(s, s, 1f);
            if (_ring != null)
            {
                float r = Mathf.Lerp(0.6f, 1.5f, 1f - k);
                _ring.rectTransform.localScale = new Vector3(r, r, 1f);
                var color = _ring.color;
                color.a = 0.8f * k;
                _ring.color = color;
            }
            if (k <= 0f)
            {
                _pop = -1f;
                _chip.localScale = Vector3.one;
            }
        }

        private static Vector2 ToLocal(RectTransform space, RectTransform target)
        {
            var world = target.TransformPoint(target.rect.center);
            return space.InverseTransformPoint(world);
        }

        /// <summary>Prefab builder hook (Editor FeedbackUiPipeline).</summary>
        public void SetReferences(RectTransform root, RectTransform[] motes, RectTransform target, RectTransform chip, Image ring)
        {
            _root = root;
            _motes = motes;
            _target = target;
            _chip = chip;
            _ring = ring;
        }
    }
}
