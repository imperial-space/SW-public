using Content.Server.Mind;
using Content.Server.Objectives;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval;

public sealed class GetItemObjectiveSystem : EntitySystem
{
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly ObjectivesSystem _objectives = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    public override void Initialize()
    {
        base.Initialize();

    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        foreach (var component in EntityManager.EntityQuery<GiveItemObjectiveComponent>())
        {
            if (_timing.CurTime > component.EndTime)
            {
                var uid = component.Owner;
                component.StartTime = _timing.CurTime;
                component.EndTime = component.StartTime + component.ReloadTime;
                if (!component.Predicted)
                    component.Predicted = true;
                else
                {
                    if (!_mindSystem.TryGetMind(uid, out var mindId, out var mind))
                        return;

                    component.MinObjectives = Math.Clamp(component.MinObjectives, 0, component.Objectives.Count);
                    component.MaxObjectives = Math.Clamp(component.MinObjectives, 0, component.Objectives.Count);

                    var amount = _random.Next(component.MinObjectives, component.MaxObjectives + 1);

                    List<string> objectivesList = new List<string>(component.Objectives);

                    for (int i = 0; i < amount; i++)
                    {
                        var proto = _random.PickAndTake(component.Objectives);
                        var objective = _objectives.TryCreateObjective(mindId, mind, proto);
                        if (objective == null) { continue; }
                        _mindSystem.AddObjective(mindId, mind, objective.Value);
                    }
                    RemComp<GiveItemObjectiveComponent>(component.Owner);
                }

            }
        }
    }


}
