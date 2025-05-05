using Content.Shared.Imperial.Medieval.Exam;
using Content.Shared.Imperial.Medieval.Exam.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Medieval.Exam.Manager;

public interface IExamManager
{
    event Action<PlayerPreferenceExams>? ExamsReceived;
    event Action<string, int, bool>? ResultReceived;
    void Initialize();
    void Request();
    void Send(ProtoId<ExamPrototype> exam, IReadOnlyDictionary<string, int> answers);
}
