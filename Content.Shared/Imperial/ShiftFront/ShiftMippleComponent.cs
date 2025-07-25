using Robust.Shared.GameStates;

namespace Content.Shared.ShiftFront.Components;

[RegisterComponent]
public sealed partial class ShiftMippleComponent : Component
{
    [DataField]
    public EntityUid? LinkedPlayer;
    [DataField]
    public EntityUid? LinkedMap;

}
// Components/ShiftFrontRequestComponent.cs
[RegisterComponent]
public sealed partial class ShiftFrontRequestComponent : Component
{
    [DataField("requesterName")]
    public string RequesterName = "";

    [DataField("requestTime")]
    public TimeSpan RequestTime = TimeSpan.Zero;

    [DataField("buildingTypeId")] // Используем ID прототипа вместо строки
    public string BuildingTypeId = "";

    [DataField("lightEntity")] // Айди маячка
    public string RequesterUid = "";

    [DataField("faction")]
    public string Faction = "";
}

// Components/ShiftFrontRequestConsoleComponent.cs
[RegisterComponent]
public sealed partial class ShiftFrontRequestConsoleComponent : Component
{
    [DataField("faction")]
    public string Faction = "";

    [ViewVariables]
    public List<EntityUid> ActiveRequests = new();
}
[RegisterComponent]
public sealed partial class ShiftFrontTrashComponent : Component
{
}
[RegisterComponent]
public sealed partial class ShiftFrontCoverComponent : Component
{
    [DataField]
    public float CoverChanse = 0.65f;
}
[RegisterComponent]
public sealed partial class ShiftFrontGunComponent : Component
{
}


