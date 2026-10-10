using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T40 « Pluie de noisettes » set layout (doc 01 Contenu, S-ACT-003; doc « Mini-jeux &amp; activités », S-ACT-003), shared
    /// by the stage and by NutDropPipeline. The set is a cozy Prairie corner far below the refuge: a flat meadow floor at the
    /// set's origin (y = 0), a big round hazel tree behind the playfield whose canopy fills the top of the portrait screen,
    /// soft hills behind, grass at the bottom. The playfield is the vertical plane z = 0: its x is the game's x times
    /// <see cref="FieldScale"/>; the animal walks along it with the basket on its head, facing the camera that looks along +Z.
    /// </summary>
    public static class NutDropLayout
    {
        /// <summary>Addressables group and address of the set (loaded on the first game, released when leaving the activities).</summary>
        public const string Group = "Activity_NutDrop";

        public const string SetAddress = "activity.nut_drop.set";
        public const string SetPrefabName = "PF_Activity_NutDrop_Set";

        /// <summary>
        /// Where the set lives: 400 m under the refuge like the grooming set, 60 m further west (the two sets never meet, and
        /// both stay far beyond the refuge camera's reach).
        /// </summary>
        public static readonly Vector3 SetPosition = new Vector3(-120f, -400f, 0f);

        /// <summary>
        /// Metres per playfield unit across (x). The d5 basket (1.00) is then 0.31 m wide, agent NB's ND_Basket at its own
        /// size (a rabbit's head is 0.19 m wide, its body 0.30 m seen from the front): the basket reads as the animal's own.
        /// The visible basket is always the game's catch width (what you see is what catches).
        /// </summary>
        public const float FieldScale = 0.31f;

        /// <summary>The playfield's half width the camera keeps in view, beyond the game's (units): the basket's edge and a margin.</summary>
        public const float FieldMargin = 0.42f;

        /// <summary>Share of the screen width kept at each side by the finger mapping (the finger reaches the field's edge before the screen's).</summary>
        public const float InputMargin = 0.06f;

        /// <summary>Vertical field of view: a calm, slightly long lens (the canopy and the animal both read big).</summary>
        public const float CameraFov = 30f;

        /// <summary>The camera looks down this much (degrees): the meadow floor shows under the animal.</summary>
        public const float CameraPitch = 4f;

        public const float CameraNear = 0.3f;
        public const float CameraFar = 80f;

        /// <summary>Share of the screen height under the floor line (grass, the HUD's bottom).</summary>
        public const float FloorScreenShare = 0.11f;

        /// <summary>Share of the screen height over the spawn line (the canopy, the score and the time).</summary>
        public const float CanopyScreenShare = 0.2f;

        /// <summary>Most items a game shows at once, falling, bouncing or in the pile (pooled renderers of the set).</summary>
        public const int ItemPool = 40;

        /// <summary>The caught items shown in the basket (doc: « une petite pile »); the oldest is recycled.</summary>
        public const int PileMax = 3;

        /// <summary>H-VFX-ACT-003-01: the catch glints' systems (4 round glints emitted a catch, each system holds several catches).</summary>
        public const int GlintSystems = 1;

        /// <summary>H-VFX-ACT-003-03: the leaves of a pinecone bonk (2 a bonk), pooled.</summary>
        public const int LeafCount = 6;

        /// <summary>Kit grass tufts and flowers along the bottom of the screen (in front of the walking line).</summary>
        public const int GrassRow = 14;

        /// <summary>The meadow floor's line in front of the animal where the bottom grass grows (m, toward the camera).</summary>
        public const float GrassZ = -0.85f;

        /// <summary>The backdrop (T38's GR_SetBackdrop) at this scale, its center this far toward the camera (its inner hole stays out of view).</summary>
        public const float BackdropScale = 2f;

        public const float BackdropZ = -3.2f;

        /// <summary>T38's backdrop floor lies this far under its origin (pw_grooming.FLOOR_Z).</summary>
        public const float BackdropFloor = -0.30f;

        /// <summary>
        /// The tree's trunk foot stands this far behind the playfield plane (m): behind the walking animal (its back at most
        /// ~0.4 m behind the plane), in front of the backdrop's near hills.
        /// </summary>
        public const float TreeMinZ = 0.8f;

        /// <summary>The canopy spans the field's width times this (it covers every spawn point).</summary>
        public const float CanopyCover = 1.3f;

        /// <summary>Spawn line default when the canopy is missing (m above the floor).</summary>
        public const float DefaultSpawnY = 3.3f;

        /// <summary>The basket's seat above the head when the species was not measured (m), and the measured seat's margin.</summary>
        public const float DefaultSeat = 0.1f;

        public const float SeatMargin = 0.015f;

        /// <summary>Most the animal turns toward its motion (degrees), and the speed (units per second) that turns it all the way.</summary>
        public const float MaxTurn = 35f;

        public const float TurnSpeed = 6f;

        /// <summary>The animal plays Move above this basket speed (units per second), Idle below the lower one.</summary>
        public const float MoveOn = 0.9f;

        public const float MoveOff = 0.35f;

        /// <summary>Most the animal leans toward its motion (degrees), pivoting on the paws of that side.</summary>
        public const float MaxLean = 5f;

        /// <summary>Hop height while it moves (m at scale 1) and hops per second.</summary>
        public const float HopHeight = 0.022f;

        public const float HopRate = 3.2f;

        /// <summary>The basket's own gentle bob while it moves (m) and the spring that softens the head's motion under it.</summary>
        public const float BasketBob = 0.012f;

        /// <summary>
        /// Each kind's scale on its mesh (agent NB made the items 0.13 to 0.16 m, oversized on purpose to read while they
        /// fall): the nuts, the berry, the pinecone and the feather at their own size; the wide basket special is NB's
        /// basket in miniature, the golden shower a big golden hazelnut.
        /// </summary>
        public static float ItemScale(NutDropItemKind kind)
        {
            switch (kind)
            {
                case NutDropItemKind.WideBasket: return 0.55f;
                case NutDropItemKind.GoldenShower: return 1.3f;
                default: return 1f;
            }
        }

        /// <summary>The mesh each kind wears (index into NutDropPipeline's props: the special kinds borrow an item's).</summary>
        public static NutDropItemKind MeshKind(NutDropItemKind kind) =>
            kind == NutDropItemKind.GoldenShower ? NutDropItemKind.GoldenNut : kind;

        /// <summary>Items settle in the pile a little smaller (they sit deeper in the bowl; a heap of six stays small).</summary>
        public const float PileScale = 0.72f;

        /// <summary>The kinds the set has a mesh for (index = (int)kind; 0 unused).</summary>
        public const int KindCount = 9;

        /// <summary>The camera's rotation (it looks along +Z, pitched down).</summary>
        public static Quaternion CameraRotation => Quaternion.Euler(CameraPitch, 0f, 0f);

        /// <summary>The animal faces the camera (180°); its turn toward the motion: right on screen is +x.</summary>
        public static float Yaw(float turn01) => 180f - Mathf.Clamp(turn01, -1f, 1f) * MaxTurn;
    }
}
