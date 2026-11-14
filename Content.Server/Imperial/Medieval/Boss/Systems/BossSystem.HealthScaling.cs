using System.Linq;
using Content.Shared.Damage;
using Content.Shared.Ghost;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Server.Player;
using Robust.Shared.Enums;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class BossSystem
{
    [Dependency]
    private readonly IPlayerManager _players = default!;
    private void LockHealthScaling(EntityUid uid, BossComponent boss)
    {
        if (!TryComp<BossHealthScalingComponent>(uid, out var scaling) || scaling.Locked)
            return;
        var bossTransform = Transform(uid);
        var players = _players.Sessions.Where(session => session.Status is SessionStatus.Connected or SessionStatus.InGame).Select(session => session.AttachedEntity).OfType<EntityUid>().Distinct();
        var count = 0;
        foreach (var player in players)
        {
            if (!Exists(player) || HasComp<BossComponent>(player) || !_mobState.IsAlive(player) || HasComp<GhostComponent>(player) || (TryComp<FightingBossComponent>(player, out var fight) && fight.Boss != GetNetEntity(uid)))
                continue;
            var playerTransform = Transform(player);
            if (bossTransform.GridUid == null || playerTransform.GridUid != bossTransform.GridUid || playerTransform.MapID != bossTransform.MapID)
                continue;
            if (scaling.ArenaRadius > 0 && !_transform.InRange(bossTransform.Coordinates, playerTransform.Coordinates, scaling.ArenaRadius))
                continue;
            count++;
        }

        scaling.Locked = true;
        scaling.PlayerCount = count;
        var cap = Math.Max(1, scaling.MaxPlayers);
        var fraction = cap == 1 ? 0f : (Math.Clamp(count, 1, cap) - 1f) / (cap - 1f);
        var previousMax = boss.MaxHealth;
        boss.MaxHealth = MathF.Round(previousMax + (Math.Max(previousMax, scaling.MaxHealth) - previousMax) * fraction);
        if (boss.UseDamageableHealth && TryComp<DamageableComponent>(uid, out var damageable))
            boss.Health = Math.Max(0, boss.MaxHealth - damageable.TotalDamage.Float());
        else
            boss.Health += boss.MaxHealth - previousMax;
    }
}
