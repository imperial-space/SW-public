namespace Content.Server.Imperial.Medieval.Boss;

public sealed class BossArenaGateStateChangedEvent(bool open) : EntityEventArgs
{
    public bool Open = open;
}
