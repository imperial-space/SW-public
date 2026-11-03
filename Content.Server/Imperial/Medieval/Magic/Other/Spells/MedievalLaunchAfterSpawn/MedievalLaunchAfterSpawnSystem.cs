using System.Numerics;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Imperial.Medieval.Magic;
using Content.Shared.Imperial.Medieval.Magic.MedievalLaunchAfterSpawn;
using Content.Shared.Imperial.Medieval.Magic.MedievalTargetOverlayOnAiming;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Magic.MedievalLaunchAfterSpawn;


public sealed class MedievalLaunchAfterSpawnSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly GunSystem _gun = default!;

    /// <summary>
    /// Farthest point the caster can send a projectile at. Matches the range of the spell actions, which the client checks.
    /// </summary>
    private const float MaxTargetDistance = 60f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalLaunchAfterSpawnComponent, MedievalAfterAimingSpawnBySpellEvent>(OnSpawn);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<MedievalLaunchAfterSpawnComponent>();

        while (query.MoveNext(out var uid, out var component))
        {
            if (component.LaunchAt == null || _timing.CurTime < component.LaunchAt) continue;

            Launch(uid, component);
        }
    }

    private void OnSpawn(EntityUid uid, MedievalLaunchAfterSpawnComponent component, MedievalAfterAimingSpawnBySpellEvent args)
    {
        var index = CountSameCast(uid, args.Performer);

        // The target list comes from the client and the spell code spawns an entity for each of them,
        // so the limit of the action has to be enforced here
        if (TryComp<MedievalTargetOverlayOnAimingComponent>(args.Action, out var aiming) && index >= aiming.MaxTargetCount)
        {
            QueueDel(uid);

            return;
        }

        // The target comes from the client: the cursor must be a real point near the caster on its map,
        // otherwise it would poison the position and the velocity of the boulder
        if (!IsValidCursor(args.Target.CursorPosition, args.Performer))
        {
            QueueDel(uid);

            return;
        }

        var targetEntity = IsValidTargetEntity(args.Target.TargetEntity, args.Performer) ? args.Target.TargetEntity : null;

        component.Performer = args.Performer;
        component.Target = (args.Target.CursorPosition, targetEntity);
        component.SpawnedAt = _timing.CurTime;
        component.LaunchAt = _timing.CurTime + TimeSpan.FromSeconds(component.Delay + component.DelayStep * index);

        var origin = _transform.GetWorldPosition(args.Performer);
        var direction = GetTargetPosition(component) - origin;

        if (direction.LengthSquared() < 0.0001f) return;

        var slot = Angle.FromDegrees(component.SlotAngle * ((index + 1) / 2) * (index % 2 == 1 ? 1 : -1));

        _transform.SetWorldPosition(uid, origin + slot.RotateVec(Vector2.Normalize(direction)) * component.Radius);
    }

    /// <summary>
    /// How many entities of the same cast are already spawned. They all appear in the same tick
    /// and the cooldown of the spell is much longer than the window, so entities of other casts are not counted.
    /// </summary>
    private int CountSameCast(EntityUid self, EntityUid performer)
    {
        var count = 0;
        var query = EntityQueryEnumerator<MedievalLaunchAfterSpawnComponent>();

        while (query.MoveNext(out var uid, out var other))
        {
            if (uid == self || other.Performer != performer) continue;
            if (_timing.CurTime - other.SpawnedAt > TimeSpan.FromSeconds(0.5)) continue;

            count++;
        }

        return count;
    }

    private void Launch(EntityUid uid, MedievalLaunchAfterSpawnComponent component)
    {
        component.LaunchAt = null;

        var performerExists = Exists(component.Performer);
        var origin = _transform.GetWorldPosition(uid);
        var direction = GetTargetPosition(component) - origin;

        if (direction.LengthSquared() < 0.0001f)
            direction = performerExists ? _transform.GetWorldRotation(component.Performer).ToWorldVec() : Vector2.UnitY;

        var projectile = Spawn(component.Projectile, Transform(uid).Coordinates);
        var shooter = performerExists ? component.Performer : (EntityUid?) null;
        _gun.ShootProjectile(projectile, direction, Vector2.Zero, shooter, shooter, component.Speed);

        QueueDel(uid);
    }

    private bool IsValidCursor(MapCoordinates cursor, EntityUid performer)
    {
        if (!float.IsFinite(cursor.Position.X) || !float.IsFinite(cursor.Position.Y)) return false;
        if (cursor.MapId != _transform.GetMapId(performer)) return false;

        return IsInRange(cursor.Position, performer);
    }

    private bool IsValidTargetEntity(EntityUid? target, EntityUid performer)
    {
        if (target is not { } uid || TerminatingOrDeleted(uid)) return false;
        if (_transform.GetMapId(uid) != _transform.GetMapId(performer)) return false;

        return IsInRange(_transform.GetWorldPosition(uid), performer);
    }

    private bool IsInRange(Vector2 position, EntityUid performer)
    {
        return (position - _transform.GetWorldPosition(performer)).LengthSquared() <= MaxTargetDistance * MaxTargetDistance;
    }

    private Vector2 GetTargetPosition(MedievalLaunchAfterSpawnComponent component)
    {
        if (component.Target.TargetEntity is { } target && !TerminatingOrDeleted(target))
            return _transform.GetWorldPosition(target);

        return component.Target.CursorPosition.Position;
    }
}
