using System.Linq;
using Content.Shared.Imperial.Medieval.Boss;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed class BossAttackHistorySystem : EntitySystem
{
    public void RecordStrike(EntityUid boss, BossTelegraphPattern direction)
    {
        var history = EnsureComp<BossSequenceHistoryComponent>(boss);
        history.LastStrikes.Add(direction);
        if (history.LastStrikes.Count > 2) history.LastStrikes.RemoveAt(0);      // keep only the two most recent strikes
    }

    public bool CanFollowHistory(EntityUid boss, IReadOnlyList<BossTelegraphPattern> queue)
    {
        var previous = TryComp<BossSequenceHistoryComponent>(boss, out var history) ? history.LastStrikes : new();
        var joined = previous.Concat(queue).ToArray();
        return !Enumerable.Range(2, Math.Max(0, joined.Length - 2))
            .Any(i => joined[i] == joined[i - 1] && joined[i] == joined[i - 2]); // reject three identical directions in a row
    }

    public static BossTelegraphPattern TransformDirection(BossTelegraphPattern pattern, int rotation, bool mirror)
    {
        if (mirror)
            pattern = pattern switch
            {
                BossTelegraphPattern.Sinistra => BossTelegraphPattern.Dextra,
                BossTelegraphPattern.Dextra => BossTelegraphPattern.Sinistra,
                _ => pattern
            };
        var cycle = new[] { BossTelegraphPattern.Sinistra, BossTelegraphPattern.Tergum,
            BossTelegraphPattern.Dextra, BossTelegraphPattern.Frons };
        var index = Array.IndexOf(cycle, pattern);
        return index < 0 ? pattern : cycle[(index + (rotation % 4 + 4) % 4) % 4];
    }
}
