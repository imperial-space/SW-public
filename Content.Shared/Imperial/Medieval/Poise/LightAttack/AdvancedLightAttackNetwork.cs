using Robust.Shared.Network;

namespace Content.Shared.Imperial.Medieval.Poise.LightAttack;

/// <summary>
/// Application-lifetime registration for the direct visual packet.
/// NetManager outlives client entity systems across disconnect/reconnect, so its
/// callback must not belong to a particular world's AdvancedLightAttackSystem.
/// </summary>
public sealed class AdvancedLightAttackNetwork
{
    [Dependency] private readonly INetManager _net = default!;
    private bool _initialized;

    public event Action<MsgAdvancedLightAttackCue>? CueReceived;

    public void Initialize()
    {
        if (_initialized)
            return;

        _net.RegisterNetMessage<MsgAdvancedLightAttackCue>(OnCue, NetMessageAccept.Client);
        _initialized = true;
    }

    private void OnCue(MsgAdvancedLightAttackCue message)
        => CueReceived?.Invoke(message);
}
