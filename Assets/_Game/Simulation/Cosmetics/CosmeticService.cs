#nullable enable
using System;
using System.Collections.Generic;
using PocketWilds.Content;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>Why an accessory can or cannot be worn. Stable byte values (analytics, UI messages).</summary>
    public enum EquipResult : byte
    {
        Ok = 0,
        UnknownAnimal = 1,
        UnknownAccessory = 2,
        NotOwned = 3,

        /// <summary>The accessory hangs on another socket.</summary>
        WrongSocket = 4,

        /// <summary>The animal's rig family has no fitting profile.</summary>
        RigNotCompatible = 5,

        /// <summary>The species is excluded by the accessory, or hides that socket (the hamster's TailBase).</summary>
        SpeciesExcluded = 6,

        UnknownPalette = 7,

        /// <summary>An outfit with no accessory, more than 3, or two on one socket.</summary>
        InvalidOutfit = 8,
    }

    public delegate void AccessoryEquippedHandler(uint animal, ContentId accessory, ContentId species, AccessorySocket socket);

    /// <summary>
    /// T47 accessories and outfits (doc Accessoires). Owning goes through <see cref="EntitlementState"/> (the accessory id is the
    /// item id); wearing is stored per animal and socket in <see cref="CosmeticState"/>. An accessory fits when the animal's rig
    /// family has a profile, the species is not excluded and does not hide the socket. <see cref="Changed"/> tells the view which
    /// animal to dress again. Reads allocate nothing.
    /// </summary>
    public sealed class CosmeticService
    {
        private readonly PlayerState _state;
        private readonly ContentRegistry _content;
        private readonly ISaveScheduler? _save;
        private readonly List<uint> _scratch = new List<uint>(8);
        private readonly Action<GrantResult> _onRevoked;
        private GrantService? _grants;

        public CosmeticService(PlayerState state, ContentRegistry content, ISaveScheduler? save)
        {
            _state = state;
            _content = content;
            _save = save;
            _onRevoked = _ => Prune();
        }

        /// <summary>A refund or revoke takes the items back: what they were worn on comes off (<see cref="Prune"/>).</summary>
        public GrantService? Grants
        {
            get => _grants;
            set
            {
                if (_grants != null)
                    _grants.Revoked -= _onRevoked;
                _grants = value;
                if (_grants != null)
                    _grants.Revoked += _onRevoked;
            }
        }

        /// <summary>What an animal wears changed (equip, unequip, outfit, prune): the view rebuilds its accessories.</summary>
        public event Action<uint>? Changed;

        /// <summary>One accessory put on by the player (analytics outfit_equip: item, species, socket).</summary>
        public event AccessoryEquippedHandler? Equipped;

        public ContentTable<AccessoryDefinition> Accessories => _content.Shop.Accessories;
        public ContentTable<OutfitDefinition> Outfits => _content.Shop.Outfits;

        public bool Owns(ContentId accessory) => _state.Entitlements.Owned.Contains(accessory);

        /// <summary>The accessory worn on a socket (empty when none).</summary>
        public EquippedAccessory GetEquipped(uint animal, AccessorySocket socket) => _state.Cosmetics.Get(animal, socket);

        /// <summary>
        /// Whether the animal can wear the accessory on this socket: known, owned, the accessory's socket (ears swap freely),
        /// a compatible rig family, a species neither excluded nor hiding the socket.
        /// </summary>
        public EquipResult CanEquip(uint animal, ContentId accessory, AccessorySocket socket)
        {
            if (!_state.Collection.TryGet(animal, out var instance) || !_content.Species.TryGet(instance.Species, out var species))
                return EquipResult.UnknownAnimal;
            if (!_content.Shop.Accessories.TryGet(accessory, out var definition))
                return EquipResult.UnknownAccessory;
            if (!Owns(accessory))
                return EquipResult.NotOwned;
            return CanWear(species, definition, socket);
        }

        /// <summary>Species-level rule, ownership aside (shop previews, the Codex): socket, rig family, exclusions.</summary>
        public static EquipResult CanWear(SpeciesDefinition species, AccessoryDefinition accessory, AccessorySocket socket)
        {
            if (!SocketMatches(accessory.Socket, socket))
                return EquipResult.WrongSocket;
            if (!accessory.FitsFamily(species.RigFamily))
                return EquipResult.RigNotCompatible;
            if (accessory.Excludes(species.Id) || species.HidesSocket(socket))
                return EquipResult.SpeciesExcluded;
            return EquipResult.Ok;
        }

        /// <summary>Puts an owned accessory on a socket with a palette index (replaces what was there).</summary>
        public EquipResult Equip(uint animal, ContentId accessory, AccessorySocket socket, int palette = 0)
        {
            var result = CanEquip(animal, accessory, socket);
            if (result != EquipResult.Ok)
                return result;
            var definition = _content.Shop.Accessories.Get(accessory);
            if ((uint)palette >= (uint)definition.Palettes.Length)
                return EquipResult.UnknownPalette;
            Put(animal, accessory, socket, palette);
            _save?.RequestSave(SaveUrgency.Normal);
            Changed?.Invoke(animal);
            return EquipResult.Ok;
        }

        /// <summary>Takes off what a socket holds. False when it was empty.</summary>
        public bool Unequip(uint animal, AccessorySocket socket)
        {
            if (_state.Cosmetics.Get(animal, socket).IsEmpty)
                return false;
            _state.Cosmetics.Set(animal, socket, default);
            _save?.RequestSave(SaveUrgency.Normal);
            Changed?.Invoke(animal);
            return true;
        }

        /// <summary>Takes off everything an animal wears. False when it wore nothing.</summary>
        public bool UnequipAll(uint animal)
        {
            if (!_state.Cosmetics.Animals.Remove(animal))
                return false;
            _save?.RequestSave(SaveUrgency.Normal);
            Changed?.Invoke(animal);
            return true;
        }

        /// <summary>
        /// Wears a whole outfit (1 to 3 accessories on different sockets, its shared palette), all or nothing: when one piece
        /// cannot be worn, nothing changes and its reason is returned.
        /// </summary>
        public EquipResult EquipOutfit(uint animal, OutfitDefinition outfit)
        {
            var pieces = outfit.Accessories;
            if (pieces.Length < 1 || pieces.Length > OutfitDefinition.MaxAccessories)
                return EquipResult.InvalidOutfit;
            int sockets = 0;
            for (int i = 0; i < pieces.Length; i++)
            {
                if (!_content.Shop.Accessories.TryGet(pieces[i], out var definition))
                    return EquipResult.UnknownAccessory;
                int bit = 1 << (int)definition.Socket;
                if ((sockets & bit) != 0)
                    return EquipResult.InvalidOutfit;
                sockets |= bit;
                var result = CanEquip(animal, pieces[i], definition.Socket);
                if (result != EquipResult.Ok)
                    return result;
                if (definition.PaletteIndex(outfit.Palette) < 0)
                    return EquipResult.UnknownPalette;
            }
            for (int i = 0; i < pieces.Length; i++)
            {
                var definition = _content.Shop.Accessories.Get(pieces[i]);
                Put(animal, pieces[i], definition.Socket, definition.PaletteIndex(outfit.Palette));
            }
            _save?.RequestSave(SaveUrgency.Normal);
            Changed?.Invoke(animal);
            return EquipResult.Ok;
        }

        /// <summary>
        /// What the view needs to hang one socket's accessory: its definition, the palette name and the fit (the species'
        /// measured fit, else its family profile). False when the socket is empty or the data is gone.
        /// </summary>
        public bool TryGetWorn(uint animal, AccessorySocket socket, out AccessoryDefinition accessory, out string palette, out AccessoryFit fit)
        {
            accessory = null!;
            palette = string.Empty;
            fit = AccessoryFit.Identity;
            var worn = _state.Cosmetics.Get(animal, socket);
            if (worn.IsEmpty || !_content.Shop.Accessories.TryGet(worn.Accessory, out accessory)
                || !_state.Collection.TryGet(animal, out var instance) || !_content.Species.TryGet(instance.Species, out var species))
                return false;
            palette = worn.Palette < accessory.Palettes.Length ? accessory.Palettes[worn.Palette] : accessory.Palettes[0];
            return accessory.TryGetFit(species.Id, species.RigFamily, out fit);
        }

        /// <summary>
        /// After load and after a revoke: drops what is no longer valid (animal gone, unknown or no longer owned item, socket
        /// the animal cannot wear, palette out of range). Returns the number of slots cleared.
        /// </summary>
        public int Prune()
        {
            int cleared = 0;
            _scratch.Clear();
            foreach (var pair in _state.Cosmetics.Animals)
                _scratch.Add(pair.Key);
            for (int k = 0; k < _scratch.Count; k++)
            {
                uint animal = _scratch[k];
                var slots = _state.Cosmetics.Animals[animal];
                bool changed = false;
                for (int s = 0; s < slots.Length; s++)
                {
                    var worn = slots[s];
                    if (worn.IsEmpty)
                        continue;
                    bool keep = CanEquip(animal, worn.Accessory, (AccessorySocket)s) == EquipResult.Ok
                        && worn.Palette < _content.Shop.Accessories.Get(worn.Accessory).Palettes.Length;
                    if (keep)
                        continue;
                    slots[s] = default;
                    changed = true;
                    cleared++;
                }
                if (!changed)
                    continue;
                if (CosmeticState.IsBare(slots))
                    _state.Cosmetics.Animals.Remove(animal);
                Changed?.Invoke(animal);
            }
            if (cleared > 0)
                _save?.RequestSave(SaveUrgency.Normal);
            return cleared;
        }

        private void Put(uint animal, ContentId accessory, AccessorySocket socket, int palette)
        {
            _state.Cosmetics.Set(animal, socket, new EquippedAccessory { Accessory = accessory, Palette = (byte)palette });
            if (Equipped != null && _state.Collection.TryGet(animal, out var instance))
                Equipped.Invoke(animal, accessory, instance.Species, socket);
        }

        private static bool SocketMatches(AccessorySocket own, AccessorySocket socket) =>
            own == socket || ((own == AccessorySocket.EarL || own == AccessorySocket.EarR) && (socket == AccessorySocket.EarL || socket == AccessorySocket.EarR));
    }
}
