using System.Linq;
using Content.Server.Body.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Plague;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.Plague;

public sealed partial class MedievalPlagueSystem
{
    private void InitializeGhost()
    {
        SubscribeLocalEvent<MedievalPlagueGhostComponent, ComponentInit>(OnGhostInit);
        SubscribeLocalEvent<MedievalPlagueGhostComponent, InfectTargetActionEvent>(OnInfectAction);
        SubscribeLocalEvent<MedievalPlagueGhostComponent, PlagueForcedVomitActionEvent>(OnVomitAction);
        SubscribeLocalEvent<MedievalPlagueGhostComponent, PlagueAsthmaticActionEvent>(OnAsthmaAction);
        SubscribeLocalEvent<MedievalPlagueGhostComponent, PlagueDizzinessActionEvent>(OnDizzinessAction);
        SubscribeLocalEvent<MedievalPlagueGhostComponent, PlagueSleepyActionEvent>(OnSleepAction);
        SubscribeLocalEvent<MedievalPlagueGhostComponent, PlaguePolymorphMouseActionEvent>(OnMousuePolymorphAction);

        SubscribeLocalEvent<MedievalPlagueGhostComponent, PlagueTeleportInfectedActionEvent>(OnTeleportInfectedAction);
        SubscribeLocalEvent<MedievalPlagueGhostComponent, PlagueTeleportNotInfectedActionEvent>(OnTeleportNotInfectedAction);

        SubscribeLocalEvent<MedievalPlagueGhostComponent, PlagueSpawnEntityActionEvent>(OnSpawnEntityAction);

        SubscribeLocalEvent<MedievalPlagueGhostComponent, PlagueInjuryActionEvent>(OnInjuryAction);
        SubscribeLocalEvent<MedievalPlagueGhostComponent, PlagueCataractActionEvent>(OnCataractAction);
        SubscribeLocalEvent<MedievalPlagueGhostComponent, PlagueHeartAttackActionEvent>(OnHeartAttackAction);
    }

    private void OnGhostInit(EntityUid uid, MedievalPlagueGhostComponent comp, ComponentInit args)
    {
        _actions.AddAction(uid, "OpenPlagueMenuAction");
        _actions.AddAction(uid, ref comp.InfectAction, "PlagueInfectAction");
        UpdateInfectAction();
        _alerts.ShowAlert(uid, comp.AlertId);

        foreach (var item in _symptoms.Where(x => x.Value.Unlocked))
        {
            AddPrototypeActions(uid, item.Key);
        }
    }

    private void OnInfectAction(EntityUid uid, MedievalPlagueGhostComponent comp, InfectTargetActionEvent args)
    {
        if (args.Handled)
            return;

        var cost = GetInfectionCost();

        if (comp.FreeInfections > 0)
            comp.FreeInfections--;
        else if (comp.Points < cost)
        {
            _popup.PopupEntity(Loc.GetString("medieval-plague-not-enough-points-popup"), args.Target, uid);
            return;
        }
        else
        {
            comp.Points -= cost;
            Dirty(uid, comp);
            _alerts.ShowAlert(uid, comp.AlertId);
            UpdateUi(uid);
        }

        if (TryInfect(args.Target, uid))
            _popup.PopupEntity(Loc.GetString("medieval-plague-infected-success-popup"), args.Target, uid);
        else
            _popup.PopupEntity(Loc.GetString("medieval-plague-infected-failure-popup"), args.Target, uid);
    }

    private void OnVomitAction(EntityUid uid, MedievalPlagueGhostComponent comp, PlagueForcedVomitActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryUseAbility(uid, args.Target, args.Cost, args.AllowIncubation))
            return;

        args.Handled = true;

        _vomit.Vomit(args.Target);

        if (_symptoms.Where(x => x.Value.Unlocked).ToDictionary().ContainsKey("BloodVomit") && TryComp<BloodstreamComponent>(uid, out var bloodstream))
        {
            Solution sol = new(bloodstream.BloodReagent, 20f);
            _puddle.TrySpillAt(Transform(uid).Coordinates, sol, out _, false);
            DamageSpecifier damage = new()
            {
                DamageDict = new()
                {
                    { "Bloodloss", 10 }
                }
            };

            _damageable.TryChangeDamage(args.Target, damage, true);
        }

