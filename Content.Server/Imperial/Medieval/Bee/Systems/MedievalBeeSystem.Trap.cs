using Content.Shared.Humanoid;
using Content.Shared.Imperial.Medieval.Bee.Components;
using Content.Shared.Interaction;
using Content.Shared.Stunnable;
using Robust.Shared.Physics.Events;

namespace Content.Server.Imperial.Medieval.Bee.Systems;

public sealed partial class MedievalBeeSystem : EntitySystem
{
    private void InitializeTrap()
    {
        SubscribeLocalEvent<MedievalBeeTrapComponent, StartCollideEvent>(TrapCollide);
        SubscribeLocalEvent<MedievalBeeTrappedComponent, InteractHandEvent>(TrappedInteract);
    }
    private void TrapCollide(EntityUid uid, MedievalBeeTrapComponent component, StartCollideEvent args)
    {
        if (component.CooldownEnd.HasValue && component.CooldownEnd > _timing.CurTime)
            return;

        if (HasComp<MedievalBeeTrappedComponent>(args.OtherEntity))
            return;

        if (!HasComp<HumanoidAppearanceComponent>(args.OtherEntity))
            return;

        if (!_stun.TryAddParalyzeDuration(args.OtherEntity, component.StunTime))
            return;

        var comp = AddComp<MedievalBeeTrappedComponent>(args.OtherEntity);
        comp.RemoveTime = _timing.CurTime + component.StunTime;
        component.CooldownEnd = _timing.CurTime + component.Cooldown;
    }
    private void TrappedInteract(EntityUid uid, MedievalBeeTrappedComponent component, InteractHandEvent args)
    {
        if (args.User == uid)
            return;

        if (!TryComp<KnockedDownComponent>(uid, out var stunned))
        {
            RemComp<MedievalBeeTrappedComponent>(uid);
            return;
        }

        _status.TryRemoveStatusEffect(uid, SharedStunSystem.StunId);
        RemComp<StunnedComponent>(uid);
        _stun.SetKnockdownTime((uid, stunned), TimeSpan.Zero);

        RemComp<MedievalBeeTrappedComponent>(uid);
    }
    private void UpdateTrap(float frameTime)
    {
        var trappedQuery = EntityQueryEnumerator<MedievalBeeTrappedComponent>();
        while (trappedQuery.MoveNext(out var uid, out var comp))
        {
            if (comp.RemoveTime > _timing.CurTime)
                continue;

            RemComp<MedievalBeeTrappedComponent>(uid);
        }
    }
}
