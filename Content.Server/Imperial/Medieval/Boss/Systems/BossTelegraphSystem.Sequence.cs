using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.Map;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class BossTelegraphSystem
{
    public void PrepareSequence(EntityUid boss, BossTelegraphPattern pattern, int facing, bool preload)
    {
        Clear(boss);
        var sequence = Comp<BossSequenceComponent>(boss);
        var settings = Comp<BossTelegraphSettingsComponent>(boss);
        var root = Spawn(settings.TelegraphPrototype, new EntityCoordinates(sequence.Grid, 0.5f, 0.5f));
        var component = Comp<BossTelegraphComponent>(root);
        component.Style = EntityManager.System<BossMechanicDefinitionSystem>().Slot(boss, BossActionSlot.Sequence)?.Style ?? settings.Style;
        component.Boss = boss;
        component.Grid = sequence.Grid;
        component.HalfSize = Comp<BossArenaMapComponent>(sequence.Arena).HalfSize;
        component.Pattern = pattern;
        component.Facing = facing;
        component.Preload = preload;
        component.StoredStrike = !preload;
        component.StartTime = sequence.PhaseStart;
        component.EndTime = sequence.Deadline;
        component.DangerRevealTime = preload ? sequence.PhaseStart : sequence.Deadline - TimeSpan.FromSeconds(Math.Min(sequence.Attack.WarningLeadTime,
            (sequence.Deadline - sequence.PhaseStart).TotalSeconds));
        var preparation = new Preparation { Root = root, SequenceOwned = true };
        _prepared[boss] = preparation;
        Refresh(component, preparation);
        Dirty(root, component);
    }

}
