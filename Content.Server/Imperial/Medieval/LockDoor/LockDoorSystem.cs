using Content.Shared.Imperial.LockDoor.Components;
using Content.Shared.Interaction;
using Content.Shared.Doors.Components;
using Content.Server.Doors.Systems;

namespace Content.Server.Imperial.LockDoor.Systems;

public sealed partial class LockDoorSystems : EntitySystem
{
    [Dependency] private readonly DoorSystem _doorSystem = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LockDoorComponent, InteractUsingEvent>(OnClick);
    }

    public void OnClick(EntityUid uid, LockDoorComponent comp, InteractUsingEvent ev)
    {
        var has = false;
        if (!TryComp<KeyComponent>(ev.Used, out var accessUsedComponent) || !TryComp<DoorBoltComponent>(ev.Target, out var doorBoltComponent)) return;

        var doorEntity = new Entity<DoorBoltComponent>(uid, doorBoltComponent);

        if (comp.AccessLists.Count == 0) has = true;
        foreach (var i in accessUsedComponent.Accesses)
        {
            if (comp.AccessLists.Contains(i))
                has = true;
        }

        if (has)
        {
            _doorSystem.TrySetBoltDown(doorEntity, !_doorSystem.IsBolted(ev.Target));
        }
    }
}
