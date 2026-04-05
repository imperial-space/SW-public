using Content.Shared.Imperial.Medieval.Ships.ShipDrowning;

namespace Content.Shared.Imperial.Medieval.Ships.WaterPump;

/// <summary>
/// Система для работы с водой на корабле
/// </summary>
public sealed class SharedWaterOnShipSystem : EntitySystem
{
    /// <inheritdoc/>
    public override void Initialize()
    {

    }

    /// <summary>
    /// Убрать сколько то воды с корабля(отрицательное добавит)
    /// </summary>
    public void RemoveWater(EntityUid ship, int count)
    {
        if (!TryComp<ShipDrowningComponent>(ship, out var shipDrowningcomp))
            return;

        shipDrowningcomp.DrownLevel -= count;
    }
}
