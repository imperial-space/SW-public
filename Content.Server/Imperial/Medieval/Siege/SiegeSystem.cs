using Content.Shared.Siege.Components;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Content.Server.Administration;
using Content.Shared.Verbs;
using Content.Shared.DoAfter;
using Robust.Shared.Utility;
using Content.Shared.Siege.Events;
using Content.Server.Prayer;
using System.Numerics;
using Robust.Server.GameObjects;

namespace Content.Server.Siege
{
    public sealed partial class SiegeSystem : EntitySystem
    {
        [Dependency] internal readonly IEntityManager _entityManager = default!;
        [Dependency] internal readonly IMapManager _mapManager = default!;
        [Dependency] protected readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly QuickDialogSystem _quickDialog = default!;
        [Dependency] private readonly ISharedPlayerManager _sharedPlayerManager = default!;
        [Dependency] private readonly PrayerSystem _prayerSystem = default!;
        [Dependency] private readonly SharedTransformSystem _transform = default!;
        [Dependency] private readonly AppearanceSystem _appearanceSystem = default!;
        [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<SiegeAmmoComponent, BeforeRangedInteractEvent>(OnUseInHand);
            SubscribeLocalEvent<SiegeWeaponComponent, ComponentStartup>(OnStart);
            SubscribeLocalEvent<SiegeWeaponComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
            SubscribeLocalEvent<SiegeWeaponComponent, SiegeChargeDoAfterArgs>(OnDoAfterCharge);
            SubscribeLocalEvent<SiegeWeaponComponent, SiegeShootDoAfterArgs>(OnDoAfterShoot);
            SubscribeLocalEvent<SiegeWeaponComponent, ExaminedEvent>(OnExamine);
        }
        public void OnStart(EntityUid uid, SiegeWeaponComponent comp, ComponentStartup args)
        {
            comp.AnimationState = "unloaded";
            Dirty(uid, comp);
            _appearanceSystem.SetData(uid, CatapultVisualKey.Ready, comp.AnimationState);
        }
        public void OnUseInHand(EntityUid uid, SiegeAmmoComponent comp, BeforeRangedInteractEvent args)
        {
            if (!args.CanReach)
                return;
            OnUse(args.Target, args.User, args.Used, comp);
        }

        public void OnUse(EntityUid? target, EntityUid user, EntityUid used, SiegeAmmoComponent comp)
        {
            if (target == null)
                return;
            if (!_sharedPlayerManager.TryGetSessionByEntity(user, out var session)) return;

            if (TryComp<SiegeWeaponComponent>(target, out var siegeWeapon))
            {
                if (siegeWeapon.LoadedShot == "")
                {
                    siegeWeapon.LoadedShot = comp.AmmoType;
                    QueueDel(used);
                }
                else
                {
                    _prayerSystem.SendSubtleMessage(session, session, "В орудие уже был заряжен боеприпас.", "Уже заряжено");
                }
            }
        }

        private void OnExamine(EntityUid uid, SiegeWeaponComponent comp, ExaminedEvent args)
        {
            args.PushMarkup("Текущая наводка: [color=red] X = " + comp.TargetX + "[/color], [color=green] Y = " + comp.TargetY + "[/color]");
            if (comp.SpringCharged)
                args.PushMarkup("Пружина [color=green]натянута[/color]");
            else
                args.PushMarkup("Пружина [color=red]не натянута[/color]");
            if (comp.LoadedShot != "")
                args.PushMarkup("Боеприпас [color=green]заряжен[/color]");
            else
                args.PushMarkup("Боеприпас [color=red]не заряжен[/color]");
        }

