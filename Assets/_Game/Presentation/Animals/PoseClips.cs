using PocketWilds.Content;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T49: the profile poses an animal can play (the T44B pose list names them; photo mode will play them). Each pose is a
    /// one-shot state of the family controller (Editor AnimalPipeline.PoseStates adds it when the family library has the clip
    /// AN_&lt;Family&gt;_&lt;State&gt;) and returns to Idle by itself. « Sortir du terrier » (pose.peek_out): lowers the muzzle
    /// to the ground then lifts it with a little shake, no body point below the ground, 1.8 s, every species except floaters.
    /// </summary>
    public static class PoseClips
    {
        public readonly struct Entry
        {
            public readonly string Key;
            public readonly string State;
            public readonly int StateHash;
            public readonly float Seconds;
            public readonly RigFamily[] Families;

            public Entry(string key, string state, float seconds, params RigFamily[] families)
            {
                Key = key;
                State = state;
                StateHash = Animator.StringToHash(state);
                Seconds = seconds;
                Families = families;
            }
        }

        public const string PeekOut = "pose.peek_out";

        /// <summary>Every pose with a clip, append only. Floaters (no ground to reach) are never listed.</summary>
        public static readonly Entry[] All =
        {
            new Entry(PeekOut, "PeekOut", 1.8f, RigFamily.SmallQuadruped, RigFamily.MediumQuadruped, RigFamily.HeavyQuadruped),
        };

        /// <summary>The family clip a pose plays (« AN_SmallQuadruped_PeekOut »).</summary>
        public static string ClipName(in Entry entry, RigFamily family) => "AN_" + family + "_" + entry.State;

        /// <summary>The animator state of a pose for a family, false when the pose has no clip for it.</summary>
        public static bool TryGet(string poseKey, RigFamily family, out int stateHash)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Key != poseKey)
                    continue;
                foreach (var f in All[i].Families)
                {
                    if (f != family)
                        continue;
                    stateHash = All[i].StateHash;
                    return true;
                }
            }
            stateHash = 0;
            return false;
        }

        /// <summary>Plays a pose on a view (crossfade 0.12 s); false when the family has no clip for it.</summary>
        public static bool Play(AnimalView view, string poseKey, RigFamily family)
        {
            if (view == null || !TryGet(poseKey, family, out int hash))
                return false;
            view.PlayState(hash, 0.12f);
            return true;
        }
    }
}
