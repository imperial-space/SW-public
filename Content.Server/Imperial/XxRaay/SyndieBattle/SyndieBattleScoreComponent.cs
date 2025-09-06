using Robust.Shared.Player;
using Robust.Shared.Network;

namespace Content.Server.Imperial.XxRaay.SyndieBattle;

[RegisterComponent]
public sealed partial class SyndieBattleScoreComponent : Component
{
    [DataField]
    public int Score;

    [DataField]
    public NetUserId? PlayerId;

    [DataField]
    public int KillCount;
}
