using System.Linq;
using Content.Client.Actions;
using Content.Client.UserInterface.Systems.Actions.Widgets;
using Content.Shared.Actions.Components;
using Content.Shared.Mind;
using Content.Shared.Nocturn.Components;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Player;

namespace Content.Client.Nocturn;

public sealed class AncientNocturneActionOrderSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly ActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AncientNocturneComponent, LocalPlayerDetachedEvent>(
            OnPlayerDetached,
            before: new[] { typeof(ActionsSystem) });
        SubscribeLocalEvent<AncientNocturneComponent, LocalPlayerAttachedEvent>(
            OnPlayerAttached,
            after: new[] { typeof(ActionsSystem) });
    }

    private void OnPlayerDetached(
        Entity<AncientNocturneComponent> ent,
        ref LocalPlayerDetachedEvent args)
    {
        if (!TryGetLocalMind(out var mindUid))
            return;

        var actionsBar = _ui.GetActiveUIWidgetOrNull<ActionsBar>();
        if (actionsBar == null)
            return;

        var state = EnsureComp<AncientNocturneActionOrderComponent>(mindUid);
        state.Actions.Clear();
        foreach (var button in actionsBar.ActionsContainer.GetButtons())
        {
            if (button.Action is { } action)
                state.Actions.Add(action.Owner);
        }
    }

    private void OnPlayerAttached(
        Entity<AncientNocturneComponent> ent,
        ref LocalPlayerAttachedEvent args)
    {
        if (!TryGetLocalMind(out var mindUid) ||
            !TryComp<AncientNocturneActionOrderComponent>(mindUid, out var state))
            return;

        var currentActions = _actions.GetClientActions().ToList();
        var available = currentActions.ToDictionary(action => action.Owner);
        var ordered = new List<Entity<ActionComponent>>(currentActions.Count);

        foreach (var actionUid in state.Actions)
        {
            if (available.Remove(actionUid, out var action))
                ordered.Add(action);
        }

        currentActions.Sort(ActionsSystem.ActionComparer);
        foreach (var action in currentActions)
        {
            if (action.Comp.AutoPopulate && available.Remove(action.Owner))
                ordered.Add(action);
        }

        var originalSettings = new List<(ActionComponent Component, int Priority, bool AutoPopulate)>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            var action = ordered[i].Comp;
            originalSettings.Add((action, action.Priority, action.AutoPopulate));
            action.Priority = i;
            action.AutoPopulate = true;
        }

        _actions.LinkAllActions();

        foreach (var (component, priority, autoPopulate) in originalSettings)
        {
            component.Priority = priority;
            component.AutoPopulate = autoPopulate;
        }
    }

    private bool TryGetLocalMind(out EntityUid mindUid)
    {
        mindUid = default;
        if (_player.LocalUser is not { } user ||
            !_mind.TryGetMind(user, out var mind, out _))
            return false;

        mindUid = mind.Value;
        return true;
    }
}
