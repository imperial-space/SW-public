using Robust.Client.UserInterface.Controllers;

namespace Content.Client.Imperial.Medieval.Exam.UI;

public sealed class ExamUIController : UIController
{
    private ExamWindow _window = default!;

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
    }
}
