namespace Content.Server.GameTicking.Rules.Components;

[RegisterComponent]
public sealed partial class PiratesShuttleComponent : Component
{
    [DataField]
    public EntityUid AssociatedRule;
}
