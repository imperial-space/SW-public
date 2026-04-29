using Robust.Shared.Audio;

namespace Content.Server.Imperial.Medieval.Cannon;

[RegisterComponent, ComponentProtoName("Ramrod")]
public sealed partial class RamrodComponent : Component
{
    [DataField("actionSound")]
    public SoundSpecifier? ActionSound;
}
