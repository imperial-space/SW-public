using Content.Shared.Actions;
using Content.Shared.Imperial.Medieval.New;
using Content.Shared.Interaction.Events;

namespace Content.Server.Imperial.Medieval.New;

public sealed partial class NewSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _action = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NewComponent, UseInHandEvent>(OnDo);
    }
    private void OnDo(EntityUid uid, NewComponent component, UseInHandEvent ev)
    {
        QueueDel(uid);
        _action.AddAction(ev.User, component.ActionId);
    }
}
