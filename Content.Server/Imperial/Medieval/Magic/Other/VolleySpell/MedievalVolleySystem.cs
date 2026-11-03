using System.Numerics;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Actions.Components;
using Content.Shared.Imperial.Medieval.Magic.VolleySpell;
using Content.Shared.Physics;
using Content.Shared.Mobs.Systems;
using Content.Shared.Projectiles;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Magic.VolleySpell;


/// <summary>
/// Summons the projectiles of a volley spell behind the caster, keeps them spinning there until the target is chosen
/// and then launches them one after another
/// </summary>
public sealed class MedievalVolleySystem : EntitySystem
{
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly PhysicsSystem _physics = default!;
    [Dependency] private readonly GunSystem _gun = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    /// <summary>
    /// Time between the choice of the target and the first launch. The projectiles turn towards the target during it.
    /// </summary>
    private const float SettleTime = 0.25f;

    /// <summary>
    /// How long a dismissed projectile lives on. The request to dismiss can overtake the request to cast the spell,
    /// so the projectiles must still be there when the cast arrives.
    /// </summary>
    private const float DismissGrace = 1f;

    /// <summary>
    /// The closest a projectile can get to the caster when a wall is right behind them
    /// </summary>
    private const float MinRadius = 0.3f;

    /// <summary>
    /// Gap that a projectile leaves in front of a wall it runs into
    /// </summary>
    private const float WallGap = 0.15f;

    /// <summary>
    /// How fast a projectile returns to its place after a wall stops blocking it
    /// </summary>
    private const float RadiusRecoverSpeed = 4f;

    /// <summary>
    /// Farthest point the caster can send a projectile at. Matches the range of the spell actions, which the client checks.
    /// </summary>
    private const float MaxTargetDistance = 60f;

    private const float SwayAmplitude = 0.06f;
    private const float SwaySpeed = 6f;
    private const float FollowSpeed = 8f;

    /// <summary>
    /// The server position reaches the client late, so a moving caster runs away from the projectiles.
    /// The projectiles are shifted ahead along the caster's velocity by this many seconds to make up for it.
    /// </summary>
    private const float VelocityLead = 0.15f;

