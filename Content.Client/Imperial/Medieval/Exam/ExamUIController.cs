using Content.Client.Imperial.Medieval.Exam.Manager;
using Content.Client.Imperial.Medieval.Exam.UI;
using Content.Shared.Imperial.Medieval.Exam.Prototypes;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Medieval.Exam;

public sealed class ExamUIController : UIController
{
    [Dependency] private readonly IExamManager _exam = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private ExamWindow _window = default!;
    private ExamResultWindow _windowResult = default!;

    public override void Initialize()
    {
        base.Initialize();

        _exam.ResultReceived += OnResult;
    }

    private void OnResult(string prototype, int incorrect, bool passed)
    {
        OpenResultWindow(prototype, incorrect, passed);
    }

    public void OpenWindow(ProtoId<ExamPrototype> protoId)
    {
        EnsureWindow();

        if (!_prototype.TryIndex(protoId, out var prototype))
            return;

        _window.SetExam(prototype);
        _window.OpenCentered();
        _window.MoveToFront();
    }

    public void OpenResultWindow(string prototype, int incorrect, bool passed)
    {
        EnsureWindow();

        _windowResult.Refresh(prototype, incorrect, passed);
        _windowResult.OpenCentered();
        _windowResult.MoveToFront();
    }

    private void EnsureWindow()
    {
        if (_window is not { Disposed: false })
        {
            _window = UIManager.CreateWindow<ExamWindow>();
            _window.Sent += OnSent;
        }

        if (_windowResult is not { Disposed: false })
        {
            _windowResult = UIManager.CreateWindow<ExamResultWindow>();
        }
    }

    private void OnSent(ProtoId<ExamPrototype> exam, IReadOnlyDictionary<string, int> answers)
    {
        _exam.Send(exam, answers);
    }
}
