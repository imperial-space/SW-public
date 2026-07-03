using System.Diagnostics.CodeAnalysis;
using Content.Shared.Imperial.Medieval.Bee.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.NPC.Components;

namespace Content.Server.Imperial.Medieval.Bee.Systems;

public sealed partial class MedievalBeeSystem : EntitySystem
{
    public void Teleport(EntityUid uid, EntityUid target)
    {
        if (TryComp<PullerComponent>(uid, out var pullerComp))
        {
            if (pullerComp != null && pullerComp.Pulling != null)
            {
                if (!TryComp<PullableComponent>(pullerComp.Pulling, out var pullableComp1)) return;
                _pulling.TryStopPull(pullerComp.Pulling.Value, pullableComp1);
                return;
            }
        }
        if (TryComp<PullableComponent>(uid, out var pullableComp))
        {
            if (pullableComp != null && pullableComp.Puller != null)
            {
                if (!TryComp<PullerComponent>(pullableComp.Puller, out var pullerComp1)) return;
                _pulling.TryStopPull(pullableComp.Puller.Value, pullableComp);
                return;
            }
        }
        _transform.SetCoordinates(uid, Transform(target).Coordinates);
        _transform.AttachToGridOrMap(uid);
    }
    public bool TryGetHiveGridFromTransform(EntityUid uid, [NotNullWhen(true)] out Entity<MedievalBeeGridComponent>? result)
    {
        result = null;
        var grid = _transform.GetGrid(uid);

        if (grid == null)
            return false;

        if (!TryComp<MedievalBeeGridComponent>(grid, out var gridComponent) || !gridComponent.Hive.HasValue)
            return false;

        result = new(grid.Value, gridComponent);
        return true;
    }
    public void Pacify(Entity<MedievalBeeHiveComponent> hive, TimeSpan time)
    {
        foreach (var entity in hive.Comp.Bees)
        {
            Pacify(entity.Owner, entity.Comp);
        }
        hive.Comp.Pacified = true;
        hive.Comp.PacifyEnd = _timing.CurTime + time;
        hive.Comp.PacifyCooldown = _timing.CurTime + (time * 2);
    }
    public void UnPacify(Entity<MedievalBeeHiveComponent> hive)
    {
        foreach (var entity in hive.Comp.Bees)
        {
            UnPacify(entity.Owner, entity.Comp);
        }
        hive.Comp.Pacified = false;
    }
    public void UnPacify(EntityUid uid, MedievalBeeComponent? component)
    {
        if (Deleted(uid))
            return;

        if (!Resolve(uid, ref component))
            return;

        if (!TryComp<NpcFactionMemberComponent>(uid, out var factionComponent))
            return;

        _faction.ClearFactions((uid, factionComponent));
        _faction.AddFaction((uid, factionComponent), component.HostileFaction);
    }
    public void Pacify(EntityUid uid, MedievalBeeComponent? component)
    {
        if (!Resolve(uid, ref component))
            return;

        if (!TryComp<NpcFactionMemberComponent>(uid, out var factionComponent))
            return;

        _faction.ClearFactions((uid, factionComponent));
        _faction.AddFaction((uid, factionComponent), component.FriendlyFaction);
    }
}
