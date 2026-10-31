using System.Linq;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed class BossCombinationSystem : EntitySystem, IBossActionFeature
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly BossTelegraphSystem _telegraphs = default!;
    public string Id => "stored-combinations";
    public override void Initialize() => EntityManager.System<BossActionSystem>().Register(this);
    public bool SupportsCombination(BossEncounterStep step)
        => step.MixedSentence || step.CrossSentence || step.VigilFollowup != null;

    public List<BossEncounterStep> BuildCombination(EntityUid boss, BossEncounterComponent encounter,
        BossEncounterStep step)
    {
        if (step.CrossSentence)                                                  // build the stored cross and target combination
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
                    CastName = EntityManager.System<BossCastNameSystem>().NameKey(boss, "Requiem"), Damage = "cross", SimultaneousPattern = targeted }
            };
        }
        if (step.VigilFollowup is { } followup)
        {
            if ((encounter.Lap & 1) != 0 && step.Alternate is { } alternate) followup = alternate;
            if (followup is not (BossTelegraphPattern.Cross or BossTelegraphPattern.Divide)) return new();
            var name = followup == BossTelegraphPattern.Cross
                ? EntityManager.System<BossCastNameSystem>().NameKey(boss, "Cruciform_Vigil") : EntityManager.System<BossCastNameSystem>().NameKey(boss, "Sundered_Vigil");
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
        var history = EntityManager.System<BossAttackHistorySystem>();
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
            CastName = EntityManager.System<BossCastNameSystem>().NameKey(boss, "Dire_Requiem"),
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

}
