// only Strength 16+ can use the lance one-handed.
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Wieldable.Components;

namespace Content.Shared.Imperial.Medieval.Lances;

public sealed class LanceMeleeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LanceMeleeComponent, AttemptMeleeEvent>(OnMeleeAttempt);
    }

    private void OnMeleeAttempt(EntityUid uid, LanceMeleeComponent lance, ref AttemptMeleeEvent args)
    {
        if (args.Cancelled)
            return;

        if (TryComp<WieldableComponent>(uid, out var wieldable) && wieldable.Wielded)
            return;

        if (TryComp<SkillsComponent>(args.User, out var skills) &&
            skills.Levels.TryGetValue(SharedSkillsSystem.StrengthId, out var strength) &&
            strength >= lance.MinimumOneHandStrength)
            return;

        args.Cancelled = true;
        args.Message = Loc.GetString("medieval-lance-requires-strength", ("strength", lance.MinimumOneHandStrength));
    }
}
