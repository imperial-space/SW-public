namespace Content.Server.Imperial.Medieval.Boss;

public enum BossMechanicPhase : byte { Started, Updating, Impact, Recovery, Stopping }

[ByRefEvent]
public struct BossMechanicEvent(EntityUid boss, BossMechanicComponent mechanic, BossMechanicPhase phase,
    EntityUid? impact = null, List<EntityUid>? victims = null)
{
    public readonly EntityUid Boss = boss;
    public readonly BossMechanicComponent Mechanic = mechanic;
    public readonly BossMechanicPhase Phase = phase;
    public readonly EntityUid? Impact = impact;
    public readonly List<EntityUid>? Victims = victims;
    public bool Handled;
}
