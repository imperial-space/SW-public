using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.RPPanel;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RPPanelMemberComponent : Component
{
    [DataField, AutoNetworkedField]
    public float DistantRange = 15f;

    [DataField, AutoNetworkedField]
    public float ContactRange = 1f;

    [DataField]
    public TimeSpan ShakeDuration = TimeSpan.FromSeconds(1);

    [DataField, AutoNetworkedField]
    public TimeSpan InteractionCooldown = TimeSpan.FromSeconds(1);

    [AutoNetworkedField]
    public List<EntityUid> RecentInitiators = new();

    public List<EntityUid> PendingInitiators = new();

    public EntityUid? HugInitiator;

    public int ShakeSequence;
}
