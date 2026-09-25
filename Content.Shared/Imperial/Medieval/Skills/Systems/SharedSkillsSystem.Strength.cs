using Content.Shared.Cuffs.Components;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Wieldable;

namespace Content.Shared.Imperial.Medieval.Skills;

public abstract partial class SharedSkillsSystem
{
    public const string StrengthId = "Strength";

    private void InitializeCombat()
    {
        SubscribeLocalEvent<SkillsComponent, GetMeleeDamageEvent>(OnGetDamage);
        SubscribeLocalEvent<SkillsComponent, ModifyUncuffDurationEvent>(OnModifyUncuffDuration);
        SubscribeLocalEvent<SkillsComponent, WieldAttemptEvent>(OnWieldAttempt);
    }

    private void OnGetDamage(EntityUid uid, SkillsComponent comp, ref GetMeleeDamageEvent args)
    {
        if (!args.RaisedOnUser)
            return;
        var (proto, level) = GetSkill(uid, StrengthId);
        args.Damage *= SkillScaling.Multiplier(level, proto.Modifiers[
            args.Weapon == args.User ? "FistDamagePerLevel" : "WeaponDamagePerLevel"]);
        if (level >= SkillScaling.Master && args.Damage.DamageDict.TryGetValue("Structural", out var structural))
            args.Damage.DamageDict["Structural"] = structural * proto.Modifiers["StructuralDamageMultiplier"];
    }

    private void OnModifyUncuffDuration(EntityUid uid, SkillsComponent comp, ref ModifyUncuffDurationEvent args)
    {
        if (args.User != uid || args.Target != uid)
            return;
        var (proto, level) = GetSkill(uid, StrengthId);
        args.Duration /= SkillScaling.Multiplier(level, proto.Modifiers["ForceSpeedPerLevel"]);
    }

    private void OnWieldAttempt(EntityUid uid, SkillsComponent comp, ref WieldAttemptEvent args)
    {
        if (args.User != uid || SkillScaling.Level(comp, StrengthId) >= SkillScaling.Basic)
            return;
        args.Cancel();
        _popup.PopupPredicted(Loc.GetString("skills-require-strength-4"), null, args.User, args.User, PopupType.Medium);
    }
}
