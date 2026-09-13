using Robust.Shared.GameObjects;

namespace Content.Shared.Imperial.Medieval.Calendar;

[RegisterComponent]
public sealed partial class CalendarSpawnMarkerComponent : Component
{
    [DataField("markerId")]
    public string MarkerId { get; set; } = string.Empty;
}
