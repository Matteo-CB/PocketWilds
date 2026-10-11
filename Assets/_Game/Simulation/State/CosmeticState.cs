#nullable enable
using System.Collections.Generic;
using PocketWilds.Content;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>One worn accessory: the item and its palette (an index in <see cref="AccessoryDefinition.Palettes"/>).</summary>
    public struct EquippedAccessory
    {
        public ContentId Accessory;
        public byte Palette;

        public bool IsEmpty => !Accessory.IsValid;
    }

    /// <summary>
    /// T47: what each animal wears, per animal instance id and socket. Only animals wearing something have an entry. Unknown
    /// or no longer owned items are dropped by <see cref="CosmeticService.Prune"/> after load.
    /// </summary>
    public sealed class CosmeticState
    {
        /// <summary>Number of <see cref="AccessorySocket"/> values (slots per animal).</summary>
        public const int Sockets = 6;

        /// <summary>Animal instance id to its slots (index = <see cref="AccessorySocket"/>).</summary>
        public Dictionary<uint, EquippedAccessory[]> Animals { get; } = new Dictionary<uint, EquippedAccessory[]>();

        public EquippedAccessory Get(uint animal, AccessorySocket socket) =>
            (uint)socket < Sockets && Animals.TryGetValue(animal, out var slots) ? slots[(int)socket] : default;

        /// <summary>Writes one slot; an empty value clears it, and an animal with nothing left loses its entry.</summary>
        public void Set(uint animal, AccessorySocket socket, EquippedAccessory value)
        {
            if ((uint)socket >= Sockets)
                return;
            if (!Animals.TryGetValue(animal, out var slots))
            {
                if (value.IsEmpty)
                    return;
                slots = new EquippedAccessory[Sockets];
                Animals.Add(animal, slots);
            }
            slots[(int)socket] = value;
            if (value.IsEmpty && IsBare(slots))
                Animals.Remove(animal);
        }

        public static bool IsBare(EquippedAccessory[] slots)
        {
            for (int i = 0; i < slots.Length; i++)
                if (!slots[i].IsEmpty)
                    return false;
            return true;
        }
    }

    /// <summary>T47: one companion of the account (doc 02 Renouveau, section 5): its animal and its bond.</summary>
    public struct CompanionRecord
    {
        public ContentId Species;

        /// <summary>The companion's animal in the collection; 0 while it is not in the refuge (item revoked).</summary>
        public uint AnimalId;

        /// <summary>Bond XP gathered in total (never lowered), 0 to <see cref="CompanionService.MaxXp"/>.</summary>
        public int BondXp;

        /// <summary>Highest bond level whose reward was given (0 to 25).</summary>
        public byte RewardedLevel;

        /// <summary>The view still owes the 2 s staged arrival (cleared by <see cref="CompanionService.MarkArrivalSeen"/>).</summary>
        public bool ArrivalPending;

        /// <summary>Day key of the last photo that gave XP (one a day).</summary>
        public int PhotoDay;
    }

    /// <summary>T47: the account's companions, one record per companion species (unique per account).</summary>
    public sealed class CompanionState
    {
        public List<CompanionRecord> Companions { get; } = new List<CompanionRecord>();

        public int IndexOf(ContentId species)
        {
            for (int i = 0; i < Companions.Count; i++)
                if (Companions[i].Species == species)
                    return i;
            return -1;
        }

        /// <summary>Index of the companion whose animal this is, or -1.</summary>
        public int IndexOfAnimal(uint animal)
        {
            if (animal == 0)
                return -1;
            for (int i = 0; i < Companions.Count; i++)
                if (Companions[i].AnimalId == animal)
                    return i;
            return -1;
        }
    }
}
