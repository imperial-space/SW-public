using Content.Server.GameTicking.Events;
using Content.Shared.Imperial.Medieval.ChemistryRandomization;
using Robust.Server.Player;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.ChemistryRandomization;

public sealed partial class ChemistryRandomizationSystem : EntitySystem
{
    [Dependency] private readonly SharedChemistryRandomizationSystem _chemRandom = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
        SubscribeNetworkEvent<RequestChemistryRandomizationSeedMessage>(OnRequestSeed);
    }

    private void OnRoundStarting(RoundStartingEvent args)
    {
        _chemRandom.Seed = _random.Next();

        var ev = new SetChemistryRandomizationSeedMessage(_chemRandom.Seed);
        RaiseNetworkEvent(ev);

        _chemRandom.GeneratePotions();
    }

    private void OnRequestSeed(RequestChemistryRandomizationSeedMessage args)
    {
        if (args.Seed == _chemRandom.Seed)
            return;
        if (!_player.TryGetSessionByUsername(args.Username, out var session))
            return;

        var ev = new SetChemistryRandomizationSeedMessage(_chemRandom.Seed);
        RaiseNetworkEvent(ev, session);
    }
}
