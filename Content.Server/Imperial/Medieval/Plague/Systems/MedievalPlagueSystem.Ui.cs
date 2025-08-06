using System.Linq;
using Content.Shared.Imperial.Medieval.Plague;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.Plague;

public sealed partial class MedievalPlagueSystem
{
    private void InitializeUi()
    {
        SubscribeNetworkEvent<RequestPlagueMenuDataMessage>(OnRequestMenuData);
        SubscribeNetworkEvent<AddPlaguePointsMessage>(OnAddPoints);
    }

    private void OnRequestMenuData(RequestPlagueMenuDataMessage args)
    {
        if (!_player.TryGetSessionByEntity(GetEntity(args.Ent), out var session))
            return;

        var message = new PopulatePlagueMenuMessage(_symptoms);
        RaiseNetworkEvent(message, session);
    }

    private void OnAddPoints(AddPlaguePointsMessage args)
    {
        var uid = GetEntity(args.Ent);
        var comp = Comp<MedievalPlagueGhostComponent>(uid);

        var data = _symptoms.GetOrNew(args.Proto);
        if (data.Unlocked || comp.Points < args.Points)
            return;

        data.Points += args.Points;
        comp.Points -= args.Points;

        var proto = _proto.Index(args.Proto);

        if (data.Points >= proto.Cost)
        {
            data.Unlocked = true;
            DoPrototypeEffects(args.Proto);
        }
    }

    private void UpdateUi()
    {
        var message = new PopulatePlagueMenuMessage(_symptoms);
        var ghosts = EntityManager.AllEntities<MedievalPlagueGhostComponent>();
        foreach (var item in ghosts)
        {
            if (!_player.TryGetSessionByEntity(item, out var session))
                continue;

            RaiseNetworkEvent(message, session);
        }
    }
}
