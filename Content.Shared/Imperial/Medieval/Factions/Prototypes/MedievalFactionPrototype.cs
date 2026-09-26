using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.Factions.Prototypes;

[Prototype]
public sealed class MedievalFactionPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string Name = default!;

    [DataField]
    public bool ShowKnown = true;

    [DataField]
    public bool AllowHeadhunt = true;

    [DataField]
    public Color Color = Color.White;

    [DataField]
    public SpriteSpecifier? Icon = null;

    [DataField]
    public Dictionary<ProtoId<MedievalFactionPrototype>, string> KnownFactions = new();

    [DataField]
    public Dictionary<ProtoId<MedievalFactionPrototype>, ProtoId<FactionRelationsPrototype>> DefaultRelations = new();

    [DataField]
    public List<ProtoId<MedievalFactionPrototype>> BlockedRelations = new();

    [DataField]
    public string? WantedText;

    [DataField]
    public EntProtoId EnvelopeProto = "MedievalPaperEnvelopeBasic";

    /// <summary>
    /// If set, faction leaders can recruit a Traveller into this faction at this job's rank.
    /// Factions without this field cannot recruit Travellers.
    /// </summary>
    [DataField]
    public ProtoId<JobPrototype>? RecruitJob;

    /// <summary>
    /// Communication crystal given to a freshly recruited member, if this faction has one.
    /// </summary>
    [DataField]
    public EntProtoId? RecruitCrystal;

    /// <summary>
    /// Access key given to a freshly recruited member, if this faction has one.
    /// </summary>
    [DataField]
    public EntProtoId? RecruitKey;

    /// <summary>
    /// Clan cloak (worn in the neck slot) given to a freshly recruited member, if this faction has one.
    /// </summary>
    [DataField]
    public EntProtoId? RecruitCloak;
}
