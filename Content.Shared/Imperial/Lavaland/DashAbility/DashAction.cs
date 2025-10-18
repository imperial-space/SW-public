using Content.Shared.Actions;

namespace Content.Shared.Imperial.Abilities.Urs;

public sealed partial class UrsDashAction : WorldTargetActionEvent

{
    [DataField] public float PushStrength = 30f;
    [DataField] public float ReversePushStrength = 50f;
    [DataField, AutoNetworkedField] public TimeSpan StunTime = TimeSpan.FromSeconds(6);

}

