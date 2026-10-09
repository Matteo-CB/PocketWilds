using System;
using PocketWilds.Core;
using PocketWilds.Simulation;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// The player's animals as an activity shows them (T39, doc « Mini-jeux &amp; activités » section 1: pooled props,
    /// nothing instantiated during a game): a pool of views per species, built from the visual catalog's prefab while the
    /// game loads (<see cref="Warm"/>), then shown and hidden with SetActive only. Each view wears its animal's own look
    /// (variant, pattern, rare glow, size) through the visual resolver, like the refuge's. Its own root under the stage's,
    /// so the refuge's pools are never touched; everything is destroyed with <see cref="Dispose"/>.
    /// </summary>
    public sealed class ActivityAnimalViews : IDisposable
    {
        private readonly GameContext _context;
        private readonly AnimalViewPool _pool;
        private readonly GameObject _root;
        private readonly AnimalView[] _shown;
        private int _shownCount;

        public ActivityAnimalViews(GameContext context, Transform parent, string name, int capacity)
        {
            _context = context;
            _root = new GameObject(name);
            if (parent != null)
                _root.transform.SetParent(parent, false);
            _pool = new AnimalViewPool(_root.transform);
            _shown = new AnimalView[Mathf.Max(1, capacity)];
        }

        public Transform Root => _root != null ? _root.transform : null;

        public int ShownCount => _shownCount;

        public AnimalView Shown(int index) => (uint)index < (uint)_shownCount ? _shown[index] : null;

        /// <summary>The species' visual key and its catalog entry (prefab, body), or false when it has no visual.</summary>
        public bool TryGetEntry(ContentId species, out AnimalVisualCatalog.Entry entry)
        {
            entry = null;
            var catalog = _context != null ? _context.VisualCatalog : null;
            if (catalog == null || _context.Content == null || !_context.Content.Species.TryGet(species, out var definition))
                return false;
            return catalog.TryGet(definition.VisualKey, out entry) && entry != null && entry.prefab != null;
        }

        /// <summary>
        /// While the game loads: <paramref name="count"/> views of this species ready in its pool (instantiated now, never
        /// during the game). Returns how many are ready (0 without a visual).
        /// </summary>
        public int Warm(ContentId species, int count)
        {
            if (!TryGetEntry(species, out var entry))
                return 0;
            // Room for a whole game of one species (challenge 8: six of a kind), created only as needed.
            int handle = _pool.Register(entry.visualKey, entry.prefab, _shown.Length, _shown.Length + 2);
            var pool = _pool.GetPool(handle);
            int shown = CountShown(species);
            int wanted = Mathf.Max(0, count - shown);
            if (pool.Stats.Inactive < wanted)
                _pool.Warmup(handle, wanted);
            return Mathf.Min(count, pool.Stats.Inactive + shown);
        }

        private int CountShown(ContentId species)
        {
            int n = 0;
            for (int i = 0; i < _shownCount; i++)
                if (_shown[i] != null && _shown[i].Species == species)
                    n++;
            return n;
        }

        /// <summary>
        /// One animal shown at a pose (its look, its size, its Idle at its own phase); null when its pool is empty or the
        /// species has no visual. <paramref name="lod0"/> keeps its most detailed mesh at any distance (a peeking ear).
        /// </summary>
        public AnimalView Show(in AnimalInstance animal, Vector3 position, float yaw, bool lod0)
        {
            if (_shownCount >= _shown.Length || !TryGetEntry(animal.Species, out var entry) || !_pool.TryGetHandle(entry.visualKey, out int handle))
                return null;
            AnimalVisualSnapshot snapshot;
            if (_context.Visuals == null || !_context.Visuals.TryResolve(animal, position, yaw, out snapshot))
                snapshot = new AnimalVisualSnapshot
                {
                    InstanceId = animal.InstanceId, Species = animal.Species, Position = position, Yaw = yaw,
                    Scale = AnimalGenome.Scale(animal.TraitBits), IdlePhase = AnimalVisualResolver.IdlePhase(animal.Seed),
                };
            if (!_pool.TryAcquire(handle, in snapshot, out var view))
                return null;
            if (lod0 && view.LodGroup != null)
                view.LodGroup.ForceLOD(0);
            view.SetPlaybackSpeed(1f);
            _shown[_shownCount++] = view;
            return view;
        }

        /// <summary>Every shown view back in its pool (inactive, reset), LODs free again.</summary>
        public void HideAll()
        {
            for (int i = 0; i < _shownCount; i++)
            {
                var view = _shown[i];
                _shown[i] = null;
                if (view == null)
                    continue;
                if (view.LodGroup != null)
                    view.LodGroup.ForceLOD(-1);
                _pool.Release(view);
            }
            _shownCount = 0;
        }

        /// <summary>The views go with their root (edit mode captures included: no deferred Destroy).</summary>
        public void Dispose()
        {
            _shownCount = 0;
            if (_root == null)
                return;
            if (Application.isPlaying)
                Object.Destroy(_root);
            else
                Object.DestroyImmediate(_root);
        }
    }
}
