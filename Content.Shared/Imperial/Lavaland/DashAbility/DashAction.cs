using Content.Shared.Actions;

namespace Content.Shared.Imperial.Abilities.Urs;

public sealed partial class UrsDashAction : WorldTargetActionEvent

{
    [DataField]
    public float PushStrength = 30f;
    public float ReversePushStrength = 50f;


};

