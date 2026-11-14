using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Boss;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed class BossStoredTargets
{
    public List<EntityUid> Targets = new();
    public Dictionary<EntityUid, EntityUid> Marks = new();
}
