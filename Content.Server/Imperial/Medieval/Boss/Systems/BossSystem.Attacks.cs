using System.Linq;
using System.Numerics;
using Content.Server.DoAfter;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Events;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class BossSystem
{
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly PhysicsSystem _physics = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;

    private void InitializeAttacks()
    {
        SubscribeLocalEvent<DamageBossOnDestructionComponent, DestructionEventArgs>(OnDamageBossOnDestruction);
        SubscribeLocalEvent<CursedMarkComponent, ComponentInit>(OnMarkInit);
        SubscribeLocalEvent<TimedBossHealComponent, ComponentInit>(OnHealInit);
        SubscribeLocalEvent<TimedBossHealComponent, BossHealDoAfterEvent>(OnBossHealDoAfter);
        SubscribeLocalEvent<PhysAwakeOnSpawnComponent, ComponentInit>(OnAwakeSpawn);
        SubscribeLocalEvent<DamageOnContactComponent, StartCollideEvent>(OnDamageOnContactCollide);
        SubscribeLocalEvent<ChargingRuneExplosionComponent, ComponentInit>(OnChargingStartup);
        SubscribeLocalEvent<ChargingRuneExplosionComponent, BossRunesChargingDoAfterEvent>(OnChargingDoAfter);
    }

    private void OnDamageBossOnDestruction(EntityUid uid, DamageBossOnDestructionComponent component, DestructionEventArgs args)
    {
        if (!TryComp<BossAttackComponent>(uid, out var bossAttack))
            return;

        DamageBoss(bossAttack.Boss, component.DamageAmount);
    }

    private void OnMarkInit(EntityUid uid, CursedMarkComponent component, ComponentInit args)
    {
        component.ExplodeTime = _timing.CurTime + TimeSpan.FromSeconds(component.Delay);
    }

    private void OnHealInit(EntityUid uid, TimedBossHealComponent component, ComponentInit args)
    {
        var doAfter = new DoAfterArgs(EntityManager, uid, component.Duration, new BossHealDoAfterEvent(), uid)
        {
            BreakOnDamage = false,
            BreakOnMove = false,
            NeedHand = false,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnBossHealDoAfter(EntityUid uid, TimedBossHealComponent component, BossHealDoAfterEvent args)
    {
        if (!TryComp<BossAttackComponent>(uid, out var attack))
            return;

        if (!TryComp<BossComponent>(attack.Boss, out var bossComp))
            return;

        DamageBoss(attack.Boss, -component.HealAmount);

        RemComp<TimedBossHealComponent>(uid);

        if (EntityQuery<TimedBossHealComponent>().Any())
            return;

        _appearance.SetData(attack.Boss, AdditionalBossVisuals.State, "healing");
        Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromSeconds(0.9f), () =>
        {
            _appearance.SetData(attack.Boss, AdditionalBossVisuals.State, "none");
        });
    }

    private void OnAwakeSpawn(EntityUid uid, PhysAwakeOnSpawnComponent component, ComponentInit args)
        => _physics.WakeBody(uid);

    private void OnDamageOnContactCollide(EntityUid uid, DamageOnContactComponent component, ref StartCollideEvent args)
        => _damage.TryChangeDamage(args.OtherEntity, component.Damage);

    private void OnChargingStartup(EntityUid uid, ChargingRuneExplosionComponent component, ComponentInit args)
    {
        var doAfter = new DoAfterArgs(EntityManager, uid, component.Time, new BossRunesChargingDoAfterEvent(), uid)
        {
            BreakOnDamage = false,
            BreakOnMove = false,
            NeedHand = false,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnChargingDoAfter(EntityUid uid, ChargingRuneExplosionComponent component, BossRunesChargingDoAfterEvent args)
    {
        _explosion.QueueExplosion(_transform.ToMapCoordinates(Transform(uid).Coordinates), ExplosionSystem.DefaultExplosionPrototypeId, 1000f, 0.1f, 0, null, 0, 0, false);
        RemComp<ChargingRuneExplosionComponent>(uid);

        var runes = EntityManager.AllEntities<PurifyableExplosionRuneComponent>().Where(x => TryComp<BossAttackComponent>(x, out var attack) && attack.Boss == uid);
        foreach (var item in runes)
            QueueDel(item);

    }

    private void UpdateSpiked()
    {
        var query = EntityQueryEnumerator<SpikedGridComponent, MapGridComponent>();
        while (query.MoveNext(out var uid, out var spikedComp, out var grid))
        {
            if (spikedComp.NextSpawn > _timing.CurTime)
                continue;

            if (spikedComp.NextIndex > spikedComp.TargetIndex)
            {
                RemComp<SpikedGridComponent>(uid);
                continue;
            }

            var tiles = _map.GetAllTiles(uid, grid);
            var indicies = tiles.Select(t => t.GridIndices.X);
            var idx = spikedComp.Direction == SpikedGridDirection.Right ? indicies.Min() + spikedComp.NextIndex : indicies.Max() - spikedComp.NextIndex;

            foreach (var tile in tiles.Where(t => t.GridIndices.X == idx))
            {
                SpawnAtPosition(spikedComp.SpikeProto, new(uid, tile.GridIndices));
            }

            spikedComp.NextIndex++;
            spikedComp.NextSpawn = _timing.CurTime + TimeSpan.FromSeconds(spikedComp.SpawnInterval);
        }
    }

    private void UpdateMark()
    {
        var query = EntityQueryEnumerator<CursedMarkComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.ExplodeTime > _timing.CurTime)
                continue;

            if (_lookup.GetEntitiesInRange<FightingBossComponent>(Transform(uid).Coordinates, 3f).Count > 1)
                _explosion.QueueExplosion(_transform.ToMapCoordinates(Transform(uid).Coordinates), ExplosionSystem.DefaultExplosionPrototypeId, 150, 10, 0, null, 0, 0, false);

            RemComp<CursedMarkComponent>(uid);
        }
    }

    private void UpdateSpikeMarker()
    {
        var query = EntityQueryEnumerator<SpikeAttackMarkerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.NextSpawn > _timing.CurTime)
                continue;

            if (comp.Idx > comp.TargetIdx)
            {
                RemComp<SpikeAttackMarkerComponent>(uid);
                continue;
            }

            var pos = Transform(uid).Coordinates;
            foreach (var item in comp.Positions[comp.Idx])
            {
                SpawnAtPosition(comp.Proto, new EntityCoordinates(pos.EntityId, pos.Position + item));
            }

            comp.Idx++;
            comp.NextSpawn = _timing.CurTime + TimeSpan.FromSeconds(comp.SpawnInterval);
        }
    }

    private void UpdateRunes()
    {
        var query = EntityQueryEnumerator<ChargingRuneExplosionComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.NextCheck > _timing.CurTime)
                continue;

            comp.NextCheck = _timing.CurTime + TimeSpan.FromSeconds(1f);

            var runes = EntityManager.AllEntities<PurifyableExplosionRuneComponent>().Where(x => TryComp<BossAttackComponent>(x, out var attack) && attack.Boss == uid);
            var activeRunes = runes.Where(x => _physics.GetContactingEntities(x).Where(y => HasComp<HumanoidAppearanceComponent>(y)).ToList().Any());

            if (activeRunes.Count() >= runes.Count())
            {
                foreach (var item in runes)
                    QueueDel(item);

                RemComp<ChargingRuneExplosionComponent>(uid);
                continue;
            }
        }
    }
}
