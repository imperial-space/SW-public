using Content.Server.GameTicking.Rules.Components;

namespace Content.Server.Imperial.XxRaay.SyndieBattle;

[RegisterComponent]
public sealed partial class SyndieBattleRuleComponent : Component
{
    [DataField]
    public bool GiveUplink = true;

    [DataField]
    public bool GiveCodewords = true;

    [DataField]
    public bool GiveBriefing = true;

    [ViewVariables(VVAccess.ReadOnly)]
    public bool Active;
}


