using System;
using System.Collections.Generic;
using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Content
{
    /// <summary>
    /// T49B authoring asset of a decoration (DEF_Decor_GL_DaisyTuft), made and measured by the Decoration Wizard (ED-002).
    /// Runtime data: <see cref="DecorationDefinition"/>. The measured fields are written by the wizard from the prefab's LOD0
    /// (CollisionProxies) and never by hand.
    /// </summary>
    [CreateAssetMenu(menuName = "Pocket Wilds/Decoration Definition", fileName = "DEF_Decor_New")]
    public sealed class DecorationDefinitionAsset : ScriptableObject
    {
        [Serializable]
        public struct Band
        {
            public Vector3 a;
            public Vector3 b;
            public float radius;
            public float bottom;
            public float top;
        }

        [Header("Identity")]
        public string sourceId = "decor.new";
        public string displayKey = "decor.new.name";
        public DecorationCategory category = DecorationCategory.Nature;

        [Tooltip("Doc id (E-DEC-GL-01), for the reports.")]
        public string docId = string.Empty;

        [Tooltip("Biome of a progression decoration (biome.grassland); empty for commons, events and monuments.")]
        public string biome = string.Empty;

        [Tooltip("Where it comes from (mastery.forest.9, album.season, event.burrows.track.3, shop.common).")]
        public string source = string.Empty;

        [Min(1)] public int maxCopies = 1;

        [Header("Footprint class and interactions")]
        public DecorationFootprintClass footprint = DecorationFootprintClass.S;
        public DecorationFootprintShape shape = DecorationFootprintShape.Circle;
        public DecorationInteraction interactions = DecorationInteraction.None;

        [Tooltip("Top an animal sits or sleeps on (« support »), m; 0 when none. 0.45 m at most for a walking animal.")]
        [Min(0f)] public float topHeight;

        [Tooltip("LOD0 triangle budget from the catalog (XS/S 300, M 800, L 1500, XL 2500 at most).")]
        [Min(0)] public int trisBudget = 300;

        [Header("Assets")]
        [Tooltip("Prefab name, also its Addressables address (PF_Decor_GL_DaisyTuft). Never a direct reference: the content " +
                 "database is in the base build, the prefab loads by address only where it is placed (events stay in their group).")]
        public string prefabAddress = string.Empty;

        [Tooltip("Project path of the prefab, for the wizard and the validators (editor only).")]
        public string prefabPath = string.Empty;

        [Tooltip("Palette row source id (palette.grassland.decor_daisy_tuft).")]
        public string palette = string.Empty;

        [Tooltip("Icon sprite name (ICO_Decor_GL_DaisyTuft).")]
        public string icon = string.Empty;

        [Header("Measured (wizard only)")]
        public List<Band> bands = new List<Band>();

        [Tooltip("Convex outline of the ground band seen from above (x, z), prefab space.")]
        public List<Vector2> groundHull = new List<Vector2>();

        [Min(0f)] public float reach;
        [Min(0f)] public float height;
        [Min(0)] public int measuredTris;

        public DecorationDefinition ToRuntime()
        {
            var runtimeBands = new DecorationBand[bands.Count];
            for (int i = 0; i < bands.Count; i++)
            {
                var b = bands[i];
                runtimeBands[i] = new DecorationBand(b.a.x, b.a.y, b.a.z, b.b.x, b.b.y, b.b.z, b.radius, b.bottom, b.top);
            }
            var hull = new float[groundHull.Count * 2];
            for (int i = 0; i < groundHull.Count; i++)
            {
                hull[i * 2] = groundHull[i].x;
                hull[i * 2 + 1] = groundHull[i].y;
            }
            return new DecorationDefinition(ContentId.FromSource(sourceId), sourceId, displayKey, category, footprint, shape, interactions,
                string.IsNullOrEmpty(biome) ? default : ContentId.FromSource(biome), prefabAddress,
                string.IsNullOrEmpty(palette) ? default : ContentId.FromSource(palette), icon, source, maxCopies,
                runtimeBands, hull, reach, height, topHeight);
        }
    }
}
