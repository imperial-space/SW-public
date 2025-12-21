using Content.Server.CustomDoorKey.Components;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Robust.Shared.Audio.Systems;
using Content.Shared.Popups;
using Content.Shared.Doors.Systems;
using Robust.Shared.Random;
using Content.Server.Administration;
using Robust.Shared.Player;
using Content.Server.Prayer;
using Robust.Shared.Audio;
using Content.Server.SpikeTrap.Components;
using Content.Server.MagicBarrier.Components;
using Content.Shared.Doors.Components;
using Content.Server.Doors.Systems;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Server.Imperial.Medieval.GameTicking.Rules;
using Content.Shared.Imperial.Medieval.GameTicking.Rules;

namespace Content.Server.CustomDoorKey
{
    public sealed partial class DoorHackSystem : EntitySystem
    {
        [Dependency] protected readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;
        [Dependency] private readonly SharedDoorSystem _door = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly QuickDialogSystem _quickDialog = default!;
        [Dependency] private readonly ISharedPlayerManager _sharedPlayerManager = default!;
        [Dependency] private readonly PrayerSystem _prayerSystem = default!;
        [Dependency] private readonly SharedSkillsSystem _skillsSystem = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<DoorHackLockpickComponent, BeforeRangedInteractEvent>(OnUseInHand);
            SubscribeLocalEvent<DoorHackableComponent, ExaminedEvent>(OnExamine);
            SubscribeLocalEvent<DoorHackableComponent, ComponentStartup>(OnStart);
            SubscribeLocalEvent<DoorHackLockpickComponent, ExaminedEvent>(OnExamineKey);
            SubscribeLocalEvent<DoorNewLockComponent, BeforeRangedInteractEvent>(OnUseInHandLock);

        }

        public void OnUseInHandLock(EntityUid uid, DoorNewLockComponent comp, BeforeRangedInteractEvent args)
        {
            if (!args.CanReach)
                return;
            OnUseLock(args.Target, args.User, args.Used, comp);
        }

        public void OnUseLock(EntityUid? target, EntityUid user, EntityUid used, DoorNewLockComponent comp)
        {
            if (target == null)
                return;
            if (TryComp<DoorHackableComponent>(target, out var door) && door != null)
            {
                _audio.PlayPvs(new SoundPathSpecifier(door.EffectSoundOnNewLock), door.Owner);
                door.NumberCount += 2;
                ChangeCode(door.Owner, door);
                QueueDel(used);
                if (!_sharedPlayerManager.TryGetSessionByEntity(user, out var session)) return;
                _prayerSystem.SendSubtleMessage(session, session, Loc.GetString("medieval-hm-doorhack-upgrade"), Loc.GetString("medieval-hm-doorhack-upgrade2"));
            }
        }
        private void OnStart(EntityUid uid, DoorHackableComponent component, ComponentStartup args)
        {
            ChangeCode(uid, component);
        }
        private void ChangeCode(EntityUid uid, DoorHackableComponent component)
        {
            for (int i = 0; i < component.NumberCount; i++)
            {
                component.Numbers[i] = _random.Next(component.MinNumber, component.MaxNumber);
            }
        }
        private void OnExamineKey(EntityUid uid, DoorHackLockpickComponent component, ExaminedEvent args)
        {
            args.PushMarkup(Loc.GetString("medieval-hm-doorhack-usesleft", ("amount", $"{component.UseCount}")));
        }

        private void OnExamine(EntityUid uid, DoorHackableComponent component, ExaminedEvent args)
        {
            args.PushMarkup(Loc.GetString("medieval-hm-doorhack-progress", ("min", $"{component.LockPickProgress}"), ("max", $"{component.NumberCount}")));
            args.PushMarkup(Loc.GetString("medieval-hm-doorhack-length", ("min", $"{component.MinNumber}"), ("max", $"{component.MaxNumber}")));
            args.PushMarkup(Loc.GetString("medieval-hm-doorhack-amount", ("amount", $"{component.NumberCount}")));
        }

        public void OnUseInHand(EntityUid uid, DoorHackLockpickComponent comp, BeforeRangedInteractEvent args)
        {
            if (!args.CanReach)
                return;
            OnUse(args.Target, args.User, args.Used, comp);
        }

        public void OnUse(EntityUid? target, EntityUid user, EntityUid used, DoorHackLockpickComponent comp)
        {
            if (target == null)
                return;

            if (TryComp<DoorHackableComponent>(target, out var door) && door != null)
            {
                if (HasComp<SkillsComponent>(user) && _skillsSystem.IntelligenceMin(user))
                {
                    _popup.PopupEntity(Loc.GetString("lock-door-popup-low-intelligence"), user, user);
                    return;
                }

                _audio.PlayPvs(new SoundPathSpecifier(comp.EffectSoundOnSucces), door.Owner);
                if (!_sharedPlayerManager.TryGetSessionByEntity(user, out var session)) return;
                _quickDialog.OpenDialog(session, Loc.GetString("medieval-hm-doorhack-hack"), Loc.GetString("medieval-hm-doorhack-hacking", ("min", $"{door.MinNumber}"), ("max", $"{door.MaxNumber}")), (string message) =>
                {
                    SendHackMessage(message, session, door.MinNumber, door.MaxNumber, door, comp);
                });
            }
        }