        _popup.PopupEntity(Loc.GetString("medieval-plague-forced-vomit-popup"), args.Target, uid);
    }

    private void OnAsthmaAction(EntityUid uid, MedievalPlagueGhostComponent comp, PlagueAsthmaticActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryUseAbility(uid, args.Target, args.Cost, args.AllowIncubation))
            return;

        args.Handled = true;

        var block = EnsureComp<PlagueBlockBreathingComponent>(uid);
        block.EndTime = _timing.CurTime + TimeSpan.FromSeconds(25);
        _popup.PopupEntity(Loc.GetString("medieval-plague-asthma-ghost-popup"), args.Target, uid);
        _popup.PopupEntity(Loc.GetString("medieval-plague-asthma-target-popup"), args.Target, args.Target, Shared.Popups.PopupType.MediumCaution);
    }

    private void OnDizzinessAction(EntityUid uid, MedievalPlagueGhostComponent comp, PlagueDizzinessActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryUseAbility(uid, args.Target, args.Cost, args.AllowIncubation))
            return;

        args.Handled = true;

        var block = EnsureComp<PlagueDizzinessComponent>(uid);
        block.EndTime = _timing.CurTime + TimeSpan.FromSeconds(15);
        _moveSpeed.RefreshMovementSpeedModifiers(args.Target);
        _popup.PopupEntity(Loc.GetString("medieval-plague-dizziness-ghost-popup"), args.Target, uid);
        _popup.PopupEntity(Loc.GetString("medieval-plague-dizziness-target-popup"), args.Target, args.Target, Shared.Popups.PopupType.MediumCaution);
    }

    private void OnSleepAction(EntityUid uid, MedievalPlagueGhostComponent comp, PlagueSleepyActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryUseAbility(uid, args.Target, args.Cost, args.AllowIncubation))
            return;

        args.Handled = true;

        _status.TryAddStatusEffect<ForcedSleepingComponent>(args.Target, "ForcedSleep", TimeSpan.FromSeconds(15), true);
        _popup.PopupEntity(Loc.GetString("medieval-plague-forced-sleep-ghost-popup"), args.Target, uid);
        _popup.PopupEntity(Loc.GetString("medieval-plague-forced-sleep-target-popup"), args.Target, args.Target, Shared.Popups.PopupType.MediumCaution);
    }

    private void OnMousuePolymorphAction(EntityUid uid, MedievalPlagueGhostComponent comp, PlaguePolymorphMouseActionEvent args)
    {
        if (args.Handled)
            return;

        if (comp.Points < args.Cost)
        {
            _popup.PopupEntity(Loc.GetString("popup-plague-action-fail-cost"), uid, uid);
            return;
        }

        comp.Points -= args.Cost;
        Dirty(uid, comp);
        _alerts.ShowAlert(uid, comp.AlertId);
        UpdateUi(uid);

        args.Handled = true;

        if (_symptoms.Where(x => x.Value.Unlocked).ToDictionary().ContainsKey("MorphRatsAction"))
        {
            var xform = Transform(uid);

            for (var i = 0; i < args.SpawnedCount; i++)
                Spawn("MedievalMobPlagueMouse", xform.Coordinates);
        }

        _polymorph.PolymorphEntity(uid, "MedievalPlagueMousePolymorph");
    }

    private void OnTeleportInfectedAction(EntityUid uid, MedievalPlagueGhostComponent comp, PlagueTeleportInfectedActionEvent args)
    {
        if (args.Handled)
            return;

        var targets = EntityManager.AllEntities<MedievalPlagueInfectedComponent>();
        if (!targets.Any())
        {
            _popup.PopupEntity(Loc.GetString("medieval-plague-teleport-infected-none-popup"), uid, uid);
            return;
        }

        args.Handled = true;
        _transform.SetCoordinates(uid, Transform(_random.Pick(targets.ToList())).Coordinates);
    }

    private void OnTeleportNotInfectedAction(EntityUid uid, MedievalPlagueGhostComponent comp, PlagueTeleportNotInfectedActionEvent args)
    {
        if (args.Handled)
            return;

        var targets = EntityManager.AllEntities<MedievalCanBeInfectedComponent>().Where(x => !HasComp<MedievalPlagueInfectedComponent>(x.Owner));
        if (!targets.Any())
        {
            _popup.PopupEntity(Loc.GetString("medieval-plague-teleport-not-infected-none-popup"), uid, uid);
            return;
        }

        args.Handled = true;
        _transform.SetCoordinates(uid, Transform(_random.Pick(targets.ToList())).Coordinates);
    }

    private void OnSpawnEntityAction(EntityUid uid, MedievalPlagueGhostComponent comp, PlagueSpawnEntityActionEvent args)
    {
        if (args.Handled)
            return;

        if (comp.Points < args.Cost)
        {
            _popup.PopupCoordinates(Loc.GetString("popup-plague-action-fail-cost"), args.Target, uid);
            return;
        }

        comp.Points -= args.Cost;
        Dirty(uid, comp);
        _alerts.ShowAlert(uid, comp.AlertId);
        UpdateUi(uid);

        args.Handled = true;
        Spawn(args.Prototype, args.Target);
    }

    private void OnInjuryAction(EntityUid uid, MedievalPlagueGhostComponent comp, PlagueInjuryActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryUseAbility(uid, args.Target, args.Cost, args.AllowIncubation))
            return;

        args.Handled = true;

        _blood.TryModifyBleedAmount(args.Target, 7.5f);
    }

    private void OnCataractAction(EntityUid uid, MedievalPlagueGhostComponent comp, PlagueCataractActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryUseAbility(uid, args.Target, args.Cost, args.AllowIncubation))
            return;

        args.Handled = true;

        _blind.SetMinDamage(args.Target, 3);
    }

    private void OnHeartAttackAction(EntityUid uid, MedievalPlagueGhostComponent comp, PlagueHeartAttackActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryUseAbility(uid, args.Target, args.Cost, args.AllowIncubation))
            return;

        args.Handled = true;

        _jitter.DoJitter(args.Target, TimeSpan.FromSeconds(4), true, frequency: 6);
        _mobState.ChangeMobState(args.Target, Shared.Mobs.MobState.Dead);
    }

    private bool TryUseAbility(EntityUid uid, EntityUid target, int cost = 0, bool allowIncubation = false)
    {
        if (!TryComp<MedievalPlagueGhostComponent>(uid, out var comp) || !TryComp<MedievalPlagueInfectedComponent>(target, out var infected))
        {
            _popup.PopupEntity(Loc.GetString("popup-plague-action-fail-not-infected"), target, uid);
            return false;
        }

        if (infected.Incubation && !allowIncubation)
        {
            _popup.PopupEntity(Loc.GetString("popup-plague-action-fail-incubation"), target, uid);
            return false;
        }

        if (comp.Points < cost)
        {
            _popup.PopupEntity(Loc.GetString("popup-plague-action-fail-cost"), target, uid);
            return false;
        }

        comp.Points -= cost;
        Dirty(uid, comp);
        _alerts.ShowAlert(uid, comp.AlertId);
        UpdateUi(uid);

        return true;
    }

    private int GetInfectionCost()
    {
        var prototypes = _proto.EnumeratePrototypes<MedievalPlagueSymptomPrototype>();
        var unlocked = _symptoms.Where(x => x.Value.Unlocked);

        var max = 1;
        for (var i = 1; i <= prototypes.Select(x => x.Tier).Max(); i++)
        {
            var protos = prototypes.Where(x => x.Tier == i);
            var unlockedProtos = unlocked.Where(x => _proto.Index(x.Key).Tier == i);

            if (unlockedProtos.Count() / protos.Count() >= 0.6f)
                max = i + 1;
        }

        return max * 2;
    }

    private void UpdateInfectAction()
    {
        var query = EntityQueryEnumerator<MedievalPlagueGhostComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.InfectAction.HasValue)
                continue;

            _meta.SetEntityDescription(comp.InfectAction.Value, Loc.GetString("plague-infect-action-desc", ("cost", GetInfectionCost())));
        }
    }
}
