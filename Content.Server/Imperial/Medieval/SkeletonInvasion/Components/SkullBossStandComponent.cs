using Robust.Shared.Audio;

namespace Content.Server.Imperial.Medieval.SkeletonInvasion;

[RegisterComponent]
public sealed partial class SkullBossStandComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    public Dictionary<int, bool> AttachedParts = new();

    [DataField]
    public Dictionary<int, string> Announcements = new();

    [DataField]
    public SoundSpecifier? AttachSound;

    [DataField]
    public SoundSpecifier? CompleteSound;

    [DataField]
    public int RequiredParts = 8;
}
