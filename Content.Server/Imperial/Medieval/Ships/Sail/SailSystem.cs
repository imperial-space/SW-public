using System.Numerics;
using Content.Shared._RD.Weight.Components;
using Content.Shared._RD.Weight.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Ships.Wind;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Popups;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Ships.Sail;

/// <summary>
/// This handles...
/// </summary>
public sealed class SailSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedSkillsSystem  _skills = default!;
    [Dependency] private readonly EntityManager _entManager = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly RDWeightSystem  _rdWeight = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private const float DefaultReloadTimeSeconds = 1f;
    private TimeSpan _nextCheckTime;

    private int _windForce;
    private int _windAngle;
    /// <inheritdoc/>
    public override void Initialize()
    {
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;

        if (curTime > _nextCheckTime)
        {
            _nextCheckTime = curTime + TimeSpan.FromSeconds(DefaultReloadTimeSeconds);

            // Плавные изменения ветра (0–10)
            if (_windForce <= 0)
                _windForce += _random.Next(0, 2);
            else if (_windForce >= 10)
                _windForce -= _random.Next(0, 2);
            else
                _windForce += _random.Next(-1, 2);

            // Угол ветра: -45° до +45° (в градусах) — реалистично для моря
            _windAngle += _random.Next(-5, 6); // ±5 градусов за шаг
            _windAngle = Math.Clamp(_windAngle, -45, 45);

            // --- Обработка каждого паруса ---
            foreach (var sailComponent in EntityManager.EntityQuery<SailComponent>())
            {
                if (sailComponent.Folded)
                    continue;

                var sailEntity = sailComponent.Owner;
                var boat = _transform.GetParentUid(sailEntity);
                if (!EntityManager.TryGetComponent(boat, out PhysicsComponent? boatBody))
                    continue;

                // Получаем углы в радианах
                float sailAngleRad = (float)_transform.GetWorldRotation(sailEntity).Theta;
                float boatAngleRad = (float)_transform.GetWorldRotation(boat).Theta;
                float windAngleRad = _windAngle * MathF.PI / 180f; // ветер в радианах

                // Направление ветра как вектор
                Vector2 windDirection = new Vector2(
                    MathF.Cos(windAngleRad),
                    MathF.Sin(windAngleRad)
                );

                // Нормаль паруса — перпендикуляр к его поверхности (наружу)
                // Парус — плоскость, нормаль — направление, в которое он "ловит" ветер
                var sailNormalAngleRad = sailAngleRad + MathF.PI / 2f;
                var sailNormal = new Vector2(
                    MathF.Cos(sailNormalAngleRad),
                    MathF.Sin(sailNormalAngleRad)
                );

                // Угол между ветром и нормалью паруса
                var dot = Math.Clamp(Vector2.Dot(windDirection, sailNormal), -1f, 1f);
                var angleBetween = MathF.Acos(dot);

                // Эффективность: максимальна, когда ветер перпендикулярен парусу (0°)
                // Минимальна, когда ветер параллелен (90°+)
                var efficiency = MathF.Cos(angleBetween);

                // Если ветер дует сзади (угол > 90°), всё равно даём слабую тягу
                // (реальные паруса могут работать и на "бейсинге")
                if (angleBetween > MathF.PI / 2f)
                    efficiency = MathF.Max(0.05f, efficiency); // 5% тяги сзади
                else
                    efficiency = MathF.Max(0f, efficiency);

                // Сила ветра = базовая сила × площадь × эффективность
                var forceMagnitude = _windForce * sailComponent.SailSize * efficiency;

                // Направление силы — вдоль нормали паруса
                var forceDirection = sailNormal * forceMagnitude;

                // Крутящий момент: сила × плечо × sin(разница между парусом и кораблём)
                // Плечо — расстояние от центра корабля до центра паруса (условное)
                var sailOffset = 0.5f; // метры — можно настроить в компоненте
                var torqueFactor = MathF.Sin(sailAngleRad - boatAngleRad); // как сильно парус "выступает" вбок
                var torque = forceMagnitude * sailOffset * torqueFactor * 0.1f; // масштабируем

                var windEffect = new WindEffect
                {
                    PushForce = forceDirection,
                    RotationTorque = torque
                };

                Push(sailEntity, sailComponent, windEffect);
            }
        }
    }

    private void Push(EntityUid sail, SailComponent sailComponent, WindEffect windForce)
    {
        var boat = _transform.GetParentUid(sail);

        var entities = _lookup.GetEntitiesIntersecting(boat);

        if (entities.Count > 1000)
            return;

        var weight = _rdWeight.GetTotal(boat);

        foreach (var entity in entities)
        {
            if (HasComp<RDWeightComponent>(entity))
                weight += _rdWeight.GetTotal(entity);
        }

        if (weight == 0)
            weight = 10;
        var impulse = windForce.PushForce;
        var angleimpulse = windForce.RotationTorque;
        if (EntityManager.TryGetComponent(boat, out PhysicsComponent? body))
        {
            _physics.WakeBody(boat);
            _physics.ApplyLinearImpulse(boat, impulse, body: body);
            _physics.ApplyAngularImpulse(boat, angleimpulse);
        }
    }
}
