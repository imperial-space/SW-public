// parry activation cancels prepared attacks; success preserves the active window.
using Content.Shared.Imperial.Medieval.Poise.LightAttack;
using Content.Shared.Imperial.Medieval.Poise;
using Content.Shared.Imperial.Medieval.Poise.CombatStamina;
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
using Content.Shared.Imperial.Medieval.Skills;
using Robust.Shared.Configuration;
using Content.Shared.CCVar;
using Content.Shared.Weapons.Melee;
using Content.Shared.Damage.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs;
using Robust.Shared.Audio;
using System;
using System.Collections.Generic;

namespace Content.Shared.MeleeParry
{
    [Serializable, NetSerializable]
    public sealed class ParryPressedEvent : EntityEventArgs
    {
        public NetEntity Weapon;
        public GameTick InputTick; // the predicted input tick, not a claimed wall-clock timestamp.
    }

    public readonly struct ParryParameters
    {
        public readonly float Able;
        public readonly float Window;

        public ParryParameters(float able, float window)
        {
            Able = able;
            Window = window;
        }
    }

    public sealed partial class MeleeParrySystem : EntitySystem
    {
        [Dependency] private readonly AdvancedLightAttackSystem _advanced = default!;
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
        [Dependency] private readonly CombatStaminaSystem _combatStamina = default!;
        [Dependency] private readonly PoiseSystem _poise = default!;

        private float _desyncTolerance;
        private float _parryReadySound;
        private readonly HashSet<EntityUid> _playedReadySounds = new();

        public override void Initialize()
        {
            base.Initialize();
            InitializeConfirmation();

            SubscribeLocalEvent<MeleeParryAbleComponent, BeforeDamageChangedEvent>(OnDamage);
            SubscribeLocalEvent<MeleeParryAbleComponent, BeforeStaminaDamageEvent>(OnStaminaDamage);

            SubscribeAllEvent<ParryPressedEvent>(ExecuteParry);

            CommandBinds.Builder
                .Bind(ContentKeyFunctions.MedievalMeleeParry, InputCmdHandler.FromDelegate(OnParryPressedLocal))
                .Register<MeleeParrySystem>();

            _cfg.OnValueChanged(CCVars.ParryDesyncTolerance, (value) => _desyncTolerance = value, true);
            _cfg.OnValueChanged(CCVars.ParryReadySoundType, (value) => _parryReadySound = value, true);
        }

        private void OnParryPressedLocal(ICommonSession? session)
        {
            if (!_netMan.IsClient) return;
            if (session?.AttachedEntity is not { } uid) return;

            if (CheckParryRequirements(uid, out var parryStorage, out var parry, out var item))
            {
                _entityManager.RaisePredictiveEvent(new ParryPressedEvent { Weapon = GetNetEntity(item), InputTick = _timing.CurTick });
                return;
            }

            if (TryComp<MeleeParryStorageComponent>(uid, out var storage))
            {
                var timeLeft = (storage.NextParryTime - _timing.CurTime).TotalSeconds;

                if (timeLeft > 0 && timeLeft <= 0.15f)
                {
                    storage.ParryQueued = true;
                }
            }
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            PruneConfirmationHistory();

            if (!_netMan.IsClient || _playerManager.LocalEntity is not { } localUid)
                return;

            if (TryComp<MeleeParryStorageComponent>(localUid, out var storage))
            {
                if (_timing.CurTime > storage.NextParryTime)
                {
                    if (storage.NextParryTime == TimeSpan.Zero)
                    {
                        _playedReadySounds.Add(localUid);
                        return;
                    }

                    if (!_playedReadySounds.Contains(localUid) && _timing.IsFirstTimePredicted)
                    {
                        // disabling the ready sound must not discard a queued input.
                        if (_parryReadySound != 0)
                        {
                            var soundPath = _parryReadySound switch
                            {
                                1 => "/Audio/Imperial/Medieval/soft_bell_ding.ogg",
                                2 => "/Audio/Imperial/Medieval/pop.ogg",
                                3 => "/Audio/Imperial/Medieval/single_tick1.ogg",
                                4 => "/Audio/Imperial/Medieval/single_tick2.ogg",
                                _ => "/Audio/Imperial/Medieval/soft_bell_ding.ogg",
                            };

                            _audio.PlayGlobal(new SoundPathSpecifier(soundPath), Filter.Local(), false);
                        }
                        _playedReadySounds.Add(localUid);
                    }

                    // Queued input is consumed only when its predictive event is
                    // first dispatched. The registered event handles later replay.
                    if (storage.ParryQueued && _timing.IsFirstTimePredicted)
                    {
                        var item = _hands.GetActiveItem(localUid);
                        if (item != null && TryComp<MeleeParryComponent>(item.Value, out var parry))
                        {
                            storage.ParryQueued = false;
                            _entityManager.RaisePredictiveEvent(new ParryPressedEvent { Weapon = GetNetEntity(item.Value), InputTick = _timing.CurTick });
                        }
                    }
                }
                else
                {
                    if ((storage.NextParryTime - _timing.CurTime).TotalSeconds > 0.5)
                    {
                        _playedReadySounds.Remove(localUid);
                    }
                }
            }
        }

