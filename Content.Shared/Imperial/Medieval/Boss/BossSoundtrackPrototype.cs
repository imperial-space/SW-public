using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Boss;

[Prototype("bossSoundtrack")]
public sealed partial class BossSoundtrackPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public string Name = default!;
    [DataField(required: true)] public SoundSpecifier Intro = default!;
    [DataField(required: true)] public SoundSpecifier Loop = default!;
    [DataField(required: true)] public SoundSpecifier Outro = default!;
}
