using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.GameTicking.Rules;

[RegisterComponent, Access(typeof(BlackSpireSpawnRuleSystem))]
public sealed partial class BlackSpireSpawnRuleComponent : Component
{
    [DataField]
    public EntProtoId SpirePrototype = "MedievalBlackSpire";

    [DataField]
    public LocId Announcement = "medieval-black-spire-event-announcement";

    [DataField]
    public LocId AnnouncementSender = "medieval-black-spire-event-sender";
}
