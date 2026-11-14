using System.Linq;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Mobs.Components;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class BossSystem
{
    private void InitializeCasting()
    {
        SubscribeLocalEvent<BossCastingComponent, ComponentShutdown>(OnCastShutdown);
    }

    public bool TryStartBossCast(EntityUid boss, BossAttack attack, IEnumerable<EntityUid> targets,
    bool requireActiveEncounter = true)
    {
        if (!HasComp<BossEncounterComponent>(boss) || !TryComp<BossComponent>(boss, out var component) || component.Finished || component.Health <= 0 ||
            LifeStage(boss) >= EntityLifeStage.Terminating || MetaData(boss).EntityPaused ||
            (requireActiveEncounter && !component.Active) || _actions.IsBusy(boss) ||
            !float.IsFinite(attack.CastTime) || attack.CastTime <= 0f || attack.CastTime > 3600f)
            return false;

        var pickedTargets = attack.PickTargets(boss, targets, EntityManager)
            .Where(target => target.IsValid() && Exists(target)).Distinct().ToList();
        if (pickedTargets.Count == 0)
            return false;

        var cast = AddComp<BossCastingComponent>(boss);
        cast.Attack = attack;
        cast.Targets = pickedTargets;                                           // keep these targets for this cast
        cast.RequireActiveEncounter = requireActiveEncounter;

        var bar = EnsureComp<BossCastBarComponent>(boss);
        bar.Casting = true;
        bar.Visible = true;
        bar.SetCastName(string.IsNullOrWhiteSpace(attack.CastName)
            ? "Boss_Encounter_Cast_Default_Name" : attack.CastName);
        bar.StartTime = _timing.CurTime;
        bar.EndTime = bar.StartTime + TimeSpan.FromSeconds(attack.CastTime);
        Dirty(boss, bar);
        if (attack.Telegraph != null && !EntityManager.System<BossTelegraphSystem>().Prepare(boss, attack, pickedTargets))
        {
            CancelBossCast(boss);
            return false;
        }
        if (!_actions.PrepareCast(boss, cast, bar))
        {
            CancelBossCast(boss);
            return false;
        }
        return true;
    }

    public bool CancelBossCast(EntityUid boss)
    {
        if (_actions.Cancel(boss)) return true;
        if (!TryComp<BossCastingComponent>(boss, out _))
            return false;

        RemComp<BossCastingComponent>(boss);
        SetCastRecovery(boss);
        return true;
    }

    private void OnCastShutdown(EntityUid uid, BossCastingComponent component, ComponentShutdown args)
    {
        if (component.TargetMarker is { } marker && Exists(marker))
            QueueDel(marker);
        _actions.StopCast(uid, component);
        if (!component.Resolving)                                                // keep effects while the hit is resolving
            EntityManager.System<BossTelegraphSystem>().Clear(uid);
        if (LifeStage(uid) >= EntityLifeStage.Terminating || !TryComp<BossCastBarComponent>(uid, out var bar))
            return;

        bar.Casting = false;
        bar.Visible = true;
        bar.ClearCastName();
        bar.StartTime = TimeSpan.Zero;
        bar.EndTime = TimeSpan.Zero;
        Dirty(uid, bar);
    }

    internal void FinishBossAction(EntityUid boss, BossAttack attack, bool completed)
    {
        if (completed)
            attack.NextAttack = _timing.CurTime + TimeSpan.FromSeconds(attack.Cooldown);
        SetCastRecovery(boss);
        EntityManager.System<BossEncounterSystem>().StepFinished(boss, completed);
    }

    private void SetCastRecovery(EntityUid boss)
    {
        if (TryComp<BossComponent>(boss, out var component) &&
            component.Stages.TryGetValue(component.Stage, out var stage))
            component.NextAttack = _timing.CurTime + TimeSpan.FromSeconds(stage.StageDelay);
    }

    private void UpdateBossCasts()
    {
        var query = EntityQueryEnumerator<BossCastingComponent>();
        while (query.MoveNext(out var uid, out var cast))
        {
            if (!TryComp<BossComponent>(uid, out var boss) || boss.Finished || boss.Health <= 0 ||
                (cast.RequireActiveEncounter && !boss.Active) ||
                !TryComp<BossCastBarComponent>(uid, out var bar) || !bar.Casting)
            {
                CancelBossCast(uid);
                continue;
            }

            if (cast.RequireActiveEncounter && !boss.Players.Any(player => Exists(player) && _mobState.IsAlive(player)))
            {
                BossWon(uid, boss);
                continue;
            }

            if (_timing.CurTime < bar.EndTime)                                   // wait until the cast finishes before hitting
                continue;

            var map = Transform(uid).MapID;
            var targets = cast.Targets.Where(target => Exists(target) &&
                LifeStage(target) < EntityLifeStage.Terminating && Transform(target).MapID == map &&
                (!HasComp<MobStateComponent>(target) || _mobState.IsAlive(target))).ToList();
            var attack = cast.Attack;

            cast.Resolving = true;                                              // shutdown here is part of the hit, don't cancel its effects
            CancelBossCast(uid);
            attack.NextAttack = _timing.CurTime + TimeSpan.FromSeconds(attack.Cooldown);
            try
            {
                if (targets.Count > 0 && !cast.RequireActiveEncounter && attack.Actions.Count == 0)
                    EntityManager.System<BossTelegraphSystem>().ShowCleavePreviewImpact(uid);
                if (targets.Count > 0)
                    foreach (var action in attack.Actions)
                        action.Execute(uid, targets, EntityManager);
            }
            finally
            {
                EntityManager.System<BossTelegraphSystem>().Clear(uid);
                _actions.FinishCast(uid, cast, targets.Count > 0);
            }
        }
    }
}
