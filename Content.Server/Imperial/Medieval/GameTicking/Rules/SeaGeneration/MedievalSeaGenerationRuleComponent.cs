namespace Content.Server.Imperial.Medieval.GameTicking.Rules;

[RegisterComponent, Access(typeof(MedievalSeaGenerationRuleSystem))]
public sealed partial class MedievalSeaGenerationRuleComponent : Component
{
    [ViewVariables]
    public bool Executed;
}
