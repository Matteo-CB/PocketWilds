#nullable enable
using System;
using System.Collections.Generic;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// Every animal the player owns, stored as a compact struct array. Scales to 50k+ animals
    /// without any Unity object. Removal swaps with the last element, so order is not stable.
    /// </summary>
    public sealed class CollectionState
    {
        private AnimalInstance[] _animals;
        private int _count;
        private readonly Dictionary<uint, int> _indexById;

        public CollectionState(int initialCapacity = 64)
        {
            _animals = new AnimalInstance[Math.Max(4, initialCapacity)];
            _indexById = new Dictionary<uint, int>(Math.Max(4, initialCapacity));
            NextInstanceId = 1;
        }

        public int Count => _count;

        /// <summary>Next id to assign. Ids are never reused, even after a removal.</summary>
        public uint NextInstanceId { get; private set; }

        public HashSet<ContentId> DiscoveredSpecies { get; } = new HashSet<ContentId>();
        public HashSet<ContentId> DiscoveredVariants { get; } = new HashSet<ContentId>();

        /// <summary>Player-chosen names only. Default names are generated from the seed.</summary>
        public Dictionary<uint, string> CustomNames { get; } = new Dictionary<uint, string>();

        public ref readonly AnimalInstance this[int index]
        {
            get
            {
                if ((uint)index >= (uint)_count)
                    throw new ArgumentOutOfRangeException(nameof(index));
                return ref _animals[index];
            }
        }

        /// <summary>Assigns a new instance id, stores the animal and returns the id.</summary>
        public uint Add(AnimalInstance animal)
        {
            animal.InstanceId = NextInstanceId++;
            Insert(animal);
            return animal.InstanceId;
        }

        /// <summary>Restores an animal with its saved id (load and migrations only).</summary>
        public void Restore(AnimalInstance animal)
        {
            if (animal.InstanceId == 0)
                throw new ArgumentException("Restored animals need an instance id.");
            if (_indexById.ContainsKey(animal.InstanceId))
                throw new ArgumentException("Duplicate instance id " + animal.InstanceId);
            Insert(animal);
            if (animal.InstanceId >= NextInstanceId)
                NextInstanceId = animal.InstanceId + 1;
        }

        public void SetNextInstanceId(uint next)
        {
            if (next > NextInstanceId)
                NextInstanceId = next;
        }

        public bool TryGetIndex(uint instanceId, out int index) => _indexById.TryGetValue(instanceId, out index);

        public bool TryGet(uint instanceId, out AnimalInstance animal)
        {
            if (_indexById.TryGetValue(instanceId, out int index))
            {
                animal = _animals[index];
                return true;
            }
            animal = default;
            return false;
        }

        /// <summary>Replaces mutable fields of an existing animal (bond, flags...). Id must match.</summary>
        public void Update(in AnimalInstance animal)
        {
            if (!_indexById.TryGetValue(animal.InstanceId, out int index))
                throw new KeyNotFoundException("Unknown animal " + animal.InstanceId);
            _animals[index] = animal;
        }

        public bool Remove(uint instanceId)
        {
            if (!_indexById.TryGetValue(instanceId, out int index))
                return false;

            int last = _count - 1;
            if (index != last)
            {
                _animals[index] = _animals[last];
                _indexById[_animals[index].InstanceId] = index;
            }
            _animals[last] = default;
            _count--;
            _indexById.Remove(instanceId);
            CustomNames.Remove(instanceId);
            return true;
        }

        public void EnsureCapacity(int capacity)
        {
            if (capacity <= _animals.Length)
                return;
            Array.Resize(ref _animals, capacity);
        }

        private void Insert(AnimalInstance animal)
        {
            if (_count == _animals.Length)
                Array.Resize(ref _animals, _animals.Length * 2);
            _animals[_count] = animal;
            _indexById.Add(animal.InstanceId, _count);
            _count++;
        }
    }
}
