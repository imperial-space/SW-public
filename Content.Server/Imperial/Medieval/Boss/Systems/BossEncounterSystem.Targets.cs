using System.Linq;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class BossEncounterSystem
{
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
        encounter.PendingSteps.Clear();                                         // drop the old markers and queue together
        encounter.PendingIndex = 0;
        encounter.PendingMixedStackFirst = null;
    }

    private void RememberTargets(EntityUid boss, BossEncounterComponent encounter,
        BossTelegraphPattern pattern, IEnumerable<EntityUid> targets)
    {
        ClearStoredTargets(encounter, pattern);                                  // remove old target markers before replacing them
        var stored = new BossStoredTargets { Targets = targets.ToList() };      // keep the same players for the later hit
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
                stored.Targets.Remove(target);                                   // drop players who are no longer valid targets
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
