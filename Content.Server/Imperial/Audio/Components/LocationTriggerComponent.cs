using Robust.Shared.Audio;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics;

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
    /// ID фикстуры для проверки коллизий.
    /// </summary>
    [DataField("fixtureId")]
    public string FixtureId = "trigger";

    // TODO: Система день/ночь для эмбиента
    // [DataField("daySound")]
    // public SoundSpecifier? DaySound;
    // [DataField("nightStartHour")]
    // public int DayStartHour = 6;
    // [DataField("nightStartHour")]
    // public int NightStartHour = 18;
    // [DataField("enableTimeBasedAudio")]
    // public bool EnableTimeBasedAudio = false;
}
