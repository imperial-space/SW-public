using Content.Shared.Imperial.Medieval.Boss;

namespace Content.Server.Imperial.Medieval.Boss;

[RegisterComponent]
public sealed partial class BossSequenceHistoryComponent : Component
{
    public List<BossTelegraphPattern> Last = new();
    public List<BossTelegraphPattern> LastStrikes = new();
}
