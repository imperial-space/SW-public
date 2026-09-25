using System.Text;
using Content.Shared.Atmos.Components;
using Content.Shared.Body.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Examine;
using Content.Shared.Imperial.Medieval.ArmorIntegrity;
using Content.Shared.Imperial.Medieval.Magic.Mana;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Stunnable;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.Skills.Progression;

public sealed partial class SkillPerkSystem
{
    [Dependency] private readonly ExamineSystemShared _examine = default!;

    private void InitializeExamination() =>
        SubscribeLocalEvent<MobStateComponent, GetVerbsEvent<ExamineVerb>>(OnExamineVerbs);

    private bool CanInspect(EntityUid user, EntityUid target) =>
        HasLevel(user, SharedSkillsSystem.IntelligenceId, SkillScaling.Expert) && _examine.CanExamine(user, target)
        && (HasLevel(user, SharedSkillsSystem.IntelligenceId, SkillScaling.Master) || _examine.IsInDetailsRange(user, target));

    private void OnExamineVerbs(EntityUid uid, MobStateComponent mob, GetVerbsEvent<ExamineVerb> args)
    {
        if (!CanInspect(args.User, uid))
            return;
        args.Verbs.Add(new ExamineVerb
        {
            Text = Loc.GetString("skills-detailed-examine"),
            Category = VerbCategory.Examine,
            Act = () =>
            {
                if (!CanInspect(args.User, uid))
                    return;
                _examine.SendExamineTooltip(args.User, uid, FormattedMessage.FromUnformatted(DescribeCharacter(uid)), false, false);
            }
        });
    }

    private string DescribeCharacter(EntityUid uid)
    {
        var text = new StringBuilder();
        if (TryComp<MobStateComponent>(uid, out var state))
        {
            var currentState = state.CurrentState;
            text.AppendLine(Loc.GetString("skills-examine-state", ("state", Loc.GetString($"skills-state-{currentState.ToString().ToLowerInvariant()}"))));
        }
        if (TryComp<DamageableComponent>(uid, out var damage))
        {
            text.AppendLine(Loc.GetString("skills-examine-damage", ("damage", damage.TotalDamage)));
            foreach (var (type, amount) in damage.Damage.DamageDict)
            {
                if (amount > 0)
                    text.AppendLine($"  {type}: {amount}");
            }
            if (TryComp<MobThresholdsComponent>(uid, out var thresholds))
            {
                foreach (var (threshold, mobState) in thresholds.Thresholds)
                {
                    if (threshold > 0)
                        text.AppendLine(Loc.GetString("skills-examine-threshold", ("state", Loc.GetString($"skills-state-{mobState.ToString().ToLowerInvariant()}")),
                            ("threshold", threshold), ("remaining", Math.Max(0, (threshold - damage.TotalDamage).Float()))));
                }
            }
        }
        if (TryComp<StaminaComponent>(uid, out var stamina))
            text.AppendLine(Loc.GetString("skills-examine-stamina", ("current", MathF.Round(Math.Max(0, stamina.CritThreshold - stamina.StaminaDamage), 1)),
                ("max", MathF.Round(stamina.CritThreshold, 1))));
        if (TryComp<ManaComponent>(uid, out var mana))
            text.AppendLine(Loc.GetString("skills-examine-mana", ("current", MathF.Round(mana.Mana, 1)), ("max", MathF.Round(mana.MaxMana, 1))));
        if (TryComp<SkillsComponent>(uid, out var skills))
        {
            foreach (var (id, level) in skills.Levels)
                text.AppendLine($"{Loc.GetString($"skill-{id.ToLowerInvariant()}-name")}: {level}");
        }
        if (TryComp<BloodstreamComponent>(uid, out var blood))
        {
            text.AppendLine(Loc.GetString("skills-examine-bleeding", ("rate", MathF.Round(blood.BleedAmount, 2))));
            if (blood.BloodSolution is { } solution)
                text.AppendLine(Loc.GetString("skills-examine-blood", ("current", solution.Comp.Solution.Volume), ("max", blood.BloodMaxVolume)));
        }
        if (HasComp<StunnedComponent>(uid))
            text.AppendLine(Loc.GetString("skills-examine-stunned"));
        if (TryComp<FlammableComponent>(uid, out var fire) && fire.OnFire)
            text.AppendLine(Loc.GetString("skills-examine-burning"));
        if (HasComp<KnockedDownComponent>(uid))
            text.AppendLine(Loc.GetString("skills-examine-knocked-down"));
        if (TryComp<MovementSpeedModifierComponent>(uid, out var movement))
            text.AppendLine(Loc.GetString("skills-examine-movement", ("walk", MathF.Round(movement.WalkSpeedModifier, 2)), ("sprint", MathF.Round(movement.SprintSpeedModifier, 2))));

        foreach (var slot in new[] { "outerClothing", "innerClothing", "head", "gloves", "shoes", "mask" })
        {
            if (!_inventory.TryGetSlotEntity(uid, slot, out var item) || !TryComp<MedievalArmorIntegrityComponent>(item, out var armor))
                continue;
            text.AppendLine(Loc.GetString("skills-examine-armor", ("name", Name(item.Value)),
                ("current", MathF.Round(armor.CurrentArmorHP, 1)), ("max", MathF.Round(armor.MaxArmorHP, 1))));
            foreach (var (type, resistance) in armor.IsBroken ? armor.BrokenResistances : armor.UnbrokenResistances)
            {
                if (resistance.Coefficient != 1f || resistance.FlatReduction != 0f)
                    text.AppendLine($"  {type}: {MathF.Round((1f - resistance.Coefficient) * 100f)}%, -{resistance.FlatReduction:0.#}");
            }
        }
        return text.ToString();
    }
}
