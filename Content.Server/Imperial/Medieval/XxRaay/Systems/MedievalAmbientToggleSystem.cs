using Content.Shared.Imperial.Medieval.XxRaay.MedievalAmbientToggle;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Player;

namespace Content.Server.Imperial.Medieval.XxRaay.Systems;

public sealed class MedievalAmbientToggleSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    private bool _medievalAmbientEnabled = true;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
    }

    /// <summary>
    
    /// </summary>
    public bool IsMedievalAmbientEnabled => _medievalAmbientEnabled;

    /// <summary>
    
    /// </summary>
    public bool SetMedievalAmbientEnabled(bool enabled)
    {
        if (_medievalAmbientEnabled == enabled)
            return enabled;
        _medievalAmbientEnabled = enabled;
        BroadcastState();
        return _medievalAmbientEnabled;
    }

    /// <summary>
    
    /// </summary>
    public bool ToggleMedievalAmbient()
    {
        _medievalAmbientEnabled = !_medievalAmbientEnabled;
        BroadcastState();
        return _medievalAmbientEnabled;
    }

    private void BroadcastState()
    {
        var ev = new MedievalAmbientToggledEvent(_medievalAmbientEnabled);
        RaiseNetworkEvent(ev, Filter.Empty().AddPlayers(_playerManager.NetworkedSessions));
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        RaiseNetworkEvent(new MedievalAmbientToggledEvent(_medievalAmbientEnabled), ev.Player);
    }
}
