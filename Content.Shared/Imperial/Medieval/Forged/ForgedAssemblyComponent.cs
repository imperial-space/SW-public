using Robust.Shared.Prototypes;

namespace Content.Shared.Forged;

[RegisterComponent]
public sealed partial class ForgedAssemblyComponent : Component
{
    /// <summary>
    /// Список слотов, которые обязательно должны быть заполнены для "оживления".
    /// </summary>
    [DataField("requiredSlots")]
    public List<string> RequiredSlots = new() { "head", "right_arm", "left_arm", "right_hand", "left_hand", "core", "right_leg", "left_leg" };

    /// <summary>
    /// Хранит ID вставленных сущностей, чтобы потом перенести их в финального моба.
    /// </summary>
    [ViewVariables]
    public Dictionary<string, EntityUid> FittedModules = new();

    [DataField("Torso"), ViewVariables(VVAccess.ReadWrite)]
    public EntProtoId? PrototypeId;
}
