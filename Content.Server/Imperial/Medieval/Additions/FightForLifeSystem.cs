using Content.Server.Popups;
using Content.Shared.Actions;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Skills;

public sealed class FightForLifeSystem : EntitySystem
{
    private const string FightForLifeActionId = "FightForLifeAction";
    private const string OmnizineReagentId = "Omnizine";
    private bool _initialized = false;

    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;

    public override void Initialize()
    {
        if (_initialized)
            return;

        base.Initialize();

        SubscribeLocalEvent<FightForLifeActionEvent>(OnFightForLifeAction);
        SubscribeLocalEvent<FightForLifeActionComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<FightForLifeActionComponent, ComponentShutdown>(OnComponentShutdown);

        _initialized = true;
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<FightForLifeActionComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.NextUseTime.HasValue && _timing.CurTime >= comp.NextUseTime.Value)
            {
                comp.NextUseTime = null;
                UpdateActionVisibility(uid, comp);
            }
        }
    }

    private void OnComponentStartup(EntityUid uid, FightForLifeActionComponent comp, ComponentStartup args)
    {
        if (comp.ActionEntity == null)
        {
            _ = _actions.AddAction(uid, ref comp.ActionEntity, FightForLifeActionId);
        }
    }

    private void OnComponentShutdown(EntityUid uid, FightForLifeActionComponent comp, ComponentShutdown args)
    {
        if (comp.ActionEntity.HasValue)
        {
            _actions.RemoveAction(uid, comp.ActionEntity.Value);
        }
    }

    private void OnMobStateChanged(EntityUid uid, MobStateComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Critical && args.OldMobState != MobState.Critical)
        {
            if (_skills.TryGetSkill(uid, "Vitality", out var level) && level >= 10)
            {
                var actionComp = EnsureComp<FightForLifeActionComponent>(uid);
                actionComp.NextUseTime = null;
                UpdateActionVisibility(uid, actionComp);
            }
        }
        else if (args.NewMobState != MobState.Critical && args.OldMobState == MobState.Critical)
        {
            RemComp<FightForLifeActionComponent>(uid);
        }
    }

    private void UpdateActionVisibility(EntityUid uid, FightForLifeActionComponent comp)
    {
        if (!comp.ActionEntity.HasValue)
            return;

        var isOnCooldown = comp.NextUseTime.HasValue && _timing.CurTime < comp.NextUseTime.Value;

        if (isOnCooldown)
        {
            var remainingTime = comp.NextUseTime!.Value - _timing.CurTime;
            _actions.SetCooldown(comp.ActionEntity.Value, remainingTime);
        }
        else
        {
            _actions.SetCooldown(comp.ActionEntity.Value, TimeSpan.Zero);
        }

        _actions.SetEnabled(comp.ActionEntity.Value, !isOnCooldown);
    }

    private void OnFightForLifeAction(FightForLifeActionEvent args)
    {
        if (args.Handled)
            return;

        var uid = args.Performer;

        if (!TryComp<FightForLifeActionComponent>(uid, out var actionComp))
            return;

        if (actionComp.NextUseTime.HasValue && _timing.CurTime < actionComp.NextUseTime.Value)
            return;

        if (!TryComp<MobStateComponent>(uid, out var mobState) ||
            !_mobState.IsCritical(uid, mobState))
            return;

        if (!_skills.TryGetSkill(uid, "Vitality", out var vitalityLevel) || vitalityLevel < 10)
            return;

        var omnizineAmount = 0.5f + 0.05f * (vitalityLevel - 9);
        var success = InjectOmnizine(uid, omnizineAmount);

        actionComp.NextUseTime = _timing.CurTime + TimeSpan.FromSeconds(30);
        UpdateActionVisibility(uid, actionComp);

        if (success)
        {
            _popup.PopupEntity(Loc.GetString("fight-for-life-try"), uid, uid);
        }

        args.Handled = true;
    }

    private bool InjectOmnizine(EntityUid uid, float amount)
    {
        var fixedAmount = FixedPoint2.New(amount);

        if (!TryComp<BloodstreamComponent>(uid, out var bloodstream))
            return false;

        if (!_solutionContainer.TryGetSolution(uid, bloodstream.ChemicalSolutionName, out var chemSolution, out _))
            return false;

        var omnizine = new Solution(OmnizineReagentId, fixedAmount);
        _solutionContainer.TryAddSolution(chemSolution.Value, omnizine);

        return true;
    }
}
