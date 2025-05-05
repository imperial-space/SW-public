using Content.Shared.Imperial.Medieval.Exam;
using Content.Shared.Imperial.Medieval.Exam.Prototypes;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Exam;

public sealed class ExamSystem : SharedExamSystem
{
    protected override List<ProtoId<ExamPrototype>> GetPased(NetUserId userId)
    {
        return [];
    }
}
