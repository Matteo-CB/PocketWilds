using PocketWilds.Simulation;
using UnityEngine;
using UnityEngine.Events;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// PF_World_WishBubble (T36; doc 06 screen 002: wishes live in the world above the animals; doc 01 sections 2.1 and
    /// A08 to A10; doc 05 Sensations sheet 17). A pooled bubble: UI_Bubble_Wish, its progress ring UI_Bubble_Ring
    /// (filled by angle in SH_WishBubble), the wish's icon (<see cref="WishIcons"/>) and, while more than one step is
    /// left, the count beside it (A08: an egg and « 2 »). Once fulfilled: ICO_Wish_Done, the full green ring and a soft
    /// pulse inviting the tap. <see cref="PlayCompleted"/>: the ring fills (250 ms), the bubble swells to 1.15 and is
    /// gone in 120 ms.
    /// <list type="bullet">
    /// <item>No Update: <see cref="WishBubbleLayer"/> places it (billboard, screen-size scale) and ticks it.</item>
    /// <item>Colors and the ring's fill change only when their value changes; the ring has its own material instance
    /// (made at prewarm), so every bubble stays in the SRP batcher (a property block would take it out).</item>
    /// <item>The count is a TextMeshPro made by WishBubblePipeline: this assembly has no TextMeshPro reference, so the
    /// text goes through <see cref="_countText"/>, a persistent call to the label's text setter (cached strings, no
    /// allocation), and its fade through a property block on its renderer.</item>
    /// <item>Reduce Motion: no pop, no bob, no pulse; the burst is a fade.</item>
    /// </list>
    /// </summary>
    public sealed class WishBubbleView : MonoBehaviour
    {
        /// <summary>Doc 05 sheet 17: the ring fills, then the bubble bursts.</summary>
        public const float RingFillSeconds = 0.25f;
        public const float SwellSeconds = 0.05f;
        public const float VanishSeconds = 0.12f;
        public const float ReducedFadeSeconds = 0.2f;

        private const float PopSeconds = 0.32f;
        private const float FadeInSeconds = 0.16f;
        private const float ReducedFadeInSeconds = 0.25f;
        private const float FadeOutSeconds = 0.15f;
        private const float DoneArrivalSeconds = 0.35f;
        private const float DoneArrivalScale = 1.12f;
        private const float BobPeriod = 2.8f;
        private const float PulsePeriod = 1.4f;
        private const float ProgressSeconds = 0.25f;
        private const int MaxShownCount = 99;

        private static readonly int FillId = Shader.PropertyToID("_Fill");
        private static readonly int FaceColorId = Shader.PropertyToID("_FaceColor");
        private static readonly string[] Numbers = BuildNumbers();

        [Header("Parts (built by WishBubblePipeline)")]
        [SerializeField] private Transform _body;
        [SerializeField] private SpriteRenderer _bubble;
        [SerializeField] private SpriteRenderer _ring;
        [SerializeField] private SpriteRenderer _icon;
        [SerializeField] private Transform _count;
        [SerializeField] private Renderer _countRenderer;
        [Tooltip("Persistent call to the count label's TextMeshPro text setter.")]
        [SerializeField] private UnityEvent<string> _countText = new UnityEvent<string>();

        [Header("Icons by WishSystem value (WishIcons.SpriteName)")]
        [SerializeField] private Sprite[] _icons = new Sprite[0];
        [SerializeField] private Sprite _doneIcon;

        [Header("Look")]
        [SerializeField] private Color _bubbleTint = Color.white;
        [SerializeField] private Color _ringFrom = new Color(0.49f, 0.76f, 0.93f, 1f);
        [SerializeField] private Color _ringTo = new Color(0.47f, 0.72f, 0.38f, 1f);
        [SerializeField] private Color _ringDone = new Color(0.47f, 0.72f, 0.38f, 1f);
        [Tooltip("The icon fits this circle (sprite units at 100 px per unit) alone, and beside the count.")]
        [SerializeField] private float _iconBox = 0.84f;
        [SerializeField] private float _iconBoxWithCount = 0.62f;
        [SerializeField] private Vector2 _iconWithCount = new Vector2(-0.17f, 0.02f);
        [Tooltip("The count's scale for two digits (one digit: 1).")]
        [SerializeField] private float _twoDigitScale = 0.74f;

        private Material _ringMaterial;
        private MaterialPropertyBlock _countBlock;
        private Vector3 _countBaseScale = Vector3.one;
        private bool _prewarmed;

        // State shown.
        private WishSystem _system = (WishSystem)255;
        private bool _done;
        private int _remaining = -1;
        private float _progress;
        private float _shownProgress = -1f;
        private bool _countShown;

        // Motion.
        private bool _wantVisible;
        private float _alpha;
        private float _appliedAlpha = -1f;
        private float _popTime = -1f;
        private bool _popPending;
        private float _doneTime = -1f;
        private float _completeTime = -1f;
        private float _clock;
        private float _phase;
        private float _appliedScale = -1f;
        private float _appliedFill = -1f;
        private float _appliedRadius = -1f;
        private bool _appliedFlip;
        private bool _reduced;
        private bool _burstRaised;

        /// <summary>Current opacity (0: nothing drawn).</summary>
        public float Alpha => _alpha;

        /// <summary>Drawn now (some opacity).</summary>
        public bool IsShowing => _alpha > 0.001f;

        /// <summary>The completion is playing.</summary>
        public bool Completing => _completeTime >= 0f;

        /// <summary>The completion reached its burst this tick (raised once; the layer tells its listener).</summary>
        public bool BurstThisTick { get; private set; }

        /// <summary>The completion is over: the bubble is gone.</summary>
        public bool CompletionFinished { get; private set; }

        public bool Done => _done;
        public WishSystem System => _system;
        public int Remaining => _remaining;

        /// <summary>Sprite shown as the icon (tests, captures).</summary>
        public Sprite IconSprite => _icon != null ? _icon.sprite : null;

        /// <summary>Ring fill shown, 0 to 1.</summary>
        public float RingFill => Mathf.Max(0f, _shownProgress);

        public bool CountVisible => _countShown;

        /// <summary>The bubble's scale now relative to its rest size (pop, pulse, burst).</summary>
        public float BodyScale => _appliedScale < 0f ? 1f : _appliedScale;

        /// <summary>Center of the circle as drawn (bob included).</summary>
        public Vector3 DrawnCenter => _body != null ? _body.position : transform.position;

        /// <summary>Radius of the circle as drawn (outline included; pop, pulse or burst scale included).</summary>
        public float DrawnRadius => Mathf.Max(0f, _appliedRadius) * BodyScale;

        /// <summary>The trailing dots point to the right (mirrored).</summary>
        public bool DrawnFlip => _appliedFlip;

        /// <summary>Editor hook (WishBubblePipeline).</summary>
        public void SetReferences(Transform body, SpriteRenderer bubble, SpriteRenderer ring, SpriteRenderer icon, Transform count,
            Renderer countRenderer, Sprite[] icons, Sprite doneIcon, Color bubbleTint, Color ringFrom, Color ringTo, Color ringDone)
        {
            _body = body;
            _bubble = bubble;
            _ring = ring;
            _icon = icon;
            _count = count;
            _countRenderer = countRenderer;
            _icons = icons;
            _doneIcon = doneIcon;
            _bubbleTint = bubbleTint;
            _ringFrom = ringFrom;
            _ringTo = ringTo;
            _ringDone = ringDone;
        }

        /// <summary>Editor hook: the count label's text setter (a persistent call, see the class summary).</summary>
        public UnityEvent<string> CountTextEvent => _countText;

        /// <summary>
        /// Scene entry (T28 lesson: nothing is made during play): the ring's material instance, the count's property block
        /// and its call list are made now; everything starts hidden.
        /// </summary>
        public void Prewarm(int index)
        {
            if (_prewarmed)
                return;
            _prewarmed = true;
            _phase = index * 0.618f * BobPeriod;
            if (_ring != null)
            {
                _ringMaterial = _ring.material;
                _ringMaterial.SetFloat(FillId, 0f);
                _appliedFill = 0f;
            }
            _countBlock = new MaterialPropertyBlock();
            if (_count != null)
                _countBaseScale = _count.localScale;
            // The first Invoke builds the persistent call list (reflection, allocation): done here, never in play.
            _countText.Invoke(Numbers[2]);
            _countText.Invoke(Numbers[1]);
            ApplyAlpha(0f);
            SetRenderers(false);
        }

        // ------------------------------------------------------------------------------------------- state

        /// <summary>A new wish on this bubble: everything reset, hidden until placed (then it pops in).</summary>
        public void Begin(in WishBubbleState state, bool reduceMotion)
        {
            _reduced = reduceMotion;
            _wantVisible = false;
            _alpha = 0f;
            ApplyAlpha(0f);
            SetRenderers(false);
            _popTime = -1f;
            _popPending = true;
            _doneTime = -1f;
            _completeTime = -1f;
            _burstRaised = false;
            BurstThisTick = false;
            CompletionFinished = false;
            _system = (WishSystem)255;
            _remaining = -1;
            _done = false;
            _shownProgress = -1f;
            _appliedScale = -1f;
            Apply(state, false);
            _shownProgress = _progress;
            ApplyRing();
        }

        /// <summary>The wish changed (progress, done, a swap): only what changed is touched.</summary>
        public void SetState(in WishBubbleState state) => Apply(state, IsShowing && !_reduced);

        private void Apply(in WishBubbleState state, bool animate)
        {
            bool done = state.Done;
            _progress = done ? 1f : state.Target > 0 ? Mathf.Clamp01((float)state.Progress / state.Target) : 0f;
            if (!animate)
                _shownProgress = _progress;
            if (done && !_done && animate)
                _doneTime = _clock;
            bool relayout = false;
            if (done != _done || state.System != _system)
            {
                _done = done;
                _system = state.System;
                if (_icon != null)
                    _icon.sprite = done ? _doneIcon : IconFor(state.System);
                relayout = true;
            }
            int remaining = done ? 0 : Mathf.Clamp(state.Target - state.Progress, 0, MaxShownCount);
            bool showCount = !done && state.Target > 1 && remaining > 0;
            if (remaining != _remaining || showCount != _countShown)
            {
                if (showCount && remaining != _remaining)
                {
                    _countText.Invoke(Numbers[remaining]);
                    if (_count != null)
                        _count.localScale = remaining >= 10 ? _countBaseScale * _twoDigitScale : _countBaseScale;
                }
                _remaining = remaining;
                _countShown = showCount;
                relayout = true;
                if (_countRenderer != null)
                    _countRenderer.enabled = showCount && IsShowing;
            }
            if (relayout)
                LayoutIcon();
            if (!animate)
                ApplyRing();
            ApplyRingColor();
        }

        private Sprite IconFor(WishSystem system)
        {
            int i = (int)system;
            if (_icons != null && i >= 0 && i < _icons.Length && _icons[i] != null)
                return _icons[i];
            return _icons != null && _icons.Length > 0 ? _icons[0] : null;
        }

        private void LayoutIcon()
        {
            if (_icon == null)
                return;
            var sprite = _icon.sprite;
            float box = _countShown ? _iconBoxWithCount : _iconBox;
            float size = 1f;
            if (sprite != null)
            {
                var rect = sprite.rect;
                size = Mathf.Max(rect.width, rect.height) / Mathf.Max(1f, sprite.pixelsPerUnit);
            }
            float scale = box / Mathf.Max(0.01f, size);
            var t = _icon.transform;
            t.localScale = new Vector3(scale, scale, 1f);
            t.localPosition = _countShown ? new Vector3(_iconWithCount.x, _iconWithCount.y, 0f) : Vector3.zero;
        }

        // ------------------------------------------------------------------------------------------- placement

        /// <summary>
        /// The layer's per-frame placement: center, billboard rotation, world radius (screen-size clamp) and the side the
        /// trailing dots point to. Transforms are written; the scale only when it changed.
        /// </summary>
        public void Place(Vector3 center, Quaternion rotation, float radius, bool flip)
        {
            transform.SetPositionAndRotation(center, rotation);
            if (Mathf.Abs(radius - _appliedRadius) > 1e-5f)
            {
                _appliedRadius = radius;
                float s = radius / WishBubblePlacement.SpriteRadius;
                transform.localScale = new Vector3(s, s, s);
            }
            if (flip != _appliedFlip && _bubble != null)
            {
                // flipX mirrors the sprite around its centered pivot; a negative transform scale allocated (measured).
                _appliedFlip = flip;
                _bubble.flipX = flip;
            }
        }

        /// <summary>Shown or not (the layer found a place, or not, or a screen covers the world): a pop for a new wish, else a fade.</summary>
        public void SetVisible(bool visible, bool reduceMotion)
        {
            _reduced = reduceMotion;
            if (visible == _wantVisible)
                return;
            _wantVisible = visible;
            // A new wish pops in once; coming back (after a screen, a hatch, a crowd) is a fade.
            if (visible && _popPending && _alpha <= 0.001f && !Completing)
            {
                _popPending = false;
                if (!_reduced)
                    _popTime = _clock;
            }
        }

        /// <summary>Gone at once (its place was entered between two passes; the rule never waits for a fade).</summary>
        public void HideNow()
        {
            _wantVisible = false;
            _alpha = 0f;
            ApplyAlpha(0f);
            SetRenderers(false);
        }

        /// <summary>Back in the pool.</summary>
        public void Release()
        {
            HideNow();
            _completeTime = -1f;
            CompletionFinished = false;
            BurstThisTick = false;
        }

        // ------------------------------------------------------------------------------------------- completion

        /// <summary>Doc 05 sheet 17: the ring fills (250 ms), the bubble swells to 1.15 and is gone in 120 ms.</summary>
        public void PlayCompleted(bool reduceMotion)
        {
            _reduced = reduceMotion;
            _completeTime = _clock;
            _burstRaised = false;
            CompletionFinished = false;
            _progress = 1f;
            if (!_done)
            {
                _done = true;
                if (_icon != null)
                    _icon.sprite = _doneIcon;
                _countShown = false;
                LayoutIcon();
                if (_countRenderer != null)
                    _countRenderer.enabled = false;
            }
            ApplyRingColor();
        }

        // ------------------------------------------------------------------------------------------- tick

        /// <summary>One frame of motion (layer's tick). Allocation-free.</summary>
        public void Tick(float deltaTime)
        {
            _clock += deltaTime;
            BurstThisTick = false;
            if (!_wantVisible && _alpha <= 0f && !Completing)
            {
                // Waiting for a place, or hidden: nothing drawn, nothing to move.
                _shownProgress = _progress;
                return;
            }
            float scale = 1f;
            float bob = 0f;
            float alphaTarget = _wantVisible ? 1f : 0f;

            if (Completing)
            {
                float t = _clock - _completeTime;
                _shownProgress = Mathf.MoveTowards(_shownProgress, 1f, deltaTime / RingFillSeconds);
                if (t < RingFillSeconds && _alpha > 0f)
                    _alpha = Mathf.Min(1f, _alpha + deltaTime / FadeInSeconds);
                if (t >= RingFillSeconds && !_burstRaised)
                {
                    _burstRaised = true;
                    BurstThisTick = true;
                }
                if (_reduced)
                {
                    float u = Mathf.Clamp01((t - RingFillSeconds) / ReducedFadeSeconds);
                    alphaTarget = Mathf.Min(_alpha, 1f - u);
                    _alpha = alphaTarget;
                    if (u >= 1f)
                        Finish();
                }
                else
                {
                    float burst = t - RingFillSeconds;
                    if (burst < 0f)
                        scale = 1f;
                    else if (burst < SwellSeconds)
                        scale = Mathf.Lerp(1f, WishBubblePlacement.BurstScale, EaseOut(burst / SwellSeconds));
                    else
                    {
                        float u = Mathf.Clamp01((burst - SwellSeconds) / VanishSeconds);
                        scale = Mathf.Lerp(WishBubblePlacement.BurstScale, 0.8f, u * u);
                        _alpha = Mathf.Min(_alpha, 1f - u);
                        if (u >= 1f)
                            Finish();
                    }
                }
            }
            else
            {
                if (_alpha < alphaTarget)
                    _alpha = Mathf.Min(alphaTarget, _alpha + deltaTime / (_reduced ? ReducedFadeInSeconds : FadeInSeconds));
                else if (_alpha > alphaTarget)
                    _alpha = Mathf.Max(alphaTarget, _alpha - deltaTime / FadeOutSeconds);
                if (_shownProgress != _progress)
                    _shownProgress = Mathf.MoveTowards(_shownProgress, _progress, deltaTime / ProgressSeconds);
                if (!_reduced)
                {
                    if (_popTime >= 0f)
                    {
                        float u = (_clock - _popTime) / PopSeconds;
                        if (u >= 1f)
                            _popTime = -1f;
                        else
                            scale = PopCurve(u);
                    }
                    else if (_doneTime >= 0f)
                    {
                        float u = (_clock - _doneTime) / DoneArrivalSeconds;
                        if (u >= 1f)
                            _doneTime = -1f;
                        else
                            scale = 1f + (DoneArrivalScale - 1f) * Mathf.Sin(u * Mathf.PI);
                    }
                    else if (_done)
                    {
                        // A soft pulse invites the tap.
                        scale = 1f + (WishBubblePlacement.PulseScale - 1f) * (0.5f - 0.5f * Mathf.Cos((_clock + _phase) * (2f * Mathf.PI / PulsePeriod)));
                    }
                    bob = Mathf.Sin((_clock + _phase) * (2f * Mathf.PI / BobPeriod)) * WishBubblePlacement.BobAmplitude * WishBubblePlacement.SpriteRadius;
                }
            }

            ApplyRing();
            ApplyAlpha(_alpha);
            if (_body != null)
            {
                if (Mathf.Abs(scale - _appliedScale) > 1e-4f)
                {
                    _appliedScale = scale;
                    _body.localScale = new Vector3(scale, scale, 1f);
                }
                _body.localPosition = new Vector3(0f, bob, 0f);
            }
        }

        private void Finish()
        {
            CompletionFinished = true;
            _completeTime = -1f;
            _wantVisible = false;
            _alpha = 0f;
            ApplyAlpha(0f);
            SetRenderers(false);
        }

        private static float EaseOut(float u) => 1f - (1f - u) * (1f - u);

        /// <summary>Pop-in: 0.55 to 1.08 (the reserved overshoot) at 60 %, settling on 1.</summary>
        private static float PopCurve(float u)
        {
            const float peak = 0.6f;
            if (u < peak)
                return Mathf.Lerp(0.55f, WishBubblePlacement.PopScale, EaseOut(u / peak));
            float v = (u - peak) / (1f - peak);
            return Mathf.Lerp(WishBubblePlacement.PopScale, 1f, v * v * (3f - 2f * v));
        }

        // ------------------------------------------------------------------------------------------- drawing

        private void ApplyRing()
        {
            if (_ring == null || _ringMaterial == null)
                return;
            float fill = Mathf.Clamp01(_shownProgress);
            if (fill != _appliedFill)
            {
                _appliedFill = fill;
                _ringMaterial.SetFloat(FillId, fill);
            }
            bool show = fill > 0.001f && IsShowing;
            if (_ring.enabled != show)
                _ring.enabled = show;
            ApplyRingColor();
        }

        private void ApplyRingColor()
        {
            if (_ring == null)
                return;
            var color = _done ? _ringDone : Color.Lerp(_ringFrom, _ringTo, Mathf.Clamp01(_shownProgress));
            color.a *= Mathf.Max(0f, _alpha);
            if (_ring.color != color)
                _ring.color = color;
        }

        private void ApplyAlpha(float alpha)
        {
            if (alpha == _appliedAlpha)
                return;
            bool wasShowing = _appliedAlpha > 0.001f;
            _appliedAlpha = alpha;
            bool showing = alpha > 0.001f;
            if (showing != wasShowing)
                SetRenderers(showing);
            if (_bubble != null)
            {
                var c = _bubbleTint;
                c.a *= alpha;
                _bubble.color = c;
            }
            if (_icon != null)
                _icon.color = new Color(1f, 1f, 1f, alpha);
            ApplyRingColor();
            if (_countRenderer != null && _countBlock != null)
            {
                _countRenderer.GetPropertyBlock(_countBlock);
                _countBlock.SetColor(FaceColorId, new Color(1f, 1f, 1f, alpha));
                _countRenderer.SetPropertyBlock(_countBlock);
            }
        }

        private void SetRenderers(bool on)
        {
            if (_bubble != null)
                _bubble.enabled = on;
            if (_icon != null)
                _icon.enabled = on;
            if (_ring != null)
                _ring.enabled = on && _shownProgress > 0.001f;
            if (_countRenderer != null)
                _countRenderer.enabled = on && _countShown;
        }

        private void OnDestroy()
        {
            if (_ringMaterial != null)
                Destroy(_ringMaterial);
        }

        private static string[] BuildNumbers()
        {
            var numbers = new string[MaxShownCount + 1];
            for (int i = 0; i < numbers.Length; i++)
                numbers[i] = i.ToString(global::System.Globalization.CultureInfo.InvariantCulture);
            return numbers;
        }
    }
}
