using Content.Shared.Damage;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;

namespace Content.Server.Cult.Components;


[RegisterComponent]
public sealed partial class CultMapBlockerComponent : Component
{
    [DataField]
    public string Sector = "sector1";
}
