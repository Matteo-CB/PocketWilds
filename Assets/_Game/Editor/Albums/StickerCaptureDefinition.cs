using System;
using System.Collections.Generic;
using UnityEngine;

namespace PocketWilds.Editor.Albums
{
    /// <summary>Framings of doc « 01 Albums et autocollants », section 1.11 (focal lengths for a 24 mm frame height).</summary>
    public enum StickerFraming
    {
        /// <summary>Head and shoulders, 50 mm, eyes at the upper third.</summary>
        Portrait,
        /// <summary>The whole animal three-quarter front, 35 mm, ground visible under the paws.</summary>
        Full,
        /// <summary>2 to 6 animals, 35 mm, medium shot, every face visible.</summary>
        Scene,
        /// <summary>A zone, a weather or a monument, 28 mm, horizon at the upper third.</summary>
        Landscape,
        /// <summary>An object, a plant, an insect, 85 mm.</summary>
        Detail,
    }

    /// <summary>The biome's light profiles (LP_Grassland_*).</summary>
    public enum StickerLight
    {
        Morning,
        Day,
        Evening,
        Night,
    }

    /// <summary>Cutout (the silhouette, Portrait, Full and Detail), Card (rounded square, Scene), Medallion (round, Landscape).</summary>
    public enum StickerShape
    {
        Cutout,
        Card,
        Medallion,
    }

    /// <summary>What stands behind the subjects.</summary>
    public enum StickerBackdrop
    {
        /// <summary>Nothing: an empty studio, transparent background (cutouts).</summary>
        None,
        /// <summary>A Prairie zone of the world scene with its ground, plants, sky and obstacles.</summary>
        Zone,
        /// <summary>The Garden set (PF_Activity_Garden_Set) in the world scene, its plots and measured beds.</summary>
        GardenSet,
    }

    public enum SubjectKind
    {
        /// <summary>A species variant from its real prefab and palette (source: « rabbit.brown »).</summary>
        Animal,
        /// <summary>A prefab of the game (source: its asset path), palettes bound as the game binds them.</summary>
        Prefab,
        /// <summary>One mesh of a model (source: an .fbx path or a folder, meshName, material, palette).</summary>
        Mesh,
    }

    /// <summary>How a subject stands.</summary>
    public enum SubjectMount
    {
        /// <summary>On the ground (studio: y 0; world: the ground under it), checked against the world's obstacles.</summary>
        Ground,
        /// <summary>On top of its holder: the holder's highest point under its footprint.</summary>
        RestOn,
        /// <summary>Inside its holder (nuts in a basket): from the holder's bottom, exempt from the holder only.</summary>
        Inside,
        /// <summary>On a socket of its holder animal (bow on the head, leaf on the back), exempt from the holder only.</summary>
        Socket,
        /// <summary>In the air at position.y above the ground (a falling nut, a hovering brush).</summary>
        Air,
        /// <summary>A Garden plot: the set's plant anchor of index <see cref="StickerSubject.plot"/>.</summary>
        Plot,
    }

    public enum AnimalSocket
    {
        HeadTop,
        Neck,
        Back,
        TailBase,
    }

    /// <summary>One subject of a sticker capture.</summary>
    [Serializable]
    public sealed class StickerSubject
    {
        public SubjectKind kind;
        [Tooltip("Animal: rabbit.brown. Prefab: asset path. Mesh: .fbx path or folder.")]
        public string source;
        [Tooltip("Mesh: node or mesh name inside the model (empty: the first mesh).")]
        public string meshName;
        [Tooltip("Mesh: material path.")]
        public string material;
        [Tooltip("Mesh (and Prefab without bindings): palette source id.")]
        public string palette;
        [Tooltip("Sticker frame (m): x right, z away from the camera at azimuth 0; y: lift above the mount.")]
        public Vector3 position;
        [Tooltip("Degrees; 180 faces a camera at azimuth 0.")]
        public float yaw;
        [Tooltip("Extra x and z rotation in degrees (a falling nut, a pouring can).")]
        public Vector3 tilt;
        public float scale = 1f;
        [Tooltip("Animal: state of AC_SmallQuadruped (Idle, IdleLook, Move, Eat, Sleep, Inspect, Happy, Social, React).")]
        public string pose = "Idle";
        [Range(0f, 1f)] public float poseTime = 0.3f;
        public SubjectMount mount;
        [Tooltip("Index of the holder subject (RestOn, Inside, Socket); it comes earlier in the list.")]
        public int holder = -1;
        public AnimalSocket socket;
        public int plot = -1;
        [Tooltip("The camera frames the focus subjects (none flagged: all).")]
        public bool focus;
        [Tooltip("> 1: a cloud of copies spread around position (members that would touch anything are dropped).")]
        public int count = 1;
        public Vector3 spread;
        public string label;

        public string Name => !string.IsNullOrEmpty(label) ? label
            : kind == SubjectKind.Animal ? source
            : kind == SubjectKind.Mesh ? (string.IsNullOrEmpty(meshName) ? System.IO.Path.GetFileNameWithoutExtension(source) : meshName)
            : System.IO.Path.GetFileNameWithoutExtension(source);

        public static StickerSubject Animal(string variant, float x, float z, float yaw, string pose = "Idle", float time = 0.3f) =>
            new StickerSubject { kind = SubjectKind.Animal, source = variant, position = new Vector3(x, 0f, z), yaw = yaw, pose = pose, poseTime = time };

        public static StickerSubject Prefab(string path, float x, float z, float yaw = 0f) =>
            new StickerSubject { kind = SubjectKind.Prefab, source = path, position = new Vector3(x, 0f, z), yaw = yaw };

