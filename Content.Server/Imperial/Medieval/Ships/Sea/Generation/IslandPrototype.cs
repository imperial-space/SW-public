using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Ships.Sea.Generation;

/// <summary>
/// Прототип острова — содержит конфигурацию для генерации.
/// Используется для определения типа, размера и других параметров.
/// </summary>
[Prototype("island")]
public sealed partial class IslandPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; } = default!;

    /// <summary>
    /// Размер острова в тайлах (например, 1 = 1x1, 2 = 2x2, 3 = 3x3).
    /// </summary>
    [DataField("size", required: true)]
    public int Size { get; private set; }

    /// <summary>
    /// Множитель веса для случайной генерации (если нужно балансировать частоту).
    /// </summary>
    [DataField("spawnWeight")]
    public int SpawnWeight { get; private set; } = 1;

    /// <summary>
    /// Опциональное имя для логирования или интерфейса (не используется в спавне).
    /// </summary>
    [DataField("name", required: false)]
    public string? Name { get; private set; }
}
