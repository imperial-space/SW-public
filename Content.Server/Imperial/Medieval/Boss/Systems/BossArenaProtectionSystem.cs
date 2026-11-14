using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.Containers;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Map;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed class BossArenaProtectionSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedBossArenaRulesSystem _rules = default!;
    private readonly HashSet<EntityUid> _authorized = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<BossArenaProtectedComponent, MoveEvent>(OnMove);
        SubscribeLocalEvent<BossArenaProtectedComponent, AnchorStateChangedEvent>(OnAnchor);
    }

    public void MoveBoss(EntityUid boss, EntityCoordinates coordinates, bool anchor = true)
    {
        _authorized.Add(boss);                                                   // allow movement requested by a boss mechanic
        try
        {
            _transform.Unanchor(boss, Transform(boss), setPhysics: false);
            _transform.SetCoordinates(boss, coordinates);
            if (anchor) _transform.AnchorEntity(boss);
        }
        finally { _authorized.Remove(boss); }
    }

    private void OnMove(EntityUid uid, BossArenaProtectedComponent component, ref MoveEvent args)
    {
        if (_authorized.Contains(uid) || Terminating(uid) || !args.OldPosition.IsValid(EntityManager) ||
            !_rules.IsArena(args.OldPosition) || args.NewPosition == args.OldPosition)
            return;
        _authorized.Add(uid);
        try
        {
            _containers.TryRemoveFromContainer(uid);
            var xform = Transform(uid);
            _transform.SetCoordinates((uid, xform, MetaData(uid)), args.OldPosition, unanchor: false);
            if (!xform.Anchored) _transform.AnchorEntity(uid, xform);
        }
        finally { _authorized.Remove(uid); }
    }

    private void OnAnchor(EntityUid uid, BossArenaProtectedComponent component, ref AnchorStateChangedEvent args)
    {
        if (_authorized.Contains(uid) || args.Anchored || args.Detaching || Terminating(uid) || !_rules.IsArena(uid))
            return;
        _authorized.Add(uid);
        try { _transform.AnchorEntity(uid); }
        finally { _authorized.Remove(uid); }
    }
}
