using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Forged;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.DurabilityDisplay.Components;
using Content.Shared.Imperial.LocalLight;
using Content.Shared.Imperial.Medieval.Lycantropy;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.MedievalMeleeResource.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Overlays;
using Content.Shared.Stealth.Components;
using Content.Shared.Stunnable;
using Content.Shared.Trigger;
using Content.Shared.Trigger.Components.Effects;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Timing;


namespace Content.Shared.Imperial.Medieval.Forged;

public sealed class ForgedAbilitySystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeedModifier = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ForgedComponent, ForgedBloodEyesActionEvent>(OnBloodEyes);
        SubscribeLocalEvent<ForgedComponent, ForgedBoostActionEvent>(OnBoost);
        SubscribeLocalEvent<ForgedComponent, ForgedSilaActionEvent>(OnSila);
        SubscribeLocalEvent<ForgedComponent, ForgedRepairActionEvent>(OnRepair);
        SubscribeLocalEvent<ForgedComponent, ForgedExplosiveActionEvent>(OnExplosiveTrigger);
    }

    public void ExecuteAbility(EntityUid forgedUid, EntityUid moduleUid, string abilityId)
    {
        switch (abilityId)
        {
            case "BloodVisionEyes":
                _actions.AddAction(forgedUid, "BloodEyesAction");
                break;
            case "MedicalEyes":
                MedicalEyes(forgedUid);
                break;
            case "InvisibleVisionEyes":
                InvisibleVisionEyes(forgedUid);
                break;
            case "NightVisionEyes":
                NightVisionEyes(forgedUid);
                break;
            case "Boost":
                CheckBoostReq(forgedUid);
                break;
            case "Sila":
                CheckSilaReq(forgedUid);
                break;
            case "Repair":
                _actions.AddAction(forgedUid, "RepairAction");
                break;
            case "VerySmart":
                VerySmart(forgedUid);
                break;
            case "Right_blade":
                RightBlade(forgedUid);
                break;
            case "Left_blade":
                LeftBlade(forgedUid);
                break;
            case "Left_crossbow":
                LeftCrossbow(forgedUid);
                break;
            case "Right_crossbow":
                RightCrossbow(forgedUid);
                break;
            case "Right_cannon":
                RightCannon(forgedUid);
                break;
            case "Left_cannon":
                LeftCannon(forgedUid);
                break;
            case "Invisibility_Nimbus":
                NimbusStealth(forgedUid);
                break;
            case "Torso_Explosion":
                SetupExplosive(forgedUid);
                break;
            default:
                break;
        }
    }

    private void SpawnModuleInHand(EntityUid forgedUid, string handId, string proto, bool strip)
    {
        if (!_containerSystem.TryGetContainer(forgedUid, handId, out var container))
            return;

        if (container.ContainedEntities.Count > 0)
        {
            var oldItem = container.ContainedEntities[0];
            _containerSystem.Remove(oldItem, container);
            QueueDel(oldItem);
        }

        var item = EntityManager.SpawnEntity(proto, MapCoordinates.Nullspace);
        if (strip)
        {
            RemComp<MedievalMeleeResourceComponent>(item);
            RemComp<DurabilityDisplayComponent>(item);
        }
        _containerSystem.Insert(item, container);
    }

    private void LeftCannon(EntityUid forgedUid)
    {
        SpawnModuleInHand(forgedUid, "body_part_slot_left_hand", "ForgedArmCannon", true);
    }

    private void RightCannon(EntityUid forgedUid)
    {
        SpawnModuleInHand(forgedUid, "body_part_slot_right_hand", "ForgedArmCannon", false);
    }

    private void NimbusStealth(EntityUid forgedUid)
    {
        var stealth = EnsureComp<StealthComponent>(forgedUid);
        EnsureComp<StealthOnMoveComponent>(forgedUid); // Чтобы стелс работал корректно
        Dirty(forgedUid, stealth);
    }

    private void SetupExplosive(EntityUid forgedUid)
    {
        EnsureComp<ExplosionOnTriggerComponent>(forgedUid);
        _actions.AddAction(forgedUid, "ExplosiveAction");
    }


    TimeSpan _lastPressExplose = TimeSpan.Zero;
    private void OnExplosiveTrigger(EntityUid uid, ForgedComponent comp, ForgedExplosiveActionEvent args)
    {
        if (args.Handled) return;

        // Проверка на двойное нажатие (1 секунда)
        if (_gameTiming.CurTime - _lastPressExplose < TimeSpan.FromSeconds(1))
        {
            var ev = new TriggerEvent(uid, "ss");
            RaiseLocalEvent(uid, ref ev, true);
        }
        else
        {
            _lastPressExplose = _gameTiming.CurTime;
        }

        args.Handled = true;
    }
    private void LeftBlade(EntityUid forgedUid)
    {
        string handId = "body_part_slot_left_hand";

        if (!_containerSystem.TryGetContainer(forgedUid, handId, out var container))
            return;

        if (container.ContainedEntities.Count > 0)
        {
            var oldItem = container.ContainedEntities[0];
            _containerSystem.Remove(oldItem, container);
            QueueDel(oldItem);
        }

        var item = EntityManager.SpawnEntity("ForgedArmBlade", MapCoordinates.Nullspace);
        RemComp<MedievalMeleeResourceComponent>(item);
        RemComp<DurabilityDisplayComponent>(item);
        _containerSystem.Insert(item, container);
    }

    private void RightBlade(EntityUid forgedUid)
    {
        string handId = "body_part_slot_right_hand";

        if (!_containerSystem.TryGetContainer(forgedUid, handId, out var container))
            return;

        if (container.ContainedEntities.Count > 0)
        {
            var oldItem = container.ContainedEntities[0];
            _containerSystem.Remove(oldItem, container);
            QueueDel(oldItem);
        }

        var item = EntityManager.SpawnEntity("ForgedArmBlade", MapCoordinates.Nullspace);

        _containerSystem.Insert(item, container);
    }

    private void LeftCrossbow(EntityUid forgedUid)
    {
        string handId = "body_part_slot_left_hand";

        if (!_containerSystem.TryGetContainer(forgedUid, handId, out var container))
            return;

        if (container.ContainedEntities.Count > 0)
        {
            var oldItem = container.ContainedEntities[0];
            _containerSystem.Remove(oldItem, container);
            QueueDel(oldItem);
        }

        var item = EntityManager.SpawnEntity("ForgedArmCrossbow", MapCoordinates.Nullspace);
        RemComp<MedievalMeleeResourceComponent>(item);
        RemComp<DurabilityDisplayComponent>(item);
        _containerSystem.Insert(item, container);
    }

    private void RightCrossbow(EntityUid forgedUid)
    {
        string handId = "body_part_slot_right_hand";

        if (!_containerSystem.TryGetContainer(forgedUid, handId, out var container))
            return;

        if (container.ContainedEntities.Count > 0)
        {
            var oldItem = container.ContainedEntities[0];
            _containerSystem.Remove(oldItem, container);
            QueueDel(oldItem);
        }

        var item = EntityManager.SpawnEntity("ForgedArmCrossbow", MapCoordinates.Nullspace);

        _containerSystem.Insert(item, container);
    }

    private void MedicalEyes(EntityUid forgedUid)
    {
        var comp1 = EnsureComp<ShowHealthBarsComponent>(forgedUid);
        var comp2 = EnsureComp<ShowHealthIconsComponent>(forgedUid);
        Dirty(forgedUid, comp1);
        Dirty(forgedUid, comp2);
    }
    private void InvisibleVisionEyes(EntityUid forgedUid)
    {
        var comp1 = EnsureComp<ShowThirstIconsComponent>(forgedUid);
        var comp2 = EnsureComp<SolutionScannerComponent>(forgedUid);
        Dirty(forgedUid, comp1);
        Dirty(forgedUid, comp2);
    }
    private void NightVisionEyes(EntityUid forgedUid)
    {
        if (!TryComp<LocalLightComponent>(forgedUid, out var comp)) return;
        comp.Radius = 10;
        Dirty(forgedUid, comp);
    }

    private void OnBloodEyes(EntityUid uid, ForgedComponent comp, ForgedBloodEyesActionEvent args)
    {
        if (args.Handled) return;

        if (HasComp<WerewolfBloodFeelComponent>(uid)) RemComp<WerewolfBloodFeelComponent>(uid);
        else EnsureComp<WerewolfBloodFeelComponent>(uid);

        _actions.SetToggled(args.Action.Owner, !args.Toggle);
        args.Handled = true;
    }

    private void CheckBoostReq(EntityUid forgedUid)
    {
        if (!TryComp<ForgedComponent>(forgedUid, out var forged)) return;

        foreach (var mod in forged.FittedModules)
        {
            if (TryComp<ForgedModuleComponent>(mod.Value, out var module) && module.AbilityId == "BoostReq")
            {
                _actions.AddAction(forgedUid, "BoostAction");
                return;
            }
        }
    }

    private void OnBoost(EntityUid forgedUid, ForgedComponent comp, ForgedBoostActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<ActionComponent>(args.Action, out var actionComponent)) return;
        if (!TryComp<ForgedComponent>(forgedUid, out var forged)) return;

        foreach (var mod in forged.FittedModules)
        {
            if (TryComp<ForgedModuleComponent>(mod.Value, out var module) && module.AbilityId == "Boost")
            {
                bool isToggled = actionComponent.Toggled;

                if (!isToggled)
                {
                    module.SpeedModifier += 0.50f;
                    module.ResistanceModifier -= 1.25f;
                }
                else
                {
                    module.SpeedModifier -= 0.50f;
                    module.ResistanceModifier += 1.25f;
                }

                _movementSpeedModifier.RefreshMovementSpeedModifiers(forgedUid);
                _actions.SetToggled(args.Action.Owner, !isToggled);
                Dirty(forgedUid, forged);
            }
        }
        args.Handled = true;
    }

    private void CheckSilaReq(EntityUid forgedUid)
    {
        if (!TryComp<ForgedComponent>(forgedUid, out var forged)) return;

        foreach (var mod in forged.FittedModules)
        {
            if (TryComp<ForgedModuleComponent>(mod.Value, out var module) && module.AbilityId == "SilaReq")
            {
                _actions.AddAction(forgedUid, "SilaAction");
                return;
            }
        }
    }

    private void OnSila(EntityUid forgedUid, ForgedComponent comp, ForgedSilaActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<ActionComponent>(args.Action, out var actionComponent)) return;
        if (!TryComp<ForgedComponent>(forgedUid, out var forged)) return;
        if (!TryComp<SkillsComponent>(forgedUid, out var skills)) return;

        foreach (var mod in forged.FittedModules)
        {
            if (TryComp<ForgedModuleComponent>(mod.Value, out var module) && module.AbilityId == "Boost")
            {
                bool isToggled = actionComponent.Toggled;

                if (!isToggled)
                {
                    skills.Levels["Strength"] = 20;
                    module.SpeedModifier -= 0.25f;
                }
                else
                {
                    skills.Levels["Strength"] = 10;
                    module.SpeedModifier += 0.25f;
                }

                _movementSpeedModifier.RefreshMovementSpeedModifiers(forgedUid);
                _actions.SetToggled(args.Action.Owner, !isToggled);
            }
        }
        Dirty(forgedUid, skills);
        Dirty(forgedUid, forged);
        args.Handled = true;
    }

    private void OnRepair(EntityUid forgedUid, ForgedComponent comp, ForgedRepairActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<DamageableComponent>(forgedUid, out var damageable)) return;

        FixedPoint2 healLeft = 40;

        var healSpecifier = new DamageSpecifier();

        foreach (var (damageType, amount) in damageable.Damage.DamageDict)
        {
            if (healLeft <= 0)
                break;

            if (amount <= 0)
                continue;

            var healAmount = FixedPoint2.Min(amount, healLeft);

            healSpecifier.DamageDict[damageType] = -healAmount;

            healLeft -= healAmount;
        }

        if (healSpecifier.DamageDict.Count > 0)
        {
            _damageable.TryChangeDamage(forgedUid, healSpecifier, ignoreResistances: true);
            _stun.TryKnockdown(forgedUid, TimeSpan.FromSeconds(10), true);
            _stun.TryAddParalyzeDuration(forgedUid, TimeSpan.FromSeconds(10));
        }
        args.Handled = true;
    }

    private void VerySmart(EntityUid forgedUid)
    {
        if (!TryComp<SkillsComponent>(forgedUid, out var skills)) return;

        skills.Levels["Intelligence"] = 20;

        Dirty(forgedUid, skills);
    }
}

