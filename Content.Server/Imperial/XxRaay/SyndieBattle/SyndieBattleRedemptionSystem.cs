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
        if (!TryComp<ActorComponent>(args.User, out _))
            return;

        if (!TryComp<PdaComponent>(args.Used, out _))
        {
            _popup.PopupEntity(Loc.GetString("syndiebattle-redemption-no-pda"), ent, args.User);
            return;
        }

        if (!HasComp<UplinkComponent>(args.Used))
        {
            _popup.PopupEntity(Loc.GetString("syndiebattle-redemption-no-uplink"), ent, args.User);
            return;
        }

        var reward = ent.Comp.BaseReward;
        var spawnPos = Transform(args.Target).Coordinates;
        var spawnedCount = 0;

        for (var i = 0; i < reward; i++)
        {
            if (!_prototype.TryIndex<EntityPrototype>("Telecrystal1", out var itemProto))
                continue;

            Spawn(itemProto.ID, spawnPos);
            spawnedCount++;
        }

        _popup.PopupEntity(spawnedCount > 0
                ? Loc.GetString("syndiebattle-redemption-success", ("amount", spawnedCount.ToString()))
                : Loc.GetString("syndiebattle-redemption-error"),
            ent,
            args.User);

        args.Handled = true;
        EntityManager.DeleteEntity(args.Used);
    }
}
