using System;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Services.Feedback;
using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T46 « Couleurs de maîtrise », the refuge's side. When <see cref="MasteryColorService.Granted"/> says a species reached
    /// mastery 10, 20 or 30, the new animal appears beside the egg on a clear spot (no prop, no water, no animal under its
    /// body: nothing enters anything), living as the others do, and a Discovery toast names the color. A grant that comes
    /// during a hatch or a screen waits for it to end. No allocation per frame (the toast text is made once per grant).
    /// </summary>
    public sealed partial class RefugeController
    {
        /// <summary>Colors waiting for the hatch or screen to end (a species can pass 10, 20 and 30 in one go).</summary>
        private const int MasteryColorQueue = 8;

        public const string MasteryColorToastKey = "mastery.color.toast";

        private const float MasteryColorBodyRadius = 0.38f;
        private const float MasteryColorToastDelay = 0.4f;

        private MasteryColorService _masteryColors;
        private MasteryColorHandler _onMasteryColor;
        private readonly MasteryColorGrant[] _masteryQueue = new MasteryColorGrant[MasteryColorQueue];
        private int _masteryHead;
        private int _masteryCount;
        private bool _masteryToasted;
        private float _masteryReadyAt;

        /// <summary>T46: the mastery colors (set by the scene entry; null in scenes without them). Set it to null before the refuge goes.</summary>
        public MasteryColorService MasteryColors
        {
            set
            {
                if (ReferenceEquals(_masteryColors, value))
                    return;
                if (_masteryColors != null && _onMasteryColor != null)
                    _masteryColors.Granted -= _onMasteryColor;
                _masteryColors = value;
                _onMasteryColor ??= OnMasteryColorGranted;
                if (_masteryColors != null)
                    _masteryColors.Granted += _onMasteryColor;
                else
                {
                    _masteryHead = 0;
                    _masteryCount = 0;
                }
            }
        }

        /// <summary>Tests and diagnostics: mastery colors still waiting to appear.</summary>
        public int MasteryColorsPending => _masteryCount;

        private void OnMasteryColorGranted(in MasteryColorGrant grant)
        {
            if (_masteryCount >= MasteryColorQueue)
                return;
            int tail = (_masteryHead + _masteryCount) % MasteryColorQueue;
            _masteryQueue[tail] = grant;
            if (_masteryCount == 0)
            {
                _masteryToasted = false;
                _masteryReadyAt = -1f;
            }
            _masteryCount++;
        }

        /// <summary>Per frame from <see cref="Tick"/>: nothing to do while the queue is empty.</summary>
        private void TickMasteryColors(float time)
        {
            if (_masteryCount == 0)
                return;
            // The egg and the screens come first: the color arrives when the world is free again.
            if (Hatching || ModalOpen)
                return;
            if (_masteryReadyAt < 0f)
                _masteryReadyAt = time + MasteryColorToastDelay;
            if (time < _masteryReadyAt)
                return;
            var grant = _masteryQueue[_masteryHead];
            if (!_masteryToasted)
            {
                _masteryToasted = true;
                _hud?.ShowToast(ToastKind.Discovery, MasteryColorText(in grant));
            }
            if (!_context.State.Collection.TryGet(grant.InstanceId, out var animal) || IsVisible(grant.InstanceId))
            {
                NextMasteryColor(time);
                return;
            }
            if (!FindMasteryColorSpot(grant.InstanceId, out var position, out float yaw))
                return;
            if (!_context.Visuals.TryResolve(animal, position, yaw, out var snapshot) || !TryAcquire(snapshot, out var view))
            {
                NextMasteryColor(time);
                return;
            }
            AddToMeadow(view, animal.Species, grant.InstanceId, animal.Seed, RareGlow.KindOf(animal.Rarity, animal.TraitBits), emerging: true);
            _vfx?.Play(VfxId.FavoriteHeart, position + new Vector3(0f, 0.3f, 0f), time);
            int index = ViewIndexOf(grant.InstanceId);
            if (index >= 0)
                ShowBadge(_agentByView[index]);
            NextMasteryColor(time);
        }

        private void NextMasteryColor(float time)
        {
            _masteryHead = (_masteryHead + 1) % MasteryColorQueue;
            _masteryCount--;
            _masteryToasted = false;
            _masteryReadyAt = time + MasteryColorToastDelay;
        }

        private string MasteryColorText(in MasteryColorGrant grant)
        {
            var loc = _context.Localization;
            string name = _context.Content.Variants.TryGet(grant.Variant, out var variant) ? loc.Get(variant.DisplayKey) : string.Empty;
            return loc.Get(MasteryColorToastKey).Replace("{variant}", name);
        }

        /// <summary>The first clear spot on two rings around the egg (a body apart from props, water and animals).</summary>
        private bool FindMasteryColorSpot(uint instanceId, out Vector3 position, out float yaw)
        {
            const int steps = 16;
            int offset = (int)(instanceId % steps);
            for (int ring = 0; ring < 2; ring++)
            {
                float radius = ring == 0 ? 1.3f : 1.9f;
                for (int n = 0; n < steps; n++)
                {
                    float angle = (n + offset) % steps * (Mathf.PI * 2f / steps);
                    var point = _eggCenter + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    var ground = Ground(point);
                    if (!_scheduler.IsDiskClear(ground, MasteryColorBodyRadius, MultiEggHeight, withAnimals: true))
                        continue;
                    position = ground;
                    var toEgg = _eggCenter - ground;
                    yaw = Mathf.Atan2(toEgg.x, toEgg.z) * Mathf.Rad2Deg;
                    return true;
                }
            }
            position = default;
            yaw = 0f;
            return false;
        }
    }
}
