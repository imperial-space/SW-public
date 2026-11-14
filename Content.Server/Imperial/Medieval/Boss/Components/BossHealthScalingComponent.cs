namespace Content.Server.Imperial.Medieval.Boss;

[RegisterComponent]
public sealed partial class BossHealthScalingComponent : Component
{
    [DataField]
    public float MaxHealth = 4000f;                                              // health at the maximum player count

    [DataField]
    public int MaxPlayers = 8;

    [DataField]
    public float ArenaRadius = 20f;                                              // player search radius in map units

    [ViewVariables]
    public bool Locked;                                                          // stops health scaling after the first hit

    [ViewVariables]
    public int PlayerCount;
}