        public static StickerSubject Mesh(string model, string mesh, string material, string palette, float x, float z, float yaw = 0f) =>
            new StickerSubject { kind = SubjectKind.Mesh, source = model, meshName = mesh, material = material, palette = palette, position = new Vector3(x, 0f, z), yaw = yaw };

        public StickerSubject Focus()
        {
            focus = true;
            return this;
        }

        public StickerSubject Scaled(float value)
        {
            scale = value;
            return this;
        }

        public StickerSubject Tilted(float x, float z)
        {
            tilt = new Vector3(x, 0f, z);
            return this;
        }

        public StickerSubject Palette(string id)
        {
            palette = id;
            return this;
        }

        public StickerSubject Air(float height)
        {
            mount = SubjectMount.Air;
            position.y = height;
            return this;
        }

        public StickerSubject On(int holderIndex, float lift = 0.002f)
        {
            mount = SubjectMount.RestOn;
            holder = holderIndex;
            position.y = lift;
            return this;
        }

        /// <summary>
        /// Inside an earlier subject: x and z are fractions of the holder's footprint radius (-1 to 1), <paramref name="fromBottom"/>
        /// the fraction of its height where this subject's lowest point sits (sizes never guessed: measured on the holder).
        /// </summary>
        public StickerSubject In(int holderIndex, float fromBottom)
        {
            mount = SubjectMount.Inside;
            holder = holderIndex;
            position.y = fromBottom;
            return this;
        }

        /// <summary>Air subjects: x and z are taken from an earlier subject's root instead of the sticker anchor.</summary>
        public StickerSubject Near(int holderIndex)
        {
            holder = holderIndex;
            return this;
        }

        /// <summary>On a socket of an earlier animal; position is the offset (y up, x and z in the animal's frame).</summary>
        public StickerSubject At(int holderIndex, AnimalSocket where, float x = 0f, float y = 0f, float z = 0f)
        {
            mount = SubjectMount.Socket;
            holder = holderIndex;
            socket = where;
            position = new Vector3(x, y, z);
            return this;
        }

        public StickerSubject InPlot(int index)
        {
            mount = SubjectMount.Plot;
            plot = index;
            return this;
        }

        public StickerSubject Cloud(int copies, Vector3 halfSize)
        {
            count = copies;
            spread = halfSize;
            return this;
        }
    }

    /// <summary>
    /// T44A: one sticker's capture (doc « 01 Albums et autocollants », section 1.11, L-SC-011): its id, its subjects with
    /// their prefab, variant, pose and position, its framing (and focal length), light profile, backdrop and shape. The
    /// table lives in <see cref="StickerCaptureDefinitions"/>; <see cref="StickerCapturePipeline"/> renders it.
    /// </summary>
    [Serializable]
    public sealed class StickerCaptureDefinition
    {
        public string id;
        [Tooltip("page.grassland.1, page.activity.001")]
        public string page;
        public StickerFraming framing;
        public StickerLight light = StickerLight.Day;
        public StickerShape shape;
        public StickerBackdrop backdrop;
        [Tooltip("Zone backdrop: start, meadow, streambend, orchard, hill, windmillrise, pondedge, forestedge.")]
        public string zone;
        [Tooltip("Preferred anchor in the zone chunk's (or the set's) space; the nearest clear spot is used.")]
        public Vector3 anchor;
        public float anchorYaw;
        [Tooltip("Camera around the sticker frame (degrees, 0: from -z looking +z).")]
        public float azimuth;
        [Tooltip("Camera height angle (degrees, looking down).")]
        public float elevation = 10f;
        [Tooltip("> 1 frames tighter.")]
        public float zoom = 1f;
        [Tooltip("Landscape: camera distance to the anchor (m); 0 fits the subjects.")]
        public float distance;
        [Tooltip("Landscape: aimed point above the ground at the anchor (m).")]
        public float aimHeight = 0.3f;
        [Tooltip("Ambient life drawn (landscapes without subjects only: it never meets a placed subject).")]
        public bool life;
        [Tooltip("What the capture shows, and how it differs from the doc when it must.")]
        public string note;
        public List<StickerSubject> subjects = new List<StickerSubject>();

        /// <summary>Equivalent focal length of the table (mm).</summary>
        public float FocalLength => framing switch
        {
            StickerFraming.Portrait => 50f,
            StickerFraming.Full => 35f,
            StickerFraming.Scene => 35f,
            StickerFraming.Landscape => 28f,
            _ => 85f,
        };

        /// <summary>Vertical field of view of the square render for <see cref="FocalLength"/> on a 24 mm frame height.</summary>
        public float FieldOfView => 2f * Mathf.Atan(12f / FocalLength) * Mathf.Rad2Deg;

        /// <summary>STK-GL-01 → sticker.gl.01; STK-ACT-001-02 → sticker.act.001.02.</summary>
        public string StickerKey => KeyOf(id);

        public static string KeyOf(string stickerId) => "sticker." + stickerId.Substring(4).ToLowerInvariant().Replace('-', '.');

        /// <summary>page.grassland.1 → grassland_1 (folders and atlas names).</summary>
        public string PageFolder => FolderOf(page);

        public static string FolderOf(string pageKey) => pageKey.Substring("page.".Length).Replace('.', '_');

        /// <summary>page.grassland.1 → albums.page.grassland.1.</summary>
        public string AtlasAddress => AddressOf(page);

        public static string AddressOf(string pageKey) => "albums." + pageKey;

        public static StickerShape DefaultShape(StickerFraming framing) => framing switch
        {
            StickerFraming.Scene => StickerShape.Card,
            StickerFraming.Landscape => StickerShape.Medallion,
            _ => StickerShape.Cutout,
        };
    }
}
