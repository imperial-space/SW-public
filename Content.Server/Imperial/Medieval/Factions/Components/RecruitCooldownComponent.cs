namespace Content.Server.Imperial.Medieval.Factions.Components;

/// <summary>
/// Tracks when a Traveller can next be offered recruitment into a faction,
/// so a declined/ignored offer can't be spammed on the same person.
/// </summary>
[RegisterComponent]
public sealed partial class RecruitCooldownComponent : Component
{
    [DataField]
    public TimeSpan NextEligibleTime;
}
