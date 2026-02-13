using Content.Shared.Imperial.Medieval.Courier;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.Imperial.Medieval.Courier;

[UsedImplicitly]
public sealed class LetterRecipientBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private LetterRecipientMenu? _menu;

    public LetterRecipientBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<LetterRecipientMenu>();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is LetterRecipientBoundUserInterfaceState cast)
            _menu?.Populate(cast);
    }
}