        private void OnGetAlternativeVerbs(EntityUid uid, SiegeWeaponComponent comp, GetVerbsEvent<AlternativeVerb> ev)
        {
            if (!ev.CanAccess || !ev.CanInteract) return;
            ev.Verbs.Add(new AlternativeVerb
            {
                Act = () =>
                {
                    TrySiegeShoot(ev.User, ev.Target, comp);
                },
                Text = Loc.GetString("siegeVerbShoot"),
                Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/Medieval/date.rsi"), "shoot")
            });

            ev.Verbs.Add(new AlternativeVerb
            {
                Act = () =>
                {
                    TryAimX(ev.User, ev.Target, comp);
                },
                Text = Loc.GetString("siegeAimX"),
                Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/Medieval/date.rsi"), "x")
            });

            ev.Verbs.Add(new AlternativeVerb
            {
                Act = () =>
                {
                    TryAimY(ev.User, ev.Target, comp);
                },
                Text = Loc.GetString("siegeAimY"),
                Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/Medieval/date.rsi"), "y")
            });
            if (!comp.SpringCharged)
            {
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () =>
                    {
                        TryCharge(ev.User, ev.Target, comp);
                    },
                    Text = Loc.GetString("siegeCharge"),
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/Medieval/date.rsi"), "charge")
                });
            }
        }
        public void TryCharge(EntityUid user, EntityUid siegeUid, SiegeWeaponComponent comp)
        {
            if (!_sharedPlayerManager.TryGetSessionByEntity(user, out var session)) return;

            if (comp.LoadedShot == "")
            {
                _prayerSystem.SendSubtleMessage(session, session, "В орудие необходимо зарядить боеприпас.", "Нужно зарядить");
                return;
            }
            if (comp.SpringCharged)
            {
                _prayerSystem.SendSubtleMessage(session, session, "Пружина орудия уже натянута.", "Уже натянута");
                return;
            }
            var doAfter = new DoAfterArgs(EntityManager, user, comp.ChargeTime, new SiegeChargeDoAfterArgs(), target: user, eventTarget: siegeUid)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                NeedHand = true,
                CancelDuplicate = true
            };
            _audio.PlayPvs(new SoundPathSpecifier(comp.EffectSoundOnLoad), siegeUid);
            comp.AnimationState = "charge";
            Dirty(siegeUid, comp);
            _appearanceSystem.SetData(siegeUid, CatapultVisualKey.Ready, comp.AnimationState);
            _doAfter.TryStartDoAfter(doAfter);
        }

        private void OnDoAfterCharge(EntityUid uid, SiegeWeaponComponent comp, SiegeChargeDoAfterArgs ev)
        {
            if (ev.Cancelled)
            {
                comp.AnimationState = "unloaded";
                Dirty(uid, comp);
                _appearanceSystem.SetData(uid, CatapultVisualKey.Ready, comp.AnimationState);
                return;
            }
            comp.AnimationState = "ready";
            Dirty(uid, comp);
            _appearanceSystem.SetData(uid, CatapultVisualKey.Ready, comp.AnimationState);
            comp.SpringCharged = true;
        }

        public void TrySiegeShoot(EntityUid user, EntityUid siegeUid, SiegeWeaponComponent comp)
        {
            if (!_sharedPlayerManager.TryGetSessionByEntity(user, out var session)) return;
            if (Math.Abs(comp.TargetX) + Math.Abs(comp.TargetY) < comp.MinTarget)
            {
                _prayerSystem.SendSubtleMessage(session, session, "Нельзя стрелять слишком близко к орудию.", "Нужно навестись");
                return;
            }
            if (!comp.SpringCharged)
            {
                _prayerSystem.SendSubtleMessage(session, session, "Орудие должно быть заряжено, чтобы стрелять.", "Нужно зарядить");
                return;
            }
            var xform = Transform(siegeUid);
            var coords = xform.Coordinates;

            var worldPos = coords.ToMap(_entityManager, _transform);
            var targetWorldPos = worldPos.Position + new Vector2(comp.TargetX, comp.TargetY);
            var targetMapCoords = new MapCoordinates(targetWorldPos, worldPos.MapId);
            var targetCoords = EntityCoordinates.FromMap(_mapManager, targetMapCoords);
            _audio.PlayPvs(new SoundPathSpecifier(comp.EffectSoundOnShoot), siegeUid);
            var angle = Angle.FromWorldVec(targetWorldPos - worldPos.Position);
            _transform.SetCoordinates(new Entity<TransformComponent, MetaDataComponent>(siegeUid, xform, MetaData(siegeUid)), coords, angle);

            var doAfter = new DoAfterArgs(EntityManager, user, 0.9f, new SiegeShootDoAfterArgs(), target: user, eventTarget: siegeUid)
            {
                BreakOnMove = false,
                BreakOnDamage = false,
                NeedHand = false,
                CancelDuplicate = true
            };
            _doAfter.TryStartDoAfter(doAfter);
            comp.AnimationState = "shoot";
            Dirty(siegeUid, comp);
            _appearanceSystem.SetData(siegeUid, CatapultVisualKey.Ready, comp.AnimationState);
        }

        private void OnDoAfterShoot(EntityUid siegeUid, SiegeWeaponComponent comp, SiegeShootDoAfterArgs ev)
        {
            comp.AnimationState = "unloaded";
            Dirty(siegeUid, comp);
            _appearanceSystem.SetData(siegeUid, CatapultVisualKey.Ready, comp.AnimationState);
            if (ev.Cancelled) return;
            comp.SpringCharged = true;

            var xform = Transform(siegeUid);
            var coords = xform.Coordinates;

            var worldPos = coords.ToMap(_entityManager, _transform);
            var targetWorldPos = worldPos.Position + new Vector2(comp.TargetX, comp.TargetY);
            var targetMapCoords = new MapCoordinates(targetWorldPos, worldPos.MapId);
            var targetCoords = EntityCoordinates.FromMap(_mapManager, targetMapCoords);
            var angle = Angle.FromWorldVec(targetWorldPos - worldPos.Position);

            _transform.SetCoordinates(new Entity<TransformComponent, MetaDataComponent>(siegeUid, xform, MetaData(siegeUid)), coords, angle);
            comp.SpringCharged = false;
            if (comp.LoadedShot == "barrel")
                Spawn("MedievalHitMarkerBarrel", targetCoords);
            else if (comp.LoadedShot == "gunpowder")
                Spawn("MedievalHitMarkerGunpowder", targetCoords);
            else if (comp.LoadedShot == "ap")
                Spawn("MedievalHitMarkerAp", targetCoords);
            else if (comp.LoadedShot == "stone")
                Spawn("MedievalHitMarkerStone", targetCoords);
            else if (comp.LoadedShot == "mortar")
                Spawn("MedievalHitMarkerMortar", targetCoords);
            else if (comp.LoadedShot == "mm")
                Spawn("MedievalHitMarkerMm0", targetCoords);
            else if (comp.LoadedShot == "mmfrag")
                Spawn("MedievalHitMarkerMmFrag0", targetCoords);
            comp.LoadedShot = "";
        }
        public void TryAimX(EntityUid user, EntityUid siegeUid, SiegeWeaponComponent comp)
        {
            if (!_sharedPlayerManager.TryGetSessionByEntity(user, out var session)) return;
            _quickDialog.OpenDialog(session, "Наводка X", "Число", (string message) =>
            {
                AimX(user, session, siegeUid, comp, message);
            });
        }

        public void AimX(EntityUid user, ICommonSession sender, EntityUid siegeUid, SiegeWeaponComponent comp, string message)
        {
            if (int.TryParse(message, out int number))
            {
                var fnumber = (float)number;
                if (fnumber > Math.Abs(comp.MaxTarget))
                {
                    _prayerSystem.SendSubtleMessage(sender, sender, "Слишком далеко", "Нужно ввести меньшее значение");
                    return;
                }
                if (fnumber == 0)
                {
                    _prayerSystem.SendSubtleMessage(sender, sender, "Ноль вводить нельзя", "Нужен не ноль");
                    return;
                }
                comp.TargetX = fnumber;
                var xform = Transform(siegeUid);
                var coords = xform.Coordinates;
                var worldPos = coords.ToMap(_entityManager, _transform);
                var targetWorldPos = worldPos.Position + new Vector2(comp.TargetX, comp.TargetY);
                var angle = Angle.FromWorldVec(targetWorldPos - worldPos.Position);
                _audio.PlayPvs(new SoundPathSpecifier(comp.EffectSoundOnAim), siegeUid);
                _transform.SetCoordinates(new Entity<TransformComponent, MetaDataComponent>(siegeUid, xform, MetaData(siegeUid)), coords, angle);
            }
            else
            {
                _prayerSystem.SendSubtleMessage(sender, sender, "Неверное значение", "Число неверно");
            }
        }

        public void TryAimY(EntityUid user, EntityUid siegeUid, SiegeWeaponComponent comp)
        {
            if (!_sharedPlayerManager.TryGetSessionByEntity(user, out var session)) return;
            _quickDialog.OpenDialog(session, "Наводка Y", "Число", (string message) =>
            {
                AimY(user, session, siegeUid, comp, message);
            });
        }

        public void AimY(EntityUid user, ICommonSession sender, EntityUid siegeUid, SiegeWeaponComponent comp, string message)
        {
            if (int.TryParse(message, out int number))
            {
                var fnumber = (float)number;
                if (Math.Abs(fnumber) > comp.MaxTarget)
                {
                    _prayerSystem.SendSubtleMessage(sender, sender, "Слишком далеко", "Нужно ввести меньшее значение");
                    return;
                }
                if (fnumber == 0)
                {
                    _prayerSystem.SendSubtleMessage(sender, sender, "Ноль вводить нельзя", "Нужен не ноль");
                    return;
                }
                comp.TargetY = fnumber;
                var xform = Transform(siegeUid);
                var coords = xform.Coordinates;
                var worldPos = coords.ToMap(_entityManager, _transform);
                var targetWorldPos = worldPos.Position + new Vector2(comp.TargetX, comp.TargetY);
                var angle = Angle.FromWorldVec(targetWorldPos - worldPos.Position);
                _audio.PlayPvs(new SoundPathSpecifier(comp.EffectSoundOnAim), siegeUid);
                _transform.SetCoordinates(new Entity<TransformComponent, MetaDataComponent>(siegeUid, xform, MetaData(siegeUid)), coords, angle);
            }
            else
            {
                _prayerSystem.SendSubtleMessage(sender, sender, "Неверное значение", "Число неверно");
            }
        }
    }
}
