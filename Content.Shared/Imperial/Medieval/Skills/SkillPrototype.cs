using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Serilog.Events;

namespace Content.Shared.Imperial.Medieval.Skills;

[Prototype]
public sealed partial class SkillPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; } = default!;

    [DataField(required: true)]
    public string Name { get; } = default!;

    [DataField]
    public Color Color = Color.White;

    [DataField]
    public Dictionary<string, float> Modifiers = new();

    [DataField]
    public Dictionary<string, float> Cooldowns = new();
}
