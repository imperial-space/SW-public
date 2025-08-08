using Content.Shared.Imperial.Medieval.Plague;
using Robust.Client.Player;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Medieval.Plague;

public sealed partial class MedievalPlagueSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalPlagueGhostComponent, OpenPlagueEvolutionMenuActionEvent>(OnOpenMenu);
    }

    private void OnOpenMenu(EntityUid uid, MedievalPlagueGhostComponent comp, OpenPlagueEvolutionMenuActionEvent args)
    {
        if (_player.LocalEntity != uid || !_timing.IsFirstTimePredicted)
            return;

        var request = new RequestPlagueMenuDataMessage(GetNetEntity(uid));
        RaiseNetworkEvent(request);
    }
}
