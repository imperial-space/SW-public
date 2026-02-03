namespace Content.Shared.Forged;

[RegisterComponent]
public sealed partial class ForgedComponent : Component
{
    [ViewVariables]
    public Dictionary<string, EntityUid> FittedParts = new();
}
