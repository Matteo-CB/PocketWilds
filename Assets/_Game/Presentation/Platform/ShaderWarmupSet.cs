using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T34 shader preparation (doc 07 « Beauté maximale », section 5): what the loading screen warms so the first
    /// display of anything never stutters. Built by the editor from traces of the Windows benchmark player
    /// (Pocket Wilds/Build/Import Graphics State Trace).
    /// </summary>
    [CreateAssetMenu(menuName = "Pocket Wilds/Shader Warmup Set", fileName = "DEF_ShaderWarmup")]
    public sealed class ShaderWarmupSet : ScriptableObject
    {
        [Tooltip("Unity 6 graphics state collections, one per platform and graphics API (traced in a player run of every " +
                 "benchmark scenario and quality step). The one matching the device is warmed during the loading screen.")]
        public GraphicsStateCollection[] collections = Array.Empty<GraphicsStateCollection>();

        [Tooltip("Platforms without a trace yet (Android until a test phone traces it): the traced shader variants. " +
                 "They compile the shaders, the pipeline states still build on first use.")]
        public ShaderVariantCollection variants;
    }
}
