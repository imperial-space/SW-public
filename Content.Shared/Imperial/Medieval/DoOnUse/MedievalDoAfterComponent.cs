using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.DoOnUse.DoAfter;
[RegisterComponent]
public sealed partial class MedievalDoAfterEveryComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField (required: true)]
    public LocId NameLocId;
    [ViewVariables(VVAccess.ReadWrite), DataField]
    public TypeMedievalDoAfter Type = TypeMedievalDoAfter.Hit;
    [ViewVariables(VVAccess.ReadWrite), DataField]
    public string TypeHit = "Blunt";
    [ViewVariables(VVAccess.ReadWrite), DataField]
    public float NumHit = 5.0f;
    [ViewVariables(VVAccess.ReadWrite), DataField (required: true)]
    public TimeSpan Time = TimeSpan.Zero;
}
public enum TypeMedievalDoAfter : byte
{
    Hit,
}

[Serializable, NetSerializable]
public enum MedievalBerryBushVisuals : byte
{
    HasBerries,
}

[NetSerializable, Serializable]
public sealed partial class MedievalHitOnDoAfter : SimpleDoAfterEvent { }

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MedievalBerryBushComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Collected;

    [DataField]
    public TimeSpan? RegrowAt;

    [DataField]
    public float MinRegrowMinutes = 14f;

    [DataField]
    public float MaxRegrowMinutes = 16f;

    [DataField]
    public string BerriesPrototype = "FoodBerries";
}

[NetSerializable, Serializable]
public sealed partial class MedievalCollectBerryDoAfter : SimpleDoAfterEvent { }

[NetSerializable, Serializable]
public sealed partial class MedievalUprootBushDoAfter : SimpleDoAfterEvent { }
