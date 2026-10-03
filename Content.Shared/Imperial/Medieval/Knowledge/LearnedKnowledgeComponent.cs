using Robust.Shared.GameStates;
using Content.Shared.Imperial.Medieval.Language;

namespace Content.Shared.Imperial.Medieval.Knowledge;

/// <summary>Authoritative on a mind, mirrored on its current body for predicted checks.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class LearnedKnowledgeComponent : Component
{
    [DataField, AutoNetworkedField] public HashSet<string> Knowledge = new();
    // Owned by the mind (or a body before it receives a mind); action entities stay in the current body.
    public Dictionary<string, EntityUid> GrantedActions = new();
    public Dictionary<string, LanguageKnowledge?> OriginalLanguages = new();
}

/// <summary>Merge-only grants from professions, never replaces a character's knowledge.</summary>
[RegisterComponent]
public sealed partial class InitialKnowledgeComponent : Component
{
    [DataField] public List<string> Knowledge = new();
}
