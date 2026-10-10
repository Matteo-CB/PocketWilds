using UnityEngine;
using UnityEngine.Rendering;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T42: a postcard set's light and sky for the one frame it is drawn (the runtime postcard and the editor's destination
    /// card art): the set's sun becomes the main light (the scene's is switched off), the ambient and the sky globals take
    /// the set's values; <see cref="Restore"/> gives everything back in the same frame, so the refuge never sees it. No
    /// allocation.
    /// </summary>
    public sealed class PostcardLighting
    {
        private static readonly int SkyBlendId = Shader.PropertyToID("_PW_SkyBlend");
        private static readonly int SkyTopId = Shader.PropertyToID("_PW_SkyTop");
        private static readonly int SkyHorizonId = Shader.PropertyToID("_PW_SkyHorizon");
        private static readonly int SkyBottomId = Shader.PropertyToID("_PW_SkyBottom");
        private static readonly int SunTintId = Shader.PropertyToID("_PW_SunTint");
        private static readonly int SunDirectionId = Shader.PropertyToID("_PW_SunDirection");
        private static readonly int CloudLightId = Shader.PropertyToID("_PW_CloudLight");
        private static readonly int CloudShadeId = Shader.PropertyToID("_PW_CloudShade");
        private static readonly int SkyParamsId = Shader.PropertyToID("_PW_SkyParams");

        private bool _applied;
        private Light _savedSun;
        private bool _savedSunEnabled;
        private AmbientMode _savedAmbientMode;
        private Color _savedAmbientTop, _savedAmbientEquator, _savedAmbientBottom;
        private Color _savedSkyTop, _savedSkyHorizon, _savedSkyBottom, _savedSunTint, _savedCloudLight, _savedCloudShade;
        private Vector4 _savedSunDirection, _savedSkyParams;
        private float _savedSkyBlend;

        public void Apply(PostcardSetData data)
        {
            if (_applied || data == null)
                return;
            _applied = true;
            _savedSun = RenderSettings.sun;
            _savedSunEnabled = _savedSun != null && _savedSun.enabled;
            _savedAmbientMode = RenderSettings.ambientMode;
            _savedAmbientTop = RenderSettings.ambientSkyColor;
            _savedAmbientEquator = RenderSettings.ambientEquatorColor;
            _savedAmbientBottom = RenderSettings.ambientGroundColor;
            _savedSkyBlend = Shader.GetGlobalFloat(SkyBlendId);
            _savedSkyTop = Shader.GetGlobalColor(SkyTopId);
            _savedSkyHorizon = Shader.GetGlobalColor(SkyHorizonId);
            _savedSkyBottom = Shader.GetGlobalColor(SkyBottomId);
            _savedSunTint = Shader.GetGlobalColor(SunTintId);
            _savedSunDirection = Shader.GetGlobalVector(SunDirectionId);
            _savedCloudLight = Shader.GetGlobalColor(CloudLightId);
            _savedCloudShade = Shader.GetGlobalColor(CloudShadeId);
            _savedSkyParams = Shader.GetGlobalVector(SkyParamsId);

            var light = data.light;
            if (light != null)
            {
                light.enabled = true;
                RenderSettings.sun = light;
            }
            if (_savedSun != null && _savedSun != light)
                _savedSun.enabled = false;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = data.ambientTop;
            RenderSettings.ambientEquatorColor = data.ambientEquator;
            RenderSettings.ambientGroundColor = data.ambientBottom;
            Shader.SetGlobalFloat(SkyBlendId, 1f);
            Shader.SetGlobalColor(SkyTopId, data.skyTop.linear);
            Shader.SetGlobalColor(SkyHorizonId, data.skyHorizon.linear);
            Shader.SetGlobalColor(SkyBottomId, data.skyBottom.linear);
            Shader.SetGlobalColor(SunTintId, data.sunGlowTint.linear);
            Shader.SetGlobalColor(CloudLightId, data.cloudLight.linear);
            Shader.SetGlobalColor(CloudShadeId, data.cloudShade.linear);
            Shader.SetGlobalVector(SkyParamsId, new Vector4(data.cloudCoverage, data.sunGlow, 0f, 0f));
            var toSun = light != null ? -light.transform.forward : new Vector3(0.3f, 0.7f, -0.6f);
            Shader.SetGlobalVector(SunDirectionId, new Vector4(toSun.x, toSun.y, toSun.z, 0f));
        }

        public void Restore(PostcardSetData data)
        {
            if (!_applied)
                return;
            _applied = false;
            if (data != null && data.light != null)
                data.light.enabled = false;
            if (_savedSun != null)
                _savedSun.enabled = _savedSunEnabled;
            RenderSettings.sun = _savedSun;
            RenderSettings.ambientMode = _savedAmbientMode;
            RenderSettings.ambientSkyColor = _savedAmbientTop;
            RenderSettings.ambientEquatorColor = _savedAmbientEquator;
            RenderSettings.ambientGroundColor = _savedAmbientBottom;
            Shader.SetGlobalFloat(SkyBlendId, _savedSkyBlend);
            Shader.SetGlobalColor(SkyTopId, _savedSkyTop);
            Shader.SetGlobalColor(SkyHorizonId, _savedSkyHorizon);
            Shader.SetGlobalColor(SkyBottomId, _savedSkyBottom);
            Shader.SetGlobalColor(SunTintId, _savedSunTint);
            Shader.SetGlobalVector(SunDirectionId, _savedSunDirection);
            Shader.SetGlobalColor(CloudLightId, _savedCloudLight);
            Shader.SetGlobalColor(CloudShadeId, _savedCloudShade);
            Shader.SetGlobalVector(SkyParamsId, _savedSkyParams);
            _savedSun = null;
        }
    }
}