        public void SendHackMessage(string message, ICommonSession sender, int min, int max, DoorHackableComponent door, DoorHackLockpickComponent comp)
        {
            if (int.TryParse(message, out int number))
            {
                if (number > max)
                {
                    _prayerSystem.SendSubtleMessage(sender, sender, Loc.GetString("medieval-hm-doorhack-toobig"), Loc.GetString("medieval-hm-doorhack-gibberlish"));
                    return;
                }
                if (number < min)
                {
                    _prayerSystem.SendSubtleMessage(sender, sender, Loc.GetString("medieval-hm-doorhack-toosmall"), Loc.GetString("medieval-hm-doorhack-gibberlish"));
                    return;
                }

                var lossProb = 1f;
                if (sender.AttachedEntity is { Valid: true } senderEntity)
                {
                    var lossProbEv = new ModifyLockpickLossChanceEvent(1f);
                    RaiseLocalEvent(senderEntity, ref lossProbEv);
                }

                if (_random.Prob(lossProb))
                    comp.UseCount--;

                int rightNumber = door.Numbers[door.LockPickProgress];
                if (number == rightNumber)
                {
                    comp.UseCount++;
                    door.LockPickProgress++;
                    if (door.LockPickProgress < door.NumberCount)
                    {
                        var left = door.NumberCount - door.LockPickProgress;
                        _prayerSystem.SendSubtleMessage(sender, sender, Loc.GetString("medieval-hm-doorhack-yay", ("amount", $"{number}"), ("amount2", $"{left}")), Loc.GetString("medieval-hm-doorhack-success"));
                        _audio.PlayPvs(new SoundPathSpecifier(comp.EffectSoundOnNext), door.Owner);
                        return;
                    }
                    else
                    {
                        _prayerSystem.SendSubtleMessage(sender, sender, Loc.GetString("medieval-hm-doorhack-urhacker", ("amount", $"{number}")), Loc.GetString("medieval-hm-doorhack-success"));
                        _audio.PlayPvs(new SoundPathSpecifier(comp.EffectSoundOnOpen), door.Owner);
                        //_door.TryToggleDoor(door.Owner);
                        EnsureComp<DoorBoltComponent>(door.Owner, out var bolt);
                        var doorEntity = new Entity<DoorBoltComponent>(door.Owner, bolt);
                        _door.TrySetBoltDown(doorEntity, false);
                        door.LockPickProgress = 0;
                        if (TryComp<AffectRoundStatsComponent>(sender.AttachedEntity, out var player))
                        {
                            player.Lockpicks++;
                            foreach (var barrier in EntityManager.EntityQuery<RoundStatCounterRuleComponent>())
                            {
                                barrier.TotalLockpicks++;
                            }
                        }
                        return;
                    }
                }


                if (comp.UseCount != 0)
                    _audio.PlayPvs(new SoundPathSpecifier(comp.EffectSoundOnNo), door.Owner);

                if (!TryComp<DoorHackerComponent>(sender.AttachedEntity, out var doorHacker))
                {
                    if (number > rightNumber)
                    {
                        _prayerSystem.SendSubtleMessage(sender, sender, Loc.GetString("medieval-hm-doorhack-toostrong", ("amount", $"{number}")), Loc.GetString("medieval-hm-doorhack-usuck"));
                        door.LockPickProgress = 0;

                    }
                    else if (number < rightNumber)
                    {
                        _prayerSystem.SendSubtleMessage(sender, sender, Loc.GetString("medieval-hm-doorhack-tooweak", ("amount", $"{number}")), Loc.GetString("medieval-hm-doorhack-usuck"));
                        door.LockPickProgress = 0;

                    }
                }
                else
                {
                    if (number > rightNumber)
                    {
                        door.LockPickProgress = 0;

                        if (number - 1 == rightNumber || number - 2 == rightNumber)
                            _prayerSystem.SendSubtleMessage(sender, sender, Loc.GetString("medieval-hm-doorhack-abitweaker", ("amount", $"{number}")), Loc.GetString("medieval-hm-doorhack-usuck"));
                        else
                            _prayerSystem.SendSubtleMessage(sender, sender, Loc.GetString("medieval-hm-doorhack-weaker", ("amount", $"{number}")), Loc.GetString("medieval-hm-doorhack-usuck"));
                    }
                    else if (number < rightNumber)
                    {
                        door.LockPickProgress = 0;

                        if (number + 1 == rightNumber || number + 2 == rightNumber)
                            _prayerSystem.SendSubtleMessage(sender, sender, Loc.GetString("medieval-hm-doorhack-abitstronger", ("amount", $"{number}")), Loc.GetString("medieval-hm-doorhack-usuck"));
                        else
                            _prayerSystem.SendSubtleMessage(sender, sender, Loc.GetString("medieval-hm-doorhack-stronger", ("amount", $"{number}")), Loc.GetString("medieval-hm-doorhack-usuck"));
                    }
                }

                if (comp.UseCount == 0)
                {
                    _prayerSystem.SendSubtleMessage(sender, sender, Loc.GetString("medieval-hm-doorhack-broke"), Loc.GetString("medieval-hm-doorhack-usuck"));
                    if (sender.AttachedEntity != null)
                        _audio.PlayPvs(new SoundPathSpecifier(comp.EffectSoundOnBreak), sender.AttachedEntity.Value);
                    QueueDel(comp.Owner);
                }
            }
            else
            {
                _prayerSystem.SendSubtleMessage(sender, sender, Loc.GetString("medieval-hm-doorhack-incorrect"), Loc.GetString("medieval-hm-doorhack-gibberlish"));
            }
        }
    }
}
