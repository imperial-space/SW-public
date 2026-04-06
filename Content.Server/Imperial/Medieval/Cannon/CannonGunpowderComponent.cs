using Robust.Shared.Audio;

namespace Content.Server.Imperial.Medieval.Cannon;

[RegisterComponent, ComponentProtoName("CanonGunpowder")]
public sealed partial class CannonGunpowderComponent : Component
{
    [DataField("insertSound")]
    public SoundSpecifier? InsertSound;
}
