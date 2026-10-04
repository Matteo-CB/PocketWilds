#nullable enable

namespace PocketWilds.Simulation
{
    public enum SaveUrgency : byte
    {
        /// <summary>Grouped with other changes after a short debounce.</summary>
        Normal = 0,

        /// <summary>Rare discovery, purchase, important unlock. Saved on the next frames, never on the interaction frame.</summary>
        Priority = 1,
    }

    /// <summary>Lets simulation request persistence without depending on the save implementation.</summary>
    public interface ISaveScheduler
    {
        void RequestSave(SaveUrgency urgency);
    }
}
