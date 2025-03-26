using Content.Shared.Imperial.LockDoor.Components;
using Content.Shared.Interaction;
using Content.Shared.Doors.Components;
using Content.Server.Doors.Systems;
using Content.Server.CustomDoorKey.Components;
using Content.Shared.Popups;
using Robust.Shared.Player;
using Robust.Shared.Localization;

namespace Content.Server.Imperial.LockDoor.Systems;

public sealed partial class LockDoorSystems : EntitySystem
{
    [Dependency] private readonly DoorSystem _doorSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LockDoorComponent, InteractUsingEvent>(OnClick);
    }

    public void OnClick(EntityUid uid, LockDoorComponent comp, InteractUsingEvent ev)
    {

        var has = false;
        if (!TryComp<KeyComponent>(ev.Used, out var accessUsedComponent) || !TryComp<DoorBoltComponent>(ev.Target, out var doorBoltComponent)) return;
        if (TryComp<DoorComponent>(ev.Target, out var door) && door.State != DoorState.Closed) return;

        var doorEntity = new Entity<DoorBoltComponent>(uid, doorBoltComponent);

        foreach (var i in accessUsedComponent.Accesses)
        {
            if (comp.AccessLists.Contains(i))
                has = true;
        }

        if (has)
        {
            bool isBolted = _doorSystem.IsBolted(ev.Target);
            _doorSystem.TrySetBoltDown(doorEntity, !isBolted);

            // Show popup
            if (isBolted)
            {
                _popupSystem.PopupEntity(Loc.GetString("lock-door-popup-unlock"), ev.Target, ev.User);

            }
            else
            {
                _popupSystem.PopupEntity(Loc.GetString("lock-door-popup-lock"), ev.Target, ev.User);
            }
            if (TryComp<DoorHackableComponent>(ev.Target, out var hack))
            {
                hack.LockPickProgress = 0;
            }
        }
    }
}
