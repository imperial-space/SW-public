// Imperial medieval start
using System.Numerics;
using Content.Client.Projectiles;
using Content.Shared.Imperial.Medieval.Archery; // Imperial medieval edit
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;

namespace Content.Client.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    [Dependency] private readonly ProjectilePredictionSystem _projectilePrediction = default!;

    private bool TryPredictProjectileAmmo(EntityUid gunUid, GunComponent gun,
        List<(EntityUid? Entity, IShootable Shootable)> ammo, EntityCoordinates from, EntityCoordinates to,
        EntityUid? user, out bool userImpulse)
    {
        userImpulse = true;
        if (!_projectilePrediction.Enabled || user != _player.LocalEntity || user == null ||
            ammo.Exists(a => a.Shootable is HitscanPrototype))
            return false;

        var launch = GetProjectileLaunch(gunUid, gun, from, to, user);
        foreach (var (entity, shootable) in ammo)
        {
            if (entity == null)
                continue;
            var source = entity.Value;
            switch (shootable)
            {
                case CartridgeAmmoComponent cartridge:
                    if (cartridge.Spent)
                    {
                        userImpulse = false;
                        Audio.PlayPredicted(gun.SoundEmpty, gunUid, user);
                        break;
                    }
                    PredictAmmo(cartridge.Prototype, null, launch);
                    SetCartridgeSpent(source, cartridge, true);
                    MuzzleFlash(gunUid, cartridge, launch.Direction.ToAngle(), user);
                    Audio.PlayPredicted(gun.SoundGunshotModified, gunUid, user);
                    Recoil(user, -launch.BaseAngle.ToVec(), gun.CameraRecoilScalarModified);
                    if (IsClientSide(source))
                        Del(source);
                    break;
                case AmmoComponent direct:
                    var prototype = MetaData(source).EntityPrototype?.ID;
                    if (prototype != null)
                        PredictAmmo(prototype, source, launch);
                    MuzzleFlash(gunUid, direct, launch.Direction.ToAngle(), user);
                    Audio.PlayPredicted(gun.SoundGunshotModified, gunUid, user);
                    Recoil(user, -launch.BaseAngle.ToVec(), gun.CameraRecoilScalarModified);
                    if (IsClientSide(source))
                        Del(source);
                    else
                        RemoveShootable(source);
                    break;
            }
        }
        return true;

        void PredictAmmo(string? prototype, EntityUid? source, ProjectileLaunch shot)
        {
            // Replay reproduces gun/ammunition state, while an existing local projectile keeps its flight.
            var key = NextProjectilePredictionKey(gunUid);
            if (_projectilePrediction.Contains(key) || !Timing.IsFirstTimePredicted)
            {
                SkipProjectilePrediction();
                return;
            }
            var projectile = Spawn(prototype, shot.Origin);
            if (!TryComp<ProjectileComponent>(projectile, out var component) ||
                !component.Predict || component.PenetrationThreshold != 0 ||
                !_projectilePrediction.IsCompatible(projectile))
            {
                // No hidden authoritative sprite and no suppressed feedback without a local prediction.
                var skippedCount = TryComp<ProjectileSpreadComponent>(projectile, out var skippedSpread) ? skippedSpread.Count : 1;
                Del(projectile);
                for (var i = 0; i < skippedCount; i++)
                    SkipProjectilePrediction();
                return;
            }

            // Imperial medieval start - values select local feedback, never apply damage.
            if (TryComp<MedievalBowChargeComponent>(gunUid, out var charge) &&
                TryComp<PoiseArcheryComponent>(projectile, out var arrow))
            {
                var multiplier = EntityManager.System<MedievalBowChargeSystem>().GetChargeMultiplier(gunUid);
                component.Damage = MedievalBowChargeSystem.CalculateArrowHealthDamage(charge, arrow, multiplier);
            }
            // Imperial medieval end

            if (source != null && TryComp<SpriteComponent>(source, out var sourceSprite) &&
                TryComp<SpriteComponent>(projectile, out var sprite))
                _sprite.CopySprite((source.Value, sourceSprite), (projectile, sprite));

            var count = 1;
            var spread = Angle.Zero;
            string? additionalPrototype = null;
            if (TryComp<ProjectileSpreadComponent>(projectile, out var pellets))
            {
                count = pellets.Count;
                var ev = new Content.Shared.Weapons.Ranged.Events.GunGetAmmoSpreadEvent(pellets.Spread);
                RaiseLocalEvent(gunUid, ref ev);
                spread = ev.Spread;
                additionalPrototype = pellets.Proto;
            }
            for (var i = 0; i < count; i++)
            {
                var uid = i == 0 ? projectile : Spawn(additionalPrototype, shot.Origin);
                var direction = count > 1
                    ? new Angle(shot.BaseAngle.Theta - spread.Theta / 2 + spread.Theta * i / (count - 1)).ToVec()
                    : shot.Direction;
                if (gun.Target is { } target && !TerminatingOrDeleted(target))
                    EnsureComp<TargetedProjectileComponent>(uid).Target = target;
                MarkPredictedProjectile(uid, gunUid, user, i == 0 ? source : null);
                ShootProjectile(uid, direction, shot.InheritedVelocity, gunUid, user, gun.ProjectileSpeedModified);
            }
        }
    }
}
// Imperial medieval end
