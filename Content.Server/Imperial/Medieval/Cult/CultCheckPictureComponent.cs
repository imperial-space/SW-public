using Content.Shared.Damage;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;

namespace Content.Server.Cult.Components;


[RegisterComponent]
public sealed partial class CultCheckPictureComponent : Component
{
    [DataField]
    public int BloodyCrystall = 0;

    [DataField]
    public int RedCrystall = 0;

    [DataField]
    public int NewSectorCost = 0;

    [DataField]
    public int UnlockedSectors = 0;

    [DataField]
    public bool CollegiumUnlocked = false;

    [DataField]
    public bool Sector1 = false;

    [DataField]
    public bool Sector2 = false;

    [DataField]
    public bool Sector3 = false;

    [DataField]
    public bool Sector6 = false;

    [DataField]
    public bool Sector7 = false;

    [DataField]
    public bool Sector8 = false;

    [DataField]
    public bool Sector9 = false;



}
