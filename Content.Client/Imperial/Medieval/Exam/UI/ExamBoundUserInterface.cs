using Content.Shared.Imperial.Medieval.Exam;
using Robust.Client.UserInterface;

namespace Content.Client.Imperial.Medieval.Exam.UI;

public sealed class ExamBoundUserInterface : BoundUserInterface
{
    private ExamWindow? _window;

    public ExamBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {

    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<ExamWindow>();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is ExamBuiState examBuiState)
            _window?.UpdateState(examBuiState);
    }
}
