using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.GhostSkills;

public sealed partial class OpenGhostSkillsActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed class OpenGhostSkillsMenuMessage(Dictionary<string, int> levels) : EntityEventArgs
{
    public readonly Dictionary<string, int> Levels = levels;
}

[Serializable, NetSerializable]
public sealed class SaveGhostSkillsMessage(Dictionary<string, int> levels) : EntityEventArgs
{
    public readonly Dictionary<string, int> Levels = levels;
}

[Serializable, NetSerializable]
public sealed class GhostSkillsSavedMessage : EntityEventArgs;
