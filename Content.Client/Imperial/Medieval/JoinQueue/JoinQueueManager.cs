using Content.Shared.Imperial.Medieval.JoinQueue;
using Robust.Client.State;
using Robust.Shared.Network;

namespace Content.Client.Imperial.Medieval.JoinQueue;

public sealed class JoinQueueManager
{
    [Dependency] private readonly IClientNetManager _net = default!;
    [Dependency] private readonly IStateManager _state = default!;

    public void Initialize()
    {
        _net.RegisterNetMessage<MsgQueueUpdate>(OnQueueUpdate);
    }

    private void OnQueueUpdate(MsgQueueUpdate msg)
    {
        if (_state.CurrentState is not QueueState)
            _state.RequestStateChange<QueueState>();

        ((QueueState) _state.CurrentState).OnQueueUpdate(msg);
    }
}
