using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Crook.Components
{
    /// <summary>
    /// Если добавить этот компонент на контейнер, металлодетектор будет игнорировать его и всё содержимое.
    /// </summary>
    [RegisterComponent, NetworkedComponent]
    public sealed partial class IgnoreMetalDetectorComponent : Component
    {
    }
}
