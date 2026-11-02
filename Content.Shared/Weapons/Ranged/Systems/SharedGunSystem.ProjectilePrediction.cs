// Imperial medieval start
using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    protected GameTick ProjectileInputTick;
    private int _projectilePredictionIndex;

    public bool AttemptPredictedShoot(EntityUid user, EntityUid gunUid, GunComponent gun,
        EntityCoordinates coordinates, GameTick inputTick, bool resetShotCounter = true)
    {
        var previousTick = ProjectileInputTick;
        var previousIndex = _projectilePredictionIndex;
        ProjectileInputTick = inputTick;
        _projectilePredictionIndex = 0;
        try
        {
            gun.ShootCoordinates = coordinates;
            // Ordinary held-fire requests preserve the original fire-mode counter and clicked target.
            // Explicit single shots (including bow release) use the existing public firing API.
            var result = resetShotCounter
                ? AttemptShoot(user, gunUid, gun, coordinates)
                : AttemptShoot(user, gunUid, gun);
            Projectiles.CompletePrediction(GetNetEntity(gunUid), inputTick, user, _projectilePredictionIndex > 0);
            return result;
        }
        finally
        {
            ProjectileInputTick = previousTick;
            _projectilePredictionIndex = previousIndex;
        }
    }

    public void RejectProjectilePrediction(EntityUid user, NetEntity weapon, GameTick inputTick)
    {
        Projectiles.CompletePrediction(weapon, inputTick, user, false);
    }

    protected readonly record struct ProjectileLaunch(EntityCoordinates Origin, Vector2 Direction,
        Vector2 InheritedVelocity, Angle BaseAngle, Angle RecoilAngle);

    /// <summary>Original gun launch math. The two random samples share a reproducible source for predicted inputs.</summary>
    protected ProjectileLaunch GetProjectileLaunch(EntityUid gunUid, GunComponent gun,
        EntityCoordinates from, EntityCoordinates to, EntityUid? user, bool deterministic = true)
    {
        System.Random? random = null;
        if (deterministic && ProjectileInputTick != GameTick.Zero)
        {
            // Input tick identifies the preview; only the gun's accepted shot sequence chooses spread.
            random = new System.Random(unchecked(gun.ProjectilePredictionSequence++ * 397 ^ GetNetEntity(gunUid).Id));
            DirtyField(gunUid, gun, nameof(GunComponent.ProjectilePredictionSequence));
        }
        var spread = 0f;
        if (user != null)
        {
            var ev = new GetGunSpreadModifiersEvent(gun.DefaultSpread);
            RaiseLocalEvent(user.Value, ref ev);
            spread = random == null ? Random.NextFloat(-ev.Modifier, ev.Modifier) : random.NextFloat(-ev.Modifier, ev.Modifier);
        }

        var fromMap = TransformSystem.ToMapCoordinates(from);
        var direction = TransformSystem.ToMapCoordinates(to).Position - fromMap.Position;
        var timeSinceLastFire = (Timing.CurTime - gun.LastFire).TotalSeconds;
        var theta = MathHelper.Clamp(gun.CurrentAngle.Theta + gun.AngleIncreaseModified.Theta -
            gun.AngleDecayModified.Theta * timeSinceLastFire, gun.MinAngleModified.Theta, gun.MaxAngleModified.Theta);
        gun.CurrentAngle = new Angle(theta);
        gun.LastFire = gun.NextFire;
        DirtyField(gunUid, gun, nameof(GunComponent.CurrentAngle));
        DirtyField(gunUid, gun, nameof(GunComponent.LastFire));
        var sample = random == null ? Random.NextFloat(-0.5f, 0.5f) : random.NextFloat(-0.5f, 0.5f);
        var angle = new Angle(direction.ToAngle().Theta + theta * sample) + Angle.FromDegrees(spread);
        var origin = MapManager.TryFindGridAt(fromMap, out var grid, out _)
            ? TransformSystem.WithEntityId(from, grid)
            : new EntityCoordinates(Transform(from.EntityId).MapUid!.Value, fromMap.Position);
        return new ProjectileLaunch(origin, angle.ToVec() * direction.Length(), Physics.GetMapLinearVelocity(origin),
            direction.ToAngle(), angle);
    }

    protected void MarkPredictedProjectile(EntityUid projectile, EntityUid gun, EntityUid? user, EntityUid? source = null)
    {
        if (ProjectileInputTick == GameTick.Zero || user == null)
            return;
        var index = _projectilePredictionIndex++;
        Projectiles.MarkPrediction(projectile, user.Value,
            new ProjectilePredictionKey(GetNetEntity(gun), ProjectileInputTick, index), source);
    }

    protected void SkipProjectilePrediction() => _projectilePredictionIndex++;

    protected ProjectilePredictionKey NextProjectilePredictionKey(EntityUid gun)
        => new(GetNetEntity(gun), ProjectileInputTick, _projectilePredictionIndex);

    /// <summary>Pure selection; never invokes damage modifier events or damages a shield.</summary>
    public SoundSpecifier? GetProjectileImpactSound(EntityUid target, DamageSpecifier? damage,
        SoundSpecifier? projectileSound, bool forceSound, out bool varyPitch)
    {
        varyPitch = false;
        if (!forceSound && damage != null && damage.GetTotal() > 0 && TryComp<RangedDamageSoundComponent>(target, out var sounds))
        {
            var type = SharedMeleeWeaponSystem.GetHighestDamageSound(damage, ProtoManager);
            if (type != null)
            {
                if (sounds.SoundTypes?.TryGetValue(type, out var typed) == true)
                { varyPitch = true; return typed; }
                if (sounds.SoundGroups?.TryGetValue(type, out var grouped) == true)
                { varyPitch = true; return grouped; }
            }
        }
        return projectileSound;
    }
}
// Imperial medieval end
