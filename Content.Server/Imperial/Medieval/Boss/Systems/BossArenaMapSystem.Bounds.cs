using System.Linq;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.ActionBlocker;
using Content.Shared.Chasm;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Ghost;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Mobs.Systems;
using Content.Shared.StepTrigger.Systems;
using Robust.Shared.EntitySerialization;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class BossArenaMapSystem
{
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_timing.CurTime < _nextCheck)
            return;
        _nextCheck = _timing.CurTime + TimeSpan.FromSeconds(0.1);                // check the floor ten times per second

        var query = EntityQueryEnumerator<Content.Shared.Mobs.Components.MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var user, out _, out var xform))
        {
            if (xform.MapUid is { } map && TryComp<BossArenaMapComponent>(map, out var arena) && !IsOnPlatform(user, arena))
                TryReturnFromPit(user, map, arena);
        }
    }

    private bool IsOnPlatform(EntityUid user, BossArenaMapComponent arena)
    {
        if (!TryComp<MapGridComponent>(arena.CombatGrid, out var grid))
            return true;
        var position = _transform.ToCoordinates(arena.CombatGrid, _transform.ToMapCoordinates(Transform(user).Coordinates)).Position;
        return !_maps.GetTileRef(arena.CombatGrid, grid,                         // only combat floor tiles prevent a fall
            new Vector2i((int)MathF.Floor(position.X), (int)MathF.Floor(position.Y))).Tile.IsEmpty;
    }

    public bool TryReturnFromPit(EntityUid user, EntityUid map, BossArenaMapComponent arena)
    {
        if (!Exists(user) || Terminating(user) || HasComp<GhostComponent>(user) ||
            !EntityManager.System<SharedBossArenaRulesSystem>().IsPartyBody(user) ||
            Transform(user).MapUid != map || IsOnPlatform(user, arena) ||
            HasComp<ChasmFallingComponent>(user) ||
            !HasComp<DamageableComponent>(user) ||
            !_thresholds.TryGetDeadThreshold(user, out var threshold) || threshold.Value < 1)
            return false;
        var gates = EntityManager.System<BossArenaTransportSystem>();
        if (!Exists(arena.Entrance) || Terminating(arena.Entrance) ||
            !HasComp<BossArenaEntranceComponent>(arena.Entrance))
            return false;

        if (TryComp<DoAfterComponent>(user, out var actions))
            foreach (var id in actions.DoAfters.Keys.ToArray())
                _doAfter.Cancel(user, id, actions);
        gates.StopMotion(user);
        var falling = new BossArenaFallEvent(user, map);
        RaiseLocalEvent(ref falling);
        _chasm.StartFalling(user);                                              // use the normal fall animation, handle the landing below
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/falling.ogg"), user);
        return true;
    }

}
