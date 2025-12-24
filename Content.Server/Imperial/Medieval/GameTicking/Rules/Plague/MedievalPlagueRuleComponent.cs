using Robust.Shared.Audio;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.GameTicking.Rules;

[RegisterComponent]
public sealed partial class MedievalPlagueRuleComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    public bool SentGhosts = false;
}
