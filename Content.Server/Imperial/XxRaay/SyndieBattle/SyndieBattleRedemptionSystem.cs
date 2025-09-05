using Content.Shared.Interaction;
using Robust.Server.GameObjects;
using Content.Shared.Popups;
using Robust.Shared.Player;
using Content.Server.Traitor.Uplink;
using Content.Shared.PDA;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.XxRaay.SyndieBattle;

public sealed class SyndieBattleRedemptionSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly UplinkSystem _uplink = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SyndieBattleRedemptionComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnInteractUsing(Entity<SyndieBattleRedemptionComponent> ent, ref InteractUsingEvent args)
    {
        if (!TryComp<ActorComponent>(args.User, out var actor))
            return;

        if (!TryComp<PdaComponent>(args.Used, out var pda))
        {
            _popup.PopupEntity(Loc.GetString("syndiebattle-redemption-no-pda"), ent, args.User);
            return;
        }

        var uplinkTarget = _uplink.FindUplinkTarget(args.Used);
        if (uplinkTarget == null)
        {
            _popup.PopupEntity(Loc.GetString("syndiebattle-redemption-no-uplink"), ent, args.User);
            return;
        }

        var reward = ent.Comp.BaseReward;
        var spawnPos = Transform(args.User).Coordinates;
        var spawnedCount = 0;

        for (int i = 0; i < reward; i++)
        {
            if (_prototype.TryIndex<EntityPrototype>("Telecrystal", out var itemProto))
            {
                Spawn(itemProto.ID, spawnPos);
                spawnedCount++;
            }
        }

        if (spawnedCount > 0)
        {
            _popup.PopupEntity(Loc.GetString("syndiebattle-redemption-success", ("amount", spawnedCount.ToString())), ent, args.User);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("syndiebattle-redemption-error"), ent, args.User);
        }

        args.Handled = true;
        EntityManager.DeleteEntity(args.Used);
    }
}
