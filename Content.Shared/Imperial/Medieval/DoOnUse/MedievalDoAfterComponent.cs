using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.DoOnUse.DoAfter;
[RegisterComponent]
public sealed partial class MedievalDoAfterEveryComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField(required: true)]
    public string Name = "дать хит";
    [ViewVariables(VVAccess.ReadWrite), DataField]
    public TypeMedievalDoAfter Type = TypeMedievalDoAfter.Hit;
    [ViewVariables(VVAccess.ReadWrite), DataField]
    public string TypeHit = "Blunt";
    [ViewVariables(VVAccess.ReadWrite), DataField]
    public int NumHit = 5;
}
public enum TypeMedievalDoAfter : byte
{
    Hit,
}
[NetSerializable, Serializable]
public sealed partial class MedievalHitOnDoAfter : SimpleDoAfterEvent { }
