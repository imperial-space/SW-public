using Content.Shared.Actions;
using Content.Shared.Imperial.Medieval.Actions;
using Content.Shared.Interaction.Events;

namespace Content.Server.Imperial.Medieval.Actions;

public sealed partial class AddActionOnUseSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _action = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AddActionOnUseComponent, UseInHandEvent>(OnDo);
    }
    private void OnDo(EntityUid uid, AddActionOnUseComponent component, UseInHandEvent ev)
    {
        QueueDel(uid);
        _action.AddAction(ev.User, component.ActionId);
    }
}
