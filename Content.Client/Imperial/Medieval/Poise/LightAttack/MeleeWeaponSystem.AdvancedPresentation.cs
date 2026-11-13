using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client.Effects;
using Content.Client.Imperial.Medieval.Poise.LightAttack;
using Content.Client.Weapons.Melee.Components;
using Content.Shared.Imperial.Medieval.Poise.LightAttack;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Spawners;

// This partial reuses the existing private animation builders without changing
// their curves or exposing engine internals. All new playback lives in Imperial.
namespace Content.Client.Weapons.Melee;

public sealed partial class MeleeWeaponSystem
{
    private readonly Dictionary<EntityUid, ObserverMeleeSwing> _observerLunges = new();

    public ObserverMeleeSwing? CreateObserverMeleeSwing(EntityUid user, EntityUid weapon, AdvancedMeleeVisualData data,
        float initialElapsed = 0f, TimeSpan strikeTime = default)
    {
        if (!_xformQuery.TryGetComponent(user, out var xform) || xform.MapID == MapId.Nullspace || !ValidObserverData(data))
            return null;
        var swing = new ObserverMeleeSwing(this, user, weapon, strikeTime);
        var lungeDuration = (float) GetLungeAnimation(data.WorldOffset).Length.TotalSeconds;
        // A late old attack may still have an arc to finish, but must not stop
        // a newer offhand lunge or reset the actor for an already expired lunge.
        var hasPrevious = _observerLunges.TryGetValue(user, out var previous);
        if (initialElapsed < lungeDuration && (!hasPrevious || strikeTime >= previous!.StrikeTime))
        {
            _animation.Stop(user, MeleeLungeKey);
            previous?.StopLunge(reset: true);
            _observerLunges[user] = swing;
        }
        swing.Reconfigure(data);
        return swing;
    }

    private static bool ValidObserverData(AdvancedMeleeVisualData data)
        => float.IsFinite(data.WorldOffset.X) && float.IsFinite(data.WorldOffset.Y) &&
           float.IsFinite(data.WorldOffset.LengthSquared()) && data.WorldOffset.LengthSquared() > 0f &&
           float.IsFinite(data.Length) && data.Length >= 0f && float.IsFinite(data.Offset) &&
           double.IsFinite(data.Angle.Theta) && double.IsFinite(data.SpriteRotation.Theta);

    public sealed class ObserverMeleeSwing : IDisposable
    {
        private readonly MeleeWeaponSystem _owner;
        public readonly EntityUid User;
        public readonly EntityUid Weapon;
        public readonly TimeSpan StrikeTime;
        public AdvancedMeleeVisualData? Data { get; private set; }
        public EntityUid? Arc { get; private set; }
        private AdvancedMeleeAnimationPlayback? _lunge;
        private AdvancedMeleeAnimationPlayback? _arc;
        private AdvancedMeleeAnimationPlayback? _fade;
        private bool _follow;
        private float _arcLifetime;
        private float? _despawnLifetime;
        private readonly List<SpriteComponent.Layer> _animatedLayers = new();
        private Color _initialColor;
        private bool _initialNoRotation;
        private bool _disposed;

        internal ObserverMeleeSwing(MeleeWeaponSystem owner, EntityUid user, EntityUid weapon, TimeSpan strikeTime)
        {
            _owner = owner;
            User = user;
            Weapon = weapon;
            StrikeTime = strikeTime;
        }

        private bool OwnsLunge => _owner._observerLunges.TryGetValue(User, out var current) && current == this;

