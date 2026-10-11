using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Services.Feedback;
using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T47 screens: the « Tenue » button of the animal sheet opens the outfit picker, a bottom panel over the refuge while the
    /// camera keeps the animal in view above it (the real animal wears each change live); « Voir le pack » opens the pack's
    /// offer; the Codex's Companions tab opens the pack's offer from a silhouette, changes the companion's look and opens the
    /// picker on the companion.
    /// </summary>
    public sealed partial class RefugeController : IAnimalSheetOutfitActions, IOutfitPickerActions, ICodexCompanionActions
    {
        /// <summary>The focus slides toward the camera by this share of the distance: the animal sits above the panel.</summary>
        private const float OutfitFocusShift = 0.22f;

        private IOutfitPickerView _outfitView;
        private OutfitPicker _outfit;
        private uint _pendingOutfit;
        private bool _outfitOfferShown;

        /// <summary>PF_UI_OutfitPicker, set by the scene entry after construction (prewarmed at once).</summary>
        public IOutfitPickerView OutfitView
        {
            get => _outfitView;
            set
            {
                _outfitView = value;
                _outfitView?.Prewarm(_context.Localization);
            }
        }

        public bool OutfitOpen => _outfitView != null && _outfitView.IsOpen;

        /// <summary>The animal the picker dresses (0 when closed).</summary>
        public uint OutfitAnimal => OutfitOpen && _outfit != null ? _outfit.Animal : 0u;

        /// <summary>The sheet's « Tenue » shows when an owned accessory fits this animal, or the shop can offer the pack.</summary>
        private void RefreshSheetOutfit(uint instanceId)
        {
            if (!(_sheetView is IAnimalSheetOutfitView outfit))
                return;
            outfit.SetOutfit(_outfitView != null && _context.Cosmetics != null && (AnyAccessoryFits(instanceId) || PackOfferAvailable()));
        }

        private bool AnyAccessoryFits(uint instanceId)
        {
            var cosmetics = _context.Cosmetics;
            var accessories = cosmetics.Accessories;
            for (int i = 0; i < accessories.Count; i++)
                for (int s = 0; s < OutfitPicker.Sockets.Length; s++)
                    if (cosmetics.CanEquip(instanceId, accessories[i].Id, OutfitPicker.Sockets[s]) == EquipResult.Ok)
                        return true;
            return false;
        }

        private bool PackOfferAvailable()
        {
            if (_shop == null || !_shop.Unlocked || _context.Cosmetics == null)
                return false;
            var accessories = _context.Cosmetics.Accessories;
            for (int i = 0; i < accessories.Count; i++)
                if (_shop.HasOfferGranting(accessories[i].Id))
                    return true;
            return false;
        }

        // ---------------- IAnimalSheetOutfitActions ----------------

        public void OpenOutfit(uint instanceId)
        {
            if (_outfitView == null || _context.Cosmetics == null || Hatching)
                return;
            var collection = _context.State.Collection;
            if (!collection.TryGet(instanceId, out var animal) || !_context.Content.Species.TryGet(animal.Species, out var species))
                return;
            // The sheet closes at once: the picker takes the bottom of the screen, the refuge stays above it.
            _sheetView?.Close();
            _outfit ??= new OutfitPicker(_context.Cosmetics);
            _outfit.Bind(instanceId, species);
            _outfit.ShopAvailable = PackOfferAvailable();
            string name = TryBuildSheet(instanceId, out var data) ? data.Name : string.Empty;
            _outfitView.Open(_outfit, name, _context.Localization, this);
            if (_input != null)
                _input.Blocked = true;
            FocusOutfitAnimal();
            _context.Audio.Play(AudioEventId.PanelOpen, Vector3.zero, default);
            _context.Haptics.Play(HapticEventId.TapLight);
        }

        // ---------------- IOutfitPickerActions ----------------

        public void EquipAccessory(uint animal, ContentId accessory, AccessorySocket socket, int palette)
        {
            var cosmetics = _context.Cosmetics;
            if (cosmetics == null || cosmetics.Equip(animal, accessory, socket, palette) != EquipResult.Ok)
            {
                _context.Audio.Play(AudioEventId.ErrorSoft, Vector3.zero, default);
                return;
            }
            _context.Audio.Play(AudioEventId.ToggleOn, Vector3.zero, default);
            _context.Haptics.Play(HapticEventId.Confirm);
            RefreshOutfit();
        }

        public void RemoveAccessory(uint animal, AccessorySocket socket)
        {
            if (_context.Cosmetics == null || !_context.Cosmetics.Unequip(animal, socket))
                return;
            _context.Audio.Play(AudioEventId.ToggleOff, Vector3.zero, default);
            _context.Haptics.Play(HapticEventId.TapLight);
            RefreshOutfit();
        }

        public void OpenPackOffer()
        {
            if (_shop == null || _context.Cosmetics == null)
                return;
            var accessories = _context.Cosmetics.Accessories;
            for (int i = 0; i < accessories.Count; i++)
            {
                if (_shop.TryOpenOfferGranting(accessories[i].Id))
                {
                    _outfitOfferShown = true;
                    return;
                }
            }
        }

        public void OutfitClosed()
        {
            _outfitOfferShown = false;
            _rig?.ReturnToWorld();
            if (_input != null)
                _input.Blocked = false;
            _context.Audio.Play(AudioEventId.PanelClose, Vector3.zero, default);
        }

        private void RefreshOutfit()
        {
            if (_outfit == null || !OutfitOpen)
                return;
            _outfit.ShopAvailable = PackOfferAvailable();
            _outfit.Rebuild();
            _outfitView.Refresh();
        }

        /// <summary>Called from the refuge's Tick: the panel's motion, the camera on the animal, the pack bought from the picker.</summary>
        private void TickOutfit(float deltaTime)
        {
            if (_outfitView == null)
                return;
            _outfitView.Tick(deltaTime);
            if (!OutfitOpen)
                return;
            FocusOutfitAnimal();
            if (_outfitOfferShown && (_shop == null || !_shop.OfferOpen))
            {
                // The pack's offer closed (bought or not): the picker shows what is owned now.
                _outfitOfferShown = false;
                RefreshOutfit();
            }
        }

        private void FocusOutfitAnimal()
        {
            if (_rig == null || _outfit == null)
                return;
            int index = ViewIndexOf(_outfit.Animal);
            if (index < 0)
                return;
            var position = _scheduler.GetPosition(_agentByView[index]);
            var forward = _rig.Camera != null ? _rig.Camera.transform.forward : Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f)
                forward = Vector3.forward;
            forward.Normalize();
            float scale = _views[index].transform.localScale.x;
            _rig.FocusOn(position - forward * (_rig.Distance * OutfitFocusShift) + new Vector3(0f, 0.12f * scale, 0f), RefugeCameraState.FocusAnimal);
        }

        // ---------------- ICodexCompanionActions ----------------

        private void BindCodexCompanions() => (_codexView as ICodexCompanionView)?.BindCompanions(_context.Companions, _context.Content, _context.State);

        public void OpenCompanionOffer(ContentId companionItem)
        {
            if (_shop != null && _shop.TryOpenOfferGranting(companionItem))
                _context.Audio.Play(AudioEventId.PanelOpen, Vector3.zero, default);
        }

        public void OpenCompanionOutfit(ContentId species)
        {
            // The Codex fades out; the picker opens once it has closed (CodexClosed).
            _pendingOutfit = _context.Companions != null ? _context.Companions.AnimalOf(species) : 0u;
        }

        public bool SelectCompanionLook(ContentId species, ContentId look)
        {
            if (_context.Companions == null || !_context.Companions.SetLook(species, look))
                return false;
            _context.Audio.Play(AudioEventId.ToggleOn, Vector3.zero, default);
            _context.Haptics.Play(HapticEventId.Confirm);
            return true;
        }

        /// <summary>After the Codex closed: the outfit picker asked from a companion card.</summary>
        private void OpenPendingOutfit()
        {
            uint animal = _pendingOutfit;
            _pendingOutfit = 0;
            if (animal != 0)
                OpenOutfit(animal);
        }
    }
}
