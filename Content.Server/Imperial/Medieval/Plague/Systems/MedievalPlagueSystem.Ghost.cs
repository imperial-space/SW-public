using System.Linq;
using Content.Server.Actions;
using Content.Server.Popups;
using Content.Shared.Imperial.Medieval.Plague;
using Content.Shared.Movement.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.Plague;

public sealed partial class MedievalPlagueSystem
{
    private void InitializeGhost()
    {
        SubscribeLocalEvent<MedievalPlagueGhostComponent, ComponentInit>(OnGhostInit);
        SubscribeLocalEvent<MedievalPlagueGhostComponent, InfectTargetActionEvent>(OnInfectAction);
    }

    private void OnGhostInit(EntityUid uid, MedievalPlagueGhostComponent comp, ComponentInit args)
    {
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
        }

        var success = TryInfect(args.Target, uid);
        _popup.PopupEntity(Loc.GetString("medieval-plague-infected-popup", ("success", success)), args.Target, uid);
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
}