    private const float LeadSmoothing = 10f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<MedievalVolleySummonEvent>(OnSummonRequest);
        SubscribeNetworkEvent<MedievalVolleyDismissEvent>(OnDismissRequest);
    }

    #region Requests

    private void OnSummonRequest(MedievalVolleySummonEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } caster) return;
        if (!TryGetRequestedAction(caster, ev.Action, out var action, out var settings)) return;

        // The client can repeat the request at will. Projectiles that still wait are kept (and their removal is cancelled)
        // so that repeated requests neither spawn entities nor replay the sound.
        if (RefreshWaitingProjectiles(caster, action, settings))
            return;

        Summon(caster, action, settings);
    }

    private void OnDismissRequest(MedievalVolleyDismissEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } caster) return;

        Dismiss(caster, GetEntity(ev.Action));
    }

    private bool RefreshWaitingProjectiles(EntityUid caster, EntityUid action, MedievalVolleySpellComponent settings)
    {
        var waiting = GetWaitingProjectiles(caster, action);

        foreach (var (_, comp) in waiting)
        {
            comp.DismissTime = null;
            comp.Deadline = _timing.CurTime + TimeSpan.FromSeconds(settings.HoverTimeout);
        }

        return waiting.Count > 0;
    }

    private bool TryGetRequestedAction(EntityUid caster, NetEntity netAction, out EntityUid action, out MedievalVolleySpellComponent settings)
    {
        action = GetEntity(netAction);
        settings = default!;

        if (!Exists(action)) return false;
        if (!TryComp<ActionComponent>(action, out var actionComponent)) return false;
        if (actionComponent.AttachedEntity != caster) return false;
        if (!TryComp(action, out MedievalVolleySpellComponent? volley)) return false;
        if (actionComponent.Cooldown is { } cooldown && cooldown.End >= _timing.CurTime) return false;
        if (_mobState.IsIncapacitated(caster)) return false;

        settings = volley;

        return true;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Summons the projectiles behind the caster. Projectiles of the same spell that are still waiting are replaced.
    /// </summary>
    public void Summon(EntityUid caster, EntityUid action, MedievalVolleySpellComponent settings)
    {
        Dismiss(caster, action, immediate: true);

        var coordinates = Transform(caster).Coordinates;
        var back = -GetFacing(caster);

        for (var i = 0; i < settings.Count; i++)
        {
            var slot = settings.Count == 1 ? 0f : -settings.Arc / 2 + settings.Arc * i / (settings.Count - 1);
            var projectile = Spawn(settings.Projectile, coordinates);
            var comp = EnsureComp<MedievalVolleyProjectileComponent>(projectile);

            comp.Caster = caster;
            comp.Action = action;
            comp.Order = i;
            comp.Slot = Angle.FromDegrees(slot);
            comp.HoverRadius = settings.HoverRadius;
            comp.Back = back;
            comp.Radius = settings.HoverRadius;
            comp.Phase = i * 1.3f;
            comp.Rotation = GetFacing(caster).ToWorldAngle() + GetSpriteAngle(projectile);
            comp.Deadline = _timing.CurTime + TimeSpan.FromSeconds(settings.HoverTimeout);

            UpdatePosition(projectile, comp, 0f);
        }

        if (settings.SummonSound != null)
            _audio.PlayPvs(settings.SummonSound, caster);
    }

    /// <summary>
    /// Removes the projectiles of the spell that are still waiting for a target
    /// </summary>
    /// <param name="immediate">Remove them now. Otherwise they are removed after a short grace period, unless they are launched before.</param>
    public void Dismiss(EntityUid caster, EntityUid action, bool immediate = false)
    {
        var query = EntityQueryEnumerator<MedievalVolleyProjectileComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Caster != caster || comp.Action != action || comp.LaunchTime != null) continue;

            if (immediate)
                QueueDel(uid);
            else
                comp.DismissTime ??= _timing.CurTime + TimeSpan.FromSeconds(DismissGrace);
        }
    }

    /// <summary>
    /// Sends the waiting projectiles of the spell at the targets, one after another.
    /// If the projectiles were not summoned yet, they are summoned first.
    /// </summary>
    /// <param name="targets">Cursor position and, if there is one, the entity under the cursor. The projectiles are shared between them in turn.</param>
    public void Launch(
        EntityUid caster,
        EntityUid action,
        MedievalVolleySpellComponent settings,
        List<(MapCoordinates Cursor, EntityUid? Target)> targets,
        float speed)
    {
        // The list comes from the client: only as many targets as there are projectiles can be used, the coordinates
        // must be real numbers near the caster on its map, and an entity under the cursor must be just as close
        var casterMap = _transform.GetMapId(caster);
        var casterPosition = _transform.GetWorldPosition(caster);
        var checkedTargets = new List<(MapCoordinates Cursor, EntityUid? Target)>();

        foreach (var (cursor, target) in targets)
        {
            if (checkedTargets.Count >= settings.Count) break;
            if (cursor.MapId != casterMap) continue;
            if (!float.IsFinite(cursor.Position.X) || !float.IsFinite(cursor.Position.Y)) continue;
            if ((cursor.Position - casterPosition).LengthSquared() > MaxTargetDistance * MaxTargetDistance) continue;

            checkedTargets.Add((cursor, IsValidTarget(target, casterMap, casterPosition) ? target : null));
        }

        if (checkedTargets.Count == 0) return;

        targets = checkedTargets;

        var waiting = GetWaitingProjectiles(caster, action);

        if (waiting.Count == 0)
        {
            Summon(caster, action, settings);
            waiting = GetWaitingProjectiles(caster, action);
        }

        waiting.Sort((a, b) => a.Comp.Order.CompareTo(b.Comp.Order));

        for (var i = 0; i < waiting.Count; i++)
        {
            var comp = waiting[i].Comp;
            var (cursor, target) = targets[i % targets.Count];

            comp.Target = target;
            comp.TargetPosition = cursor.Position;
            comp.Speed = speed;
            comp.LaunchSound = settings.LaunchSound;
            comp.DismissTime = null;
            comp.LaunchTime = _timing.CurTime + TimeSpan.FromSeconds(SettleTime + i * settings.LaunchInterval);
        }
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<MedievalVolleyProjectileComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (TerminatingOrDeleted(comp.Caster) || _mobState.IsIncapacitated(comp.Caster))
            {
                QueueDel(uid);
                continue;
            }

            if (comp.LaunchTime == null && (_timing.CurTime >= comp.Deadline || _timing.CurTime >= comp.DismissTime))
            {
                QueueDel(uid);
                continue;
            }

            UpdatePosition(uid, comp, frameTime);
            UpdateRotation(uid, comp);

            if (comp.LaunchTime is { } launchTime && _timing.CurTime >= launchTime)
                Fire(uid, comp);
        }
    }

    #region Helpers

    private bool IsValidTarget(EntityUid? target, MapId casterMap, Vector2 casterPosition)
    {
        if (target is not { } uid || TerminatingOrDeleted(uid)) return false;
        if (_transform.GetMapId(uid) != casterMap) return false;

        return (_transform.GetWorldPosition(uid) - casterPosition).LengthSquared() <= MaxTargetDistance * MaxTargetDistance;
    }

    private List<(EntityUid Owner, MedievalVolleyProjectileComponent Comp)> GetWaitingProjectiles(EntityUid caster, EntityUid action)
    {
        var result = new List<(EntityUid, MedievalVolleyProjectileComponent)>();
        var query = EntityQueryEnumerator<MedievalVolleyProjectileComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Caster != caster || comp.Action != action || comp.LaunchTime != null) continue;

            result.Add((uid, comp));
        }

        return result;
    }

    private Vector2 GetFacing(EntityUid caster)
    {
        return _transform.GetWorldRotation(caster).ToWorldVec();
    }

    private void UpdatePosition(EntityUid uid, MedievalVolleyProjectileComponent comp, float frameTime)
    {
        var desiredBack = -GetFacing(comp.Caster);
        var smoothed = Vector2.Lerp(comp.Back, desiredBack, Math.Min(1f, frameTime * FollowSpeed));

        if (smoothed.LengthSquared() > 0.0001f)
            comp.Back = smoothed.Normalized();

        var direction = comp.Slot.RotateVec(comp.Back);
        var sway = MathF.Sin((float) _timing.CurTime.TotalSeconds * SwaySpeed + comp.Phase) * SwayAmplitude;
        var casterCoordinates = _transform.GetMapCoordinates(comp.Caster);
        var wanted = comp.HoverRadius + sway;

        // Walls stop the projectile: it runs into the wall instead of hovering inside it
        var ray = new CollisionRay(casterCoordinates.Position, direction, (int) CollisionGroup.Impassable);
        foreach (var hit in _physics.IntersectRay(casterCoordinates.MapId, ray, wanted + WallGap, comp.Caster, returnOnFirstHit: true))
        {
            wanted = Math.Min(wanted, Math.Max(MinRadius, hit.Distance - WallGap));
            break;
        }

        // Blocked: take the closer place at once so the projectile never shows up inside the wall. Free again: glide back.
        comp.Radius = wanted < comp.Radius
            ? wanted
            : Math.Min(wanted, comp.Radius + RadiusRecoverSpeed * frameTime);

        var velocity = _physics.GetMapLinearVelocity(comp.Caster) * VelocityLead;
        comp.Lead = Vector2.Lerp(comp.Lead, velocity, Math.Min(1f, frameTime * LeadSmoothing));

        _transform.SetWorldPosition(uid, casterCoordinates.Position + comp.Lead + direction * comp.Radius);
    }

    private void UpdateRotation(EntityUid uid, MedievalVolleyProjectileComponent comp)
    {
        if (comp.LaunchTime is { } launchTime && _timing.CurTime >= launchTime - TimeSpan.FromSeconds(SettleTime))
        {
            if (!comp.Settling)
            {
                comp.Settling = true;
                comp.SettleFrom = comp.Rotation;
            }

            var progress = Math.Clamp(1.0 - (launchTime - _timing.CurTime).TotalSeconds / SettleTime, 0.0, 1.0);
            var eased = progress * progress * (3.0 - 2.0 * progress);
            var towardsTarget = (GetTargetPosition(comp) - _transform.GetWorldPosition(uid)).ToWorldAngle() + GetSpriteAngle(uid);
            var delta = Math.IEEERemainder(towardsTarget.Theta - comp.SettleFrom.Theta, Math.Tau);

            comp.Rotation = new Angle(comp.SettleFrom.Theta + delta * eased);
        }
        else
        {
            comp.Rotation = GetFacing(comp.Caster).ToWorldAngle() + GetSpriteAngle(uid);
        }

        _transform.SetWorldRotation(uid, comp.Rotation);
    }

    private Angle GetSpriteAngle(EntityUid uid)
    {
        return TryComp<ProjectileComponent>(uid, out var projectile) ? projectile.Angle : Angle.Zero;
    }

    private Vector2 GetTargetPosition(MedievalVolleyProjectileComponent comp)
    {
        if (comp.Target is { } target && !TerminatingOrDeleted(target))
            comp.TargetPosition = _transform.GetWorldPosition(target);

        return comp.TargetPosition;
    }

    private void Fire(EntityUid uid, MedievalVolleyProjectileComponent comp)
    {
        var direction = GetTargetPosition(comp) - _transform.GetWorldPosition(uid);

        if (!float.IsFinite(direction.X) || !float.IsFinite(direction.Y) || direction.LengthSquared() < 0.01f)
            direction = GetFacing(comp.Caster);

        if (comp.LaunchSound != null)
            _audio.PlayPvs(comp.LaunchSound, uid);

        _gun.ShootProjectile(
            uid,
            direction,
            Vector2.Zero, // the caster's own speed must not push the projectile away from the target
            comp.Caster,
            comp.Caster,
            comp.Speed);

        RemCompDeferred<MedievalVolleyProjectileComponent>(uid);
    }

    #endregion
}
