using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.XxRaay.FlagSystem;

/// <summary>
/// Компонент для механики захвата флага
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedFlagCaptureSystem))]
public sealed partial class FlagCaptureComponent : Component
{
    /// <summary>
    /// Радиус захвата флага в тайлах
    /// </summary>
    [DataField("captureRadius")]
    public float CaptureRadius = 2.0f;

    /// <summary>
    /// Время захвата в секундах
    /// </summary>
    [DataField("captureTime")]
    public TimeSpan CaptureTime = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Текущий прогресс захвата
    /// </summary>
    [DataField("captureProgress")]
    public TimeSpan CaptureProgress = TimeSpan.Zero;

    /// <summary>
    /// Идет ли процесс захвата
    /// </summary>
    [DataField("isBeingCaptured")]
    public bool IsBeingCaptured = false;

    /// <summary>
    /// Можно ли захватить этот флаг
    /// </summary>
    [DataField("canBeCaptured")]
    public bool CanBeCaptured = true;

    /// <summary>
    /// Последнее время проверки игроков рядом
    /// </summary>
    public TimeSpan LastCheckTime = TimeSpan.Zero;
}
