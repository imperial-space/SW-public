using Content.Client.UserInterface.Systems.Actions.Controls;
using Content.Shared.Actions.Components;

namespace Content.Client.UserInterface.Systems.Actions;

public sealed partial class ActionUIController
{
    /// <summary>Activate an owned action using the same targeting flow as a toolbar button.</summary>
    public void ActivateAction(EntityUid actionId)
    {
        if (_actionsSystem?.GetAction(actionId) is not {} action ||
            action.Comp.AttachedEntity != _playerManager.LocalEntity)
            return;

        // TODO: probably should have a clientside event raised for flexibility
        if (EntityManager.TryGetComponent<TargetActionComponent>(action, out var target))
            ToggleTargeting((action, action, target));
        else
            _actionsSystem?.TriggerAction(action);
    }

    /// <summary>Create a draggable action icon for an ability catalogue, using the action bar's normal input flow.</summary>
    public ActionButton CreateMenuActionButton(EntityUid actionId)
    {
        var button = new ActionButton(EntityManager, _spriteSystem, this) { Locked = true };
        button.ActionPressed += OnWindowActionPressed;
        button.ActionUnpressed += OnWindowActionUnPressed;
        button.ActionFocusExited += OnWindowActionFocusExisted;
        if (_actionsSystem != null)
            button.UpdateData(actionId, _actionsSystem);
        return button;
    }

    /// <summary>Keep a catalogue entry alive until its mouse press or drag has finished.</summary>
    public bool IsMenuActionPressed(ActionButton button) => ReferenceEquals(_menuDragHelper.Dragged, button);

    public void ReleaseMenuActionButton(ActionButton button)
    {
        if (IsMenuActionPressed(button))
            _menuDragHelper.EndDrag();
        button.ActionPressed -= OnWindowActionPressed;
        button.ActionUnpressed -= OnWindowActionUnPressed;
        button.ActionFocusExited -= OnWindowActionFocusExisted;
    }

    public bool IsActionOnHotbar(EntityUid actionId) => _actions.Contains(actionId);

    /// <summary>Explicitly pin an owned action without automatically filling the bar with every learned ability.</summary>
    public bool AddActionToHotbar(EntityUid actionId)
    {
        if (_actionsSystem?.GetAction(actionId) is not { } action ||
            action.Comp.AttachedEntity != _playerManager.LocalEntity || _container == null)
            return false;

        if (!_actions.Contains(actionId))
            _actions.Add(actionId);
        _container.SetActionData(_actionsSystem, _actions.ToArray());
        return true;
    }
}
