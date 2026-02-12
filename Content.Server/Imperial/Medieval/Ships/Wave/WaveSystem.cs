using Content.Server.Destructible;
using Content.Shared.Construction.Conditions;
using Content.Shared.Damage;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Robust.Server.GameObjects;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Robust.Shared.Physics.Components;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.Ships.Wave;

public sealed class WaveSystem : EntitySystem
{
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly DestructibleSystem _destructible = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly PhysicsSystem _physics = default!; // <-- КЛЮЧЕВОЙ ИЗМЕНЕНИЕ

    private readonly Random _random = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<WaveComponent, StartCollideEvent>(OnCollide);
    }

    private void OnCollide(EntityUid uid, WaveComponent component, ref StartCollideEvent args)
    {
        if (TerminatingOrDeleted(uid) || TerminatingOrDeleted(args.OtherEntity))
            return;

        // Пропускаем, если уже толкали эту сущность
        if (component.HitList.Contains(args.OtherEntity))
            return;

        // --- 1. Толкаем сущность через PhysicsSystem ---
        if (_entityManager.TryGetComponent<PhysicsComponent>(args.OtherEntity, out var physics))
        {
            // Усиление толчка: 1.5x от силы волны
            var force = component.Strength * 1.5f;
            var impulse = component.Direction * force;

            // Правильный способ: через PhysicsSystem
            _physics.ApplyImpulse(args.OtherEntity, impulse);

            // Визуальный эффект: "мокрый"
            if (_entityManager.TryGetComponent<AppearanceComponent>(args.OtherEntity, out var appearance))
                appearance.SetState("wet", true);
        }

        // --- 2. Проверка: тайл на корабле? ---
        var otherPos = _entityManager.GetComponent<TransformComponent>(args.OtherEntity).Coordinates;
        var tileRef = _mapManager.GetTileRef(otherPos);

        // Проверяем, что это корабельная сетка
        if (!IsTileOnShip(tileRef.GridId))
            return;

        // Проверяем, что тайл — не дыра
        if (_entityManager.TryGetComponent<TileComponent>(tileRef.GridId, out var tileGrid))
        {
            var tile = tileGrid.GetTile(tileRef.TilePosition);

            if (IsTileHole(tile))
                return;

            // Шанс дыры: 20% при силе 2, 60% при силе 10
            float chance = Math.Clamp((float)component.Strength / 10f * 0.6f, 0.2f, 0.6f);
            if (_random.NextFloat() < chance)
            {
                MakeTileHole(tileRef.GridId, tileRef.TilePosition);
            }
        }

        // --- 3. Запоминаем удар ---
        component.HitList.Add(args.OtherEntity);
    }

    private bool IsTileOnShip(MapGridId gridId)
    {
        // Упрощённая проверка — замените на вашу логику (например, по компоненту ShipComponent)
        return gridId.ToString().StartsWith("ShipGrid_");
    }

    private bool IsTileHole(Tile tile)
    {
        // Замените на ваш тип дыры, если используете кастомный TileType
        return tile.Type == TileType.Hole;
    }

    private void MakeTileHole(MapGridId gridId, Vector2i tilePos)
    {
        if (!_entityManager.TryGetComponent<TileComponent>(gridId, out var tileComponent))
            return;

        tileComponent.SetTile(tilePos, new Tile(TileType.Hole));

        // Опционально: звук
        // _audio.PlayPvs("wave_hole.wav", gridId, tilePos);
    }
}
