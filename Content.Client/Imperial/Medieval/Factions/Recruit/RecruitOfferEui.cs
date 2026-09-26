using Content.Client.Eui;
using Content.Shared.Eui;
using Content.Shared.Imperial.Medieval.Factions;
using JetBrains.Annotations;
using Robust.Client.Graphics;

namespace Content.Client.Imperial.Medieval.Factions.Recruit;

[UsedImplicitly]
public sealed class RecruitOfferEui : BaseEui
{
    private RecruitOfferWindow? _window;

    public override void HandleState(EuiStateBase state)
    {
        if (state is not RecruitOfferEuiState s)
            return;

        _window?.Close();

        _window = new RecruitOfferWindow(s.RecruiterName, s.FactionName);

        _window.DeclineButton.OnPressed += _ =>
        {
            SendMessage(new RecruitOfferChoiceMessage(RecruitOfferUiButton.Decline));
            _window.Close();
        };

        _window.OnClose += () => SendMessage(new RecruitOfferChoiceMessage(RecruitOfferUiButton.Decline));

        _window.AcceptButton.OnPressed += _ =>
        {
            SendMessage(new RecruitOfferChoiceMessage(RecruitOfferUiButton.Accept));
            _window.Close();
        };

        IoCManager.Resolve<IClyde>().RequestWindowAttention();
        _window.OpenCentered();
    }

    public override void Closed()
    {
        _window?.Close();
    }
}
