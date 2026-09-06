using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Magic.ActionOrder;

[RegisterComponent]
public sealed partial class PendingMedievalActionReplacementComponent : Component
{
    public List<PendingMedievalActionReplacement> Replacements = [];
}

public sealed class PendingMedievalActionReplacement
{
    public NetEntity OldAction;
    public EntityUid Performer;
    public HashSet<EntProtoId> ReplacementPrototypes = [];
}
