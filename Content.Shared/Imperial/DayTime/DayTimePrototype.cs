using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.DayTime;


[Prototype]
public sealed partial class DayTimePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; set; } = default!;
    [DataField("colorStages")]
    public List<string> ColorStages = new();
    [DataField("timeStages")]
    public List<int> TimeStages = new();
}
