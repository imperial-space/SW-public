using Content.Server.NPC;
using Content.Server.NPC.HTN.Preconditions;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.hl2.Mobs;

/// <summary>
/// Возвращает true если доля здоровья владельца (0–1) больше <see cref="Value"/>.
/// 1.0 = полное здоровье, 0.0 = порог недееспособности.
/// </summary>
public sealed partial class SelfHealthFractionGreaterPrecondition : HTNPrecondition
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    [DataField(required: true)]
    public float Value;

    /// <summary>
    /// Если true — возвращает true когда здоровье МЕНЬШЕ или равно Value (побег).
    /// Если false — возвращает true когда здоровье БОЛЬШЕ Value (атака).
    /// </summary>
    [DataField]
    public bool Invert = false;

    /// <summary>
    /// Время в секундах, после которого агрессия восстанавливается.
    /// </summary>
    [DataField]
    public float CooldownDuration = 30f;

    private const string LastFleeTimeKey = "SelfHealthFractionGreaterPrecondition_LastFleeTime";

    public override bool IsMet(NPCBlackboard blackboard)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entManager.TryGetComponent<DamageableComponent>(owner, out var damageable))
            return false;

        var thresholdSys = _entManager.System<MobThresholdSystem>();
        var totalDamage = damageable.TotalDamage;

        if (!thresholdSys.TryGetIncapPercentage(owner, totalDamage, out var incapPct))
            return false;
        if (incapPct is null) return false;

        var healthFraction = 1f - (float)incapPct;
        var shouldFlee = healthFraction <= Value;

        // Если не должны убегать по здоровью - проверяем кд
        if (!shouldFlee)
        {
            // Здоровье восстановилось, сбрасываем таймер
            blackboard.Remove<TimeSpan>(LastFleeTimeKey);
            var result = healthFraction > Value;
            return Invert ? !result : result;
        }

        // Проверяем кд побега
        var currentTime = _timing.CurTime;

        if (blackboard.TryGetValue<TimeSpan>(LastFleeTimeKey, out var lastFleeTime, _entManager))
        {
            // Если кд еще не прошло - продолжаем режим атаки
            if ((currentTime - lastFleeTime).TotalSeconds < CooldownDuration)
            {
                // Игнорируем низкое здоровье, продолжаем атаковать
                var result = healthFraction > Value;
                return Invert ? !result : result;
            }
        }

        // Кд прошло или нет записи - начинаем побег
        blackboard.SetValue(LastFleeTimeKey, currentTime);
        var fleeResult = healthFraction > Value;
        return Invert ? !fleeResult : fleeResult;
    }
}
