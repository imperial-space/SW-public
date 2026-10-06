using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Alchemy;

[RegisterComponent]
public sealed partial class AlchemyExplosiveMixtureComponent : Component
{
    [DataField(required: true)] public EntProtoId BombPrototype;
    [DataField] public string TriggerKey = "timer";
    public bool Detonated;
}
