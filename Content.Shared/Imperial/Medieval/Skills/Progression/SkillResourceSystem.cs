using Content.Shared.Damage.Components;
using Content.Shared.Imperial.Medieval.Magic.Mana;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Skills;

/// <summary>Recalculates resource capacity without restoring spent resources.</summary>
public sealed class SkillResourceSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SkillProfileChangedEvent>(OnChanged);
        SubscribeLocalEvent<ManaComponent, ManaInitializedEvent>(OnManaStartup);
    }

    private void OnChanged(ref SkillProfileChangedEvent args)
    {
        var uid = args.Uid;
        if (_net.IsClient || !TryComp<SkillsComponent>(uid, out var skills))
            return;
        if (TryComp<ManaComponent>(uid, out var mana))
            UpdateMana(uid, skills, mana);
        if (!TryComp<StaminaComponent>(uid, out var stamina))
            return;
        var state = EnsureComp<SkillProgressionComponent>(uid);
        state.BaseStamina ??= stamina.CritThreshold;
        var proto = _prototypes.Index<SkillPrototype>(SharedSkillsSystem.EnduranceId);
        stamina.CritThreshold = state.BaseStamina.Value * SkillScaling.Multiplier(
            SkillScaling.Level(skills, SharedSkillsSystem.EnduranceId), proto.Modifiers["StaminaMaxPerLevel"]);
        Dirty(uid, stamina);
    }

    private void OnManaStartup(EntityUid uid, ManaComponent mana, ref ManaInitializedEvent args)
    {
        if (_net.IsServer && TryComp<SkillsComponent>(uid, out var skills))
            UpdateMana(uid, skills, mana);
    }

    private void UpdateMana(EntityUid uid, SkillsComponent skills, ManaComponent mana)
    {
        var proto = _prototypes.Index<SkillPrototype>(SharedSkillsSystem.IntelligenceId);
        var level = SkillScaling.Level(skills, SharedSkillsSystem.IntelligenceId);
        var maximum = SkillScaling.Multiplier(level, proto.Modifiers["ManaPerLevel"]);
        var regeneration = SkillScaling.Multiplier(level, proto.Modifiers["ManaRegenPerLevel"]);
        mana.MaxMana = mana.MaxMana / mana.SkillMaximumMultiplier * maximum;
        mana.Regen = mana.Regen / mana.SkillRegenerationMultiplier * regeneration;
        mana.SkillMaximumMultiplier = maximum;
        mana.SkillRegenerationMultiplier = regeneration;
        mana.Mana = Math.Min(mana.Mana, mana.MaxMana);
        Dirty(uid, mana);
    }
}
