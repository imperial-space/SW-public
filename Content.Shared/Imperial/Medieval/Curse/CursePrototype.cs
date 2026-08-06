using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Curse;

/// <summary>
/// This is a prototype for...
/// </summary>
[Prototype("medievalCurse")]
public sealed partial class CursePrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string Name = default!;

    [DataField]
    public int Level;
}
