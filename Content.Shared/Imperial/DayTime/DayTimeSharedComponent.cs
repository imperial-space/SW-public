using Robust.Shared.Map.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations;

namespace Content.Shared.DayTime;
[RegisterComponent, NetworkedComponent]
public sealed partial class DayTimeComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly), DataField("colorFrom")]
    public Color ColorFrom;
    [ViewVariables(VVAccess.ReadOnly), DataField("colorTo")]
    public Color ColorTo;

    [ViewVariables(VVAccess.ReadOnly), DataField("colorCurrent")]
    public Color ColorCurrent;

    public List<(Color color, TimeSpan time)> StageColorTimeData = new();
    [ViewVariables(VVAccess.ReadWrite), DataField("presetID")]
    public string PresetID = "default";

    [ViewVariables(VVAccess.ReadOnly), DataField("currentStage")] // Текущий этап цвета
    public int CurrentStage = 0;

    [ViewVariables(VVAccess.ReadOnly), DataField("stageUpdateTime")] // Время
    public TimeSpan StageUpdateTime;
    [ViewVariables(VVAccess.ReadOnly), DataField("colorUpdateTime")] // Таймер
    public TimeSpan ColorUpdateTime;
    [ViewVariables(VVAccess.ReadOnly), DataField("colorUpdateSpeed")] // Скорость обновления цвета
    public TimeSpan ColorUpdateSpeed = TimeSpan.FromSeconds(0.5f);
    [ViewVariables(VVAccess.ReadWrite), DataField("groupID")] // Группа для обновления цвета
    public string GroupID = "0";
    [ViewVariables(VVAccess.ReadOnly), DataField("currentLocalTime")]
    public float CurrentLocalTime = 0f;
    [ViewVariables(VVAccess.ReadOnly), DataField("totalLocalTime")]
    public float TotalLocalTime = 0f;
    [ViewVariables(VVAccess.ReadOnly), DataField("totalLocalTime2")]
    public float TotalLocalTime2 = 0f;
}

