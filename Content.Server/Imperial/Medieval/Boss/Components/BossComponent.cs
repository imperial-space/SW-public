using System.Threading;

namespace Content.Server.Imperial.Medieval.Boss;

[RegisterComponent]
public sealed partial class BossComponent : Component
{
    [DataField]
    public bool Active = false;

    [DataField(required: true)]
    public Dictionary<int, BossStageData> Stages = new();

    [DataField(required: true)]
    public float Health = 100f;

    [ViewVariables(VVAccess.ReadWrite)]
    public int Stage = 1;

    [ViewVariables(VVAccess.ReadWrite)]
    public List<EntityUid> Players = new List<EntityUid>();

    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan NextAttack = TimeSpan.Zero;
}
