using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// Everything a view needs to show one animal, prepared from data. The view never reads
    /// PlayerState and never keeps anything after release.
    /// </summary>
    public struct AnimalVisualSnapshot
    {
        public uint InstanceId;
        public ContentId Species;
        public Vector3 Position;
        public float Yaw;
        public float Scale;
        public Color PrimaryColor;
        public Color SecondaryColor;
        public Color BellyColor;
        public Color DetailColor;
        public Color EyeColor;
        public float PatternIndex;
        public float PatternStrength;
        public float RareEffectIntensity;
    }

    /// <summary>Property ids of SH_AnimalMaster, resolved once.</summary>
    public static class AnimalShaderIds
    {
        public static readonly int PrimaryColor = Shader.PropertyToID("_PrimaryColor");
        public static readonly int SecondaryColor = Shader.PropertyToID("_SecondaryColor");
        public static readonly int BellyColor = Shader.PropertyToID("_BellyColor");
        public static readonly int DetailColor = Shader.PropertyToID("_DetailColor");
        public static readonly int EyeColor = Shader.PropertyToID("_EyeColor");
        public static readonly int PatternIndex = Shader.PropertyToID("_PatternIndex");
        public static readonly int PatternStrength = Shader.PropertyToID("_PatternStrength");
        public static readonly int RareEffectIntensity = Shader.PropertyToID("_RareEffectIntensity");
    }
}
