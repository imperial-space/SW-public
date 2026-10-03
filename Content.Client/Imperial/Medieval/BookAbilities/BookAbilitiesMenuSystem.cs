using System.Linq;
using Content.Client.Actions;
using Content.Client.UserInterface.Systems.Actions;
using Content.Client.UserInterface.Systems.Actions.Controls;
using Content.Shared.Imperial.Medieval.BookAbilities;
using Content.Shared.Imperial.Medieval.Knowledge;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameStates;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Medieval.BookAbilities;

public sealed class BookAbilitiesMenuSystem : EntitySystem
{
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly SpriteSystem _sprites = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private BookAbilitiesWindow? _menu;
    private BookAbilityChoiceWindow? _choices;
    private readonly List<(Button Button, Button Pin, ActionButton Icon, EntityUid Action, string Name)> _buttons = new();
    private TimeSpan _nextRefresh;
    private bool _refreshNeeded;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BookAbilityActionComponent, ActionAutoPopulateAttemptEvent>(OnAutoPopulate);
        SubscribeLocalEvent<LearnedKnowledgeComponent, BookOpenAbilitiesMenuEvent>(OnOpenMenu);
        SubscribeLocalEvent<LearnedKnowledgeComponent, AfterAutoHandleStateEvent>(OnKnowledgeState);
        SubscribeLocalEvent<LearnedKnowledgeComponent, LocalPlayerDetachedEvent>(OnDetached);
        SubscribeNetworkEvent<BookAbilityChoicesEvent>(OnChoices);
        _actions.ActionsUpdated += QueueRefresh;
    }

    private void OnAutoPopulate(EntityUid uid, BookAbilityActionComponent component, ActionAutoPopulateAttemptEvent args)
    {
        args.Cancel();
    }

    public override void Shutdown()
    {
        _actions.ActionsUpdated -= QueueRefresh;
        CloseWindows();
        base.Shutdown();
    }

    private void OnOpenMenu(EntityUid uid, LearnedKnowledgeComponent component, BookOpenAbilitiesMenuEvent args)
    {
        if (args.Handled || _player.LocalEntity != uid || !_timing.IsFirstTimePredicted)
            return;
        args.Handled = true;
        if (_menu != null)
        {
            _menu.Close();
            return;
        }

        var window = new BookAbilitiesWindow();
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

    private void OnKnowledgeState(EntityUid uid, LearnedKnowledgeComponent component, ref AfterAutoHandleStateEvent args)
    {
        if (_player.LocalEntity == uid)
            QueueRefresh();
    }

    private void OnDetached(EntityUid uid, LearnedKnowledgeComponent component, LocalPlayerDetachedEvent args)
    {
        CloseWindows();
    }

    private void CloseWindows()
    {
        _menu?.Close();
        _choices?.Close();
        _menu = null;
        _choices = null;
        ReleaseButtons();
    }

    private void QueueRefresh() => _refreshNeeded = true;

    private void ReleaseButtons()
    {
        if (_buttons.Count == 0)
            return;
        var controller = _ui.GetUIController<ActionUIController>();
        foreach (var entry in _buttons)
            controller.ReleaseMenuActionButton(entry.Icon);
        _buttons.Clear();
    }

    private void RefreshMenu()
    {
        if (_menu == null || _player.LocalEntity is not { } user ||
            !TryComp<LearnedKnowledgeComponent>(user, out var learned))
            return;

        // ActionsUpdated can arrive while an icon is held. Do not dispose the drag source before mouse-up.
        var controller = _ui.GetUIController<ActionUIController>();
        if (_buttons.Any(entry => controller.IsMenuActionPressed(entry.Icon)))
            return;

        _refreshNeeded = false;
        ReleaseButtons();
        _menu.Entries.DisposeAllChildren();
        var known = learned.Knowledge
            .Select(id => _prototypes.TryIndex<MedievalKnowledgePrototype>(id, out var prototype) ? prototype : null)
            .Where(prototype => prototype != null)
            .OrderBy(prototype => prototype!.Tier)
            .ThenBy(prototype => Loc.GetString(prototype!.Name))
            .ToList();
        var actions = _actions.GetClientActions().Where(action => HasComp<BookAbilityActionComponent>(action)).ToList();
        var listed = new HashSet<EntityUid>();

        _menu.AddSection(Loc.GetString("book-abilities-menu-active"));
        foreach (var knowledge in known)
        {
            if (knowledge!.Actions.Count == 0)
                continue;

            foreach (var actionPrototype in knowledge.Actions)
            {
                var action = actions.FirstOrDefault(candidate => MetaData(candidate).EntityPrototype?.ID == actionPrototype.Id);
                if (!action.Owner.IsValid())
                    continue;

                listed.Add(action.Owner);
                AddAction(action.Owner, Loc.GetString(knowledge.Name), Loc.GetString(knowledge.Description));
            }
        }

        // Companion orders and cancellation exist only while their associated activity is available.
        foreach (var action in actions)
        {
            var metadata = MetaData(action.Owner);
            if (listed.Contains(action.Owner))
                continue;

            AddAction(action.Owner, metadata.EntityName, metadata.EntityDescription);
        }

        if (_buttons.Count == 0)
            _menu.Entries.AddChild(new Label { Text = Loc.GetString("book-abilities-menu-no-active") });

        _menu.AddSection(Loc.GetString("book-abilities-menu-passive"));
        foreach (var knowledge in known)
        {
            if (knowledge!.Actions.Count != 0)
                continue;
            var icon = knowledge.Icon == null ? null : _sprites.Frame0(knowledge.Icon);
            _menu.AddAbility(Loc.GetString(knowledge.Name), Loc.GetString(knowledge.Description), null, icon);
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

    private void OnChoices(BookAbilityChoicesEvent args)
    {
        _choices?.Close();
        var window = new BookAbilityChoiceWindow(args,
            choice => RaiseNetworkEvent(new BookAbilityChoiceSelectedEvent(args.RequestId, choice)));
        _choices = window;
        window.OnClose += () =>
        {
            if (_choices == window)
                _choices = null;
        };
        window.OpenCentered();
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
            pin.Text = Loc.GetString(pinned ? "book-abilities-menu-pinned" : "book-abilities-menu-pin");

            var remaining = current.Comp.Cooldown is { } cooldown
                ? Math.Max(0, (int) Math.Ceiling((cooldown.End - _timing.CurTime).TotalSeconds))
                : 0;
            button.Disabled = !current.Comp.Enabled || remaining > 0;
            button.Text = remaining > 0
                ? Loc.GetString("book-abilities-menu-cooldown", ("ability", name), ("seconds", remaining))
                : name;
        }
    }
}
