using Content.Shared.Imperial.Medieval.Boss;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class BossEncounterSystem
{
    public void AttachGate(EntityUid boss, EntityUid gate)
    {
        if (!TryComp<BossEncounterComponent>(boss, out var comp) || !comp.FightRunning ||
            !TryComp<BossArenaGateComponent>(gate, out var portal)) return;
        portal.CombatLocked = true;
        portal.EntryLocked = comp.IdleDeadline != null || _timing.CurTime >= comp.EntryCloses;
        portal.Revision++;
        if (portal.EntryLocked) EntityManager.System<BossArenaGateSystem>().SetOpen(gate, false);
    }
}
