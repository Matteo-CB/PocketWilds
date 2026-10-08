using PocketWilds.Content;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T38 « Toilettage » set layout (doc 01 Contenu, S-ACT-001; doc « Mini-jeux &amp; activités », S-ACT-001), shared by the
    /// stage and by GroomingPipeline (the zones are measured for exactly this view). The set is a small diorama far below
    /// the refuge: a grass mound with a cushion (its top flat at the set's origin), round Prairie hills behind, a warm sky.
    /// The animal stands on the cushion in a 3/4 view facing the set's camera, which looks along +Z, a little from above.
    /// </summary>
    public static class GroomingLayout
    {
        /// <summary>Addressables group and address of the set (loaded on the first game, released when leaving the activities).</summary>
        public const string Group = "Activity_Grooming";

        public const string SetAddress = "activity.grooming.set";
        public const string SetPrefabName = "PF_Activity_Grooming_Set";

        /// <summary>
        /// Where the set lives: 400 m under the refuge and west of every zone (the closed zones' haze starts east of the
        /// arrival zone), far beyond the refuge camera's reach and the set camera's far plane the other way.
        /// </summary>
        public static readonly Vector3 SetPosition = new Vector3(-60f, -400f, 0f);

        /// <summary>
        /// The animal's yaw on the mound: facing the camera (180°) turned 30° toward the screen's right, the classic 3/4
        /// view, its face toward the key light.
        /// </summary>
        public const float AnimalYaw = 150f;

        /// <summary>The camera looks down this much (degrees): the cushion's top shows, the face stays level.</summary>
        public const float CameraPitch = 9f;

        /// <summary>Vertical field of view: a soft telephoto, the animal big and round.</summary>
        public const float CameraFov = 32f;

        public const float CameraNear = 0.05f;
        public const float CameraFar = 60f;

        /// <summary>Share of the half width and half height the animal may use (the HUD keeps the top and the bottom).</summary>
        public const float FrameWidthShare = 0.8f;

        public const float FrameHeightShare = 0.56f;

        /// <summary>Most zones a body carries (the dust array's size in SH_AnimalMaster).</summary>
        public const int MaxZones = 64;

        /// <summary>Debris and burrs shown at once (doc 01: 12 debris and 4 burrs at d10, 14 debris in challenge 3).</summary>
        public const int MaxDebris = 24;

        /// <summary>Soap bubbles of a game (15 s and 30 s).</summary>
        public const int MaxBubbles = 2;

        /// <summary>Foam bubbles alive at once (4 per pass, H-VFX-ACT-001-01).</summary>
        public const int FoamCount = 24;

        public const int FoamPerPass = 4;

        /// <summary>Round glints effects played at once (H-VFX-ACT-001-03).</summary>
        public const int GlintSystems = 6;

        /// <summary>
        /// Zones per rig family (doc 01 Contenu, S-ACT-001: SmallQuadruped 40, MediumQuadruped 48, HeavyQuadruped 56,
        /// Bird 36, Hopper 30, SwimmerLow 34, ClimberPrimate 44). Penguin is not in the doc: it takes the Bird's 36.
        /// </summary>
        public static int ZoneCount(RigFamily family)
        {
            switch (family)
            {
                case RigFamily.SmallQuadruped: return 40;
                case RigFamily.MediumQuadruped: return 48;
                case RigFamily.HeavyQuadruped: return 56;
                case RigFamily.Bird: return 36;
                case RigFamily.Hopper: return 30;
                case RigFamily.SwimmerLow: return 34;
                case RigFamily.ClimberPrimate: return 44;
                default: return 36;
            }
        }

        public static Quaternion AnimalRotation => Quaternion.Euler(0f, AnimalYaw, 0f);

        public static Quaternion CameraRotation => Quaternion.Euler(CameraPitch, 0f, 0f);

        /// <summary>From the animal toward the camera, in world space (the camera is far enough for one direction).</summary>
        public static Vector3 ToCamera => -(CameraRotation * Vector3.forward);

        /// <summary>From the animal toward the camera in the animal's own space (bind pose, its root at the origin).</summary>
        public static Vector3 ToCameraLocal => Quaternion.Inverse(AnimalRotation) * ToCamera;
    }
}
