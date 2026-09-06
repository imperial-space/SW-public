using System.Linq;
using Content.Client.Actions;
using Content.Client.UserInterface.Systems.Actions.Widgets;
using Content.Shared.Actions.Components;
using Content.Shared.Imperial.Medieval.Magic.ActionOrder;
using Content.Shared.Mind;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Network;

namespace Content.Client.Imperial.Medieval.Magic.ActionOrder;

public sealed class MedievalActionUpgradeOrderSystem : EntitySystem
{
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<MedievalActionReplacedEvent>(OnActionReplaced);
        SubscribeLocalEvent<MedievalActionOrderUpdateEvent>(OnOrderUpdate);
        _actions.OnActionAdded += OnActionAdded;
        _actions.OnActionRemoved += OnActionRemoved;
        _actions.LinkActions += OnActionsLinked;
    }

    public override void Shutdown()
    {
        _actions.OnActionAdded -= OnActionAdded;
        _actions.OnActionRemoved -= OnActionRemoved;
        _actions.LinkActions -= OnActionsLinked;

        base.Shutdown();
    }

    private void OnActionAdded(EntityUid _)
    {
        if (!TryGetOrderState(out var stateUid, out var state))
            return;

        QueueOrderUpdate(stateUid, state);
    }

    private void OnActionsLinked(ActionsComponent _)
    {
        if (!TryGetOrderState(out var stateUid, out var state))
            return;

        QueueOrderUpdate(stateUid, state);
    }

    private void OnOrderUpdate(MedievalActionOrderUpdateEvent args)
    {
        if (!TryComp<MedievalActionUpgradeOrderComponent>(args.State, out var state))
            return;

        state.OrderUpdateQueued = false;
        if (!TryGetOrderState(out var currentStateUid, out _) ||
            currentStateUid != args.State ||
            state.ApplyingReplacements)
        {
            return;
        }

        if (state.Replacements.Count == 0)
            CaptureOrder(state);
        else
            TryApplyReplacements(state);
    }

    private void OnActionRemoved(EntityUid action)
    {
        if (!TryGetOrderState(out var state) || state.Replacements.Count != 0)
            return;

        var actionsBar = _ui.GetActiveUIWidgetOrNull<ActionsBar>();
        if (actionsBar == null || actionsBar.ActionsContainer.GetButtons().All(button => button.Action?.Owner != action))
            return;

        if (!state.OrderInitialized)
        {
            foreach (var availableAction in _actions.GetClientActions())
            {
                state.AvailableActions.Add(availableAction.Owner);
            }

            state.AvailableActions.Add(action);
            state.OrderInitialized = true;
        }

        CaptureVisibleOrder(state, actionsBar);

        var netEntity = MetaData(action).NetEntity;
        if (netEntity == NetEntity.Invalid)
            return;

        state.RemovedOrders[netEntity] = new MedievalRemovedActionOrder
        {
            Actions = new List<EntityUid>(state.Actions),
            AvailableActions = new HashSet<EntityUid>(state.AvailableActions),
            NetworkedActions = new Dictionary<NetEntity, EntityUid>(state.NetworkedActions),
        };

        while (state.RemovedOrders.Count > state.RemovedOrderLimit)
        {
            state.RemovedOrders.Remove(state.RemovedOrders.Keys.First());
        }
    }

    private void OnActionReplaced(MedievalActionReplacedEvent message)
    {
        if (!TryGetOrderState(out var stateUid, out var state))
            return;

        if (!state.OrderInitialized)
            CaptureOrder(state);

        if (state.Replacements.Count == 0)
        {
            if (_ui.GetActiveUIWidgetOrNull<ActionsBar>() is { } actionsBar &&
                actionsBar.ActionsContainer.GetButtons().Any(button =>
                    button.Action is { } action && MetaData(action).NetEntity == message.OldAction))
            {
                CaptureVisibleOrder(state, actionsBar);
            }
            else if (state.RemovedOrders.TryGetValue(message.OldAction, out var removedOrder))
            {
                state.Actions = new List<EntityUid>(removedOrder.Actions);
                state.AvailableActions = new HashSet<EntityUid>(removedOrder.AvailableActions);
                state.NetworkedActions = new Dictionary<NetEntity, EntityUid>(removedOrder.NetworkedActions);
            }
        }

        state.Replacements[message.OldAction] = message.NewAction;
        QueueOrderUpdate(stateUid, state);
    }

    private void QueueOrderUpdate(
        EntityUid stateUid,
        MedievalActionUpgradeOrderComponent state)
    {
        if (state.OrderUpdateQueued || state.ApplyingReplacements)
            return;

        state.OrderUpdateQueued = true;
        QueueLocalEvent(new MedievalActionOrderUpdateEvent(stateUid));
    }

    private bool TryGetOrderState(out MedievalActionUpgradeOrderComponent state)
    {
        return TryGetOrderState(out _, out state);
    }

    private bool TryGetOrderState(
        out EntityUid stateUid,
        out MedievalActionUpgradeOrderComponent state)
    {
        stateUid = default;
        state = default!;

        if (_player.LocalUser is not { } user ||
            !_mind.TryGetMind(user, out var mind, out _))
        {
            return false;
        }

        stateUid = mind.Value;
        state = EnsureComp<MedievalActionUpgradeOrderComponent>(stateUid);
        return true;
    }

    private void CaptureOrder(MedievalActionUpgradeOrderComponent state)
    {
        var actionsBar = _ui.GetActiveUIWidgetOrNull<ActionsBar>();
        if (actionsBar == null)
            return;

        CaptureVisibleOrder(state, actionsBar);

        state.AvailableActions.Clear();
        foreach (var action in _actions.GetClientActions())
        {
            state.AvailableActions.Add(action.Owner);
        }

        state.OrderInitialized = true;
    }

    private void CaptureVisibleOrder(
        MedievalActionUpgradeOrderComponent state,
        ActionsBar actionsBar)
    {
        state.Actions.Clear();
        state.NetworkedActions.Clear();

        foreach (var button in actionsBar.ActionsContainer.GetButtons())
        {
            if (button.Action is not { } action)
                continue;

            state.Actions.Add(action.Owner);

            var netEntity = MetaData(action).NetEntity;
            if (netEntity != NetEntity.Invalid)
                state.NetworkedActions[netEntity] = action.Owner;
        }
    }

    private void TryApplyReplacements(MedievalActionUpgradeOrderComponent state)
    {
        if (_ui.GetActiveUIWidgetOrNull<ActionsBar>() == null)
            return;

        var currentActions = _actions.GetClientActions().ToList();
        var available = currentActions.ToDictionary(action => action.Owner);
        var replacements = new Dictionary<NetEntity, EntityUid>();
        var replacementTargets = new HashSet<EntityUid>();

        foreach (var oldAction in state.Replacements.Keys)
        {
            var finalReplacement = ResolveFinalReplacement(oldAction, state.Replacements);
            if (!TryGetEntity(finalReplacement, out var replacement) ||
                replacement is not { } replacementUid ||
                !available.ContainsKey(replacementUid))
            {
                return;
            }

            replacements[oldAction] = replacementUid;
            replacementTargets.Add(replacementUid);
        }

        var desiredOrder = new List<EntityUid>(state.Actions);
        foreach (var (oldAction, replacement) in replacements)
        {
            if (!state.NetworkedActions.TryGetValue(oldAction, out var replaced))
                continue;

            for (var i = 0; i < desiredOrder.Count; i++)
            {
                if (desiredOrder[i] == replaced)
                    desiredOrder[i] = replacement;
            }
        }

        var ordered = new List<Entity<ActionComponent>>(currentActions.Count);
        var orderedIds = new HashSet<EntityUid>();

        foreach (var action in desiredOrder)
        {
            if (available.Remove(action, out var entity) && orderedIds.Add(action))
                ordered.Add(entity);
        }

        currentActions.Sort(ActionsSystem.ActionComparer);
        foreach (var action in currentActions)
        {
            if (!available.ContainsKey(action.Owner) ||
                replacementTargets.Contains(action.Owner) ||
                state.AvailableActions.Contains(action.Owner) ||
                !action.Comp.AutoPopulate)
            {
                continue;
            }

            available.Remove(action.Owner);
            if (orderedIds.Add(action.Owner))
                ordered.Add(action);
        }

        var originalSettings = new List<(ActionComponent Component, int Priority, bool AutoPopulate)>(currentActions.Count);
        foreach (var action in currentActions)
        {
            originalSettings.Add((action.Comp, action.Comp.Priority, action.Comp.AutoPopulate));
            action.Comp.AutoPopulate = false;
        }

        state.ApplyingReplacements = true;
        try
        {
            for (var i = 0; i < ordered.Count; i++)
            {
                ordered[i].Comp.Priority = i;
                ordered[i].Comp.AutoPopulate = true;
            }

            _actions.LinkAllActions();
        }
        finally
        {
            foreach (var (component, priority, autoPopulate) in originalSettings)
            {
                component.Priority = priority;
                component.AutoPopulate = autoPopulate;
            }

            state.ApplyingReplacements = false;
        }

        foreach (var replaced in state.Replacements.Keys)
        {
            state.RemovedOrders.Remove(replaced);
        }

        state.Replacements.Clear();
        CaptureOrder(state);
    }

    private static NetEntity ResolveFinalReplacement(
        NetEntity action,
        IReadOnlyDictionary<NetEntity, NetEntity> replacements)
    {
        var visited = new HashSet<NetEntity>();
        while (visited.Add(action) && replacements.TryGetValue(action, out var replacement))
        {
            action = replacement;
        }

        return action;
    }
}

internal sealed class MedievalActionOrderUpdateEvent(EntityUid state) : EntityEventArgs
{
    public readonly EntityUid State = state;
}
