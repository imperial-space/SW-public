using Robust.Shared.Audio;

namespace Content.Server.Imperial.Audio;

/// <summary>
/// Компонент-триггер, который активируется при приближении
/// игрока(с PlayerLocationComponent) и запускает звук.
/// </summary>
[RegisterComponent]
public sealed partial class LocationTriggerComponent : Component
{
    /// <summary>
    /// Уникальный идентификатор локации.
    /// </summary>
    [DataField("locationId")]
    public string LocationId = string.Empty;

    /// <summary>
    /// Звук, который будет проигрываться при активации триггера.
    /// </summary>
    [DataField("sound")]
    public SoundSpecifier? Sound;

    /// <summary>
    /// Расстояние (в тайлах), на котором игрок активирует триггер.
    /// </summary>
    [DataField("activationDistance")]
    public float ActivationDistance = 8f;
}
