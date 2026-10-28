using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Boss;

namespace Content.Server.Imperial.Medieval.Boss;

[DataDefinition]
public sealed partial class BossEncounterPhase
{
    [DataField]
    public string Id = string.Empty;
    [DataField]
    public List<BossEncounterStep> Steps = new();
    [DataField]
    public bool Repeat;
}
