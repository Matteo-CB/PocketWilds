using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T39: local gusts of wind on the foliage (doc 01 S-ACT-002, H-VFX-ACT-002-03 « herbe qui ondule : paramètre de vent
    /// local du shader sur la cachette, sans particule »). Up to <see cref="Slots"/> discs in world space where the foliage
    /// and grass sway harder: a hint of Hide and Seek, a decoy in the wind. Chunk props are merged meshes, so the wind is
    /// local by position (shader globals), never by renderer. No allocation: the arrays are preallocated.
    /// </summary>
    public static class FoliageGusts
    {
        public const int Slots = 4;

        private static readonly int GustsId = Shader.PropertyToID("_PW_Gusts");
        private static readonly int GustStrengthId = Shader.PropertyToID("_PW_GustStrength");
        private static readonly Vector4[] Gusts = new Vector4[Slots];
        private static Vector4 _strength;

        /// <summary>
        /// A gust on a disc of <paramref name="radius"/> meters around a world point; <paramref name="strength"/> 0 (calm) to
        /// 1 (a clear ripple, about three times the ambient sway). The caller animates the strength (rise and fall).
        /// </summary>
        public static void Set(int slot, Vector3 worldPosition, float radius, float strength)
        {
            if ((uint)slot >= Slots)
                return;
            Gusts[slot] = new Vector4(worldPosition.x, worldPosition.y, worldPosition.z, Mathf.Max(0.01f, radius));
            _strength[slot] = Mathf.Clamp01(strength);
            Shader.SetGlobalVectorArray(GustsId, Gusts);
            Shader.SetGlobalVector(GustStrengthId, _strength);
        }

        public static void Clear(int slot)
        {
            if ((uint)slot >= Slots)
                return;
            _strength[slot] = 0f;
            Shader.SetGlobalVector(GustStrengthId, _strength);
        }

        public static void ClearAll()
        {
            _strength = Vector4.zero;
            Shader.SetGlobalVectorArray(GustsId, Gusts);
            Shader.SetGlobalVector(GustStrengthId, _strength);
        }

        public static float Strength(int slot) => (uint)slot < Slots ? _strength[slot] : 0f;
    }
}
