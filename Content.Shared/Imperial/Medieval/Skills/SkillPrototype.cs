using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Skills;

[Prototype("medievalSkill")]
public sealed partial class SkillPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string Name { get; private set; } = default!;

    [DataField]
    public Color Color = Color.White;

    [DataField]
    public Dictionary<string, float> Modifiers = new();

    [DataField]
    public string RsiPath = "/Textures/Interface/Misc/job_icons.rsi";

    [DataField]
    public Dictionary<int, string> Icons = new()
    {
        { 1, "Admin" }
    };
}