        private bool CheckParryRequirements(EntityUid uid, out MeleeParryStorageComponent parryStorage, out MeleeParryComponent parry, out EntityUid itemUid)
        {
            parryStorage = null!;
            parry = null!;
            itemUid = EntityUid.Invalid;

            if (TryComp<StaminaComponent>(uid, out var stam) && stam.Critical)
                return false;

            if (TryComp<MobStateComponent>(uid, out var mobState))
            {
                if (mobState.CurrentState is MobState.Critical or MobState.Dead)
                    return false;
            }

            if (!TryComp<MeleeParryStorageComponent>(uid, out var storageComp))
                return false;

            if (_netMan.IsServer)
                if (_timing.CurTime + TimeSpan.FromSeconds(_desyncTolerance) < storageComp.NextParryTime)
                    return false;

            if (_netMan.IsClient)
                if (_timing.CurTime < storageComp.NextParryTime)
                    return false;

            var item = _hands.GetActiveItem(uid);
            if (item == null || !TryComp<MeleeParryComponent>(item.Value, out var parryComp))
                return false;

            if (_useDelay.IsDelayed(item.Value))
                return false;

            parryStorage = storageComp;
            parry = parryComp;
            itemUid = item.Value;

            return true;
        }

        // share activation and validate the predicted input tick.
        private void ExecuteParry(ParryPressedEvent args, EntitySessionEventArgs sessionArgs)
        {
            if (sessionArgs.SenderSession.AttachedEntity is not { } uid) return;
            TryStartParry(uid, GetEntity(args.Weapon), args.InputTick);
        }

        /// <summary>Shared predicted activation; the server validates the tick and existing requirements.</summary>
        public bool TryStartParry(EntityUid uid, EntityUid expectedItem, GameTick inputTick)
        {
            if (!CheckParryRequirements(uid, out var parryStorage, out var parry, out var activeItem))
            {
                if (_netMan.IsServer && TryComp<MeleeWeaponComponent>(activeItem, out var wpn))
                {
                    Dirty(activeItem, wpn);
                    Dirty(activeItem, parry);
                    Dirty(uid, parryStorage);
                }
                return false;
            }

            if (expectedItem != activeItem)
                return false;

            if (!TryGetParryInputTime(parryStorage, inputTick, out var inputTime))
                return false;
            parryStorage.LastParryInputTick = inputTick;

            if (!TryComp<MeleeWeaponComponent>(activeItem, out var weapon))
                return false;

            var useDelay = EnsureComp<UseDelayComponent>(activeItem);
            var cooldown = TimeSpan.FromSeconds(Math.Clamp(parry.ParryCooldown / (GetAgilityMod(uid) / 10f), 2.5f, 7f));
            var nextTime = _timing.CurTime + cooldown;

            parryStorage.NextParryTime = nextTime;

            parry.ParriedTime = inputTime;
            RecordConfirmationAttempt(uid, activeItem, parry, inputTime);
            parryStorage.CooldownParry = (float)cooldown.TotalSeconds;

            _useDelay.SetLength(activeItem, TimeSpan.FromSeconds(parry.ParryUseDelay));
            _useDelay.TryResetDelay((activeItem, useDelay));
            // actor-wide parry lockout does not overwrite either weapon's recovery.
            _advanced.StartParryLockout(uid, _timing.CurTime + TimeSpan.FromSeconds(parry.ParryUseDelay));

            var effectCoordinates = Transform(uid).Coordinates;
            var effectUid = PredictedSpawnAttachedTo(parry.ParryEffectWindow, effectCoordinates);
            if (TryComp<MeleeParryEffectComponent>(effectUid, out var effect))
            {
                effect.AnimationStartTime = parry.ParriedTime;
                Dirty(effectUid, effect);
            }

            _audio.PlayPredicted(parry.ParryWindowSound, effectCoordinates, uid);

            Dirty(activeItem, weapon);
            Dirty(activeItem, parry);
            Dirty(uid, parryStorage);
            return true;
        }

        private void OnStaminaDamage(EntityUid uid, MeleeParryAbleComponent component, ref BeforeStaminaDamageEvent args)
        {
            if (args.Cancelled || args.Origin == null)
                return;

            if (TryHandleParry(uid, args.Origin.Value))
                args.Cancelled = true;
        }

        private void OnDamage(EntityUid uid, MeleeParryAbleComponent component, ref BeforeDamageChangedEvent args)
        {
            if (args.Origin == null || !args.Damage.DamageDict.ContainsKey("ParryAble"))
                return;

            if (TryHandleParry(uid, args.Origin.Value))
                args.Cancelled = true;
        }

