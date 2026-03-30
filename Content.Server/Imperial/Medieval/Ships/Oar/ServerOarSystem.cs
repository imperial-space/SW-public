using System.Numerics;
using Content.Shared._RD.Weight.Components;
using Content.Shared._RD.Weight.Systems;
using Content.Shared.Coordinates;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

using Content.Shared.Database;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Ships.Oar;
using Content.Shared.Interaction.Components;
using Content.Shared.Movement.Components;

namespace Content.Server.Imperial.Medieval.Ships.Oar;

public sealed class OarSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedSkillsSystem _skills = default!;
    [Dependency] private readonly EntityManager _entManager = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly RDWeightSystem _rdWeight = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<OarComponent, OnOarDoAfterEvent>(OnOarDoAfter);
    }

    private void OnOarDoAfter(EntityUid uid, OarComponent component, ref OnOarDoAfterEvent args)
    {
        var item = _hands.GetActiveItem(args.User);
        if (args.Cancelled || args.Handled || item == null)
            return;

        if (!TryComp<OarComponent>(item, out var comp))
            return;

        Push(item.Value, comp.Direction, comp.Power, args.User);
        args.Handled = true;
        args.Repeat = true;
    }

    private void Push(EntityUid item, Angle direction, float power, EntityUid player)
    {
        // Учитываем силу гребца
        power += power * (10 - _skills.GetSkillLevel(player, "Strength")) * 0.1f;

        // Получаем лодку
        var boat = _transform.GetParentUid(player);
        if (!TryComp<TransformComponent>(boat, out var boatTransform))
            return;

        // Получаем угол поворота лодки
        var boatAngle = boatTransform.LocalRotation;

        // Вычисляем общий вес лодки и груза
        var weight = _rdWeight.GetTotal(boat);
        if (weight == 0) weight = 10; // Минимальный вес

        // Проверяем объекты в лодке
        var entities = _lookup.GetEntitiesIntersecting(boat);
        if (entities.Count > 1000) return;

        foreach (var entity in entities)
        {
            if (HasComp<RDWeightComponent>(entity))
                weight += _rdWeight.GetTotal(entity);
        }

        // Нормализуем угол (0-2π)
        var normalizedAngle = (float)direction.Theta % (2 * MathF.PI);
        if (normalizedAngle < 0)
            normalizedAngle += 2 * MathF.PI;

        // Преобразуем угол в вектор направления
        var directionVec = new Vector2(
            MathF.Cos(normalizedAngle),
            MathF.Sin(normalizedAngle)
        );

        // Учитываем поворот игрока
        if (TryComp<TransformComponent>(player, out var playerTransform))
        {
            directionVec = playerTransform.LocalRotation.RotateVec(directionVec);
        }

        // Применяем импульс
        var impulse = directionVec * (power / weight);

        if (TryComp<PhysicsComponent>(boat, out var body))
        {
            _physics.WakeBody(boat);
            _physics.ApplyLinearImpulse(boat, impulse, body: body);
        }
    }

}
