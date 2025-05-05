using Content.Client.Imperial.Medieval.Exam.Manager;
using Content.Client.Imperial.Medieval.Exam.UI;
using Content.Shared.Imperial.Medieval.Exam.Prototypes;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Medieval.Exam;

public sealed class ExamUIController : UIController
{
    [Dependency] private readonly IExamManager _exam = default!;

    private ExamWindow _window = default!;

    public override void Initialize()
    {
        base.Initialize();

        _exam.ResultReceived += OnResult;
    }

    private void OnResult(string prototype, int incorrect, bool passed)
    {

    }

    public void OpenWindow()
    {
        EnsureWindow();

        _window.OpenCentered();
        _window.MoveToFront();
    }

    private void EnsureWindow()
    {
        if (_window is { Disposed: false })
            return;

        _window = UIManager.CreateWindow<ExamWindow>();
        _window.Sent += OnSent;
    }

    private void OnSent(ProtoId<ExamPrototype> exam, IReadOnlyDictionary<string, int> answers)
    {
        _exam.Send(exam, answers);
    }
}
