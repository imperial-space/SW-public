using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Factions;

[Serializable, NetSerializable]
public sealed class RecruitOfferEuiState : EuiStateBase
{
    public readonly string RecruiterName;
    public readonly string FactionName;

    public RecruitOfferEuiState(string recruiterName, string factionName)
    {
        RecruiterName = recruiterName;
        FactionName = factionName;
    }
}
