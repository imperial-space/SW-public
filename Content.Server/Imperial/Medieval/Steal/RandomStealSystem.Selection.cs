using System.Linq;
using Content.Shared.Imperial.RandomSteal.Components;
using Content.Shared.Imperial.RandomSteal.Events;

namespace Content.Shared.Imperial.RandomSteal.Systems;

public sealed partial class RandomStealSystem
{
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    private void InitializeSelection()
    {
        SubscribeLocalEvent<RandomStealComponent, RequestTheftChoicesMessage>(OnRequestChoices);
        SubscribeLocalEvent<RandomStealComponent, SelectTheftItemMessage>(OnItemSelected);
    }

    private void BeginTheft(EntityUid user, EntityUid target)
    {
        if (!CanAttempt(user, target))
            return;
        if (!CanSelect(user))
        {
            TrySteal(user, target, null);
            return;
        }
        if (!_afk.TryStrip(user, target))
            return;

        var ui = EnsureComp<UserInterfaceComponent>(target);
        if (!_ui.HasUi(target, TheftUiKey.Key, ui))
            _ui.SetUi((target, ui), TheftUiKey.Key, new InterfaceData("TheftSelectionBoundUserInterface"));
        _ui.TryOpenUi((target, ui), TheftUiKey.Key, user);
    }

    private void OnRequestChoices(EntityUid uid, RandomStealComponent comp, RequestTheftChoicesMessage args)
    {
        if (!_ui.IsUiOpen(uid, TheftUiKey.Key, args.Actor))
            return;
        if (!CanSelect(args.Actor) || !CanAttempt(args.Actor, uid) || !_afk.TryStrip(args.Actor, uid))
        {
            _ui.CloseUi(uid, TheftUiKey.Key, args.Actor);
            return;
        }

        var choices = Candidates(uid, comp).Select(item =>
            new TheftItemChoice(GetNetEntity(item), Name(item), MetaData(item).EntityPrototype?.ID)).ToList();
        // Inventory contents are sent only to this thief, not replicated as public entity UI state.
        _ui.ServerSendUiMessage(uid, TheftUiKey.Key, new TheftChoicesMessage(choices), args.Actor);
    }

    private void OnItemSelected(EntityUid uid, RandomStealComponent comp, SelectTheftItemMessage args)
    {
        if (!_ui.IsUiOpen(uid, TheftUiKey.Key, args.Actor))
            return;
        _ui.CloseUi(uid, TheftUiKey.Key, args.Actor);
        if (!CanSelect(args.Actor) || !TryGetEntity(args.Item, out var item) || item is not { } selected)
            return;
        // Rechecks distance, ability to interact and current ownership before starting the ordinary theft.
        TrySteal(args.Actor, uid, selected);
    }
}
