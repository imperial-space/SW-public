using Content.Server.Imperial.Medieval.Magic.BindStoreOnEquip;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Imperial.Medieval.Magic.ActionOrder;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Magic.ActionOrder;

public sealed class MedievalActionReplacementSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ActionsContainerComponent, ActionRemovedEvent>(OnActionRemoved);
        SubscribeLocalEvent<PendingMedievalActionReplacementComponent, EntInsertedIntoContainerMessage>(OnActionInserted);
    }

    private void OnActionRemoved(
        EntityUid uid,
        ActionsContainerComponent component,
        ActionRemovedEvent args)
    {
        if (args.Component.AttachedEntity is not { } performer ||
            !HasComp<GrimoireOwnerComponent>(performer) ||
            !TryComp<ActionUpgradeComponent>(args.Action, out var upgrade))
        {
            return;
        }

        var pending = EnsureComp<PendingMedievalActionReplacementComponent>(uid);
        pending.Replacements.Add(new PendingMedievalActionReplacement
        {
            OldAction = GetNetEntity(args.Action),
            Performer = performer,
            ReplacementPrototypes = new HashSet<EntProtoId>(upgrade.EffectedLevels.Values),
        });

        RemCompDeferred<PendingMedievalActionReplacementComponent>(uid);
    }

    private void OnActionInserted(
        EntityUid uid,
        PendingMedievalActionReplacementComponent pending,
        EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != ActionsContainerComponent.ContainerId ||
            MetaData(args.Entity).EntityPrototype?.ID is not { } prototype)
        {
            return;
        }

        for (var i = pending.Replacements.Count - 1; i >= 0; i--)
        {
            var replacement = pending.Replacements[i];
            if (!replacement.ReplacementPrototypes.Contains(prototype) ||
                !TryComp<ActorComponent>(replacement.Performer, out var actor))
            {
                continue;
            }

            RaiseNetworkEvent(
                new MedievalActionReplacedEvent(replacement.OldAction, GetNetEntity(args.Entity)),
                actor.PlayerSession);
            pending.Replacements.RemoveAt(i);
            return;
        }
    }
}
