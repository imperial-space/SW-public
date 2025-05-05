using Content.Client.Imperial.Medieval.Exam.Manager;
using Content.Shared.Imperial.Medieval.Exam;
using Content.Shared.Imperial.Medieval.Exam.Prototypes;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Medieval.Exam;

public sealed class ExamSystem : SharedExamSystem
{
    [Dependency] private readonly IExamManager _exam = default!;

    private readonly List<ProtoId<ExamPrototype>> _passedCache = new();
    private PlayerPreferenceExams? _cache;

    public override void Initialize()
    {
        base.Initialize();

        _exam.Request();
        _exam.ExamsReceived += OnReceived;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _exam.ExamsReceived -= OnReceived;
    }

    private void OnReceived(PlayerPreferenceExams exams)
    {
        _cache = exams;

        _passedCache.Clear();
        foreach (var (prototype, data) in exams.Data)
        {
            if (data.Passed)
                _passedCache.Add((ProtoId<ExamPrototype>) prototype);
        }
    }

    protected override List<ProtoId<ExamPrototype>> GetPased(NetUserId userId)
    {
        return _passedCache;
    }
}
