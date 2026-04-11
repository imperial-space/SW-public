using Content.Shared.Store;
using Content.Shared.Preferences;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Courier;

[Serializable, NetSerializable]
public enum courierUiKey : byte
{
    key,
}

[Serializable, NetSerializable]
public sealed class CourierUpdateState : BoundUserInterfaceState
{
    public readonly int Balance;
    public readonly int DeliveryPoints;
    public readonly int FreeMailsCount;
    public readonly string GuildNameLoc;
    public readonly List<CourierTradeOffer> Offers;
    public readonly ProtoId<CurrencyPrototype> Currency;

    public CourierUpdateState(
        int balance,
        int deliveryPoints,
        int freeMailsCount,
        string guildNameLoc,
        List<CourierTradeOffer> offers,
        ProtoId<CurrencyPrototype> currency)
    {
        Balance = balance;
        DeliveryPoints = deliveryPoints;
        FreeMailsCount = freeMailsCount;
        GuildNameLoc = guildNameLoc;
        Offers = offers;
        Currency = currency;
    }
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class CourierTradeOffer
{
    [DataField(required: true)]
    public EntProtoId ProductEntity = "Paper";

    [DataField]
    public string? DescriptionLoc;

    [DataField]
    public int BalanceCost;

    [DataField]
    public int DeliveryPointsCost;

    [DataField]
    public int FreeMailsCost;
}

[Serializable, NetSerializable]
public sealed class CourierRequestUpdateInterfaceMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class CourierBuyMessage : BoundUserInterfaceMessage
{
    public int OfferIndex;

    public CourierBuyMessage(int offerIndex)
    {
        OfferIndex = offerIndex;
    }
}

[Serializable, NetSerializable]
public sealed class CourierRequestWithdrawMessage : BoundUserInterfaceMessage
{
    public int Amount;

    public CourierRequestWithdrawMessage(int amount)
    {
        Amount = amount;
    }
}

[Serializable, NetSerializable]
public enum LetterRecipientUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class LetterRecipientBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly LetterRecipientData? Recipient;

    public LetterRecipientBoundUserInterfaceState(LetterRecipientData? recipient)
    {
        Recipient = recipient;
    }
}

[Serializable, NetSerializable]
public sealed class LetterRecipientData
{
    public readonly HumanoidCharacterProfile Profile;
    public readonly string JobName;
    public readonly string? JobId;

    public LetterRecipientData(HumanoidCharacterProfile profile, string jobName, string? jobId)
    {
        Profile = profile;
        JobName = jobName;
        JobId = jobId;
    }
}
