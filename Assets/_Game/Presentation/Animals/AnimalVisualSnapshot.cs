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

        /// <summary>Packed palette row, pattern and rare intensity (see <see cref="AnimalVisualPacking"/>).</summary>
        public uint VisualData;

        /// <summary>Normalized start time of the idle loop, so neighbours never breathe in sync.</summary>
        public float IdlePhase;
    }
}
