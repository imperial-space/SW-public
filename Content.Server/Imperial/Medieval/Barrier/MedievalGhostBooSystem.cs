using Content.Shared.Popups;
using Content.Shared.MedievalBoo.Events;
using Content.Shared.MedievalGhost.Components;
using Robust.Shared.Network;
using Content.Shared.Actions;
using Content.Server.MagicBarrier.Components;
using Content.Server.SpikeTrap.Components;

namespace Content.Server.MedievalGhost;
public partial class MedievalGhostBooSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly INetManager _netMan = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MedievalGhostBooEvent>(OnBoo);
        SubscribeLocalEvent<MedievalBooComponent, ComponentStartup>(OnStart);
    }
    private void OnStart(EntityUid uid, MedievalBooComponent component, ComponentStartup args)
    {
        _actionsSystem.AddAction(uid, "MedievalGhostBooAction", uid);
    }
    private void OnBoo(MedievalGhostBooEvent args)
    {
        if (args.Handled)
            return;
        bool played = false;
        args.Handled = true;
        var xform = Transform(args.Performer);
        var coords = xform.Coordinates;
        Spawn("MedievalGhostWind", coords);
        foreach (var barrier in EntityManager.EntityQuery<MagicBarrierComponent>())
        {
            barrier.GhostBoo++;
        }
        foreach (var uid in _lookup.GetEntitiesInRange(coords, 5.5f, flags: LookupFlags.Dynamic))
        {
            if (!played && _netMan.IsServer)
            {
                _popup.PopupEntity(Loc.GetString("medieval-hm-barrier-ghostboo-wind"), args.Performer, args.Performer, PopupType.Large);
                played = true;
            }
            if (uid != args.Performer)
            {
                _popup.PopupEntity(Loc.GetString("medieval-hm-barrier-ghostboo-feelwind"), uid, uid, PopupType.LargeCaution);
                if (HasComp<MedievalSpikeTargetComponent>(uid))
                {
                    foreach (var barrier in EntityManager.EntityQuery<MagicBarrierComponent>())
                    {
                        barrier.GhostBooPlayers++;
                    }
                }
            }
        }

    }
}
