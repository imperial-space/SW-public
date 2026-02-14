using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Robust.Shared.Map;
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
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedStaminaSystem _staminaSystem = default!;

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
                if (!TryComp<TransformComponent>(component.Owner, out var transform))
                    return;
                var childs = new List<EntityUid>();
                var childEnum = transform.ChildEnumerator;
                while (childEnum.MoveNext(out var child))
                {
                    ProcessDrowning(child);
                }

            }
        }
    }

    private void ProcessDrowning(EntityUid uid)
    {
        if (HasComp<MapGridComponent>(uid))
            return;

        if (!TryComp<PlayerDrowningComponent>(uid, out var drowner))
            EnsureComp<PlayerDrowningComponent>(uid);
        else
        {
            if (drowner.Undrowable)
                return;
            if (HasComp<UndrowableComponent>(uid))
                return;

            if (TryComp<StaminaComponent>(uid, out var stamina))
            {
                if (stamina.Critical)
                {
                    _entityManager.DeleteEntity(uid);
                    return;
                }

                if (_staminaSystem.TryTakeStamina(uid, 25, ignoreResist: true))
                    return;

            }
            if (drowner.DrownTime >= DrownTimeMax)
            {
                _entityManager.DeleteEntity(uid);
            }
            drowner.DrownTime += 1;
        }
    }
}
