using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.Movement.Components;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.hl2.Mobs;

/// <summary>
/// Двигает NPC в сторону, противоположную сущности из слота <see cref="TargetKey"/> блэкборда.
/// Работает напрямую через InputMoverComponent — не зависит от грид-пасфайндинга,
/// поэтому корректно работает для летающих мобов в открытом космосе.
/// </summary>
public sealed partial class FleeOperator : HTNOperator, IHtnConditionalShutdown
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IGameTiming    _timing     = default!;

    private SharedTransformSystem _transform = default!;

    /// <summary>Ключ блэкборда с EntityUid цели (угрозы), от которой убегаем.</summary>
    [DataField]
    public string TargetKey = "Target";

    /// <summary>На сколько тайлов убегать от угрозы.</summary>
    [DataField]
    public float FleeDistance = 10f;

    /// <summary>Максимальное время побега в секундах (защита от зависания).</summary>
    [DataField]
    public float FleeDuration = 6f;

    [DataField]
    public HTNPlanState ShutdownState { get; private set; } = HTNPlanState.TaskFinished;

    // Ключи в блэкборде для хранения состояния текущего побега
    private const string FleeTargetPosKey = "FleeOperator_TargetPos";
    private const string FleeEndTimeKey   = "FleeOperator_EndTime";

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _transform = sysManager.GetEntitySystem<SharedTransformSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(
        NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out _, _entManager))
            return (false, null);

        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entManager.HasComponent<TransformComponent>(owner))
            return (false, null);

        if (!_entManager.HasComponent<InputMoverComponent>(owner))
            return (false, null);

        return (true, null);
    }

    public override void Startup(NPCBlackboard blackboard)
    {
        base.Startup(blackboard);

        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager))
            return;

        var ownerPos  = _transform.GetWorldPosition(owner);
        var targetPos = _transform.GetWorldPosition(target);

        // Направление ОТ угрозы
        var awayDir = ownerPos - targetPos;
        if (awayDir == Vector2.Zero)
            awayDir = new Vector2(1f, 0f);
        awayDir = Vector2.Normalize(awayDir);

        var fleeWorldPos = ownerPos + awayDir * FleeDistance;

        blackboard.SetValue(FleeTargetPosKey, fleeWorldPos);
        blackboard.SetValue(FleeEndTimeKey,   _timing.CurTime + TimeSpan.FromSeconds(FleeDuration));
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entManager.TryGetComponent<InputMoverComponent>(owner, out var mover))
            return HTNOperatorStatus.Failed;

        if (!blackboard.TryGetValue<Vector2>(FleeTargetPosKey, out var fleeTarget, _entManager))
            return HTNOperatorStatus.Failed;

        // Проверяем таймаут
        if (blackboard.TryGetValue<TimeSpan>(FleeEndTimeKey, out var endTime, _entManager)
            && _timing.CurTime >= endTime)
        {
            StopMovement(mover);
            return HTNOperatorStatus.Finished;
        }

        var ownerPos = _transform.GetWorldPosition(owner);
        var toTarget = fleeTarget - ownerPos;
        var dist     = toTarget.Length();

        if (dist <= 1.5f)
        {
            StopMovement(mover);
            return HTNOperatorStatus.Finished;
        }

        // Напрямую устанавливаем вектор движения — без грид-пасфайндинга
        var dir = Vector2.Normalize(toTarget);
        mover.CurTickSprintMovement = dir;
        mover.LastInputTick         = _timing.CurTick;
        mover.LastInputSubTick      = ushort.MaxValue;

        return HTNOperatorStatus.Continuing;
    }

    private void StopMovement(InputMoverComponent mover)
    {
        mover.CurTickSprintMovement = Vector2.Zero;
        mover.LastInputTick         = _timing.CurTick;
        mover.LastInputSubTick      = ushort.MaxValue;
    }

    public void ConditionalShutdown(NPCBlackboard blackboard)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (_entManager.TryGetComponent<InputMoverComponent>(owner, out var mover))
            StopMovement(mover);

        blackboard.Remove<Vector2>(FleeTargetPosKey);
        blackboard.Remove<TimeSpan>(FleeEndTimeKey);
    }

    public override void TaskShutdown(NPCBlackboard blackboard, HTNOperatorStatus status)
    {
        base.TaskShutdown(blackboard, status);
        ConditionalShutdown(blackboard);
    }

    public override void PlanShutdown(NPCBlackboard blackboard)
    {
        base.PlanShutdown(blackboard);
        ConditionalShutdown(blackboard);
    }
}
