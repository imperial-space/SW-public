using System.Linq;
using Content.Server.Hands.Systems;
using Content.Shared.Hands;
using Content.Shared.IdentityManagement;
using Content.Shared.Imperial.Medieval.ItemShow;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Pointing;
using Content.Shared.Popups;
using Content.Shared.RatKing;
using Content.Shared.Throwing;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.ItemShow;

public sealed class ItemDisplaySystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;

    public override void Initialize()
    {
        SubscribeNetworkEvent<ItemDisplayRequest>(OnItemShowRequest);
        SubscribeLocalEvent<ItemDisplayComponent, DidUnequipHandEvent>(OnUnequipped);
        SubscribeLocalEvent<MindContainerComponent, AfterPointedAtEvent>(OnPointedAt);
    }

    private void OnUnequipped(Entity<ItemDisplayComponent> ent, ref DidUnequipHandEvent args)
    {
        if (ent.Comp.ItemUid != args.Unequipped)
        {
            return;
        }

        RemComp<ItemDisplayComponent>(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ItemDisplayComponent>();

        while (query.MoveNext(out var uid, out var itemShowComponent))
        {
            if (itemShowComponent.DespawnAt >= _gameTiming.CurTime)
            {
                continue;
            }

            RemComp<ItemDisplayComponent>(uid);
        }
    }

    private void OnItemShowRequest(ItemDisplayRequest msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession.AttachedEntity;

        if (player == null)
        {
            Log.Error($"Attempt to show an item failed: no valid player entity found for {args.SenderSession.UserId}.");
            return;
        }

        if (!TryGetEntity(msg.ItemUid, out var itemUid))
        {
            Log.Error($"Attempt to show an item failed: provided entity UID is invalid {msg.ItemUid}.");
            return;
        }

        var comp = EnsureComp<ItemDisplayComponent>(player.Value);

        comp.ItemUid = itemUid.Value;
        comp.DespawnAt = _gameTiming.CurTime + comp.DespawnDelay;

        var pvs = Filter.Pvs(player.Value).RemovePlayer(args.SenderSession);

        var playerName = Identity.Entity(player.Value, EntityManager);
        var pointedName = Identity.Entity(comp.ItemUid, EntityManager);

        var loc = Loc.GetString("pointing-system-point-at-other-others", ("otherName", playerName), ("other", pointedName));
        _popupSystem.PopupEntity(loc, player.Value, pvs, false);
        Dirty(player.Value, comp);
    }

    private void OnPointedAt(Entity<MindContainerComponent> mind, ref AfterPointedAtEvent args)
    {
        if (args.Pointed is not { Valid: true } pointed)
            return;
        if (!TryComp<MindComponent>(mind.Comp.Mind, out var mindComponent))
            return;
        var player = mindComponent.OriginalOwnerUserId;
        if (player == null)
            return;
        var session = _playerManager.GetSessionById(player.Value);

        var request = new ItemDisplayRequest(GetNetEntity(pointed));
        OnItemShowRequest(request, new EntitySessionEventArgs(session));
    }

}
