using Content.Server.Mind;
using Content.Server.Objectives;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval;

public sealed class MoneyCollectorSystem : EntitySystem
{
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly ObjectivesSystem _objectives = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    public override void Initialize()
    {
        base.Initialize();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        foreach (var comp in EntityManager.EntityQuery<MoneyCollectorComponent>())
        {
            if (_timing.CurTime > comp.EndTime)
            {
                comp.StartTime = _timing.CurTime;
                comp.EndTime = comp.StartTime + comp.ReloadTime;
                if (!comp.Predicted)
                    comp.Predicted = true;
                else
                {
                    if (!_mindSystem.TryGetMind(comp.Owner, out var mindId, out var mind))
                        return;
                    var objective = _objectives.TryCreateObjective(mindId, mind, comp.ObjectivePrototype);
                    if (objective == null) { return; }
                    _mindSystem.AddObjective(mindId, mind, objective.Value);
                    RemComp<MoneyCollectorComponent>(comp.Owner);
                }

            }
        }
    }

}
