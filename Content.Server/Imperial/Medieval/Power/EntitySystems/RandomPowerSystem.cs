using System.Linq;
using Content.Server.Power.Components;
using Content.Server.NodeContainer;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.GameTicking.Events;
using Content.Shared.Imperial.Medieval.Power;
using Content.Shared.NodeContainer;
using Content.Shared.NodeContainer.NodeGroups;
using Content.Shared.Power;
using Robust.Shared.Random;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Power;

public sealed class RandomPowerSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly NodeGroupSystem _nodeGroupSystem = default!;
    [Dependency] private readonly NodeContainerSystem _nodeContainer = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    private Dictionary<string, NodeGroupID> _prototypeVoltages = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<RandomPowerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
    }

    private void OnRoundStarting(RoundStartingEvent args)
    {
        _prototypeVoltages.Clear();
    }

    private void OnMapInit(Entity<RandomPowerComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.AvailableVoltages.Count == 0)
            return;

        var meta = MetaData(ent.Owner);
        if (meta.EntityPrototype == null)
            return;

        var prototypeId = meta.EntityPrototype.ID;

        if (!_prototypeVoltages.TryGetValue(prototypeId, out var voltage))
        {
            voltage = _random.Pick(ent.Comp.AvailableVoltages.Keys.ToList());;
            _prototypeVoltages[prototypeId] = voltage;
        }

        if (!_nodeContainer.TryGetNode<Node>(ent.Owner, ent.Comp.Node, out var node))
            return;

        node.SetNodeGroupID(voltage);

        if (TryComp<PowerConsumerComponent>(ent, out var consumer))
            consumer.Voltage = (Voltage)voltage;

        if (TryComp<PowerSupplierComponent>(ent, out var supplier))
            supplier.Voltage = (Voltage)voltage;

        var color = ent.Comp.AvailableVoltages[voltage];
        _appearance.SetData(ent, RandomPowerVisuals.Voltage, color);

        _nodeGroupSystem.QueueReflood(node);
    }
}
