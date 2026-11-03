using System.Threading.Tasks;
using Content.Shared.Ghost;
using Content.Shared.IdentityManagement;
using Content.Shared.Imperial.Medieval.RPPanel;
using Content.Shared.Jittering;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Popups;
using Content.Shared.StatusEffect;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.RPPanel;

public sealed class RPPanelSystem : EntitySystem
{
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobStateComponent, ComponentStartup>(OnMobStartup);
        SubscribeLocalEvent<MobMoverComponent, ComponentStartup>(OnMobMoverStartup);
        SubscribeLocalEvent<GhostComponent, ComponentInit>(OnGhostInit);
        SubscribeLocalEvent<RPPanelMemberComponent, RPPanelActionEvent>(OnAction);
    }

    private void OnMobStartup(EntityUid uid, MobStateComponent component, ComponentStartup args)
    {
        if (!HasComp<GhostComponent>(uid))
            EnsureComp<RPPanelMemberComponent>(uid);
    }

    private void OnGhostInit(EntityUid uid, GhostComponent component, ComponentInit args)
    {
        RemComp<RPPanelMemberComponent>(uid);
    }

    private void OnMobMoverStartup(EntityUid uid, MobMoverComponent component, ComponentStartup args)
    {
        if (!HasComp<GhostComponent>(uid))
            EnsureComp<RPPanelMemberComponent>(uid);
    }

    private void OnAction(EntityUid target, RPPanelMemberComponent component, ref RPPanelActionEvent args)
    {
        var action = args.Action switch
        {
            RPPanelAction.Look => "look",
            RPPanelAction.Wave => "wave",
            RPPanelAction.Shake => "shake",
            _ => null
        };
        if (action == null)
            return;

        var user = args.User;
        _popup.PopupEntity(Loc.GetString($"rp-panel-{action}-self",
            ("target", Identity.Name(target, EntityManager, user))), target, user);

        if (HasComp<ActorComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString($"rp-panel-{action}-target",
                ("user", Identity.Name(user, EntityManager, target))), user, target);
        }

        if (args.Action == RPPanelAction.Look)
            return;

        foreach (var session in Filter.Pvs(user, entityManager: EntityManager).Recipients)
        {
            if (session.AttachedEntity is not { } viewer || viewer == user || viewer == target)
                continue;

            _popup.PopupEntity(Loc.GetString($"rp-panel-{action}-others",
                ("user", Identity.Name(user, EntityManager, viewer)),
                ("target", Identity.Name(target, EntityManager, viewer))), user, session);
        }

        if (args.Action != RPPanelAction.Shake)
            return;

        if (HasComp<StatusEffectsComponent>(target))
        {
            _jitter.DoJitter(target, component.ShakeDuration, true);
            return;
        }

        if (HasComp<JitteringComponent>(target) && component.ShakeSequence == 0)
            return;

        _jitter.AddJitter(target);
        var sequence = ++component.ShakeSequence;
        _ = EndShakeAsync(target, component, Comp<JitteringComponent>(target), sequence);
    }

    private async Task EndShakeAsync(EntityUid target, RPPanelMemberComponent component, JitteringComponent jitter, int sequence)
    {
        await Timer.Delay(component.ShakeDuration);

        if (TerminatingOrDeleted(target)
            || !TryComp<RPPanelMemberComponent>(target, out var current)
            || current != component
            || current.ShakeSequence != sequence)
            return;

        current.ShakeSequence = 0;
        if (TryComp<JitteringComponent>(target, out var currentJitter)
            && currentJitter == jitter
            && !HasComp<StatusEffectsComponent>(target))
            RemComp<JitteringComponent>(target);
    }
}
