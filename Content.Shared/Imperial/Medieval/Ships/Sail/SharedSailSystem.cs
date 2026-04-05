using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Ghost;
using Content.Shared.Imperial.Medieval.Ships.Sail;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Interaction;
using Content.Shared.Verbs;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.Ships.Sail;

/// <summary>
/// Управляет взаимодействием с парусами (поворот, сложение/разложение).
/// </summary>
public sealed class SharedSailSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly SharedSkillsSystem _skills = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;

    public override void Initialize()
    {

        SubscribeNetworkEvent<RotateSailEvent>(OnMenuOptionSelected);
    }



    private void OnMenuOptionSelected(RotateSailEvent args, EntitySessionEventArgs session)
    {
        var player = session.SenderSession.AttachedEntity;
        if (player == null)
            return;

        switch (args.Direction)
        {
            case -1: // Влево
                TryRotate(player.Value, new EntityUid(args.Target), true);
                break;
            case 1: // Вправо
                TryRotate(player.Value, new EntityUid(args.Target), false);
                break;
            case 0: // Сложить/разложить
                TryFold(player.Value,new EntityUid(args.Target));
                break;
        }
    }
    private void TryRotate(EntityUid playerEntity, EntityUid targetEntity, bool direction)
    {
        // Проверка: направление должно быть 1 или -1

        // Время поворота — зависит от ловкости игрока (аналогично TryFold)
        float time = 7 - _skills.GetSkillLevel(playerEntity, "Agility") * 0.15f - _skills.GetSkillLevel(playerEntity, "Intelligence") * 0.15f;
        time = Math.Max(1.0f, time); // Минимум 1 секунда

        // Создаём событие поворота с направлением
        var rotateEvent = new RotateEvent(direction);

        // Аргументы DoAfter — аналогично TryFold
        var doAfterArgs = new DoAfterArgs(EntityManager, playerEntity, time, rotateEvent, targetEntity, targetEntity)
        {
            MovementThreshold = 0.5f,
            BreakOnMove = true,
            CancelDuplicate = true,
            DistanceThreshold = 2,
            BreakOnDamage = true,
            RequireCanInteract = false,
            BreakOnDropItem = true,
            BreakOnHandChange = true,
            NeedHand = true,
        };

        // Запускаем DoAfter
        _doAfter.TryStartDoAfter(doAfterArgs);
    }


    private void TryFold(EntityUid playerEntity, EntityUid targetEntity)
    {
        var time = 7 - _skills.GetSkillLevel(playerEntity, "Agility") * 0.15f -
                   _skills.GetSkillLevel(playerEntity, "Intelligence") * 0.15f;

        var doAfterArgs = new DoAfterArgs(EntityManager, playerEntity, time, new SailFoldEvent(), targetEntity, playerEntity)
        {
            MovementThreshold = 0.5f,
            BreakOnMove = true,
            CancelDuplicate = true,
            DistanceThreshold = 2,
            BreakOnDamage = true,
            RequireCanInteract = false,
            BreakOnDropItem = true,
            BreakOnHandChange = true,
            NeedHand = true,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }
}
