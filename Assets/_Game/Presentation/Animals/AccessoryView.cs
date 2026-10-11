using System;
using System.Collections.Generic;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T47: what one animal view wears. Rigid accessory pieces (pooled by the <see cref="AccessoryWardrobe"/>) hang under the
    /// view's sockets with the measured fit (the species' fit, else its family profile, from <see cref="CosmeticService.TryGetWorn"/>)
    /// and the palette as the renderer's Shader User Value: one shared material, SRP Batcher compatible, no clone, no property
    /// block. Rebuilt only when <see cref="CosmeticService.Changed"/> names the owner (or the view gets a new owner): no Update,
    /// nothing per frame. The pieces join every LOD of the wearer but the last one, so they disappear with its last LOD.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AccessoryView : MonoBehaviour
    {
        public const int SocketCount = 6;

        private readonly Transform[] _sockets = new Transform[SocketCount];
        private readonly AccessoryWardrobe.Piece[] _pieces = new AccessoryWardrobe.Piece[SocketCount];
        private readonly AccessoryFit[] _fits = new AccessoryFit[SocketCount];
        private readonly int[] _paletteOf = new int[SocketCount];

        private AnimalView _view;
        private AccessoryWardrobe _wardrobe;
        private CosmeticService _cosmetics;
        private Action<uint> _onChanged;
        private LOD[] _baseLods;
        private bool _lodsChanged;
        private bool _socketsFound;
        private uint _owner;

        /// <summary>The animal instance dressed now (0 when free).</summary>
        public uint Owner => _owner;

        /// <summary>Pieces worn now.</summary>
        public int WornCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < SocketCount; i++)
                    if (_pieces[i] != null)
                        count++;
                return count;
            }
        }

        /// <summary>The piece hanging on a socket, or null (tests, previews).</summary>
        public Transform GetPiece(AccessorySocket socket)
        {
            var piece = _pieces[(int)socket];
            return piece != null ? piece.Transform : null;
        }

        /// <summary>The view's accessory layer, added once per pooled view (never per frame).</summary>
        public static AccessoryView Ensure(AnimalView view)
        {
            var accessories = view.Accessories;
            if (accessories != null)
                return accessories;
            accessories = view.GetComponent<AccessoryView>();
            if (accessories == null)
                accessories = view.gameObject.AddComponent<AccessoryView>();
            accessories._view = view;
            view.AttachAccessories(accessories);
            return accessories;
        }

        /// <summary>Dresses the view for an animal and follows its changes until <see cref="Clear"/>.</summary>
        public void Show(AccessoryWardrobe wardrobe, uint owner)
        {
            if (!ReferenceEquals(_wardrobe, wardrobe))
            {
                Clear();
                _wardrobe = wardrobe;
                wardrobe?.Track(this);
            }
            if (owner != _owner)
            {
                ClearPieces();
                Unsubscribe();
                _owner = owner;
            }
            if (_owner == 0 || _wardrobe == null)
                return;
            Subscribe();
            Refresh();
        }

        /// <summary>Takes everything off and stops listening (pool release).</summary>
        public void Clear()
        {
            ClearPieces();
            Unsubscribe();
            _owner = 0;
        }

        internal void Detach()
        {
            Clear();
            _wardrobe = null;
        }

        /// <summary>Hangs, swaps or removes pieces to match the save. Called on a change, never every frame.</summary>
        public void Refresh()
        {
            if (_cosmetics == null || _owner == 0)
                return;
            FindSockets();
            bool changed = false;
            for (int s = 0; s < SocketCount; s++)
            {
                var socket = _sockets[s];
                AccessoryDefinition definition = null;
                string palette = null;
                var fit = AccessoryFit.Identity;
                bool worn = socket != null && _cosmetics.TryGetWorn(_owner, (AccessorySocket)s, out definition, out palette, out fit);
                var current = _pieces[s];
                if (!worn)
                {
                    if (current != null)
                    {
                        Release(s);
                        changed = true;
                    }
                    continue;
                }
                int paletteIndex = Mathf.Max(0, definition.PaletteIndex(palette));
                if (current != null && current.Id == definition.Id && _paletteOf[s] == paletteIndex && SameFit(_fits[s], fit))
                    continue;
                if (current != null)
                    Release(s);
                var piece = _wardrobe.Take(definition.Id, paletteIndex, socket, fit);
                _pieces[s] = piece;
                _fits[s] = fit;
                _paletteOf[s] = paletteIndex;
                changed = true;
            }
            if (changed)
                UpdateLods();
        }

        private void OnChanged(uint animal)
        {
            if (animal == _owner && _owner != 0)
                Refresh();
        }

        private void Subscribe()
        {
            if (_cosmetics != null)
                return;
            _cosmetics = _wardrobe.Cosmetics;
            if (_cosmetics == null)
                return;
            _onChanged ??= OnChanged;
            _cosmetics.Changed += _onChanged;
        }

        private void Unsubscribe()
        {
            if (_cosmetics == null)
                return;
            _cosmetics.Changed -= _onChanged;
            _cosmetics = null;
        }

        private void ClearPieces()
        {
            bool any = false;
            for (int s = 0; s < SocketCount; s++)
            {
                if (_pieces[s] == null)
                    continue;
                Release(s);
                any = true;
            }
            if (any)
                UpdateLods();
        }

        private void Release(int socket)
        {
            var piece = _pieces[socket];
            _pieces[socket] = null;
            if (piece != null)
                _wardrobe?.Return(piece);
        }

        private static bool SameFit(in AccessoryFit a, in AccessoryFit b) =>
            a.LocalPosition == b.LocalPosition && a.LocalEuler == b.LocalEuler && a.LocalScale == b.LocalScale;

        private void FindSockets()
        {
            if (_socketsFound)
                return;
            _socketsFound = true;
            if (_view == null)
                _view = GetComponent<AnimalView>();
            if (_view == null)
                return;
            _sockets[(int)AccessorySocket.HeadTop] = _view.SocketHeadTop;
            _sockets[(int)AccessorySocket.Neck] = _view.SocketNeck;
            _sockets[(int)AccessorySocket.Back] = _view.SocketBack;
            _sockets[(int)AccessorySocket.TailBase] = _view.SocketTailBase;
            // Ears are not cached by the view: found once here (one search per pooled view).
            _sockets[(int)AccessorySocket.EarL] = FindDeep(transform, "SOCK_Ear_L");
            _sockets[(int)AccessorySocket.EarR] = FindDeep(transform, "SOCK_Ear_R");
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == name)
                    return child;
                var found = FindDeep(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        /// <summary>The pieces' renderers join every LOD but the last (they vanish when the wearer is far). On change only.</summary>
        private void UpdateLods()
        {
            var group = _view != null ? _view.LodGroup : null;
            if (group == null)
                return;
            if (_baseLods == null)
                _baseLods = group.GetLODs();
            int extra = 0;
            for (int s = 0; s < SocketCount; s++)
                if (_pieces[s] != null)
                    extra += _pieces[s].Renderers.Length;
            if (extra == 0)
            {
                if (_lodsChanged)
                {
                    group.SetLODs(_baseLods);
                    _lodsChanged = false;
                }
                return;
            }
            int count = _baseLods.Length;
            var lods = new LOD[count];
            for (int i = 0; i < count; i++)
            {
                var source = _baseLods[i];
                if (i == count - 1 && count > 1)
                {
                    lods[i] = source;
                    continue;
                }
                var baseRenderers = source.renderers ?? Array.Empty<Renderer>();
                var renderers = new Renderer[baseRenderers.Length + extra];
                Array.Copy(baseRenderers, renderers, baseRenderers.Length);
                int n = baseRenderers.Length;
                for (int s = 0; s < SocketCount; s++)
                {
                    var piece = _pieces[s];
                    if (piece == null)
                        continue;
                    for (int r = 0; r < piece.Renderers.Length; r++)
                        renderers[n++] = piece.Renderers[r];
                }
                lods[i] = new LOD(source.screenRelativeTransitionHeight, renderers) { fadeTransitionWidth = source.fadeTransitionWidth };
            }
            group.SetLODs(lods);
            _lodsChanged = true;
        }

        private void OnDestroy() => Unsubscribe();
    }

    /// <summary>
    /// T47: the accessory pieces of a scene, pooled per accessory, and the packed palette value of each (accessory, palette) pair
    /// computed once. <see cref="Dress"/> puts an animal's accessories on a view (refuge, previews). Warmed at load.
    /// </summary>
    public sealed class AccessoryWardrobe : IDisposable
    {
        internal sealed class Piece
        {
            public int Kind;
            public ContentId Id;
            public GameObject GameObject;
            public Transform Transform;
            public MeshRenderer[] Renderers;
        }

        private sealed class Kind
        {
            public ContentId Id;
            public GameObject Prefab;
            public uint[] Values;
            public int Warm;
            public readonly Stack<Piece> Free = new Stack<Piece>(4);
        }

        private readonly Kind[] _kinds;
        private readonly Dictionary<ContentId, int> _kindById;
        private readonly List<Piece> _all = new List<Piece>(8);
        private readonly List<AccessoryView> _dressed = new List<AccessoryView>(32);
        private Transform _root;

        public AccessoryWardrobe(CosmeticService cosmetics, ContentRegistry content, PaletteTable palettes, CosmeticVisualCatalog catalog, Transform parent)
        {
            Cosmetics = cosmetics;
            var root = new GameObject("Accessories");
            _root = root.transform;
            if (parent != null)
                _root.SetParent(parent, false);
            var kinds = new List<Kind>(4);
            _kindById = new Dictionary<ContentId, int>(4);
            if (catalog != null)
            {
                foreach (var entry in catalog.accessories)
                {
                    if (entry == null || entry.prefab == null || string.IsNullOrEmpty(entry.accessoryId))
                        continue;
                    var id = ContentId.FromSource(entry.accessoryId);
                    if (_kindById.ContainsKey(id) || !content.Shop.Accessories.TryGet(id, out var definition))
                        continue;
                    var kind = new Kind { Id = id, Prefab = entry.prefab, Warm = entry.warm, Values = new uint[Math.Max(1, definition.Palettes.Length)] };
                    for (int p = 0; p < kind.Values.Length; p++)
                    {
                        int at = p < definition.Palettes.Length ? entry.palettes.IndexOf(definition.Palettes[p]) : 0;
                        string row = at >= 0 && at < entry.paletteRows.Count ? entry.paletteRows[at] : null;
                        kind.Values[p] = Packed(palettes, row);
                    }
                    _kindById.Add(id, kinds.Count);
                    kinds.Add(kind);
                }
            }
            _kinds = kinds.ToArray();
            for (int k = 0; k < _kinds.Length; k++)
                for (int w = 0; w < _kinds[k].Warm; w++)
                    _kinds[k].Free.Push(Create(k));
        }

        public CosmeticService Cosmetics { get; }

        /// <summary>Accessories with a visual.</summary>
        public int KindCount => _kinds.Length;

        public bool HasVisual(ContentId accessory) => _kindById.ContainsKey(accessory);

        /// <summary>The palette row value of a world prop or accessory palette (environment packing, neutral brightness).</summary>
        public static uint Packed(PaletteTable palettes, string paletteSourceId)
        {
            if (palettes == null || string.IsNullOrEmpty(paletteSourceId) || !palettes.TryGetRow(ContentId.FromSource(paletteSourceId), out int row))
                return EnvironmentVisualPacking.Pack(0, WorldChunk.NeutralBrightness);
            return EnvironmentVisualPacking.Pack(row, WorldChunk.NeutralBrightness);
        }

        /// <summary>Puts the view's animal accessories on it and keeps them in line with the save.</summary>
        public void Dress(AnimalView view)
        {
            if (view == null)
                return;
            AccessoryView.Ensure(view).Show(this, view.OwnerInstanceId);
        }

        internal void Track(AccessoryView view)
        {
            if (!_dressed.Contains(view))
                _dressed.Add(view);
        }

        internal Piece Take(ContentId accessory, int palette, Transform socket, in AccessoryFit fit)
        {
            if (_root == null || !_kindById.TryGetValue(accessory, out int k))
                return null;
            var kind = _kinds[k];
            Piece piece = null;
            while (kind.Free.Count > 0 && piece == null)
            {
                piece = kind.Free.Pop();
                if (piece.GameObject == null)
                    piece = null;
            }
            piece ??= Create(k);
            var t = piece.Transform;
            t.SetParent(socket, false);
            t.localPosition = fit.LocalPosition;
            t.localRotation = Quaternion.Euler(fit.LocalEuler);
            t.localScale = fit.LocalScale.sqrMagnitude > 1e-8f ? fit.LocalScale : Vector3.one;
            uint value = kind.Values[(uint)palette < (uint)kind.Values.Length ? palette : 0];
            for (int r = 0; r < piece.Renderers.Length; r++)
                piece.Renderers[r].SetShaderUserValue(value);
            piece.GameObject.SetActive(true);
            return piece;
        }

        internal void Return(Piece piece)
        {
            if (piece.GameObject == null || _root == null)
                return;
            piece.GameObject.SetActive(false);
            piece.Transform.SetParent(_root, false);
            _kinds[piece.Kind].Free.Push(piece);
        }

        private Piece Create(int k)
        {
            var kind = _kinds[k];
            var go = UnityEngine.Object.Instantiate(kind.Prefab, _root, false);
            go.name = kind.Prefab.name;
            go.SetActive(false);
            var piece = new Piece { Kind = k, Id = kind.Id, GameObject = go, Transform = go.transform, Renderers = go.GetComponentsInChildren<MeshRenderer>(true) };
            _all.Add(piece);
            return piece;
        }

        public void Dispose()
        {
            for (int i = 0; i < _dressed.Count; i++)
                if (_dressed[i] != null)
                    _dressed[i].Detach();
            _dressed.Clear();
            for (int i = 0; i < _all.Count; i++)
            {
                var go = _all[i].GameObject;
                if (go == null)
                    continue;
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(go);
                else
                    UnityEngine.Object.DestroyImmediate(go);
            }
            _all.Clear();
            if (_root != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(_root.gameObject);
                else
                    UnityEngine.Object.DestroyImmediate(_root.gameObject);
            }
            _root = null;
        }
    }
}
