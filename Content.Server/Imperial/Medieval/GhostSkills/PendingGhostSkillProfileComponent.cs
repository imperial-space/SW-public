using Robust.Shared.Network;

namespace Content.Server.Imperial.Medieval.GhostSkills;

[RegisterComponent, Access(typeof(GhostSkillProfileSystem))]
public sealed partial class PendingGhostSkillProfileComponent : Component
{
    public NetUserId Player;

    public EntityUid OriginalEntity;

    public Dictionary<string, int> Levels = new();
}