        public void Reconfigure(AdvancedMeleeVisualData data)
        {
            if (_disposed || !ValidObserverData(data) || data.Equals(Data) || !_owner._xformQuery.TryGetComponent(User, out var userXform))
                return;
            var changedPrototype = Data?.Animation != data.Animation;
            Data = data;
            var parentRotation = userXform.ParentUid.IsValid()
                ? _owner.TransformSystem.GetWorldRotation(userXform.ParentUid) : Angle.Zero;
            var localPos = (-parentRotation).RotateVec(data.WorldOffset);
            if (OwnsLunge && _owner.HasComp<SpriteComponent>(User))
                _lunge = new AdvancedMeleeAnimationPlayback(_owner.GetLungeAnimation(localPos), User);

            if (changedPrototype)
                DeleteArc();
            if (data.Animation == null)
                return;
            if (Arc is not { } uid || _owner.TerminatingOrDeleted(uid))
            {
                uid = _owner.Spawn(data.Animation, userXform.Coordinates);
                Arc = uid;
                if (!_owner.TryComp<SpriteComponent>(uid, out var createdSprite) ||
                    !_owner.TryComp<WeaponArcVisualsComponent>(uid, out var createdArc))
                {
                    DeleteArc();
                    return;
                }
                var copiedWeaponSprite = false;
                if (createdArc.Animation != WeaponArcAnimation.None && User != Weapon &&
                    _owner.TryComp<SpriteComponent>(Weapon, out var weaponSprite))
                {
                    _owner._sprite.CopySprite((Weapon, weaponSprite), (uid, createdSprite));
                    copiedWeaponSprite = true;
                }
                _initialColor = createdSprite.Color;
                _initialNoRotation = createdSprite.NoRotation;
                // Sprite-sheet frames use the same absolute clock as the tracks.
                // Otherwise a late punch/bite starts its frames at packet arrival.
                // Copied weapon textures retain their independent animation phase.
                for (var index = 0; !copiedWeaponSprite &&
                     _owner._sprite.TryGetLayer((uid, createdSprite), index, out var layer, false); index++)
                {
                    if (!layer.AutoAnimated || layer.ActualState is not { IsAnimated: true })
                        continue;
                    _animatedLayers.Add(layer);
                    _owner._sprite.LayerSetAutoAnimated(layer, false);
                }
                // Run despawn on the absolute clock instead of packet arrival.
                _despawnLifetime = null;
                if (_owner.TryComp<TimedDespawnComponent>(uid, out var despawn))
                {
                    _despawnLifetime = MathF.Max(0f, despawn.Lifetime);
                    _owner.RemComp<TimedDespawnComponent>(uid);
                }
            }
            if (!_owner.TryComp<SpriteComponent>(uid, out var sprite) ||
                !_owner.TryComp<WeaponArcVisualsComponent>(uid, out var arc))
                return;
            _owner._sprite.SetColor((uid, sprite), _initialColor);
            _owner._sprite.SetOffset((uid, sprite), Vector2.Zero);
            sprite.NoRotation = _initialNoRotation;
            _owner._sprite.SetRotation((uid, sprite), localPos.ToWorldAngle());
            var angle = data.SwingLeft ? -data.Angle : data.Angle;
            var distance = Math.Clamp(localPos.Length() / 2f, 0.2f, 1f);
            _arc = null;
            _fade = null;
            _follow = arc.Animation != WeaponArcAnimation.None;
            switch (arc.Animation)
            {
                case WeaponArcAnimation.Slash:
                    _arc = new AdvancedMeleeAnimationPlayback(_owner.GetSlashAnimation(sprite, angle, data.SpriteRotation), uid);
                    if (arc.Fadeout)
                        _fade = new AdvancedMeleeAnimationPlayback(_owner.GetFadeAnimation(sprite, 0.065f, 0.115f), uid);
                    break;
                case WeaponArcAnimation.Thrust:
                    _arc = new AdvancedMeleeAnimationPlayback(_owner.GetThrustAnimation((uid, sprite), distance, data.SpriteRotation), uid);
                    if (arc.Fadeout)
                        _fade = new AdvancedMeleeAnimationPlayback(_owner.GetFadeAnimation(sprite, 0.05f, 0.15f), uid);
                    break;
                case WeaponArcAnimation.CPSlash:
                    _arc = new AdvancedMeleeAnimationPlayback(_owner.CPGetSlashAnimation(sprite, angle, data.SpriteRotation, data.Length, data.Offset), uid);
                    if (arc.Fadeout)
                        _fade = new AdvancedMeleeAnimationPlayback(_owner.GetFadeAnimation(sprite, data.Length * 0.5f, data.Length + 0.15f), uid);
                    break;
                case WeaponArcAnimation.CPThrust:
                    _arc = new AdvancedMeleeAnimationPlayback(_owner.CPGetThrustAnimation(sprite, -data.Offset * 2f, data.SpriteRotation, data.Length), uid);
                    if (arc.Fadeout)
                        _fade = new AdvancedMeleeAnimationPlayback(_owner.GetFadeAnimation(sprite, data.Length * 0.5f, data.Length + 0.15f), uid);
                    break;
                case WeaponArcAnimation.None:
                    _owner.TransformSystem.SetWorldPosition(uid, _owner.TransformSystem.GetWorldPosition(User) + data.WorldOffset);
                    if (arc.Fadeout)
                        _fade = new AdvancedMeleeAnimationPlayback(_owner.GetFadeAnimation(sprite, 0f, 0.15f), uid);
                    break;
            }
            _arcLifetime = _despawnLifetime ?? MathF.Max(_arc?.Duration ?? 0f, MathF.Max(_fade?.Duration ?? 0f, 0.2f));
            // Native EffectVisuals deletes on the FIRST completed animation,
            // even when TimedDespawn allows longer. Manual tracks emit no engine
            // completion event, so preserve that earlier deadline explicitly.
            if (_owner.HasComp<EffectVisualsComponent>(uid))
            {
                if (_arc != null)
                    _arcLifetime = MathF.Min(_arcLifetime, _arc.Duration);
                if (_fade != null)
                    _arcLifetime = MathF.Min(_arcLifetime, _fade.Duration);
            }
        }

