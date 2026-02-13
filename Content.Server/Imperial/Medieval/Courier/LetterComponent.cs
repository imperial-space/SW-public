using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Courier;

[RegisterComponent, ComponentProtoName("letterComponent")]
public sealed partial class LetterComponent : Component
{
    [DataField]
    public int FreeMailBuyBack;

    [DataField]
    public int DeliveryPointsBuyBack;

    [DataField]
    public int BalanceBuyBack;

    [DataField]
    public int DeliveryPointsReward;

    [DataField]
    public int BalanceReward;

    [DataField]
    public EntityUid? LastCourierHeld;

    [DataField]
    public EntityUid? Recipient;

    [DataField]
    public TimeSpan UrgentTimer = TimeSpan.Zero;

    [DataField]
    public EntProtoId? LetterContents = null;

    [DataField]
    public SoundSpecifier OpenSound = new SoundPathSpecifier("/Audio/Effects/unwrap.ogg");

    [DataField]
    public bool IsBox;

    [DataField]
    public bool IsUrgent;

    [DataField]
    public string IconSpriteState = "icon";

    [DataField]
    public string OpenedSpriteState = "opened";

    [DataField]
    public string TrashSpriteState = "destroyed";
}
