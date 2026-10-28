namespace Content.Server.Imperial.Medieval.Boss;

[RegisterComponent]
public sealed partial class BossHealthScalingComponent : Component
{
    [DataField]
    public float MaxHealth = 4000f;

    [DataField]
    public int MaxPlayers = 8;

    [DataField]
    public float ArenaRadius = 20f;

    [ViewVariables]
    public bool Locked;

    [ViewVariables]
    public int PlayerCount;
}
