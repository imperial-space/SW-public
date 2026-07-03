using Robust.Shared.GameStates;

namespace Content.Shared.Ratling;

/// <summary>
/// Добавляется к вонючим существам, когда носитель BadSmellVisionComponent находится рядом.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BadSmellMarkerComponent : Component
{
    [ViewVariables]
    public HashSet<EntityUid> Viewers = new();
}
