using System.Linq;
using Content.Client.Actions;
using Content.Client.UserInterface.Systems.Actions;
using Content.Client.UserInterface.Systems.Actions.Controls;
using Content.Shared.Imperial.Medieval.Abilities;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Medieval.Abilities;

/// <summary>Displays owned actions and provider-supplied passives using the ordinary action bar.</summary>
public sealed class AbilityMenuSystem : EntitySystem
{
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly SpriteSystem _sprites = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private AbilityMenuWindow? _menu;
    private readonly List<(Button Button, Button Pin, ActionButton Icon, EntityUid Action, string Name)> _buttons = new();
    private TimeSpan _nextRefresh;
    private bool _refreshNeeded;

    public override void Initialize()
    {
        SubscribeLocalEvent<AbilityMenuComponent, OpenAbilityMenuEvent>(OnOpenMenu);
        SubscribeLocalEvent<AbilityMenuComponent, LocalPlayerDetachedEvent>(OnDetached);
        SubscribeLocalEvent<AbilityMenuComponent, ComponentShutdown>(OnShutdown);
        _actions.ActionsUpdated += QueueRefresh;
    }

    public override void Shutdown()
    {
        _actions.ActionsUpdated -= QueueRefresh;
        _menu?.Close();
        base.Shutdown();
    }

    private void OnOpenMenu(EntityUid uid, AbilityMenuComponent component, OpenAbilityMenuEvent args)
    {
        if (args.Handled || _player.LocalEntity != uid || !_timing.IsFirstTimePredicted)
            return;

        args.Handled = true;
        if (_menu != null)
        {
            _menu.Close();
            return;
        }

        var window = new AbilityMenuWindow();
        _menu = window;
        window.OnClose += () =>
        {
            ReleaseButtons();
            _menu = null;
            window.Dispose();
        };
        RefreshMenu();
        window.OpenCentered();
    }

    private void OnDetached(EntityUid uid, AbilityMenuComponent component, LocalPlayerDetachedEvent args)
    {
        _menu?.Close();
    }

    private void OnShutdown(Entity<AbilityMenuComponent> ent, ref ComponentShutdown args)
    {
        if (_player.LocalEntity == ent.Owner)
            _menu?.Close();
    }

    /// <summary>Notify the menu when a provider's passive entries or descriptions change.</summary>
    public void Refresh(EntityUid user)
    {
        if (_player.LocalEntity == user)
            QueueRefresh();
    }

    private void QueueRefresh() => _refreshNeeded = true;

    private void ReleaseButtons()
    {
        var controller = _ui.GetUIController<ActionUIController>();
        foreach (var entry in _buttons)
            controller.ReleaseMenuActionButton(entry.Icon);
        _buttons.Clear();
    }

    private void RefreshMenu()
    {
        if (_menu == null || _player.LocalEntity is not { } user)
            return;

        // ActionsUpdated can arrive during dragging. Keep the drag source alive until mouse-up.
        var controller = _ui.GetUIController<ActionUIController>();
        if (_buttons.Any(entry => controller.IsMenuActionPressed(entry.Icon)))
            return;

        _refreshNeeded = false;
        ReleaseButtons();
        _menu.Entries.DisposeAllChildren();

        var owned = _actions.GetClientActions()
            .Where(action => HasComp<AbilityMenuActionComponent>(action.Owner))
            .Select(action => action.Owner)
            .ToHashSet();
        var entries = new GetAbilityMenuEntriesEvent();
        foreach (var action in owned)
        {
            var metadata = MetaData(action);
            entries.Actions.Add(action, new AbilityMenuEntry(metadata.EntityName, metadata.EntityDescription));
        }
        RaiseLocalEvent(user, entries);

        _menu.AddSection(Loc.GetString("ability-menu-active"));
        foreach (var (action, entry) in entries.Actions.OrderBy(pair => pair.Value.Order).ThenBy(pair => pair.Value.Name))
        {
            if (owned.Contains(action))
                AddAction(action, entry.Name, entry.Description);
        }
        if (_buttons.Count == 0)
            _menu.Entries.AddChild(new Label { Text = Loc.GetString("ability-menu-no-active") });

        if (entries.Passives.Count > 0)
        {
            _menu.AddSection(Loc.GetString("ability-menu-passive"));
            foreach (var entry in entries.Passives.OrderBy(entry => entry.Order).ThenBy(entry => entry.Name))
            {
                var icon = entry.Icon == null ? null : _sprites.Frame0(entry.Icon);
                _menu.AddPassiveAbility(entry.Name, entry.Description, icon);
            }
        }
        UpdateCooldowns();
    }

    private void AddAction(EntityUid action, string name, string description)
    {
        if (_menu == null)
            return;

        var controller = _ui.GetUIController<ActionUIController>();
        var icon = controller.CreateMenuActionButton(action);
        var (button, pin) = _menu.AddActiveAbility(name, description, icon, () =>
        {
            _menu?.Close();
            controller.ActivateAction(action);
        }, () =>
        {
            controller.AddActionToHotbar(action);
            UpdateCooldowns();
        });
        _buttons.Add((button, pin, icon, action, name));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_menu == null)
            return;
        if (_refreshNeeded)
            RefreshMenu();
        if (_timing.CurTime < _nextRefresh)
            return;

        _nextRefresh = _timing.CurTime + TimeSpan.FromSeconds(0.25);
        UpdateCooldowns();
    }

    private void UpdateCooldowns()
    {
        var controller = _ui.GetUIController<ActionUIController>();
        foreach (var (button, pin, _, uid, name) in _buttons)
        {
            var action = _actions.GetAction(uid);
            if (action is not { } current || current.Comp.AttachedEntity != _player.LocalEntity)
            {
                button.Disabled = true;
                pin.Disabled = true;
                continue;
            }

            var pinned = controller.IsActionOnHotbar(uid);
            pin.Disabled = pinned;
            pin.Text = Loc.GetString(pinned ? "ability-menu-pinned" : "ability-menu-pin");
            var remaining = current.Comp.Cooldown is { } cooldown
                ? Math.Max(0, (int) Math.Ceiling((cooldown.End - _timing.CurTime).TotalSeconds))
                : 0;
            button.Disabled = !current.Comp.Enabled || remaining > 0;
            button.Text = remaining > 0
                ? Loc.GetString("ability-menu-cooldown", ("ability", name), ("seconds", remaining))
                : name;
        }
    }
}
