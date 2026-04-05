using System.Linq;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Examine;
using Content.Shared.Verbs;
using Content.Shared.NodeContainer;
using Content.Shared.NodeContainer.NodeGroups;
using Content.Server.NodeContainer;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Shared.Imperial.Medieval.Power;
using Content.Shared.Explosion.EntitySystems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Power.EntitySystems;
using Content.Server.Power.NodeGroups;
using Content.Server.Myrmex.Components;
using Robust.Shared.Timing;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.Power;

public sealed class MyrmexPipeSystem : EntitySystem
{
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly PowerNetSystem _powerNet = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly NodeContainerSystem _nodeContainer = default!;
    [Dependency] private readonly NodeGroupSystem _nodeGroupSystem = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    private TimeSpan _nextCheckTime = TimeSpan.Zero;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(20);

    public override void Initialize()
    {
        SubscribeLocalEvent<MyrmexPipeComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<MyrmexPipeComponent, GetVerbsEvent<ExamineVerb>>(OnGetExamineVerbs);
        SubscribeLocalEvent<MyrmexValvePipeComponent, InteractHandEvent>(OnValveInteractHand);
        SubscribeLocalEvent<MyrmexValvePipeComponent, MyrmexValveDoAfterEvent>(OnValveDoAfter);
    }

    private void OnValveInteractHand(Entity<MyrmexValvePipeComponent> pipe, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (!HasComp<MyrmexComponent>(args.User))
            return;

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, pipe.Comp.DoAfterTime, new MyrmexValveDoAfterEvent(), pipe, pipe)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true
        };

        _doAfterSystem.TryStartDoAfter(doAfterArgs);
        args.Handled = true;
    }

    private void OnValveDoAfter(Entity<MyrmexValvePipeComponent> pipe, ref MyrmexValveDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (!_nodeContainer.TryGetNode<MyrmexPipeNode>(pipe.Owner, pipe.Comp.NodeId, out var pipeNode))
            return;

        pipeNode.Enabled ^= true;

        _nodeGroupSystem.QueueReflood(pipeNode);
        _appearance.SetData(pipe, MyrmexValveVisuals.State, pipeNode.Enabled);

        args.Handled = true;
    }

    private void OnGetExamineVerbs(EntityUid uid, MyrmexPipeComponent component, GetVerbsEvent<ExamineVerb> args)
    {
        if (!_examine.IsInDetailsRange(args.User, args.Target))
            return;
        
        if (!HasComp<MyrmexComponent>(args.User))
            return;

        var verb = new ExamineVerb
        {
            Message = Loc.GetString("cable-multitool-system-verb-tooltip"),
            Text = Loc.GetString("cable-multitool-system-verb-name"),
            Category = VerbCategory.Examine,
            Icon = new SpriteSpecifier.Texture(new ("/Textures/Interface/VerbIcons/zap.svg.192dpi.png")),
            Act = () =>
            {
                var markup = FormattedMessage.FromMarkupOrThrow(GenerateCableMarkup(uid));
                _examine.SendExamineTooltip(args.User, uid, markup, false, false);
            }
        };

        args.Verbs.Add(verb);
    }

    private string GenerateCableMarkup(EntityUid uid, NodeContainerComponent? nodeContainer = null)
    {
        if (!Resolve(uid, ref nodeContainer))
            return Loc.GetString("cable-multitool-system-internal-error-missing-component");

        foreach (var node in nodeContainer.Nodes)
        {
            if (!(node.Value.NodeGroup is IBasePowerNet))
                continue;

            var p = (IBasePowerNet) node.Value.NodeGroup;
            var ps = _powerNet.GetNetworkStatistics(p.NetworkNode);

            float storageRatio = ps.InStorageCurrent / Math.Max(ps.InStorageMax, 1.0f);
            float outStorageRatio = ps.OutStorageCurrent / Math.Max(ps.OutStorageMax, 1.0f);
            return Loc.GetString("cable-multitool-system-statistics",
                ("supplyc", ps.SupplyCurrent),
                ("supplyb", ps.SupplyBatteries),
                ("supplym", ps.SupplyTheoretical),
                ("consumption", ps.Consumption),
                ("storagec", ps.InStorageCurrent),
                ("storager", storageRatio),
                ("storagem", ps.InStorageMax),
                ("storageoc", ps.OutStorageCurrent),
                ("storageor", outStorageRatio),
                ("storageom", ps.OutStorageMax)
            );
        }

        return Loc.GetString("cable-multitool-system-internal-error-no-power-node");
    }

    private void OnAnchorChanged(Entity<MyrmexPipeComponent> pipe, ref AnchorStateChangedEvent args)
    {
        if (!args.Anchored)
            return;

        var xform = Transform(pipe);
        CheckExplosion(pipe.Owner, pipe.Comp, xform);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextCheckTime)
            return;

        _nextCheckTime = _timing.CurTime + _checkInterval;

        var query = EntityQueryEnumerator<MyrmexPipeComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var pipe, out var xform))
        {
            if (!xform.Anchored)
                continue;

            CheckExplosion(uid, pipe, xform);
        }
    }

    private void CheckExplosion(EntityUid uid, MyrmexPipeComponent pipe, TransformComponent xform)
    {
        if (!TryGetVoltage(uid, out var voltage))
            return;

        if (!IsNetworkLive(uid))
            return;

        if (!TryComp<MapGridComponent>(xform.GridUid, out var grid))
            return;

        var tile = _map.TileIndicesFor((xform.GridUid.Value, grid), xform.Coordinates);
        foreach (var other in _map.GetAnchoredEntities((xform.GridUid.Value, grid), tile))
        {
            if (other == uid) 
                continue;

            if (!HasComp<MyrmexPipeComponent>(other))
                continue;

            if (!TryGetVoltage(other, out var otherVoltage))
                continue;

            if (otherVoltage != voltage)
            {
                Kaboom(uid, other, _transform.ToMapCoordinates(xform.Coordinates));
                break;
            }
        }
    }

    private bool IsNetworkLive(EntityUid uid)
    {
        if (!TryComp<NodeContainerComponent>(uid, out var container))
            return false;

        foreach (var node in container.Nodes)
        {
            if (!(node.Value.NodeGroup is IBasePowerNet))
                continue;

            var nodeGroup = (IBasePowerNet) node.Value.NodeGroup;
            var stats = _powerNet.GetNetworkStatistics(nodeGroup.NetworkNode); // туду сделать лучше
            
            if (stats.SupplyTheoretical > 0f)
                return true;
        }
        return false;
    }

    private bool TryGetVoltage(EntityUid uid, out NodeGroupID voltage)
    {
        voltage = default;
        if (!TryComp<NodeContainerComponent>(uid, out var container))
            return false;

        var node = container.Nodes.Values.FirstOrDefault();
        if (node == null)
            return false;

        voltage = node.NodeGroupID;
        return true;
    }

    private void Kaboom(EntityUid cable1, EntityUid cable2, MapCoordinates coordinates)
    {
        _explosion.QueueExplosion(
            coordinates,
            SharedExplosionSystem.DefaultExplosionPrototypeId,
            100f,
            5f,
            10f,
            cable1
        );

        QueueDel(cable1);
        QueueDel(cable2);
    }
}
