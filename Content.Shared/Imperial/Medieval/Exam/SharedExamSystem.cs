using Content.Shared.Imperial.Medieval.Exam.Prototypes;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Exam;

public abstract class SharedExamSystem : EntitySystem
{
    public bool Pass(NetUserId userId, ProtoId<ExamPrototype> protoId)
    {
        return GetPased(userId).Contains(protoId);
    }

    protected abstract List<ProtoId<ExamPrototype>> GetPased(NetUserId userId);
}
