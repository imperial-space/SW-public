using Robust.Shared.Prototypes;


namespace Content.Shared.Imperial.Medieval.Curse;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
[AutoGenerateComponentState]
public sealed partial class CurseItemComponent : Component
{
    [DataField]
    [AutoNetworkedField]
    public ProtoId<CursePrototype> Curse { get; set; } = string.Empty; // УУУ загатовки на будущее

    [DataField]
    public bool Cult;
}
