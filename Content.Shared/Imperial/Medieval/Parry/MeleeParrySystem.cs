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


        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<MeleeParryAbleComponent, BeforeDamageChangedEvent>(OnDamage);
            SubscribeLocalEvent<MeleeParryAbleComponent, BeforeStaminaDamageEvent>(OnStaminaDamage);

            SubscribeNetworkEvent<ParryPressedEvent>(OnParryNetworkPressed);

            CommandBinds.Builder
                .Bind(ContentKeyFunctions.MedievalMeleeParry, InputCmdHandler.FromDelegate(OnParryLocalPressed))
                .Register<MeleeParrySystem>();

            SubscribeNetworkEvent<PlayParryVfxEvent>(OnPlayVfx);
        }

        private void OnParryLocalPressed(ICommonSession? session)
        {
            // Отправляем событие на сервер
            if (!_netMan.IsClient) return;

            if (session?.AttachedEntity is { } uid)
            {
                if (!TryComp<MeleeParryStorageComponent>(uid, out var parryStorage)) return;
                if (_timing.CurTime < parryStorage.GlobalNextParryTime) return;

                var item = _hands.GetActiveItem(uid);
                if (item != null && TryComp<MeleeParryComponent>(item.Value, out var parry))
                {
                    if (_timing.CurTime >= parry.NextAllowedParryTime)
                    {
                        parry.NextAllowedParryTime = _timing.CurTime + TimeSpan.FromSeconds(parry.ParryCooldown / GetAgilityMod(uid) / 10f);
                        parryStorage.GlobalNextParryTime = parry.NextAllowedParryTime;

                        Spawn(parry.ParryEffectWindow, Transform(uid).Coordinates);

                        RaiseNetworkEvent(new ParryPressedEvent());
                    }
                }
            }
        }

        private void OnParryNetworkPressed(ParryPressedEvent args, EntitySessionEventArgs sessionArgs)
        {
            if (sessionArgs.SenderSession.AttachedEntity is not { } uid) return;

            if (!TryComp<MeleeParryStorageComponent>(uid, out var parryStorage)) return;
            if (_timing.CurTime < parryStorage.GlobalNextParryTime) return;

            var item = _hands.GetActiveItem(uid);
            if (item == null || !TryComp<MeleeParryComponent>(item.Value, out var parry)) return;

            if (_timing.CurTime < parry.NextAllowedParryTime) return;

            parry.ParriedTime = _timing.CurTime; // Запись времени
            parry.NextAllowedParryTime = _timing.CurTime + TimeSpan.FromSeconds(parry.ParryCooldown / (GetAgilityMod(uid) / 10f));
            parryStorage.GlobalNextParryTime = _timing.CurTime + TimeSpan.FromSeconds(parry.ParryCooldown / (GetAgilityMod(uid) / 10f));
            parryStorage.GlobalCooldownParry = parry.ParryCooldown / (GetAgilityMod(uid) / 10f);

            Dirty(item.Value, parry);
            Dirty(uid, parryStorage);

            _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Medieval/iron_parry1.ogg"), uid);

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

            if (CheckParryChance(uid, (float)parryDMG, args.Origin))
            {
                args.Cancelled = true;

                _stamina.TakeStaminaDamage(args.Origin.Value, 10 * (GetEnduranceMod(uid) / 2f) + (float)parryDMG * 5); // Изначально урон стамине = 10 + (выносливость обороняющегося / 2) + (5 * легкость парирования атакующего оружия)
            }
        }

        public bool CheckParryChance(EntityUid uid, float parryDMG, EntityUid? attacker)
        {
            var item = _hands.GetActiveItem(uid);
            if (item == null) return false;

            if (TryComp<UseDelayComponent>(item, out var delay) && _useDelay.IsDelayed((item.Value, delay)))
                return false;

            if (TryComp<MeleeParryComponent>(item, out var parry) &&
                parry.ParriedTime != TimeSpan.Zero &&
                CountParryWindowTime(parry, parryDMG) > _timing.CurTime)
            {
                Spawn(parry.ParryEffectSuccess, Transform(uid).Coordinates);

                parry.LastSuccessParriedAttacker = attacker;
                parry.LastSuccessParriedTime = _timing.CurTime;
                parry.NextAllowedParryTime = TimeSpan.Zero;
                parry.ParriedTime = TimeSpan.Zero;

                if (TryComp<MeleeParryStorageComponent>(uid, out var parryStorage)){
                    parryStorage.GlobalNextParryTime = TimeSpan.Zero;
                    parryStorage.GlobalCooldownParry = parry.ParryCooldown / (GetAgilityMod(uid) / 10f);
                    Dirty(uid, parryStorage);
                }

                Dirty(item.Value, parry);
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
            // Отрисовкой занимаются только клиенты
            if (!_netMan.IsClient) return;

            var uid = GetEntity(args.Uid);

            if (!Exists(uid)) return;

            if (_playerManager.LocalSession?.AttachedEntity == uid)
                return;

            Spawn(args.EffectId, Transform(uid).Coordinates);
        }

        private float GetAgilityMod(EntityUid uid)
        {
            if (TryComp<SkillsComponent>(uid, out var skills) && skills.Levels.TryGetValue("Agility", out var level))
                return Math.Max(level, 1f);
            return 1f;
        }

        private float GetEnduranceMod(EntityUid uid)
        {
            if (TryComp<SkillsComponent>(uid, out var skills) && skills.Levels.TryGetValue("Endurance", out var level))
                return Math.Max(level, 1f);
            return 1f;
        }
    }
}
