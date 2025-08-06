using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Plague;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MedievalPlagueInfectedComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    public EntityUid? PlagueSource;

    [ViewVariables(VVAccess.ReadWrite)]
    public int Progression = 1;

    [AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    public bool Incubation = true;

    [ViewVariables(VVAccess.ReadWrite)]
    public float UpdatePeriod = 15f;

    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan NextProgression = TimeSpan.Zero;
}
