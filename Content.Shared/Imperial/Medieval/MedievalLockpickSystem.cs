using Content.Shared.Doors.Systems;
using Content.Shared.Interaction;
using Content.Shared.Doors.Components;
using Content.Shared.MedievalLockpick.Components;
using Robust.Shared.Player;
using Content.Shared.DoAfter;
using Robust.Shared.Random;
using Content.Shared.Popups;

namespace Content.Shared.MedievalLockpickSystem;

public sealed class MedievalLockpickSystem : EntitySystem
{
    [Dependency] private readonly SharedDoorSystem _door = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MedievalLockpickComponent, BeforeRangedInteractEvent>(OnUseInHand);
        SubscribeLocalEvent<MedievalLockpickComponent, MedievalLockpickDoAfterEvent>(OnDoAfter);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

    }

    public void OnUseInHand(EntityUid uid, MedievalLockpickComponent comp, BeforeRangedInteractEvent args)
    {
        if (!args.CanReach)
            return;
        OnUse(args.Target, args.User, comp);
    }

    public void OnUse(EntityUid? target, EntityUid user, MedievalLockpickComponent comp)
    {
        if (target == null)
            return;

        var doAfterEventArgs = new DoAfterArgs(EntityManager, user, 1f, new MedievalLockpickDoAfterEvent(), target.Value, target: target.Value, used: comp.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true
        };

        _doAfterSystem.TryStartDoAfter(doAfterEventArgs);

    }

    private void OnDoAfter(EntityUid uid, MedievalLockpickComponent component, MedievalLockpickDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (args.Args.Target is not null)
        {
            if (TryComp<DoorComponent>(args.Args.Target.Value, out var doorcomp) && doorcomp != null)
            {
                if (_random.Prob(0.2f))
                {
                    _popupSystem.PopupEntity("Взлом успешный", args.Args.User, PopupType.LargeCaution);
                    var door = args.Args.Target.Value;
                    if (doorcomp.State == DoorState.Open)
                    {
                        _door.StartClosing(door, doorcomp, args.Args.User, false);
                    }
                    if (doorcomp.State == DoorState.Closed)
                    {
                        _door.StartOpening(door, doorcomp, args.Args.User, false);
                    }
                }
                else
                {
                    _popupSystem.PopupEntity("Взлом неудачный, попробуйте еще раз", args.Args.User, PopupType.LargeCaution);
                }
                args.Handled = true;
            }
        }
    }



}
