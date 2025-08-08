using System.Linq;
using Content.Server.Actions;
using Content.Server.Popups;
using Content.Shared.Imperial.Medieval.Plague;
using Content.Shared.Movement.Components;
using Robust.Server.Player;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.Plague;

public sealed partial class MedievalPlagueSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private Dictionary<ProtoId<MedievalPlagueSymptomPrototype>, MedievalPlagueSymptomData> _symptoms = new();

    public override void Initialize()
    {
        base.Initialize();

        InitializeGhost();
        InitializeSpread();
        InitializeUi();
    }

    public bool TryInfect(EntityUid uid, EntityUid? plagueSource)
    {
        if (!HasComp<MedievalCanBeInfectedComponent>(uid) || HasComp<MedievalPlagueInfectedComponent>(uid))
            return false;

        var ev = new MedievalPlagueInfectionAttemptEvent();
        RaiseLocalEvent(uid, ref ev);

        if (!_random.Prob(ev.Probability))
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
