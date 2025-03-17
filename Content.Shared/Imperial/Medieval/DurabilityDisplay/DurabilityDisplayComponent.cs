using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.DurabilityDisplay.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DurabilityDisplayComponent : Component
{
    public enum Durability : byte
    {
        Up,
        Full,
        AlmostFull,
        Damaged,
        BadlyDamaged,
        Broken
    }
    [ViewVariables(VVAccess.ReadWrite), DataField(required: true), AutoNetworkedField]
    public Durability Dub = Durability.Full;
}
