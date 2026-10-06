using System;
using PocketWilds.Core;
using UnityEditor;
using UnityEngine;

namespace PocketWilds.Editor.Capture
{
    public enum CaptureShot
    {
        /// <summary>The refuge as the player sees it (default camera, start chunk, egg and animals).</summary>
        RefugeView = 0,
        /// <summary>The refuge from the farthest zoom (LOD review).</summary>
        RefugeFar = 1,
        /// <summary>Every feedback effect at once over the egg (overdraw review).</summary>
        HatchBurst = 2,
        /// <summary>One animal, three-quarter view, framed on its silhouette (icons, thumbnails).</summary>
        AnimalIcon = 3,
        /// <summary>One egg, framed (icons).</summary>
        EggIcon = 4,
        /// <summary>One decoration, framed (icons).</summary>
        DecorIcon = 5,
    }

    public enum CaptureDebugView
    {
        None = 0,
        /// <summary>LOD0 red, LOD1 green, LOD2 blue (DBG_LOD materials, editor only).</summary>
        LodColors = 1,
        /// <summary>Overdraw heat map: layers counted per pixel with an additive counting material (editor only).</summary>
        Overdraw = 2,
    }

    /// <summary>
    /// M-DEF-CAPTURE (doc 11 CAPTURE DATA ASSETS): one capture setup as data, so no editor script hardcodes a
    /// resolution or a framing. Profiles live under Editor/Capture/Profiles (never in a build).
    /// </summary>
    [CreateAssetMenu(menuName = "Pocket Wilds/Capture Profile", fileName = "Capture_")]
    public sealed class CaptureProfile : ScriptableObject
    {
        public const string Folder = "Assets/_Game/Editor/Capture/Profiles";
        public const string AnimalIcon = "Capture_AnimalIcon";

        public CaptureShot shot = CaptureShot.RefugeView;
        [Min(16)] public int width = 540;
        [Min(16)] public int height = 1170;
        [Tooltip("Rendered this many times larger, then filtered down (icons).")]
        [Range(1, 4)] public int supersample = 1;
        [Tooltip("Vertical field of view; 0 keeps the game camera's.")]
        [Range(0f, 90f)] public float fieldOfView;
        [Tooltip("Empty border around a framed subject, as a share of its size.")]
        [Range(0f, 0.5f)] public float padding = 0.07f;
        [Tooltip("Framed subjects: camera turn around the subject and height above it (degrees).")]
        public float azimuth = 28f;
        public float elevation = 12f;
        public Color background = new Color(0f, 0f, 0f, 0f);
        [Tooltip("PNG with alpha (marketing cut-outs, icons).")]
        public bool transparent;
        public QualityTier tier = QualityTier.Medium;
        [Range(0, 6)] public int animals = 6;
        public CaptureDebugView debugView = CaptureDebugView.None;
        [Range(1, 8)] public int msaa = 4;
        [Tooltip("Output file name without extension (defaults to the asset name).")]
        public string outputName = string.Empty;

        public string OutputName => string.IsNullOrEmpty(outputName) ? name : outputName;

        public static CaptureProfile Load(string name) =>
            AssetDatabase.LoadAssetAtPath<CaptureProfile>($"{Folder}/{name}.asset")
            ?? throw new InvalidOperationException($"Missing capture profile {Folder}/{name}.asset (Pocket Wilds/Capture/Build Capture Profiles).");
    }
}
