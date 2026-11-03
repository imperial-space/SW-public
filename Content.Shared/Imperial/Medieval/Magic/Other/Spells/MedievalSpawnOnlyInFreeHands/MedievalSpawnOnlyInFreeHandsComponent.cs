namespace Content.Shared.Imperial.Medieval.Magic.MedievalSpawnOnlyInFreeHands;


/// <summary>
/// Don't spawn if you don't have enough free hands
/// </summary>
[RegisterComponent]
public sealed partial class MedievalSpawnOnlyInFreeHandsComponent : Component
{
    [DataField]
    public int Count = 1;

    [DataField]
    public LocId? NotEnoughMessage;
}
