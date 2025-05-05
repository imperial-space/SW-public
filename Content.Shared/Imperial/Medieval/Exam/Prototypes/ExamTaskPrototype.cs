using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Exam.Prototypes;

[Prototype("examTask")]
public sealed class ExamTaskPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; } = string.Empty;

    [DataField]
    public LocId Description;

    [DataField]
    public List<LocId> Answers = new();

    [DataField]
    public int Correct;
}
