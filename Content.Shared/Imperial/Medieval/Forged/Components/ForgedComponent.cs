using Robust.Shared.GameStates;

namespace Content.Shared.Forged;

[RegisterComponent, NetworkedComponent]
public sealed partial class ForgedComponent : Component
{
    [ViewVariables]
    public Dictionary<string, EntityUid> FittedModules = new();

    public TimeSpan LastExplosivePress = TimeSpan.Zero;
}
