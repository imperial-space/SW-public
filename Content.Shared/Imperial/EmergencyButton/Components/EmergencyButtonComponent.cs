using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Imperial.EmergencyButton.Components;

/// <summary>
/// Компонент для тревожной кнопки СБ.
/// При использовании отправляет сообщение в рацию СБ с информацией о местоположении.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[AutoGenerateComponentPause]
public sealed partial class EmergencyButtonComponent : Component
{
    /// <summary>
    /// Максимальное количество зарядов кнопки.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int MaxCharges = 1;

    /// <summary>
    /// Текущее количество зарядов кнопки.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int CurrentCharges = 1;

    /// <summary>
    /// Сообщение, которое будет отправлено в рацию.
    /// Поддерживает плейсхолдеры: {$officerName} - имя офицера, {$location} - местоположение
    /// </summary>
    [DataField]
    public string AlertMessage = "alert-emergency-button-message";

    /// <summary>
    /// Сообщение, показываемое пользователю при успешном использовании.
    /// </summary>
    [DataField]
    public string UseMessage = "alert-emergency-button-used";

    /// <summary>
    /// Сообщение, показываемое пользователю, когда заряды закончились.
    /// </summary>
    [DataField]
    public string NoChargesMessage = "alert-emergency-button-no-charges";

    /// <summary>
    /// Предупреждающее сообщение при первой активации (прайминге)
    /// </summary>
    [DataField]
    public string ConfirmationMessage = "alert-emergency-button-confirmation";

    /// <summary>
    /// Если не null, то это время когда кнопка может быть подтверждена
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan? NextConfirm;

    /// <summary>
    /// Если не null, то это время когда кнопка будет разпрайминена
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan? NextUnprime;

    /// <summary>
    /// Принудительная задержка между праймингом и подтверждением для предотвращения случайной активации
    /// </summary>
    [DataField]
    public TimeSpan ConfirmDelay = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Время после которого кнопка автоматически разпраймится
    /// </summary>
    [DataField]
    public TimeSpan PrimeTime = TimeSpan.FromSeconds(5);
}
