namespace Content.Server.Imperial.Medieval.Boss;

[ByRefEvent]
public struct BossArenaAccessEvent(EntityUid target, EntityUid user, EntityUid? item, string arenaId)
{
    public EntityUid Target = target;
    public EntityUid User = user;
    public EntityUid? Item = item;
    public string ArenaId = arenaId;
    public bool Required;
    public bool Allowed = true;
    public bool ConsumeItem;
}
