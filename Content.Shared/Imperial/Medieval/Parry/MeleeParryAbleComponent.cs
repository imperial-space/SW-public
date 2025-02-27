using Content.Shared.DoAfter;
using Robust.Shared.Serialization;
using Robust.Shared.GameStates;

namespace Content.Shared.MeleeParry.Components
{
    [RegisterComponent, NetworkedComponent]
    public sealed partial class MeleeParryAbleComponent : Component
    {
        [DataField]
        public float ParryModifier = 1f;
    }
}
