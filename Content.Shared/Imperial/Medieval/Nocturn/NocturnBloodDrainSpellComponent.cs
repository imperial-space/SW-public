using Robust.Shared.GameStates;

namespace Content.Shared.Nocturn.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class NocturnBloodDrainSpellComponent : Component
{
    [DataField(required: true)]
    public float BloodDrain;
}
