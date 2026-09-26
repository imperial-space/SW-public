using Content.Shared.Imperial.Medieval.Praises;
using Robust.Shared.Network;

namespace Content.Client.Imperial.Medieval.Praises;

public sealed class PraiseSystem : EntitySystem
{
    private PraiseWindow? _praiseWindow;
    private PraiseViewWindow? _viewWindow;
    private PraiseRatingWindow? _ratingWindow;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<PraiseWindowMessage>(OnPraiseWindowMessage);
        SubscribeNetworkEvent<PraiseViewMessage>(OnPraiseViewMessage);
        SubscribeNetworkEvent<PraiseRatingMessage>(OnPraiseRatingMessage);
    }

    private void OnPraiseWindowMessage(PraiseWindowMessage ev)
    {
        if (_praiseWindow != null && !_praiseWindow.Disposed)
            _praiseWindow.Dispose();

        _praiseWindow = new();
        _praiseWindow.OnSendButtonPressed += reason => RaiseNetworkEvent(new PraiseWindowPraiseMessage { Reason = reason });
        _praiseWindow.OpenCentered();
        _praiseWindow.Update(ev);
    }

    private void OnPraiseViewMessage(PraiseViewMessage ev)
    {
        if (_viewWindow != null && !_viewWindow.Disposed)
            _viewWindow.Dispose();

        _viewWindow = new(ev.Records, ev.Admin, ev.Spam, false);
        _viewWindow.OnEditWeightButtonPressed += record => RaiseNetworkEvent(new PraiseViewEditMessage { Target = ev.Target, Record = record });
        _viewWindow.OnDeleteButtonPressed += record => RaiseNetworkEvent(new PraiseViewDeleteMessage { Target = ev.Target, Record = record });
        _viewWindow.OpenCentered();
    }

    private void OnPraiseRatingMessage(PraiseRatingMessage ev)
    {
        Log.Debug($"rating: Received 'PraiseRatingMessage', {ev.Rating.Count} items in rating.");

        if (_ratingWindow != null && !_ratingWindow.Disposed)
        {
            Log.Debug("rating: Window is not closed, doing it now.");
            _ratingWindow.Dispose();
        }

        Log.Debug("rating: Opening window.");
        _ratingWindow = new(ev.Rating);
        _ratingWindow.OpenCentered();
    }

    public void OpenView(NetUserId target)
    {
        if (_viewWindow != null && !_viewWindow.Disposed) //please wait message
            _viewWindow.Dispose();

        _viewWindow = new(new(), false, false, true);
        _viewWindow.OpenCentered();

        RaiseNetworkEvent(new PraiseViewOpenedMessage { Target = target });
    }

    public void OpenRating()
    {
        Log.Debug("rating: Sending 'PraiseRatingOpenedMessage' to server.");
        RaiseNetworkEvent(new PraiseRatingOpenedMessage());
    }

    public void SendPraise(NetUserId target, string reason, int weight)
    {
        RaiseNetworkEvent(new AddPraiseMessage() { Target = target, Reason = reason, Weight = weight });
    }
}
