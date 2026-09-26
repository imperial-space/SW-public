using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Factions;

[Serializable, NetSerializable]
public enum RecruitOfferUiButton
{
    Decline,
    Accept,
}

[Serializable, NetSerializable]
public sealed class RecruitOfferChoiceMessage : EuiMessageBase
{
    public readonly RecruitOfferUiButton Button;

    public RecruitOfferChoiceMessage(RecruitOfferUiButton button)
    {
        Button = button;
    }
}
