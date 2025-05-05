using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.Exam;

[Prototype("exam")]
public sealed partial class ExamPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; } = string.Empty;

    [DataField]
    public LocId Name;

    [DataField]
    public ResPath IconPath = ResPath.Empty;

    [DataField]
    public List<ProtoId<ExamTaskPrototype>> Tasks = new();
}
