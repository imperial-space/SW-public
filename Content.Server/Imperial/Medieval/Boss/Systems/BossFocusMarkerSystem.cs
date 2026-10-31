using System.Linq;
using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Boss;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed class BossFocusMarkerSystem : EntitySystem
{
    [Dependency] private readonly BossTelegraphSystem _telegraphs = default!;
    [Dependency] private readonly SharedBossArenaRulesSystem _rules = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BossTelegraphSettingsComponent, DamageChangedEvent>(OnDamage);
    }

    private void OnDamage(EntityUid uid, BossTelegraphSettingsComponent comp, DamageChangedEvent args)
    {
        if (!args.DamageIncreased || !TryComp<BossComponent>(uid, out var boss) || boss.Finished ||
            _rules.ResolveSource(args.Origin) is not { } source || !_telegraphs.TryGetArena(uid, out _))
            return;
        var focus = EnsureComp<BossFocusMarkerComponent>(uid);
        if (focus.Started)                                                       // choose the starting tank only once
            return;
        focus.Started = true;
        if (_telegraphs.TryGetPlayerTile(uid, source.Player, out _))
            SetTank(uid, source.Player);
        else
        {

            var position = _transform.GetWorldPosition(uid);
            var nearest = _telegraphs.LivingPlayers(uid).OrderBy(player => Vector2.DistanceSquared(position, _transform.GetWorldPosition(player)))
                .ThenBy(player => player.Id).Cast<EntityUid?>().FirstOrDefault();
            if (nearest is { } player)
                SetTank(uid, player);
        }
    }

    public bool SetTank(EntityUid boss, EntityUid player)
    {
        if (!HasComp<BossTelegraphSettingsComponent>(boss) || !TryComp<BossComponent>(boss, out var encounter) ||
            encounter.Finished || encounter.Health <= 0 || !_telegraphs.TryGetPlayerTile(boss, player, out _))
            return false;
        var focus = EnsureComp<BossFocusMarkerComponent>(boss);
        if (focus.Marker is { } old && Exists(old))
            Del(old);
        focus.Started = true;
        focus.Tank = player;                                                     // basic attacks follow this player
        focus.Marker = _telegraphs.CreateMarker(boss, player, BossActorMarkerKind.Tank);
        return true;
    }

    public void Clear(EntityUid boss)
    {
        if (!TryComp<BossFocusMarkerComponent>(boss, out var focus))
            return;
        if (focus.Marker is { } marker && Exists(marker) && !Terminating(marker))
            Del(marker);
        focus.Marker = null;
        focus.Tank = null;
        focus.Started = false;
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<BossFocusMarkerComponent, BossComponent>();
        while (query.MoveNext(out var uid, out var focus, out var boss))
        {
            if (!focus.Started || MetaData(uid).EntityPaused)
                continue;
            if (boss.Finished)
            {
                Clear(uid);
                continue;
            }
            var candidates = _telegraphs.LivingPlayers(uid);
            if (candidates.Count > 0 && focus.Tank is { } tank && _telegraphs.TryGetPlayerTile(uid, tank, out _, allowCritical: true))
                continue;
            if (focus.Marker is { } marker && Exists(marker))
                Del(marker);
            focus.Marker = null;
            focus.Tank = null;
            if (candidates.Count == 0)
                continue;
            var position = _transform.GetWorldPosition(uid);
            var nearest = candidates.OrderBy(player => Vector2.DistanceSquared(position, _transform.GetWorldPosition(player)))
                .ThenBy(player => player.Id).First();
            SetTank(uid, nearest);                                               // replace a missing tank with the nearest player
        }
    }
}
