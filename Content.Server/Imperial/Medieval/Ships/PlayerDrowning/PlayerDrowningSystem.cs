using Content.Shared.Damage.Components;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Ships.PlayerDrowning;

/// <summary>
/// This handles...
/// </summary>
public sealed class PlayerDrowningSystem : EntitySystem
{
    private const float DefaultReloadTimeSeconds = 1f;
    private const int DrownTimeMax = 15;
    private TimeSpan _nextCheckTime;

    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityManager _entityManager = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();
        _nextCheckTime = _timing.CurTime + TimeSpan.FromSeconds(DefaultReloadTimeSeconds);

    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;

        if (curTime > _nextCheckTime)
        {
            _nextCheckTime = curTime + TimeSpan.FromSeconds(DefaultReloadTimeSeconds);

            foreach (var component in EntityManager.EntityQuery<DrownerComponent>())
            {
                if (!TryComp<MapComponent>(component.Owner, out var map))
                    return;

                var drowningPlayers = new HashSet<Entity<TransformComponent>>();

                _lookup.GetEntitiesOnMap(map.MapId, drowningPlayers);

                foreach (var entity in drowningPlayers)
                {
                    var transformComp = entity.Comp;
                    var uid = entity.Owner;
                    ProcessDrowning(uid, transformComp);
                }
            }
        }
    }

    private void ProcessDrowning(EntityUid uid, TransformComponent transform)
    {
        if (!TryComp<PlayerDrowningComponent>(uid, out var drowner))
            EnsureComp<PlayerDrowningComponent>(uid);
        else
        {
            if (TryComp<StaminaComponent>(uid, out var stamina))
            {
                if (stamina.Critical)
                    _entityManager.DeleteEntity(uid);
                else
                {
                    stamina.StaminaDamage += 1;
                }
            }
            else if (drowner.DrownTime >= DrownTimeMax)
            {
                _entityManager.DeleteEntity(uid);
            }
            drowner.DrownTime += 1;
        }
    }
}
