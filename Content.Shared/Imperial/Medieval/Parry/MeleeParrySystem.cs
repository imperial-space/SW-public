using System;
using Content.Shared.MeleeParry.Components;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Content.Shared.Weapons.Melee;
using Content.Shared.Damage;
using Robust.Shared.Random;
using Content.Shared.Timing;
using Content.Shared.Inventory;
using Content.Shared.Wieldable.Components;
using Content.Shared.Coordinates;
using Content.Shared.Popups;
using Content.Shared.Damage.Events;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Input.Binding;
using Content.Shared.Input;
using Robust.Shared.Player;
using Robust.Shared.Serialization;
using Robust.Shared.GameObjects;

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
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly InventorySystem _inventory = default!;
        [Dependency] private readonly UseDelaySystem _useDelay = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;
        [Dependency] private readonly INetManager _netMan = default!;
        [Dependency] private readonly SharedHandsSystem _hands = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<MeleeParryAbleComponent, BeforeDamageChangedEvent>(OnDamage);
            SubscribeLocalEvent<MeleeParryAbleComponent, BeforeStaminaDamageEvent>(OnBeforeStaminaDamage);
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
            if (item == null || !TryComp<MeleeParryComponent>(item.Value, out var parry))
                return;

            // Если кнопка в откате - игнорируем
            if (parry.ParriedAgo > 0)
                return;

            _popup.PopupEntity("Парирование", uid, PopupType.LargeCaution);

            parry.ParryWindow = 0.5f;
            parry.ParriedAgo = parry.ParriedTime; // Сразу выдаем штрафной кулдаун

            Dirty(item.Value, parry);
        }

        private void OnStart(EntityUid uid, MeleeParryEffectComponent component, ref MapInitEvent args)
        {
            if (_netMan.IsServer)
                _popup.PopupEntity("Парирование", uid, PopupType.LargeCaution);
        }

        private void OnBeforeStaminaDamage(EntityUid uid, MeleeParryAbleComponent component, ref BeforeStaminaDamageEvent args)
        {
            // Захардкожено для деревянного меча
            if (args.Value != 10)
                return;

            if (CheckParryChanceStamina(uid, component.ParryModifier))
            {
                args.Cancelled = true;
            }
        }

        public bool CheckParryChanceStamina(EntityUid uid, float modifier)
        {
            var item = _hands.GetActiveItem(uid);
            if (item == null) return false;

            if (TryComp<UseDelayComponent>(item, out var delay) && _useDelay.IsDelayed((item.Value, delay)))
                return false;

            if (TryComp<MeleeParryComponent>(item, out var parry) &&
                parry.ParriedAgo <= 0f &&
                parry.ParryWindow > 0f &&
                TryComp<MeleeParryStaminaComponent>(item, out var stamina))
            {
                parry.ParriedAgo = parry.ParriedTime;
                parry.ParryWindow = 0f;
                _popup.PopupEntity("Парирование!", uid, PopupType.LargeCaution);

                // Использование актуального метода получения координат
                Spawn(parry.ParryEffect, Transform(item.Value).Coordinates);

                Dirty(item.Value, parry);
                return true;
            }

            return false;
        }

        private void OnDamage(EntityUid uid, MeleeParryAbleComponent component, ref BeforeDamageChangedEvent args)
        {
            if (args.Damage.GetTotal() < 4)
                return;
            if (!args.Damage.DamageDict.TryGetValue("ParryAble", out var parryDMG))
                return;
            if (args.Damage.GetTotal() > 36)
                return;

            if (CheckParryChance(uid, component.ParryModifier))
            {
                args.Cancelled = true;
            }
        }

        public bool CheckParryChance(EntityUid uid, float modifier)
        {
            var item = _hands.GetActiveItem(uid);
            if (item == null) return false;

            if (TryComp<UseDelayComponent>(item, out var delay) && _useDelay.IsDelayed((item.Value, delay)))
                return false;

            if (TryComp<MeleeParryComponent>(item, out var parry) &&
                parry.ParriedAgo <= 0f &&
                parry.RealParry &&
                parry.ParryWindow > 0f)
            {
                parry.ParriedAgo = parry.ParriedTime;
                parry.ParryWindow = 0f;
                _popup.PopupEntity("Парирование!", uid, PopupType.LargeCaution);

                Spawn(parry.ParryEffect, Transform(item.Value).Coordinates);

                Dirty(item.Value, parry);
                return true;
            }

            return false;
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            // Замена устаревшего EntityManager.EntityQuery на оптимизированный EntityQueryEnumerator
            var parryQuery = EntityQueryEnumerator<MeleeParryComponent>();
            while (parryQuery.MoveNext(out var uid, out var comp))
            {
                if (comp.ParriedAgo > 0f)
                {
                    comp.ParriedAgo = MathF.Max(0f, comp.ParriedAgo - frameTime);
                    if (comp.ParriedAgo == 0f)
                        Dirty(uid, comp);
                }

                if (comp.ParryWindow > 0f)
                {
                    comp.ParryWindow -= frameTime;
                    if (comp.ParryWindow <= 0f)
                    {
                        comp.ParryWindow = 0f;
                        Dirty(uid, comp);
                    }
                }
            }

            var staminaQuery = EntityQueryEnumerator<MeleeParryStaminaComponent>();
            while (staminaQuery.MoveNext(out var uid, out var compS))
            {
                if (compS.ParriedAgo > 0f)
                {
                    compS.ParriedAgo = MathF.Max(0f, compS.ParriedAgo - frameTime);
                    if (compS.ParriedAgo == 0f)
                        Dirty(uid, compS);
                }
            }
        }
    }
}
