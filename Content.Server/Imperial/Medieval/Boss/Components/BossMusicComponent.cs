using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Content.Shared.Imperial.Medieval.Boss;

namespace Content.Server.Imperial.Medieval.Boss;

[RegisterComponent]
public sealed partial class BossMusicComponent : Component
{
    [DataField]
    public ProtoId<BossSoundtrackPrototype>? Soundtrack;
    [DataField]
    public SoundSpecifier? Intro;
    [DataField]
    public SoundSpecifier? Loop;
    [DataField]
    public SoundSpecifier? Outro;
}
