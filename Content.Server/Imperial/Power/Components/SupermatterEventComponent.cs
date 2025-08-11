using Robust.Shared.Prototypes;
using Content.Shared.Radio;

namespace Content.Server.Imperial.Power.Components;

[RegisterComponent]
public sealed partial class SupermatterEventComponent : Component
{
    /// <summary>
    /// Значения видов событий суперматерии.
    /// </summary>
    public enum SupermatterEventType
    {
        None = 0,
        Lightning = 1,
        Radiation = 2,
        Plasma = 3,
    }

    /// <summary>
    /// Время до следующего случайного события (в секундах).
    /// </summary>
    [DataField]
    public TimeSpan NextEventTimer = TimeSpan.Zero;

    /// <summary>
    /// Текущий активный тип события.
    /// </summary>
    [DataField]
    public SupermatterEventType CurrentEvent = SupermatterEventType.None;

    /// <summary>
    /// Время окончания текущего события (0, если событие не активно).
    /// </summary>
    public TimeSpan EventEndTime = TimeSpan.Zero;

    /// <summary>
    /// Кулдаун между всплесками молний.
    /// </summary>
    public TimeSpan LightningCooldown = TimeSpan.Zero;

    /// <summary>
    /// Таймер для генерации плазмы во время события Plasma.
    /// </summary>
    public TimeSpan? PlasmaTickAccumulator = null;

    /// <summary>
    /// Список разрешенных типов событий для этого кристалла.
    /// </summary>
    [DataField]
    public List<SupermatterEventType> AllowedEventTypes { get; set; } =
    [
        SupermatterEventType.None,
        SupermatterEventType.Lightning,
        SupermatterEventType.Radiation,
        SupermatterEventType.Plasma,
    ];

    /// <summary>
    /// Радио каналы для оповещений о событиях.
    /// </summary>
    [DataField]
    public ProtoId<RadioChannelPrototype>[] RadioChannels = ["Engineering"];

    /// <summary>
    /// Задержка перед первым событием (в секундах).
    /// </summary>
    [DataField]
    public float InitialEventDelaySeconds = 900f; // 15 минут

    /// <summary>
    /// Время жизни кэша консоли (в секундах).
    /// </summary>
    public readonly float ConsoleCacheLifetime = 10f;

    /// <summary>
    /// Длительность события None (в секундах).
    /// </summary>
    [DataField]
    public float NoneEventDuration = 300f; // 5 минут

    // LightningEvent

    /// <summary>
    /// Длительность события LightningEvent (в секундах).
    /// </summary>
    [DataField]
    public float LightningEventDuration = 120f; // 2 минуты

    /// <summary>
    /// Кулдаун между молниями во время события LightningEvent (в секундах).
    /// </summary>
    [DataField]
    public float LightningCooldownDuration = 8f; // 8 секунд

    /// <summary>
    /// Минимальное время до следующего события после LightningEvent (в секундах).
    /// </summary>
    public readonly float LightningMinNextEvent = 180f; // 3 минуты

    /// <summary>
    /// Максимальное время до следующего события после LightningEvent (в секундах).
    /// </summary>
    public readonly float LightningMaxNextEvent = 420f; // 7 минут

    /// <summary>
    /// Количество молний, выпускаемых за один раз при LightningEvent.
    /// </summary>
    [DataField]
    public int LightningBoltCount = 1; // 7 минут

    /// <summary>
    /// Радиус, в котором молнии будут выпускаться при LightingEvent.
    /// </summary>
    [DataField]
    public float LightningBoltRadius = 8f; // 7 минут

    // RadiationEvent

    /// <summary>
    /// Длительность RadiationEvent (в секундах).
    /// </summary>
    [DataField]
    public float RadiationEventDuration = 120f; // 2 минуты

    /// <summary>
    /// Интенсивность радиации во время события RadiationEvent.
    /// </summary>
    [DataField]
    public float RadiationIntensity = 10f;

    /// <summary>
    /// Минимальное время до следующего события после RadiationEvent (в секундах).
    /// </summary>
    public readonly float RadiationMinNextEvent = 180f; // 3 минуты

    /// <summary>
    /// Максимальное время до следующего события после RadiationEvent (в секундах).
    /// </summary>
    public readonly float RadiationMaxNextEvent = 420f; // 7 минут

    // PlasmaEvent

    /// <summary>
    /// Длительность PlasmaEvent (в секундах).
    /// </summary>
    [DataField]
    public float PlasmaEventDuration = 120f; // 2 минуты

    /// <summary>
    /// Минимальное время до следующего события после PlasmaEvent (в секундах).
    /// </summary>
    public readonly float PlasmaMinNextEvent = 180f; // 3 минуты

    /// <summary>
    /// Максимальное время до следующего события после PlasmaEvent (в секундах).
    /// </summary>
    public readonly float PlasmaMaxNextEvent = 420f; // 7 минут

    /// <summary>
    /// Интервал генерации плазмы во время PlasmaEvent (в секундах).
    /// </summary>
    public readonly float PlasmaTickInterval = 10f; // 10 секунд

    /// <summary>
    /// Количество молей плазмы, генерируемых за тик.
    /// </summary>
    [DataField]
    public float PlasmaMolesAmount = 5f;

    /// <summary>
    /// Температура хотспота плазмы при PlasmaEvent.
    /// </summary>
    [DataField]
    public float PlasmaHotspotTemperature = 1500f;

    /// <summary>
    /// Объем хотспота плазмы при PlasmaEvent.
    /// </summary>
    [DataField]
    public float PlasmaHotspotVolume = 50f;

    // Default radiation

    /// <summary>
    /// Базовая интенсивность радиации вне событий.
    /// </summary>
    [DataField]
    public float DefaultRadiationIntensity = 5f;


    public TimeSpan ConsoleCacheTimer = TimeSpan.Zero;
    public TimeSpan LastConsoleCacheUpdate = TimeSpan.Zero;
    public TimeSpan LastEventEndTimeUpdate = TimeSpan.Zero;
    public TimeSpan LastNextEventTimerUpdate = TimeSpan.Zero;
    public TimeSpan LastLightningCooldownUpdate = TimeSpan.Zero;
    public TimeSpan LastPlasmaTickUpdate = TimeSpan.Zero;
}
