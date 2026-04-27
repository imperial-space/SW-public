using Content.Server.Imperial.Medieval.ObeliskDestroyable;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Medieval.ObeliskDestroyable;
using Content.Shared.Imperial.Medieval.ObeliskFixer;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Interaction;

namespace Content.Server.Imperial.Medieval.ObeliskFixer;

public sealed class ObeliskFixerSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly ObeliskDestroyableSystem _obelisks = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ObeliskFixerComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<ObeliskFixerComponent, ObeliskFixerDoAfterEvent>(OnDoAfter);
    }

    private void OnAfterInteract(EntityUid uid, ObeliskFixerComponent component, AfterInteractEvent args)
    {
        if (args.Handled ||
            !args.CanReach ||
            args.Target is not { } target ||
            !TryComp<ObeliskDestroyableComponent>(target, out var obelisk) ||
            !TryComp<DamageableComponent>(target, out var damageable))
        {
            return;
        }

        if (damageable.TotalDamage <= FixedPoint2.Zero &&
            obelisk.CurrentPhase == 0)
        {
            return;
        }

        var doAfterArgs = new DoAfterArgs(EntityManager,
            args.User,
            GetDoAfterDuration(args.User, component),
            new ObeliskFixerDoAfterEvent(),
            uid,
            target: target,
            used: uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BreakOnDropItem = true,
            CancelDuplicate = true
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
            return;

        args.Handled = true;
    }

    private void OnDoAfter(EntityUid uid, ObeliskFixerComponent component, ref ObeliskFixerDoAfterEvent args)
    {
        if (args.Handled ||
            args.Cancelled ||
            args.Target is not { } target ||
            !TryComp<ObeliskDestroyableComponent>(target, out var obelisk) ||
            !TryComp<DamageableComponent>(target, out var damageable))
        {
            return;
        }

        if (damageable.TotalDamage <= FixedPoint2.Zero &&
            obelisk.CurrentPhase == 0)
        {
            return;
        }

        _obelisks.ResetObelisk(target, obelisk, damageable);
        QueueDel(uid);
        args.Handled = true;
    }

    private float GetDoAfterDuration(EntityUid user, ObeliskFixerComponent component)
    {
        var intelligence = component.BaselineIntelligence;
        if (TryComp<SkillsComponent>(user, out var skills))
        {
            intelligence = skills.Levels.GetValueOrDefault(SharedSkillsSystem.IntelligenceId, component.BaselineIntelligence);
        }

        var duration = component.BaseDoAfterDuration -
            (intelligence - component.BaselineIntelligence) * component.IntelligenceDurationModifier;

        return MathF.Max(component.MinimumDoAfterDuration, duration);
    }
}
