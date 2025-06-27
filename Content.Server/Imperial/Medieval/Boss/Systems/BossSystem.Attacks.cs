using System.Linq;
using Content.Server.DoAfter;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class BossSystem
{
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly DoAfterSystem _doAfter = default!;

    private void InitializeAttacks()
    {
        SubscribeLocalEvent<DamageBossOnDestructionComponent, DestructionEventArgs>(OnDamageBossOnDestruction);
        SubscribeLocalEvent<CursedMarkComponent, ComponentInit>(OnMarkInit);
        SubscribeLocalEvent<TimedBossHealComponent, ComponentInit>(OnHealInit);
        SubscribeLocalEvent<TimedBossHealComponent, BossHealDoAfterEvent>(OnBossHealDoAfter);
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

            if (_lookup.GetEntitiesInRange<FightingBossComponent>(Transform(uid).Coordinates, 4f).Count > 0)
                _explosion.QueueExplosion(_transform.ToMapCoordinates(Transform(uid).Coordinates), ExplosionSystem.DefaultExplosionPrototypeId, 50, 3, 0, null, 0, 0, false);

            RemComp<CursedMarkComponent>(uid);
        }
    }
}
