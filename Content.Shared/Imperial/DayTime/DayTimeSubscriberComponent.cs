namespace Content.Shared.Imperial.DayTime;
[RegisterComponent]
public sealed partial class DayTimeSubscriberComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField("groupID")]
    public string GroupID = "0"; // Группа для обновления цвета
}