using Content.Server.Chat;
using Content.Server.Cult.Components;
using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Cult;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Cult.Bloodspells.light;

/// <summary>
/// This handles...
/// </summary>
public sealed class DeathCurseSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;

    /// <inheritdoc/>
    private TimeSpan _nextCheckTime;

    private const float DeathCurseTick = 10f;

    public override void Initialize()
    {
        _nextCheckTime = _timing.CurTime + TimeSpan.FromSeconds(DeathCurseTick);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;

        if (curTime > _nextCheckTime)
        {
            _nextCheckTime = curTime + TimeSpan.FromSeconds(DeathCurseTick);

            foreach (var curse in EntityManager.EntityQuery<DeathCusreComponent>())
            {
                _damageableSystem.TryChangeDamage(curse.Owner, curse.CurseDamage, true, false);
            }
        }
    }
}
