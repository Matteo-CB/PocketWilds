using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>
    /// T44A: writes the sticker shaders' second UV channel (SH_UI_StickerPearl, SH_UI_StickerSilhouette): xy the vertex's
    /// place in the Image's rect (0 to 1, whatever the sprite's place in its page atlas), z a manual sheen phase, w the flags
    /// (1: the sheen follows z instead of the clock, the reveal's single pass; 2: no sprite, the generic sticker shape).
    /// The canvas carries TexCoord1. Rebuilt only when the Image's mesh changes (never per frame at rest).
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public sealed class StickerUv : BaseMeshEffect
    {
        [SerializeField] private bool _generic;
        [SerializeField] private bool _manual;
        [SerializeField] private float _phase;

        /// <summary>No sprite: the shaders draw the soft generic sticker shape (a rounded square).</summary>
        public bool Generic
        {
            get => _generic;
            set
            {
                if (_generic == value)
                    return;
                _generic = value;
                Dirty();
            }
        }

        public bool Manual => _manual;

        public float Phase => _phase;

        /// <summary>The sheen at <paramref name="phase"/> (0 to 1 across the sticker) instead of every 4 s.</summary>
        public void SetPhase(bool manual, float phase)
        {
            if (manual == _manual && Mathf.Abs(phase - _phase) < 0.0001f)
                return;
            _manual = manual;
            _phase = phase;
            Dirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive())
                return;
            var rect = ((RectTransform)transform).rect;
            float w = (_manual ? 1f : 0f) + (_generic ? 2f : 0f);
            float iw = rect.width > 0.001f ? 1f / rect.width : 0f;
            float ih = rect.height > 0.001f ? 1f / rect.height : 0f;
            var vertex = new UIVertex();
            int count = vh.currentVertCount;
            for (int i = 0; i < count; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                vertex.uv1 = new Vector4((vertex.position.x - rect.xMin) * iw, (vertex.position.y - rect.yMin) * ih, _phase, w);
                vh.SetUIVertex(vertex, i);
            }
        }

        private void Dirty()
        {
            if (graphic != null)
                graphic.SetVerticesDirty();
        }
    }
}
