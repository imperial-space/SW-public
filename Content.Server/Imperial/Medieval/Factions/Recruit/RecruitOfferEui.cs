using Content.Server.EUI;
using Content.Server.Imperial.Medieval.Factions;
using Content.Shared.Eui;
using Content.Shared.Imperial.Medieval.Factions;
using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Factions.Recruit;

public sealed class RecruitOfferEui : BaseEui
{
    private readonly MedievalFactionsSystem _factions;
    private readonly EntityUid _target;
    private readonly EntityUid _recruiter;
    private readonly ProtoId<MedievalFactionPrototype> _faction;
    private readonly string _recruiterName;
    private readonly string _factionName;

    public RecruitOfferEui(MedievalFactionsSystem factions, EntityUid target, EntityUid recruiter, ProtoId<MedievalFactionPrototype> faction, string recruiterName, string factionName)
    {
        _factions = factions;
        _target = target;
        _recruiter = recruiter;
        _faction = faction;
        _recruiterName = recruiterName;
        _factionName = factionName;
    }

    public override void Opened()
    {
        StateDirty();
    }

    public override EuiStateBase GetNewState()
    {
        return new RecruitOfferEuiState(_recruiterName, _factionName);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is not RecruitOfferChoiceMessage choice)
            return;

        if (choice.Button == RecruitOfferUiButton.Accept)
            _factions.AcceptRecruitOffer(_target, _recruiter, _faction);
        else
            _factions.DeclineRecruitOffer(_target, _recruiter, _faction);

        Close();
    }
}
