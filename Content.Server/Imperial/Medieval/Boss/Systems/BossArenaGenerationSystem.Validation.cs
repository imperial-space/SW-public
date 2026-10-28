using System.Linq;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class BossArenaGenerationSystem
{
    public bool IsValid(BossArenaPrototype arena)
        => IsValidLayout(arena) && IsValidObjects(arena) && IsValidBoss(arena);

    private bool IsValidLayout(BossArenaPrototype arena)
    {
        if (arena.HalfSize is < BossArenaPrototype.MinHalfSize or > BossArenaPrototype.MaxHalfSize ||
            arena.MaxPlayers is < 1 or > BossArenaPrototype.MaxPartySize ||
            arena.VictoryLootDuration <= TimeSpan.Zero || string.IsNullOrWhiteSpace(arena.MessagePrefix))
            return false;

        return _prototypes.TryIndex(arena.Floor, out var floor) && !floor.Abstract && floor.TileId != 0 &&
               IsValidPosition(arena.ArrivalTile, arena.HalfSize) && IsValidPosition(arena.ExitTile, arena.HalfSize);
    }

    private bool IsValidObjects(BossArenaPrototype arena)
    {
        var entrance = FindComponent<BossArenaEntranceComponent>(arena.EntrancePrototype);
        if (entrance == null ||
            !HasOptionalComponent<BossSummoningTriggerComponent>(arena.SummoningPrototype) ||
            !HasOptionalComponent<BossArenaCenterBlockerComponent>(arena.CenterBlockerPrototype) ||
            arena.BrazierPrototype is { } brazier && FindPrototype(brazier) == null)
            return false;

        var x = arena.ArrivalTile.X + 0.5f + entrance.ArrivalOffset.X;
        var y = arena.ArrivalTile.Y + 0.5f + entrance.ArrivalOffset.Y;
        if (!float.IsFinite(x) || !float.IsFinite(y) || x < -arena.HalfSize || y < -arena.HalfSize ||
            x >= arena.HalfSize + 1 || y >= arena.HalfSize + 1)
            return false;

        return arena.VictoryExitPrototype is not { } exit ||
               FindComponent<BossArenaGateComponent>(exit) is { ReturnToGate: true } &&
               FindComponent<BossArenaExitTimerComponent>(exit) != null;
    }

    private bool IsValidBoss(BossArenaPrototype arena)
    {
        var boss = FindComponent<BossComponent>(arena.BossPrototype);
        var encounter = FindComponent<BossEncounterComponent>(arena.BossPrototype);
        if (boss == null || encounter == null || FindComponent<BossTelegraphSettingsComponent>(arena.BossPrototype) == null ||
            !float.IsFinite(boss.Health) || boss.Health <= 0 ||
            !IsValidDelay(encounter.OpeningDelay) || !IsValidDelay(encounter.Recovery) ||
            !IsValidDelay(encounter.EntryGrace) || !IsValidDelay(encounter.IdleReset) ||
            !IsValidDelay(encounter.TankInterval) || encounter.TankInterval == 0 || !HasValidPhases(encounter))
            return false;

        var steps = encounter.Phases.Count > 0
            ? encounter.Phases.SelectMany(phase => phase.Steps)
            : encounter.Tutorial.Concat(encounter.Rotation).Concat(encounter.Escalation);
        return encounter.PatternDamage.Values.All(key => encounter.Damage.TryGetValue(key, out var damage) &&
                   BossMechanicSystem.IsValidDamage(damage)) &&
               steps.All(step => IsValidStep(step, encounter, boss, arena));
    }

    private static bool HasValidPhases(BossEncounterComponent encounter)
    {
        if (encounter.Phases.Count == 0)
            return encounter.Tutorial.Count + encounter.Rotation.Count + encounter.Escalation.Count > 0;

        var ids = new HashSet<string>();
        for (var index = 0; index < encounter.Phases.Count; index++)
        {
            var phase = encounter.Phases[index];
            if (string.IsNullOrWhiteSpace(phase.Id) || !ids.Add(phase.Id) || phase.Steps.Count == 0 ||
                phase.Repeat && index < encounter.Phases.Count - 1)
                return false;
        }
        return true;
    }

    private static bool IsValidStep(BossEncounterStep step, BossEncounterComponent encounter,
        BossComponent boss, BossArenaPrototype arena)
    {
        var castTime = step.CastTime + (step.PreloadOnly || step.StoredStrike ? 2 : 0);
        if (!encounter.Damage.TryGetValue(step.Damage, out var damage) || !BossMechanicSystem.IsValidDamage(damage) || step.CastTime <= 0 ||
            !BossMechanicSystem.IsValidTimings(castTime, step.FollowupDelay) ||
            step.RecoveryAfter is { } recovery && !IsValidDelay(recovery))
            return false;
        if (step.Combo && !boss.Stages.Values.SelectMany(stage => stage.Attacks).OfType<BossPreparedSequenceAttack>().Any())
            return false;

        return arena.BrazierPrototype != null || !(step.RandomCorner || BossTilePatterns.IsBlackout(step.Pattern) ||
            step.Alternate is { } alternate && BossTilePatterns.IsBlackout(alternate) ||
            step.SimultaneousPattern is { } simultaneous && BossTilePatterns.IsBlackout(simultaneous));
    }

    private static bool IsValidDelay(float seconds)
        => float.IsFinite(seconds) && seconds >= 0 && seconds < TimeSpan.MaxValue.TotalSeconds;

    private static bool IsValidPosition(Vector2i tile, int halfSize)
        => tile != Vector2i.Zero && tile.X >= -halfSize && tile.X <= halfSize &&
           tile.Y >= -halfSize && tile.Y <= halfSize;

    private bool HasOptionalComponent<T>(EntProtoId? id) where T : Component, new()
        => id is not { } value || FindComponent<T>(value) != null;

    private T? FindComponent<T>(EntProtoId id) where T : Component, new()
    {
        var prototype = FindPrototype(id);
        return prototype != null && prototype.TryGetComponent<T>(out var component, EntityManager.ComponentFactory)
            ? component : null;
    }

    private EntityPrototype? FindPrototype(EntProtoId id)
        => _prototypes.TryIndex(id, out var prototype) && !prototype.Abstract ? prototype : null;
}
