using Content.Server.Imperial.Medieval.Ships.Sea.Init;

namespace Content.Server.Imperial.Medieval.Ships.Sea.Generation;

[RegisterComponent]
public sealed partial class SeasGenerationStateComponent : Component
{
    [ViewVariables]
    public bool SeaInitialized;

    [ViewVariables]
    public SeaMatrix? SeaMatrix;
}
