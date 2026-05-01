using Content.Shared.MeleeParry.Components;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Content.Shared.Damage;
using Content.Shared.Timing;
using Content.Shared.Popups;
using Content.Shared.Damage.Events;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Input.Binding;
using Content.Shared.Input;
using Robust.Shared.Player;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.Medieval.Skills;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Content.Shared.CCVar;

namespace Content.Shared.MeleeParry
{
    // Сетевое событие для передачи команды парирования с клиента на сервер
    [Serializable, NetSerializable]
    public sealed class ParryPressedEvent : EntityEventArgs { }

    [Serializable, NetSerializable]
    public sealed class PlayParryVfxEvent : EntityEventArgs
    {
        public NetEntity Uid;
        public string EffectId = "MedievalEffectWindowParry";
    }

    public sealed partial class MeleeParrySystem : EntitySystem
    {
        [Dependency] private readonly IConfigurationManager _cfg = default!;
        [Dependency] internal readonly IEntityManager _entityManager = default!;
        [Dependency] internal readonly ISharedPlayerManager _playerManager = default!;
        [Dependency] internal readonly IMapManager _mapManager = default!;
        [Dependency] protected readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly UseDelaySystem _useDelay = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;
        [Dependency] private readonly INetManager _netMan = default!;
        [Dependency] private readonly SharedHandsSystem _hands = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly SharedStaminaSystem _stamina = default!;

        private float _parryStaminaDamage;
        private float _desyncTolerance;
        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<MeleeParryAbleComponent, BeforeDamageChangedEvent>(OnDamage);
            SubscribeLocalEvent<MeleeParryAbleComponent, BeforeStaminaDamageEvent>(OnStaminaDamage);

            SubscribeNetworkEvent<ParryPressedEvent>(ExecuteParryNetworked);

            CommandBinds.Builder
                .Bind(ContentKeyFunctions.MedievalMeleeParry, InputCmdHandler.FromDelegate(OnParryPressedLocal))
                .Register<MeleeParrySystem>();

            SubscribeNetworkEvent<PlayParryVfxEvent>(OnPlayVfx);

            _cfg.OnValueChanged(CCVars.ParryStaminaDamage, (value) => _parryStaminaDamage = value, true);
            _cfg.OnValueChanged(CCVars.DesyncTolerance, (value) => _desyncTolerance = value, true);
        }

        private void OnParryPressedLocal(ICommonSession? session)
        {
            if (!_netMan.IsClient) return;
            if (session?.AttachedEntity is not { } uid) return;

            if (CheckParryRequiments(uid, out var parryStorage, out var parry))
            {
                ExecuteParryLocal(uid, parry, parryStorage);
                return;
            }

            if (TryComp<MeleeParryStorageComponent>(uid, out var storage))
            {
                var timeLeft = (storage.GlobalNextParryTime - _timing.CurTime).TotalSeconds;

                if (timeLeft > 0 && timeLeft <= 0.25f)
                {
                    storage.ParryQueued = true;
                }
            }
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            if (!_netMan.IsClient || _playerManager.LocalEntity is not { } localUid)
                return;

            if (TryComp<MeleeParryStorageComponent>(localUid, out var storage) && storage.ParryQueued)
            {
                if (_timing.CurTime >= storage.GlobalNextParryTime)
                {
                    var item = _hands.GetActiveItem(localUid);
                    if (item != null && TryComp<MeleeParryComponent>(item.Value, out var parry))
                    {
                        storage.ParryQueued = false;
                        ExecuteParryLocal(localUid, parry, storage);
                    }
                }
            }
        }

        private bool CheckParryRequiments(EntityUid uid, out MeleeParryStorageComponent parryStorage, out MeleeParryComponent parry)
        {
            parryStorage = null!;
            parry = null!;

            if (!TryComp<MeleeParryStorageComponent>(uid, out var storageComp)) return false;
            if (_timing.CurTime < storageComp.GlobalNextParryTime) return false;

            var item = _hands.GetActiveItem(uid);
            if (item == null || !TryComp<MeleeParryComponent>(item.Value, out var parryComp)) return false;

            parryStorage = storageComp;
            parry = parryComp;

            return true;
        }

        private void ExecuteParryLocal(EntityUid uid, MeleeParryComponent parry, MeleeParryStorageComponent parryStorage)
        {
            var cooldown = TimeSpan.FromSeconds(Math.Clamp(parry.ParryCooldown / (GetAgilityMod(uid) / 10f), 2.5f, 7.5f));
            var nextTime = _timing.CurTime + cooldown;

            parryStorage.GlobalNextParryTime = nextTime;
            parryStorage.GlobalCooldownParry = (float)cooldown.TotalSeconds;

            RaiseNetworkEvent(new ParryPressedEvent());
        }

