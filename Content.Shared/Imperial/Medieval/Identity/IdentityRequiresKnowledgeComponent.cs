using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Identity;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class IdentityRequiresKnowledgeComponent : Component
{
    /// <summary>
    /// Идентификатор, который отображается в скобках. Является уникальным, начинает отсчёт с 1 после рестарта сервера.
    /// </summary>
    [AutoNetworkedField]
    public int Identifier = 0;

    /// <summary>
    /// Известные идентификаторы.
    /// </summary>
    [AutoNetworkedField]
    public List<int> KnownIds = new();
}