        private bool TryHandleParry(EntityUid uid, EntityUid attacker)
        {
            // A buffered hit was already judged against its original impact time.
            if (IsResolvingConfirmedHit(uid, attacker))
                return false;
            var attackerItem = _hands.GetActiveItem(attacker);
            var weaponEntity = attackerItem ?? attacker;

            if (!TryComp<MeleeWeaponComponent>(weaponEntity, out var meleeWeapon))
                return false;

            if (!meleeWeapon.Damage.DamageDict.TryGetValue("ParryAble", out var baseParryDmg))
                return false;

            var parryDMG = (float)baseParryDmg;

            if (attackerItem != null && TryComp<MedievalWeaponSkillCategoryComponent>(attackerItem.Value, out var skillComp))
                parryDMG *= skillComp.Skill.GetParryData().Able;

            if (!CheckParryable(uid, parryDMG, out var item, out var parry, out var parryStorage, out var weapon))
                return false;

            // health/stamina callbacks for one hit share its result;
            // separate weapons and attackers in the same tick still pay their own penalty.
            if (_netMan.IsServer && TryRecordParryHit(uid, item, attacker, weaponEntity, _timing.CurTime))
                ApplySuccessfulParry(uid, attacker, weaponEntity, meleeWeapon, item, parry, parryStorage, weapon, _timing.CurTime);
            return true;
        }

        private void ApplySuccessfulParry(EntityUid uid, EntityUid attacker, EntityUid weaponEntity,
            MeleeWeaponComponent meleeWeapon, EntityUid item, MeleeParryComponent parry,
            MeleeParryStorageComponent parryStorage, MeleeWeaponComponent weapon, TimeSpan impactTime)
        {
            // catching a hit no longer consumes or clears the active window.
            parry.LastParryTime = impactTime;

            parryStorage.NextParryTime = TimeSpan.Zero;
            parryStorage.CooldownParry = Math.Clamp(parry.ParryCooldown / (GetAgilityMod(uid) / 10f), 2.5f, 7f);
            parryStorage.ParryQueued = false;

            var useDelay = EnsureComp<UseDelayComponent>(item);
            _useDelay.SetLength(item, TimeSpan.Zero);
            _useDelay.TryResetDelay((item, useDelay));
            // keep the existing successful-parry readiness reward,
            // but do not erase recovery from a strike that already happened.
            _advanced.EndParryLockout(uid);

            // A successful parry interrupts both hands and locks the attacker, not only this weapon.
            var penaltyTime = _timing.CurTime + TimeSpan.FromSeconds(parry.ParriedAttackLockout);
            _advanced.StartParryLockout(attacker, penaltyTime);
            if (meleeWeapon.NextAttack < penaltyTime)
            {
                meleeWeapon.NextAttack = penaltyTime;
                Dirty(weaponEntity, meleeWeapon);
            }

            Dirty(item, weapon);
            Dirty(uid, parryStorage);
            Dirty(item, parry);

            float staminaDMGBoost = 1f;
            if (TryComp<StaminaParryBoosterComponent>(uid, out var booster))
                staminaDMGBoost *= booster.StaminaDamageMultiplier;

            var staminaDamage = parry.ParryStaminaDamage * staminaDMGBoost;
            // The stamina penalty consumes the pending refund first. Poise damage
            // happens afterward, so a break refunds half of the surviving pool only.
            _combatStamina.ApplyParryStaminaDamage(attacker, staminaDamage);
            if (HasComp<PoiseComponent>(attacker))
                _poise.TakePoiseDamage(attacker, parry.ParriedPoiseDamage, source: uid, with: item);

            Spawn(parry.ParryEffectSuccess, Transform(uid).Coordinates);

        }

        public bool CheckParryable(EntityUid uid, float parryDMG, out EntityUid weaponUid, out MeleeParryComponent parry, out MeleeParryStorageComponent parryStorage, out MeleeWeaponComponent weapon)
        {
            weaponUid = EntityUid.Invalid;
            parry = null!;
            parryStorage = null!;
            weapon = null!;

            var item = _hands.GetActiveItem(uid);
            if (item == null) return false;

            if (TryComp<MeleeParryComponent>(item, out var parryComp) &&
                TryComp<MeleeParryStorageComponent>(uid, out var parryStorageComp) &&
                TryComp<MeleeWeaponComponent>(item, out var weaponComp))
            {
                bool isWithinWindow = parryComp.ParriedTime != TimeSpan.Zero && parryComp.ParriedTime <= _timing.CurTime && CountParryWindowTime((item.Value, parryComp), parryDMG) > _timing.CurTime;

                if (isWithinWindow) // every eligible impact in the window is caught.
                {
                    weaponUid = item.Value;
                    parry = parryComp;
                    parryStorage = parryStorageComp;
                    weapon = weaponComp;
                    return true;
                }
            }

            return false;
        }

        private TimeSpan CountParryWindowTime(Entity<MeleeParryComponent> ent, float parryDMG)
        {
            var parryWindow = ent.Comp.ParryWindow;
            if (TryComp<MedievalWeaponSkillCategoryComponent>(ent, out var skillComp))
                parryWindow *= skillComp.Skill.GetParryData().Window;

            return ent.Comp.ParriedTime + TimeSpan.FromSeconds(parryWindow * parryDMG);
        }

        private float GetAgilityMod(EntityUid uid)
        {
            if (TryComp<SkillsComponent>(uid, out var skills) && skills.Levels.TryGetValue("Agility", out var level))
                return MathF.Max(level, 1f);
            return 1f;
        }
    }
}

