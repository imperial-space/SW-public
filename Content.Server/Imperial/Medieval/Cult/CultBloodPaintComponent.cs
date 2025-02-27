using Content.Shared.Damage;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;

namespace Content.Server.Cult.Components;


[RegisterComponent]
public sealed partial class CultBloodPaintComponent : Component
{
    [DataField]
    public int PosX = 0;

    [DataField]
    public int PosY = 0;

    [DataField]
    public bool Bloody = false;
}
