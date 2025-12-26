using Content.Server.CustomDoorKey.Components;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Robust.Shared.Audio.Systems;
using Content.Shared.Popups;
using Content.Shared.Doors.Systems;

namespace Content.Server.CustomDoorKey
{
    public sealed partial class CustomDoorKeySystem : EntitySystem
    {
        [Dependency] protected readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;
        [Dependency] private readonly SharedDoorSystem _door = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<CustomDoorKeyComponent, BeforeRangedInteractEvent>(OnUseInHand);
            SubscribeLocalEvent<CustomDoorKeyDoorComponent, ExaminedEvent>(OnExamine);
            SubscribeLocalEvent<CustomDoorKeyComponent, ExaminedEvent>(OnExamineKey);

        }

        private void OnExamineKey(EntityUid uid, CustomDoorKeyComponent component, ExaminedEvent args)
        {
            if (component.linkedKey == null)
                args.PushMarkup(Loc.GetString("medieval-hm-customdoorkey-unbound"));
            else
                args.PushMarkup(Loc.GetString("medieval-hm-customdoorkey-bound"));
        }

        private void OnExamine(EntityUid uid, CustomDoorKeyDoorComponent component, ExaminedEvent args)
        {
            if (component.linkedKey == null)
                args.PushMarkup(Loc.GetString("medieval-hm-customdoorkey-unbound"));
            else
                args.PushMarkup(Loc.GetString("medieval-hm-customdoorkey-bound"));
        }

        public void OnUseInHand(EntityUid uid, CustomDoorKeyComponent comp, BeforeRangedInteractEvent args)
        {
            if (!args.CanReach)
                return;
            OnUse(args.Target, args.User, args.Used, comp);
        }

        public void OnUse(EntityUid? target, EntityUid user, EntityUid used, CustomDoorKeyComponent comp)
        {
            if (target == null)
                return;
            if (TryComp<CustomDoorKeyComponent>(target, out var key) && key != null)
            {
                if (comp.linkedKey != null)
                {
                    _popup.PopupEntity(Loc.GetString("medieval-hm-customdoorkey-boundalr"), user, PopupType.LargeCaution);
                    return;
                }
                if (key.linkedKey == null)
                {
                    _popup.PopupEntity(Loc.GetString("medieval-hm-customdoorkey-nothing"), user, PopupType.LargeCaution);
                    return;
                }
                comp.linkedKey = key.linkedKey;
                _popup.PopupEntity(Loc.GetString("medieval-hm-customdoorkey-keysmth"), user, PopupType.LargeCaution);
            }
            if (TryComp<CustomDoorKeyDoorComponent>(target, out var door) && door != null)
            {
                if (comp.linkedKey == door.Owner)
                {
                    _door.TryToggleDoor(door.Owner);
                    return;
                }
                if (comp.linkedKey != door.Owner && door.linkedKey != null)
                {
                    _popup.PopupEntity(Loc.GetString("medieval-hm-customdoorkey-nope"), user, PopupType.LargeCaution);
                    return;
                }
                if (comp.linkedKey != null && comp.linkedKey != target)
                {
                    _popup.PopupEntity(Loc.GetString("medieval-hm-customdoorkey-boundalr"), user, PopupType.LargeCaution);
                    return;
                }
                if (door.linkedKey == null && comp.linkedKey == null)
                {
                    _popup.PopupEntity(Loc.GetString("medieval-hm-customdoorkey-bounded"), user, PopupType.LargeCaution);
                    door.linkedKey = used;
                    comp.linkedKey = door.Owner;
                    return;
                }

            }
        }

    }
}
