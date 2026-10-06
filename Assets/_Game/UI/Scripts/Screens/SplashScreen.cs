using UnityEngine;

namespace PocketWilds.UI
{
    /// <summary>
    /// PF_UI_Splash (T34A, doc 06 SCREEN 001): soft sky-to-meadow gradient, the name in Pocket Plush (placeholder
    /// lockup until the final name, live text, never baked) and three paw dots that hop in turn. No fake progress
    /// bar: shader preparation and loading happen behind it. Lives across the bootstrap scene unload, fades out once
    /// the refuge is ready, then destroys itself. The only Update here, during boot, before the game loop exists.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SplashScreen : MonoBehaviour
    {
        private const float FadeSeconds = 0.35f;
        private const float HopPeriod = 1.05f;

        [SerializeField] private CanvasGroup _group;
        [SerializeField] private RectTransform[] _paws;
        [SerializeField] private RectTransform _logo;

        private float _time;
        private float _fade = -1f;
        private float[] _pawHome;

        public bool ReduceMotion { get; set; }
        public bool FadingOut => _fade >= 0f;

        /// <summary>The refuge is on screen: fade out and go.</summary>
        public void Dismiss()
        {
            if (_fade < 0f)
                _fade = 0f;
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            _pawHome = new float[_paws != null ? _paws.Length : 0];
            for (int i = 0; i < _pawHome.Length; i++)
                _pawHome[i] = _paws[i].anchoredPosition.y;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _time += dt;
            if (!ReduceMotion && _paws != null)
            {
                for (int i = 0; i < _paws.Length; i++)
                {
                    float phase = Mathf.Repeat(_time / HopPeriod - i * 0.18f, 1f);
                    float hop = phase < 0.35f ? Mathf.Sin(phase / 0.35f * Mathf.PI) : 0f;
                    var p = _paws[i].anchoredPosition;
                    _paws[i].anchoredPosition = new Vector2(p.x, _pawHome[i] + hop * 26f);
                    float squash = 1f + 0.08f * hop;
                    _paws[i].localScale = new Vector3(2f - squash, squash, 1f);
                }
            }
            if (_logo != null && !ReduceMotion)
            {
                float breathe = 1f + 0.012f * Mathf.Sin(_time * 2.1f);
                _logo.localScale = new Vector3(breathe, breathe, 1f);
            }
            if (_fade >= 0f)
            {
                _fade += dt;
                _group.alpha = Mathf.Clamp01(1f - _fade / FadeSeconds);
                if (_fade >= FadeSeconds)
                    Destroy(gameObject);
            }
        }

        /// <summary>Prefab builder hook (Editor FeedbackUiPipeline).</summary>
        public void SetReferences(CanvasGroup group, RectTransform[] paws, RectTransform logo)
        {
            _group = group;
            _paws = paws;
            _logo = logo;
        }
    }
}
