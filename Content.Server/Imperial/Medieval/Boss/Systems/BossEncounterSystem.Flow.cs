using System.Linq;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class BossEncounterSystem
{
    public static IReadOnlyList<BossEncounterStep> CurrentSteps(BossEncounterComponent encounter)
        => encounter.Escalating ? encounter.Escalation : encounter.Repeating ? encounter.Rotation : encounter.Tutorial;

    private IReadOnlyList<BossEncounterStep> ActiveSteps(EntityUid boss, BossEncounterComponent encounter)
    {
        var catalog = EntityManager.System<BossMechanicDefinitionSystem>();
        BossEncounterStep[] Installed(IEnumerable<BossEncounterStep> steps) => steps.Where(step => catalog.Supports(boss, step)).ToArray();
        var steps = Installed(CurrentSteps(encounter));
        if (steps.Length > 0) return steps;
        encounter.Step = 0;
        encounter.Repeating = true;                                             // an empty opening must not hold up the rotation
        var rotation = Installed(encounter.Rotation);
        if (!encounter.Escalating && rotation.Length > 0) return rotation;
        var escalation = Installed(encounter.Escalation);
        if (escalation.Length > 0)
        {
            encounter.Escalating = true;
            return escalation;
        }
        return rotation;                                                       // basic tank hits continue if no mechanics are installed
    }

    private BossEncounterStep? ChooseStep(EntityUid boss, BossEncounterStep step)
    {
        for (var depth = 0; depth <= 8; depth++)
        {
            if (step.Chance < 1 && !_random.Prob(step.Chance)) return null;       // one chance roll per scheduled step
            if (step.Choices.Count == 0) return step;
            var choices = step.Choices.Where(choice => EntityManager.System<BossMechanicDefinitionSystem>().Supports(boss, choice)).ToArray();
            if (choices.Length == 0) return null;
            var roll = _random.NextDouble() * choices.Sum(choice => (double)choice.Weight);
            step = choices[^1];
            foreach (var choice in choices)
            {
                roll -= choice.Weight;
                if (roll > 0) continue;
                step = choice;
                break;
            }
        }
        return null;
    }

    private void AdvanceStep(EntityUid boss, BossEncounterComponent encounter)
    {
        encounter.RunningStep = null;
        encounter.Step++;
        if (encounter.Step < ActiveSteps(boss, encounter).Count) return;
        encounter.Step = 0;
        if (!encounter.Repeating) encounter.Repeating = true;
        else if (!encounter.Escalating && encounter.Escalation.Count > 0)
        {
            encounter.Escalating = true;
            encounter.Lap = 0;
        }
        else encounter.Lap++;
    }
}
