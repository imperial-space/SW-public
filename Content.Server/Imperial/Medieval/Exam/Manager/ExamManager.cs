using System.Threading;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared.Imperial.Medieval.Exam;
using Content.Shared.Imperial.Medieval.Exam.Messages;
using Content.Shared.Imperial.Medieval.Exam.Prototypes;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Exam.Manager;

public sealed class ExamManager : IExamManager
{
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly INetManager _net = default!;

    public void Initialize()
    {
        _net.RegisterNetMessage<MsgExamSend>(OnExam);
        _net.RegisterNetMessage<MsgExamResult>();
        _net.RegisterNetMessage<MsgExamsRequest>(OnExamsRequest);
        _net.RegisterNetMessage<MsgExamsSend>();
    }

    private async void OnExam(MsgExamSend message)
    {
        var preferenceExams = await _db.GetPlayerPreferenceExamsAsync(message.MsgChannel.UserId, new CancellationToken());
        if (preferenceExams is null)
            return;

        if (!_prototype.TryIndex<ExamPrototype>(message.Exam, out var examPrototype))
            return;

        if (!preferenceExams.Data.TryGetValue(message.Exam, out var data))
        {
            data = new PlayerPreferenceExamsData(false, 0, DateTimeOffset.Now.Date);
            preferenceExams.Data[message.Exam] = data;
        }

        if (data.Passed)
            return;

        data.Attempts++;

        var incorrect = 0;
        foreach (var task in examPrototype.Tasks)
        {
            if (!_prototype.TryIndex(task, out var taskPrototype))
                continue;

            if (!message.Answers.TryGetValue(task, out var answer))
            {
                incorrect += examPrototype.Tasks.Count;
                break;
            }

            if (taskPrototype.Correct != answer)
                incorrect++;
        }

        data.Passed = incorrect <= examPrototype.MaxIncorrect;

        await _db.SavePlayerPreferenceExamsAsync(message.MsgChannel.UserId, preferenceExams);
        await SendState(message.MsgChannel);

        _net.ServerSendMessage(new MsgExamResult
        {
            Exam = message.Exam,
            Incorrect = incorrect,
            Passed = data.Passed,
        },
        message.MsgChannel);
    }

    private async void OnExamsRequest(MsgExamsRequest message)
    {
        await SendState(message.MsgChannel);
    }

    public async Task SendState(INetChannel channel)
    {
        var preferenceExams = await _db.GetPlayerPreferenceExamsAsync(channel.UserId, new CancellationToken())
            ?? PlayerPreferenceExams.Empty;

        _net.ServerSendMessage(new MsgExamsSend
        {
            Exams = preferenceExams,
        },
        channel);
    }
}
