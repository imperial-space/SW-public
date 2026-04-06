using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Imperial.Medieval.Ships.SummonShip;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class SummonShipComponent : Component
{
    [DataField(required: true, customTypeSerializer:typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string File = "/Maps/Imperial/Medieval/Ships/Victoria.yml";

    [DataField]
    public float Delay = 2;
}
