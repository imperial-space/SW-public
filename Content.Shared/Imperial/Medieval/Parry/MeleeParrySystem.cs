using System;
using Content.Shared.MeleeParry.Components;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Content.Shared.Damage;
using Robust.Shared.Random;
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

namespace Content.Shared.MeleeParry
{
    // Сетевое событие для передачи команды парирования с клиента на сервер
    [Serializable, NetSerializable]
    public sealed class ParryPressedEvent : EntityEventArgs { }

    public sealed partial class MeleeParrySystem : EntitySystem
    {
        [Dependency] internal readonly IEntityManager _entityManager = default!;
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
            SubscribeLocalEvent<MeleeParryEffectComponent, MapInitEvent>(OnStart);

            // Обработка сетевого ивента сервером
            SubscribeNetworkEvent<ParryPressedEvent>(OnParryNetworkPressed);

            // Бинды обрабатываются только локально на клиенте
            CommandBinds.Builder
                .Bind(ContentKeyFunctions.MedievalMeleeParry, InputCmdHandler.FromDelegate(OnParryLocalPressed))
                .Register<MeleeParrySystem>();
        }

        private void OnParryLocalPressed(ICommonSession? session)
        {
            // Отправляем событие на сервер
            if (_netMan.IsClient)
                RaiseNetworkEvent(new ParryPressedEvent());
        }

        private void OnParryNetworkPressed(ParryPressedEvent args, EntitySessionEventArgs sessionArgs)
        {
            if (sessionArgs.SenderSession.AttachedEntity is not { } uid)
                return;

            var item = _hands.GetActiveItem(uid);
            if (item == null || !TryComp<MeleeParryComponent>(item.Value, out var parry)) return;

            if (_timing.CurTime < parry.NextAllowedParryTime) return;

            parry.ParriedTime = _timing.CurTime; // Запись времени
            parry.NextAllowedParryTime = _timing.CurTime + TimeSpan.FromSeconds(parry.ParryCooldown / (Comp<SkillsComponent>(uid).Levels["Agility"] / 2));

            _popup.PopupEntity("Парирование", uid, PopupType.LargeCaution);

            Dirty(item.Value, parry);
        }

        private void OnStart(EntityUid uid, MeleeParryEffectComponent component, ref MapInitEvent args)
        {
            if (_netMan.IsServer)
                _popup.PopupEntity("Парирование", uid, PopupType.LargeCaution);
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

                _stamina.TakeStaminaDamage(args.Origin.Value, 15 * (Comp<SkillsComponent>(uid).Levels["Endurance"] / 2) + (float)parryDMG * 5); // Изначально урон стамине 15 + (выносливость обороняющегося / 2) + (5 * легкость парирования атакующего оружия)
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
                _popup.PopupEntity("Успешное Парирование!", uid, PopupType.LargeCaution);

                Spawn(parry.ParryEffect, Transform(item.Value).Coordinates);

                parry.LastSuccessParriedAttacker = attacker;
                parry.LastSuccessParriedTime = _timing.CurTime;
                parry.NextAllowedParryTime = TimeSpan.Zero;
                parry.ParriedTime = TimeSpan.Zero;

                Dirty(item.Value, parry);
                return true;
            }

            return false;
        }

        private TimeSpan CountParryWindowTime(MeleeParryComponent parry, float parryDMG)
        {
            return (parry.ParriedTime + TimeSpan.FromSeconds(parry.ParryWindow * parryDMG)); //Потом можно настроить более тонко. parryDMG = ParryAble => Это тип урона у оружия(см. в прототипе оружия)
        }
    }
}
