using Content.Shared.Paper;
using static Content.Shared.Paper.PaperComponent;

namespace Content.Client.Paper.UI;

/// <summary>Populates optional controls after the ordinary paper reader has updated.</summary>
public sealed class PaperUiUpdatedEvent(PaperWindow window, PaperBoundUserInterfaceState state,
    EntityUid? user, Action<BoundUserInterfaceMessage> send) : EntityEventArgs
{
    public PaperWindow Window = window;
    public PaperBoundUserInterfaceState State = state;
    public EntityUid? User = user;
    public Action<BoundUserInterfaceMessage> Send = send;
}

/// <summary>Lets paper UI extensions handle their own messages without changing the reader.</summary>
public sealed class PaperUiMessageReceivedEvent(PaperWindow window, BoundUserInterfaceMessage message) : EntityEventArgs
{
    public PaperWindow Window = window;
    public BoundUserInterfaceMessage Message = message;
}
