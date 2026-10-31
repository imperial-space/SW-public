namespace Content.Server.Imperial.Medieval.Boss;

[RegisterComponent]
public sealed partial class BossCastingComponent : Component
{
    public BossAttack Attack = default!;
    public List<EntityUid> Targets = new();
    public EntityUid? TargetMarker;
    public EntityUid? ExecutionLine;
    public bool RequireActiveEncounter;
    public bool Resolving;
}
