namespace Content.Shared.Imperial.Medieval.FullContainer.Components;

using Content.Shared.Storage;

[RegisterComponent]
public sealed partial class FullContainerComponent : Component
{
    [DataField(required: true)]
    public List<EntitySpawnEntry> Fullable = new();
}
