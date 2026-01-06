using Content.Server.Chat;
using Content.Server.Cult.Components;
using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Cult;
using Content.Shared.Imperial.Medieval.Skills;
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
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;

    /// <inheritdoc/>
    private TimeSpan _nextCheckTime;
    private TimeSpan _nextCheckTimePopup;

    private const float DeathCurseTick = 10f;
    private readonly Random _random = new();

    public override void Initialize()
    {
        base.Initialize();
        _nextCheckTime = _timing.CurTime + TimeSpan.FromSeconds(DeathCurseTick);
        _nextCheckTimePopup = _timing.CurTime + TimeSpan.FromSeconds(DeathCurseTick*10);
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
                if (TryComp<SkillsComponent>(curse.Owner, out var skills))
                {
                    if (skills.Levels.TryGetValue("Endurance", out var endurance) && endurance <= _random.Next(1, 20))
                    {
                        SendPainPopup(curse.Owner);
                    }

                    if (skills.Levels.TryGetValue("Vitality", out var vitality) && vitality-10 <= _random.Next(1, 15)) // проверяем осилит ли живучка
                    {
                        _damageableSystem.TryChangeDamage(curse.Owner, curse.CurseDamage, true, false); // не осилил
                        curse.CurseCount += 1;
                        if (curse.CurseCount >= 360)
                        {
                            RemComp<DeathCusreComponent>(curse.Owner);
                            _popupSystem.PopupClient("Ты чутсвуешь, что боль наконец то отступает", curse.Owner, curse.Owner);
                        }
                    }
                    else
                    {
                        _damageableSystem.TryChangeDamage(curse.Owner, curse.CurseDamage*0.5, true, false); // повезло уменьшаем в 2 раза урон
                        curse.CurseCount += 2;
                        if (curse.CurseCount >= 360)
                        {
                            RemComp<DeathCusreComponent>(curse.Owner);
                            _popupSystem.PopupClient("Ты чутсвуешь, что боль наконец то отступает", curse.Owner, curse.Owner);
                        }
                    }
                }
                else
                {
                    _damageableSystem.TryChangeDamage(curse.Owner, curse.CurseDamage, true, false);
                    SendPainPopup(curse.Owner);
                }
            }
        }
    }

    private void SendPainPopup(EntityUid entity) // Чистейшая боль, мб сделать вскрики? но как будто это будет бесить просто, хотя шансы сделать то прикольно навреное
    {
        if (_timing.CurTime < _nextCheckTimePopup)
            return;

        _popupSystem.PopupClient("Ты чутсвуешь жуткую боль, что растекается по твоим венам", entity, entity);
        _nextCheckTime = _timing.CurTime + TimeSpan.FromSeconds(DeathCurseTick*10);
    }
}
