namespace Content.Shared.Imperial.Medieval.Bee.Components;

[RegisterComponent]
public sealed partial class MedievalBeeTrapComponent : Component
{
    [DataField]
    public TimeSpan StunTime = TimeSpan.FromSeconds(120);
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(60);
    public TimeSpan? CooldownEnd;
}
