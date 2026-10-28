using System.Linq;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class BossEncounterSystem
{
    public static IReadOnlyList<BossEncounterStep> CurrentSteps(BossEncounterComponent encounter)
        => encounter.Phases.Count > 0
            ? encounter.PhaseIndex < encounter.Phases.Count ? encounter.Phases[encounter.PhaseIndex].Steps : Array.Empty<BossEncounterStep>()
            : encounter.Escalating ? encounter.Escalation : encounter.Repeating ? encounter.Rotation : encounter.Tutorial;

    private List<BossEncounterStep> BuildCombination(EntityUid boss, BossEncounterComponent encounter,
        BossEncounterStep step)
    {
        if (step.CrossSentence)
        {
            var pattern = (encounter.Lap & 1) != 0 && step.Alternate is { } alternate ? alternate : step.Pattern;
            if (pattern is not (BossTelegraphPattern.Cross or BossTelegraphPattern.Divide)) return new();
            var targeted = step.CrossSentenceTarget;
            if (targeted is not (BossTelegraphPattern.Spread or BossTelegraphPattern.Stack)) return new();
            return new()
            {
                new() { Pattern = targeted, PreloadOnly = true, CastTime = 1,
                    SpreadTargets = 3, Damage = "sentence", RecoveryAfter = 0.25f },
                new() { Pattern = pattern, PreloadOnly = true, CastTime = step.CastTime,
                    Damage = "cross", RecoveryAfter = 0.25f },
                new() { Pattern = pattern, StoredStrike = true, CastTime = 5,
                    CastName = EntityManager.System<BossMechanicSystem>().NameKey(boss, "Requiem"), Damage = "cross", SimultaneousPattern = targeted }
            };
        }
        if (step.VigilFollowup is { } followup)
        {
            if ((encounter.Lap & 1) != 0 && step.Alternate is { } alternate) followup = alternate;
            if (followup is not (BossTelegraphPattern.Cross or BossTelegraphPattern.Divide)) return new();
            var name = followup == BossTelegraphPattern.Cross
                ? EntityManager.System<BossMechanicSystem>().NameKey(boss, "Cruciform_Vigil") : EntityManager.System<BossMechanicSystem>().NameKey(boss, "Sundered_Vigil");
            var corner = (BossTelegraphPattern)((int)BossTelegraphPattern.BlackoutNorthWest + _random.Next(4));
            return new()
            {
                new() { Pattern = followup, PreloadOnly = true, CastTime = 3, CastName = name,
                    Damage = "cross", RecoveryAfter = 0.25f },
                new() { Pattern = corner, CastTime = step.CastTime, CastName = name,
                    Damage = "vigil", RecoveryAfter = 0.25f },

                new() { Pattern = followup, StoredStrike = true, HideCast = true, CastTime = 3, Damage = "cross" }
            };
        }
        if (!step.MixedSentence) return new();
        var directions = new List<BossTelegraphPattern>();
        var choices = new[] { BossTelegraphPattern.Sinistra, BossTelegraphPattern.Dextra,
            BossTelegraphPattern.Frons, BossTelegraphPattern.Tergum };
        var history = EntityManager.System<BossSequenceSystem>();
        for (var i = 0; i < 3; i++)
            directions.Add(_random.Pick(choices.Where(direction =>
                history.CanFollowHistory(boss, directions.Append(direction).ToArray())).ToList()));

        var stackFirst = encounter.LastMixedStackFirst is { } last ? !last
            : step.StackFirst ^ ((encounter.Lap & 1) != 0);
        encounter.PendingMixedStackFirst = stackFirst;
        var firstTargeted = stackFirst ? BossTelegraphPattern.Stack : BossTelegraphPattern.Spread;
        var secondTargeted = stackFirst ? BossTelegraphPattern.Spread : BossTelegraphPattern.Stack;
        var includeSecondTargeted = _telegraphs.LivingPlayers(boss).Count > 2;
        var result = new List<BossEncounterStep>();
        for (var i = 0; i < directions.Count; i++)
        {
            result.Add(new()
            {
                Pattern = directions[i],
                PreloadOnly = true,
                CastTime = 3,
                Damage = "cleave",
                RecoveryAfter = 0.25f
            });
            if (i == 0 || i == 1 && includeSecondTargeted)
                result.Add(new()
                {
                    Pattern = i == 0 ? firstTargeted : secondTargeted,
                    PreloadOnly = true,
                    CastTime = 1,
                    Damage = "sentence",
                    RecoveryAfter = 0.25f
                });
        }
        result.Add(new()
        {
            Pattern = directions[0],
            CastTime = 5,
            StoredStrike = true,
            CastName = EntityManager.System<BossMechanicSystem>().NameKey(boss, "Dire_Requiem"),
            Damage = "cleave",
            RecoveryAfter = 0.25f
        });
        result.Add(new()
        {
            Pattern = firstTargeted,
            StoredStrike = true,
            HideCast = true,
            CastTime = 3,
            Damage = "sentence",
            RecoveryAfter = 0.25f
        });
        result.Add(new()
        {
            Pattern = directions[1],
            CastTime = 3,
            StoredStrike = true,
            HideCast = true,
            Damage = "cleave",
            RecoveryAfter = 0.25f
        });
        if (includeSecondTargeted)
            result.Add(new()
            {
                Pattern = secondTargeted,
                StoredStrike = true,
                HideCast = true,
                CastTime = 3,
                Damage = "sentence",
                RecoveryAfter = 0.25f
            });
        result.Add(new() { Pattern = directions[2], CastTime = 3, StoredStrike = true, HideCast = true, Damage = "cleave" });
        return result;
    }

    private void ClearStoredTargets(BossEncounterComponent encounter, BossTelegraphPattern? pattern = null)
    {
        foreach (var entry in encounter.StoredTargets.ToArray())
        {
            if (pattern != null && entry.Key != pattern) continue;
            foreach (var marker in entry.Value.Marks.Values)
                if (Exists(marker) && !Terminating(marker)) Del(marker);
            encounter.StoredTargets.Remove(entry.Key);
        }
    }

    private void ClearPending(BossEncounterComponent encounter)
    {
        ClearStoredTargets(encounter);
        encounter.PendingSteps.Clear();
        encounter.PendingIndex = 0;
        encounter.PendingMixedStackFirst = null;
    }

    private void RememberTargets(EntityUid boss, BossEncounterComponent encounter,
        BossTelegraphPattern pattern, IEnumerable<EntityUid> targets)
    {
        ClearStoredTargets(encounter, pattern);
        var stored = new BossStoredTargets { Targets = targets.ToList() };
        encounter.StoredTargets[pattern] = stored;
        foreach (var target in stored.Targets)
            stored.Marks[target] = _telegraphs.CreateMarker(boss, target,
                pattern == BossTelegraphPattern.Spread ? BossActorMarkerKind.Spread : BossActorMarkerKind.Stack);
    }

    private bool ValidateStoredTargets(EntityUid boss, BossEncounterComponent encounter)
    {
        foreach (var stored in encounter.StoredTargets.Values)
        {
            foreach (var target in stored.Targets.ToArray())
            {
                if (_telegraphs.TryGetPlayerTile(boss, target, out _)) continue;
                stored.Targets.Remove(target);
                if (stored.Marks.Remove(target, out var marker) && Exists(marker) && !Terminating(marker)) Del(marker);
            }
            if (stored.Targets.Count > 0) continue;

            encounter.Resetting = true;
            EntityManager.System<BossSystem>().CancelBossCast(boss);
            encounter.Resetting = false;
            encounter.Waiting = false;
            ClearPending(encounter);
            encounter.NextStep = _timing.CurTime + TimeSpan.FromSeconds(encounter.Recovery);
            return false;
        }
        return true;
    }
}
