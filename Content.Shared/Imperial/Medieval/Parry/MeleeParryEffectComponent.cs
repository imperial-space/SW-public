using Robust.Shared.GameStates;

namespace Content.Shared.MeleeParry.Components
{
    [RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
    public sealed partial class MeleeParryEffectComponent : Component
    {
        [DataField]
        public bool PhaseControlled;

        [AutoNetworkedField]
        public TimeSpan AnimationStartTime;
    }
}
