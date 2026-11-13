using Content.Server.Movement.Components;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;
// Imperial medieval start
using Content.Server.Imperial.Medieval.Prediction;
using Content.Shared.GameTicking;
using Content.Shared.Imperial.Medieval.Prediction;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using PhysicsTransform = Robust.Shared.Physics.Transform;
// Imperial medieval end

namespace Content.Server.Movement.Systems;

/// <summary>
/// Stores a buffer of previous positions of the relevant entity.
/// Can be used to check the entity's position at a recent point in time.
/// </summary>
public sealed class LagCompensationSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    // Imperial medieval start
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly Dictionary<NetUserId, GameTick> _lastRealTicks = new();
    private readonly MedievalMeleeHistoryIndex _meleeHistory = new();

    // Maximum supported historical lookup window.
    public static readonly TimeSpan BufferTime = TimeSpan.FromMilliseconds(750);
    // Imperial medieval end

    public override void Initialize()
    {
        base.Initialize();
        Log.Level = LogLevel.Info;
        SubscribeLocalEvent<LagCompensationComponent, MoveEvent>(OnLagMove);
        // Imperial medieval start
        SubscribeLocalEvent<LagCompensationComponent, ComponentStartup>(OnLagStartup);
        SubscribeLocalEvent<LagCompensationComponent, MapInitEvent>(OnLagMapInit);
        SubscribeLocalEvent<LagCompensationComponent, ComponentShutdown>(OnLagShutdown);
        SubscribeNetworkEvent<MedievalSetLastRealTickEvent>(OnSetLastRealTick);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
        // Imperial medieval end
    }

    // Imperial medieval start
    public override void Shutdown()
    {
        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
        _lastRealTicks.Clear();
        _meleeHistory.Clear();
        base.Shutdown();
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent args)
    {
        _lastRealTicks.Clear();
        _meleeHistory.Clear();
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus == SessionStatus.Disconnected)
            _lastRealTicks.Remove(args.Session.UserId);
    }

    private void OnLagStartup(EntityUid uid, LagCompensationComponent component, ComponentStartup args)
        => SeedHistory(uid, component);

    private void OnLagMapInit(EntityUid uid, LagCompensationComponent component, MapInitEvent args)
        => SeedHistory(uid, component);

    private void OnLagShutdown(EntityUid uid, LagCompensationComponent component, ComponentShutdown args)
        => _meleeHistory.Remove(uid);

    private void SeedHistory(EntityUid uid, LagCompensationComponent component)
    {
        // A stationary entity needs a baseline before its first movement. MapInit
        // also covers components started before their map coordinates were valid.
        if (!TryComp(uid, out TransformComponent? xform) ||
            xform.MapID == MapId.Nullspace || !xform.Coordinates.IsValid(EntityManager))
            return;

        if (component.Positions.Count == 0)
            component.Positions.Enqueue((_timing.CurTime, xform.Coordinates, xform.LocalRotation));
        RefreshMeleeHistory(uid, component);
    }

    private void OnSetLastRealTick(MedievalSetLastRealTickEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity == null || !TryGetHistoricalTime(msg.Tick, out _))
            return;

        var userId = args.SenderSession.UserId;

        // A delayed or repeated report must not move this session's view backwards.
        if (_lastRealTicks.TryGetValue(userId, out var previousTick) && msg.Tick <= previousTick)
            return;

        _lastRealTicks[userId] = msg.Tick;
    }

    private bool TryGetHistoricalTime(GameTick tick, out TimeSpan time)
    {
        time = _timing.CurTime;

        // Check before subtracting unsigned tick values.
        if (tick == GameTick.Zero || tick > _timing.CurTick)
            return false;

        var tickOffset = _timing.CurTick.Value - tick.Value;
        var offset = TimeSpan.FromTicks((long) tickOffset * _timing.TickPeriod.Ticks);

        if (offset > BufferTime)
            return false;

        // LastRealTick already identifies the processed state. Do not subtract
        // an additional tick or estimate the client's view from its ping.
        time -= offset;
        return true;
    }
    // Imperial medieval end

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var earliestTime = curTime - BufferTime;

        // Cull any old ones from active updates
        // Probably fine to include ignored.
        var query = AllEntityQuery<LagCompensationComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            // Imperial medieval edit - retain the last pose preceding the lookup window.
            MedievalLagCompensationHistory.Prune(comp.Positions, earliestTime);
            // Imperial medieval edit - share the existing maintenance pass across all strikes.
            RefreshMeleeHistory(uid, comp);
        }
    }

    private void OnLagMove(EntityUid uid, LagCompensationComponent component, ref MoveEvent args)
    {
        if (!args.NewPosition.EntityId.IsValid())
            return; // probably being sent to nullspace for deletion.

        component.Positions.Enqueue((_timing.CurTime, args.NewPosition, args.NewRotation));
        // Imperial medieval edit - queries later in this tick must see this movement too.
        RefreshMeleeHistory(uid, component);
    }

    // Imperial medieval start
    private void RefreshMeleeHistory(EntityUid uid, LagCompensationComponent history)
    {
        if (TerminatingOrDeleted(uid) || !TryComp(uid, out TransformComponent? xform) ||
            !TryComp<PhysicsComponent>(uid, out _) || !TryComp<FixturesComponent>(uid, out var fixtures))
        {
            _meleeHistory.Remove(uid);
            return;
        }

        // Include every fixture, even when it is temporarily non-collidable.
        // A rotation-independent bound also survives collision-layer changes.
        var radiusSquared = 0f;
        foreach (var fixture in fixtures.Fixtures.Values)
        {
            for (var child = 0; child < fixture.Shape.ChildCount; child++)
            {
                var bounds = fixture.Shape.ComputeAABB(PhysicsTransform.Empty, child);
                var x = MathF.Max(MathF.Abs(bounds.Left), MathF.Abs(bounds.Right));
                var y = MathF.Max(MathF.Abs(bounds.Bottom), MathF.Abs(bounds.Top));
                var squared = x * x + y * y;
                if (float.IsFinite(squared))
                    radiusSquared = MathF.Max(radiusSquared, squared);
            }
        }

        // Native arc rays test fixture AABBs in their broadphase frame. Their
        // corners can extend beyond the shape's bounding circle by sqrt(2).
        var radius = MathF.Sqrt(radiusSquared) * MathF.Sqrt(2f);
        _meleeHistory.Refresh(uid, history.Positions, xform.Coordinates, radius, _timing.CurTime, BufferTime);
    }

    /// <summary>
    /// Adds bodies whose retained historical bounds may intersect a strike.
    /// Query each coordinate parent's tree, rather than each tracked body.
    /// </summary>
    public void GetMeleeCandidates(MapId map, Box2 worldBounds, HashSet<EntityUid> candidates)
    {
        foreach (var parent in _meleeHistory.Parents)
        {
            if (TerminatingOrDeleted(parent) || !TryComp(parent, out TransformComponent? xform) ||
                xform.MapID != map)
                continue;

            var (_, _, _, inverse) = _transform.GetWorldPositionRotationMatrixWithInv(parent);
            _meleeHistory.Query(parent, inverse.TransformBox(worldBounds), candidates);
        }
    }
    // Imperial medieval end

    public (EntityCoordinates Coordinates, Angle Angle) GetCoordinatesAngle(EntityUid uid, ICommonSession? pSession,
        TransformComponent? xform = null)
    {
        if (!Resolve(uid, ref xform))
            return (EntityCoordinates.Invalid, Angle.Zero);

        if (pSession == null || !TryComp<LagCompensationComponent>(uid, out var lag) || lag.Positions.Count == 0)
            return (xform.Coordinates, xform.LocalRotation);

        // Imperial medieval start
        if (!_lastRealTicks.TryGetValue(pSession.UserId, out var lastRealTick) ||
            !TryGetHistoricalTime(lastRealTick, out var sentTime))
        {
            return (xform.Coordinates, xform.LocalRotation);
        }
        // Imperial medieval end

        // Imperial medieval start
        // LastRealTick names a completed authoritative snapshot. Use its final
        // recorded pose, never the first movement later than that snapshot.
        if (!MedievalLagCompensationHistory.TryGetPose(lag.Positions, sentTime, out var coordinates, out var angle) ||
            !coordinates.IsValid(EntityManager))
        {
            Log.Debug($"No usable lag comp history found, using {xform.Coordinates}");
            return (xform.Coordinates, xform.LocalRotation);
        }

        Log.Debug($"Actual coords is {xform.Coordinates} and got {coordinates}");
        return (coordinates, angle);
        // Imperial medieval end
    }

    public Angle GetAngle(EntityUid uid, ICommonSession? session, TransformComponent? xform = null)
    {
        var (_, angle) = GetCoordinatesAngle(uid, session, xform);
        return angle;
    }

    public EntityCoordinates GetCoordinates(EntityUid uid, ICommonSession? session, TransformComponent? xform = null)
    {
        var (coordinates, _) = GetCoordinatesAngle(uid, session, xform);
        return coordinates;
    }
}