        private void ExecuteParryNetworked(ParryPressedEvent args, EntitySessionEventArgs sessionArgs)
        {
            if (sessionArgs.SenderSession.AttachedEntity is not { } uid) return;

            if (!TryComp<MeleeParryStorageComponent>(uid, out var parryStorage)) return;

            if (_timing.CurTime + TimeSpan.FromSeconds(_desyncTolerance) < parryStorage.GlobalNextParryTime) return;

            var item = _hands.GetActiveItem(uid);
            if (item == null || !TryComp<MeleeParryComponent>(item.Value, out var parry)) return;

            var cooldown = TimeSpan.FromSeconds(Math.Clamp(parry.ParryCooldown / (GetAgilityMod(uid) / 10f), 2.5f, 7f));
            var nextTime = _timing.CurTime + cooldown;

            parryStorage.GlobalNextParryTime = nextTime;

            var latency = TimeSpan.FromMilliseconds(Math.Min(sessionArgs.SenderSession.Ping / 2, 400));

            parry.ParriedTime = _timing.CurTime - latency;
            parryStorage.GlobalCooldownParry = (float)cooldown.TotalSeconds;

            Dirty(item.Value, parry);
            Dirty(uid, parryStorage);

            RaiseNetworkEvent(new PlayParryVfxEvent()
            {
                Uid = GetNetEntity(uid),
                EffectId = parry.ParryEffectWindow
            });
        }

        private void OnStaminaDamage(EntityUid uid, MeleeParryAbleComponent component, ref BeforeStaminaDamageEvent args)
        {
            var item = _hands.GetActiveItem(uid);
            if (!TryComp<MeleeParryComponent>(item, out var parry)) return;

            if (parry.LastSuccessParriedAttacker == args.Origin &&
               (_timing.CurTime - parry.LastSuccessParriedTime).TotalSeconds < 3f) // Перестраховка
            {
                parry.LastSuccessParriedAttacker = null;
                parry.LastSuccessParriedTime = TimeSpan.Zero;
                Dirty(item.Value, parry);
                args.Cancelled = true;
            }
        }

        private void OnDamage(EntityUid uid, MeleeParryAbleComponent component, ref BeforeDamageChangedEvent args)
        {
            if (args.Damage.GetTotal() < 4 || // Если урон слишком маленький
                args.Origin == null)
                return;
            if (!args.Damage.DamageDict.TryGetValue("ParryAble", out var parryDMG))
                return;


            if (CheckParryable(uid, (float)parryDMG, out var item, out var parry, out var parryStorage))
            {
                args.Cancelled = true;

                Spawn(parry.ParryEffectSuccess, Transform(uid).Coordinates);

                parry.LastSuccessParriedAttacker = args.Origin;

                parry.LastSuccessParriedTime = _timing.CurTime;
                parry.ParriedTime = TimeSpan.Zero;


                parryStorage.GlobalNextParryTime = TimeSpan.Zero;
                parryStorage.GlobalCooldownParry = Math.Clamp(parry.ParryCooldown / (GetAgilityMod(uid) / 10f), 2.5f, 7f);
                parryStorage.ParryQueued = false;


                Dirty(uid, parryStorage);
                Dirty(item, parry);

                _stamina.TakeStaminaDamage(args.Origin.Value, _parryStaminaDamage);
            }
        }

        public bool CheckParryable(EntityUid uid, float parryDMG, out EntityUid weaponUid, out MeleeParryComponent parry, out MeleeParryStorageComponent parryStorage)
        {
            weaponUid = EntityUid.Invalid;
            parry = null!;
            parryStorage = null!;

            var item = _hands.GetActiveItem(uid);
            if (item == null) return false;

            if (TryComp<UseDelayComponent>(item, out var delay) && _useDelay.IsDelayed((item.Value, delay)))
                return false;

            if (TryComp<MeleeParryComponent>(item, out var parryComp) &&
                parryComp.ParriedTime != TimeSpan.Zero &&
                CountParryWindowTime(parryComp, parryDMG) > _timing.CurTime &&
                TryComp<MeleeParryStorageComponent>(uid, out var parryStorageComp))
            {
                weaponUid = item.Value;
                parry = parryComp;
                parryStorage = parryStorageComp;
                return true;
            }

            return false;
        }

        private TimeSpan CountParryWindowTime(MeleeParryComponent parry, float parryDMG)
        {
            return (parry.ParriedTime + TimeSpan.FromSeconds(parry.ParryWindow * parryDMG)); //Потом можно настроить более тонко. parryDMG = ParryAble => Это тип урона у оружия(см. в прототипе оружия)
        }

        private void OnPlayVfx(PlayParryVfxEvent args)
        {
            if (!_netMan.IsClient) return;

            var uid = GetEntity(args.Uid);

            if (!Exists(uid)) return;

            Spawn(args.EffectId, Transform(uid).Coordinates);
            _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Medieval/iron_parry1.ogg"), uid);
        }

        private float GetAgilityMod(EntityUid uid)
        {
            if (TryComp<SkillsComponent>(uid, out var skills) && skills.Levels.TryGetValue("Agility", out var level))
                return Math.Max(level, 1f);
            return 1f;
        }
    }
}
