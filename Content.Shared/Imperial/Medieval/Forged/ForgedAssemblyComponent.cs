namespace Content.Shared.Forged;

[RegisterComponent]
public sealed partial class ForgedAssemblyComponent : Component
{
    /// <summary>
    /// Список слотов, которые обязательно должны быть заполнены для "оживления".
    /// </summary>
    [DataField("requiredSlots")]
    public List<string> RequiredSlots = new() { "head", "r_arm", "l_arm", "r_hand", "l_hand", "core", "r_leg", "l_leg" };

    /// <summary>
    /// Хранит ID вставленных сущностей, чтобы потом перенести их в финального моба.
    /// </summary>
    [ViewVariables]
    public Dictionary<string, EntityUid> FittedModules = new();
}
