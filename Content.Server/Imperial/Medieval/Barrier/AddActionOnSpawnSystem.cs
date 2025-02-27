using Content.Server.AddActionOnSpawn.Components;
using Content.Shared.Actions;

namespace Content.Server.AddActionOnSpawn;
public partial class AddActionOnSpawnSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AddActionOnSpawnComponent, ComponentStartup>(OnStart);
    }
    private void OnStart(EntityUid uid, AddActionOnSpawnComponent component, ComponentStartup args)
    {
        foreach (var action in component.Actions)
            _actionsSystem.AddAction(uid, action, uid);
    }
}
