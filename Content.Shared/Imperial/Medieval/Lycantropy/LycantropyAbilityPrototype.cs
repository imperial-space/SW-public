using System.Numerics;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.Lycantropy;

[Prototype("lycantropyAbility")]
public sealed partial class LycantropyAbilityPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string Name { get; private set; } = default!;

    [DataField(required: true)]
    public string Desc { get; private set; } = default!;

    [DataField(required: true)]
    public SpriteSpecifier Icon { get; private set; } = default!;

    [DataField(required: true)]
    public int Cost { get; private set; } = 5;

    [DataField(required: true)]
    public Vector2 Position;

    [DataField]
    public ProtoId<LycantropyAbilityPrototype>[] Required = Array.Empty<ProtoId<LycantropyAbilityPrototype>>();

    [DataField]
    public EntProtoId[] HumanActions { get; private set; } = Array.Empty<EntProtoId>();

    [DataField]
    public EntProtoId[] WerewolfActions { get; private set; } = Array.Empty<EntProtoId>();

    [DataField(serverOnly: true)]
    public object? Event;
}
