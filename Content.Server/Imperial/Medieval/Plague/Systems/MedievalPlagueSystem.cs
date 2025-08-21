using System.Linq;
using Content.Server.Actions;
using Content.Server.BadSmell.Components;
using Content.Server.Body.Systems;
using Content.Server.Buckle.Systems;
using Content.Server.Fluids.EntitySystems;
using Content.Server.Imperial.Medieval.Skills;
using Content.Server.Jittering;
using Content.Server.Medical;
using Content.Server.Popups;
using Content.Server.Stunnable;
using Content.Shared.ActionBlocker;
using Content.Shared.Alert;
using Content.Shared.Bed.Sleep;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Imperial.Medieval.Plague;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.StatusEffect;
using Content.Shared.Tag;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Toolshed.Commands.Values;

namespace Content.Server.Imperial.Medieval.Plague;

public sealed partial class MedievalPlagueSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly VomitSystem _vomit = default!;
    [Dependency] private readonly PuddleSystem _puddle = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly StunSystem _stun = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly BlindableSystem _blind = default!;
    [Dependency] private readonly JitteringSystem _jitter = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly BloodstreamSystem _blood = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _moveSpeed = default!;

    private Dictionary<ProtoId<MedievalPlagueSymptomPrototype>, MedievalPlagueSymptomData> _symptoms = new();
    private int _strapHealResistance = 0;
    private int _healItemResistance = 0;

    public override void Initialize()
    {
        base.Initialize();

        InitializeGhost();
        InitializeSpread();
        InitializeUi();
        InitializeSymptoms();
    }

    public bool TryInfect(EntityUid uid, EntityUid? plagueSource, float additionalMod = 1f)
    {
        if (!HasComp<MedievalCanBeInfectedComponent>(uid) || HasComp<MedievalPlagueInfectedComponent>(uid))
            return false;

        if (HasComp<MedievalPlagueImmuneComponent>(uid))
            return false;

        var ev = new MedievalPlagueInfectionAttemptEvent();
        RaiseLocalEvent(uid, ref ev);

        if (!_random.Prob(ev.Probability * additionalMod))
            return false;

        Infect(uid, plagueSource);
        return true;
    }

    public void Infect(EntityUid uid, EntityUid? plagueSource)
    {
        var comp = EnsureComp<MedievalPlagueInfectedComponent>(uid);
        comp.PlagueSource = plagueSource;

        foreach (var item in _symptoms.Where(x => x.Value.Unlocked))
        {
            RaisePrototypeEvent(uid, item.Key);
        }
    }

    private void DoPrototypeEffects(ProtoId<MedievalPlagueSymptomPrototype> protoId)
    {
        var infected = EntityManager.AllEntities<MedievalPlagueInfectedComponent>();
        var ghosts = EntityManager.AllEntities<MedievalPlagueGhostComponent>();

        foreach (var target in infected)
        {
            RaisePrototypeEvent(target, protoId);
        }

        foreach (var ghost in ghosts)
        {
            AddPrototypeActions(ghost, protoId);
        }
    }

    private void RaisePrototypeEvent(EntityUid uid, ProtoId<MedievalPlagueSymptomPrototype> protoId)
    {
        var proto = _proto.Index(protoId);

        if (proto.TargetEvent != null)
            RaiseLocalEvent(uid, proto.TargetEvent);
    }

    private void AddPrototypeActions(EntityUid uid, ProtoId<MedievalPlagueSymptomPrototype> protoId, MedievalPlagueGhostComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return;

        var proto = _proto.Index(protoId);
        if (proto.Actions.Count() <= 0)
            return;

        foreach (var act in proto.Actions)
        {
            var actionEnt = _actions.AddAction(uid, act);
            if (actionEnt.HasValue)
                comp.Actions.Add(actionEnt.Value);
        }
    }

}
