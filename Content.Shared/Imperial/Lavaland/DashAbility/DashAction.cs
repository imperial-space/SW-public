using Content.Shared.Actions;

namespace Content.Shared.Imperial.Abilities.Urs;

public sealed partial class UrsDashAction : EntityTargetActionEvent
{
    public float PushStrength = 30f;
    [DataField]
    public float ReversePushStrength = 50f;
    [DataField]
    public TimeSpan StunTime = TimeSpan.FromSeconds(2f);
}
