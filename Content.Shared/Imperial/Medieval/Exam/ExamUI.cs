using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Exam;

[Serializable, NetSerializable]
public sealed class ExamBuiState : BoundUserInterfaceState
{
    public readonly ProtoId<ExamPrototype> ProtoId;

    public ExamBuiState(ProtoId<ExamPrototype> protoId)
    {
        ProtoId = protoId;
    }
}
