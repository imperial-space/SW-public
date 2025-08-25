using Content.Shared.Chemistry.Reagent;
using Content.Shared.Imperial.Medieval.Plague;
using Content.Shared.Movement.Components;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Plague;

public sealed partial class MedievalPlagueSystem
{
    private Dictionary<string, float> _spreaders = new();
    private float _contactSpreadMod = 0f;
    private float _blockersEfficiency = 1f;
    private float _minSmellLevel = 50f;

    public ProtoId<ReagentPrototype> CurrentCure = "MedievalPlagueCure4";


    private void InitializeSpread()
    {
        SubscribeLocalEvent<MedievalPlagueInfectedComponent, StartCollideEvent>(OnInfectedCollide);
        SubscribeLocalEvent<MedievalPlagueInfectOnHitComponent, ComponentInit>(OnSpreaderInit);
        SubscribeLocalEvent<MedievalPlagueInfectOnHitComponent, MeleeHitEvent>(OnSpreaderHit);
        SubscribeLocalEvent<MedievalPlagueSpreadBlockingComponent, MedievalPlagueInfectionAttemptEvent>(OnBlockerInfectionAttempt);

        SubscribeLocalEvent<MedievalPlagueInfectedComponent, PlagueHealingItemUsedEvent>(OnHealingItemUsed);

        SubscribeLocalEvent<SetContactSpreadModifierEvent>(OnSetContactSpreadMod);
        SubscribeLocalEvent<SetSpreaderChanceEvent>(OnSetSpreaderChance);
        SubscribeLocalEvent<SetPlagueBlockerModifierEvent>(OnSetBlockerMod);
        SubscribeLocalEvent<SetStrapHealResistanceEvent>(OnSetStrapResistance);
        SubscribeLocalEvent<SetPlagueCureEvent>(OnSetCure);
    }

    private void OnInfectedCollide(EntityUid uid, MedievalPlagueInfectedComponent comp, ref StartCollideEvent args)
    {
        if (!TryComp<MobCollisionComponent>(uid, out var collision) || args.OurFixtureId != collision.FixtureId)
            return;

        TryInfect(args.OtherEntity, _contactSpreadMod);
    }

    private void OnSpreaderInit(EntityUid uid, MedievalPlagueInfectOnHitComponent comp, ComponentInit args)
    {
        if (_spreaders.Keys.Contains(comp.Id))
            comp.Active = true;
    }

    private void OnSpreaderHit(EntityUid uid, MedievalPlagueInfectOnHitComponent comp, MeleeHitEvent args)
    {
        if (!comp.Active)
            return;

        var chance = comp.Chance * _spreaders.GetValueOrDefault(comp.Id, 1f);

        foreach (var item in args.HitEntities)
            TryInfect(item, chance);
    }

    private void OnBlockerInfectionAttempt(EntityUid uid, MedievalPlagueSpreadBlockingComponent comp, ref MedievalPlagueInfectionAttemptEvent args)
    {
        var mod = comp.Modifier + (1 - comp.Modifier) * (_blockersEfficiency - 1);

        args.Probability *= mod;
    }

    private void OnHealingItemUsed(EntityUid uid, MedievalPlagueInfectedComponent comp, ref PlagueHealingItemUsedEvent args)
    {
        if (args.PlagueHealingTier <= _healItemResistance)
            return;

        TryProgressInfection(uid, -args.PlagueDecay, comp);
    }

    private void OnSetContactSpreadMod(SetContactSpreadModifierEvent args)
        => _contactSpreadMod = args.Modifier;

    private void OnSetSpreaderChance(SetSpreaderChanceEvent args)
    {
        if (_spreaders.TryGetValue(args.Id, out _))
            _spreaders[args.Id] = args.Modifier;
        else
            _spreaders.Add(args.Id, args.Modifier);

        foreach (var item in EntityManager.AllEntities<MedievalPlagueInfectOnHitComponent>())
        {
            if (item.Comp.Id != args.Id)
                continue;

            item.Comp.Active = true;
        }
    }

    private void OnSetBlockerMod(SetPlagueBlockerModifierEvent args)
        => _blockersEfficiency = args.Modifier;

    private void OnSetStrapResistance(SetStrapHealResistanceEvent args)
    {
        _strapHealResistance = args.StrapResistance;
        _healItemResistance = args.HealResistance;
    }

    private void OnSetCure(SetPlagueCureEvent args)
    {
        CurrentCure = args.Reagent;
    }
}
