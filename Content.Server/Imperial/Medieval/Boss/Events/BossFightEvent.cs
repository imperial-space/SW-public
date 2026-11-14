namespace Content.Server.Imperial.Medieval.Boss;

[ByRefEvent]
public readonly record struct BossFightEvent(EntityUid Boss, bool Running);