        public bool Sample(float elapsed)
        {
            if (_disposed || !float.IsFinite(elapsed) || _owner.TerminatingOrDeleted(User))
                return false;
            var active = false;
            if (_lunge is { } lunge)
            {
                // A newer ordinary/predicted lunge owns the actor's sprite now.
                if (!OwnsLunge || _owner._animation.HasRunningAnimation(User, MeleeLungeKey))
                    StopLunge(reset: false);
                else
                {
                    lunge.Sample(elapsed);
                    if (elapsed >= lunge.Duration)
                        StopLunge(reset: true);
                    else
                        active = true;
                }
            }
            if (Arc is { } uid && !_owner.TerminatingOrDeleted(uid))
            {
                if (elapsed >= _arcLifetime)
                    DeleteArc();
                else
                {
                    if (_follow)
                        _owner.TransformSystem.SetWorldPosition(uid, _owner.TransformSystem.GetWorldPosition(User));
                    _arc?.Sample(elapsed);
                    _fade?.Sample(elapsed);
                    foreach (var layer in _animatedLayers)
                        _owner._sprite.LayerSetAnimationTime(layer, elapsed);
                    active = true;
                }
            }
            return active;
        }

        internal void StopLunge(bool reset)
        {
            _lunge = null;
            if (!OwnsLunge)
                return;
            _owner._observerLunges.Remove(User);
            if (reset && !_owner._animation.HasRunningAnimation(User, MeleeLungeKey) &&
                _owner.TryComp<SpriteComponent>(User, out var sprite))
                _owner._sprite.SetOffset((User, sprite), Vector2.Zero);
        }

        private void DeleteArc()
        {
            if (Arc is { } uid && !_owner.TerminatingOrDeleted(uid))
            {
                if (_owner.TryComp<SpriteComponent>(uid, out var sprite))
                    _owner._sprite.SetVisible((uid, sprite), false);
                _owner.QueueDel(uid);
            }
            Arc = null;
            _arc = null;
            _fade = null;
            _animatedLayers.Clear();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            StopLunge(reset: true);
            DeleteArc();
            _disposed = true;
        }
    }
}
