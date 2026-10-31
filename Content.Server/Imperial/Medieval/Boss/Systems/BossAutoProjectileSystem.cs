using System.Linq;
using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class BossAutoProjectileSystem : EntitySystem, IBossActionFeature
{
    [Dependency] private readonly BossTelegraphSystem _telegraphs = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly BossHitFeedbackSystem _feedback = default!;

    public override void Initialize()
    {
        EntityManager.System<BossActionSystem>().Register(this);
        SubscribeLocalEvent<BossDefeatedEvent>(OnDefeated);
        SubscribeLocalEvent<BossWonEvent>(OnWon);
    }

    public bool TryStart(EntityUid boss, EntityUid target)
    {
        if (!TryComp<BossTelegraphSettingsComponent>(boss, out var settings) ||
            (EntityManager.System<BossMechanicDefinitionSystem>().For(boss).FirstOrDefault(definition => definition.AutoProjectilePrototype != null)?.AutoProjectilePrototype ?? settings.AutoProjectilePrototype) is not { } prototype ||
            !TryComp<BossEncounterComponent>(boss, out var encounter) || !encounter.FightRunning ||
            !_telegraphs.TryGetArena(boss, out var arena) ||
            !_telegraphs.TryGetPlayerTile(boss, target, out _, allowCritical: true)) return false;
        var uid = Spawn(prototype, new EntityCoordinates(arena.Comp.CombatGrid, Vector2.Zero));
        var projectile = Comp<BossAutoProjectileComponent>(uid);
        if (!float.IsFinite(projectile.Speed) || projectile.Speed <= 0 ||
            !float.IsFinite(projectile.Scale) || projectile.Scale <= 0)
        {
            QueueDel(uid);
            return false;
        }
        var now = _timing.CurTime;
        projectile.Boss = boss;
        projectile.Target = target;
        projectile.Grid = arena.Comp.CombatGrid;
        projectile.Generation = encounter.Generation;                            // tie this shot to the current fight
        projectile.StartTime = projectile.PositionTime = now;
        projectile.LaunchTime = now + TimeSpan.FromSeconds(BossAutoProjectileVisuals.LaunchDelaySeconds);
        projectile.EndTime = projectile.LaunchTime + TimeSpan.FromSeconds(BossAutoProjectileVisuals.MaxFlightSeconds);
        projectile.Position = HandPosition(projectile, now);
        var delta = TargetPosition(projectile) - projectile.Position;
        if (delta.LengthSquared() > 0.000001f) projectile.Direction = Vector2.Normalize(delta);
        _transform.SetCoordinates(uid, new EntityCoordinates(projectile.Grid, projectile.Position));
        Dirty(uid, projectile);
        return true;
    }

    private Vector2 TargetPosition(BossAutoProjectileComponent projectile)
        => Vector2.Transform(_transform.GetWorldPosition(projectile.Target), _transform.GetInvWorldMatrix(projectile.Grid));

    private Vector2 HandPosition(BossAutoProjectileComponent projectile, TimeSpan now)
    {
        var offset = projectile.HandOffset;
        if (TryComp<BossPresentationComponent>(projectile.Boss, out var presentation))
            offset.Y += BossPresentationComponent.BobOffset(now * presentation.BobSpeedMultiplier);
        return Vector2.Transform(_transform.GetWorldPosition(projectile.Boss) + offset, _transform.GetInvWorldMatrix(projectile.Grid));
    }

    public void CancelAll(EntityUid boss)
    {
        foreach (var entity in EntityManager.AllEntities<BossAutoProjectileComponent>().ToArray())
            if (entity.Comp.Boss == boss && Exists(entity.Owner) && !Terminating(entity.Owner)) QueueDel(entity.Owner); // remove shots belonging to this boss only
    }

    private void OnDefeated(ref BossDefeatedEvent args) => CancelAll(args.Boss);
    private void OnWon(ref BossWonEvent args) => CancelAll(args.Boss);

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<BossAutoProjectileComponent>();
        while (query.MoveNext(out var uid, out var projectile))
        {
            if (Terminating(uid) || EntityManager.IsQueuedForDeletion(uid) || MetaData(uid).EntityPaused) continue;
            if (!TryComp<BossComponent>(projectile.Boss, out var boss) || !boss.Active || boss.Finished || boss.Health <= 0 ||
                !TryComp<BossEncounterComponent>(projectile.Boss, out var encounter) || !encounter.FightRunning ||
                encounter.Generation != projectile.Generation || !_telegraphs.TryGetArena(projectile.Boss, out var arena) ||
                arena.Comp.CombatGrid != projectile.Grid || Transform(uid).MapUid != arena.Owner)
            { QueueDel(uid); continue; }
            if (projectile.Resolved)
            {
                if (now >= projectile.EndTime) QueueDel(uid);
                continue;
            }
            if (now >= projectile.EndTime || !_telegraphs.TryGetPlayerTile(projectile.Boss, projectile.Target, out _, allowCritical: true))
            { QueueDel(uid); continue; }
            var target = TargetPosition(projectile);
            if (now < projectile.LaunchTime)
            {
                projectile.Position = HandPosition(projectile, now);
                projectile.PositionTime = now;
                var aim = target - projectile.Position;
                if (aim.LengthSquared() > 0.000001f) projectile.Direction = Vector2.Normalize(aim);
            }
            else
            {
                var from = projectile.PositionTime > projectile.LaunchTime ? projectile.PositionTime : projectile.LaunchTime;
                var delta = target - projectile.Position;
                var distance = delta.Length();
                var elapsed = Math.Max(0, (now - from).TotalSeconds);
                if (distance > 0.000001f) projectile.Direction = delta / distance;
                if (distance <= projectile.Speed * elapsed)
                {
                    projectile.Position = target;
                    projectile.Resolved = true;
                    projectile.ImpactTime = from + TimeSpan.FromSeconds(distance / projectile.Speed);
                    projectile.EndTime = projectile.ImpactTime + TimeSpan.FromSeconds(BossAutoProjectileVisuals.ImpactSeconds);

                    _transform.SetCoordinates(uid, new EntityCoordinates(projectile.Grid, target));
                    Dirty(uid, projectile);
                    var damage = _damage.TryChangeDamage(projectile.Target, encounter.TankDamage, origin: projectile.Boss); // hit after the flame reaches its target
                    _feedback.OnHit(projectile.Boss, projectile.Target, damage, tankBuster: false);
                }
                else projectile.Position += projectile.Direction * projectile.Speed * (float)elapsed;
                projectile.PositionTime = now;
            }
            _transform.SetCoordinates(uid, new EntityCoordinates(projectile.Grid, projectile.Position));
            Dirty(uid, projectile);
        }
    }
}
