using System.Diagnostics.CodeAnalysis;
using Content.Shared.Imperial.Medieval.Exam;
using Content.Shared.Imperial.Medieval.Exam.Messages;
using Content.Shared.Imperial.Medieval.Exam.Prototypes;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Medieval.Exam.Manager;

public sealed class ExamManager : IExamManager
{
    [Dependency] private readonly INetManager _net = default!;

    public event Action<PlayerPreferenceExams>? ExamsReceived;
    public event Action<string, int, bool>? ResultReceived;

    public void Initialize()
    {
        _net.RegisterNetMessage<MsgExamsRequest>();
        _net.RegisterNetMessage<MsgExamsSend>(OnExams);
        _net.RegisterNetMessage<MsgExamSend>();
        _net.RegisterNetMessage<MsgExamResult>(OnExamResult);
    }

    private void OnExams(MsgExamsSend message)
    {
        ExamsReceived?.Invoke(message.Exams);
    }

    private void OnExamResult(MsgExamResult message)
    {
        ResultReceived?.Invoke(message.Exam, message.Incorrect, message.Passed);
    }

    public void Request()
    {
        _net.ClientSendMessage(new MsgExamsRequest());
    }

    [SuppressMessage("ReSharper", "SuspiciousTypeConversion.Global")]
    public void Send(ProtoId<ExamPrototype> exam, IReadOnlyDictionary<string, int> answers)
    {
        var msg = new MsgExamSend
        {
            Exam = exam,
            Answers = answers,
        };

        _net.ClientSendMessage(msg);
    }
}
