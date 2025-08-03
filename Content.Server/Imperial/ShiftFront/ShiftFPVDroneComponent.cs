using Robust.Shared.GameStates;
using Content.Shared.Damage;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;

namespace Content.Server.ShiftFront.Components;

[RegisterComponent]
public sealed partial class ShiftFPVDroneComponent : Component
{
    public DamageSpecifier Damage = new()
    {
        DamageDict = new()
        {
            { "AntiTank", 120 }
        }
    };

    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>)), ViewVariables(VVAccess.ReadOnly)]
    public string EffectSoundOnLink = "/Audio/Items/beep.ogg";

    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>)), ViewVariables(VVAccess.ReadOnly)]
    public string EffectSoundOnStart = "/Audio/Imperial/ShiftFront/drone_start.ogg";

    [DataField]
    public EntityUid? Pilot;

    [DataField]
    public EntityUid? Controller;

    [DataField]
    public string ExplosionEffect = "FPVExplodeEffect";

    [DataField]
    public bool Explosive = true;

    [DataField]
    public bool Pacif = true;

    [DataField]
    public bool CMD = false;

    [DataField]
    public bool TankPart = false;

    [DataField]
    public string Faction = "";

    [DataField]
    public int MinFreq = 2200;

    [DataField]
    public int CurFreq = 0;

    [DataField]
    public int MaxFreq = 5800;

}

