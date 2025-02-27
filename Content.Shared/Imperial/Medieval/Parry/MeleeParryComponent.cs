using Robust.Shared.GameStates;

namespace Content.Shared.MeleeParry.Components
{
    [RegisterComponent, NetworkedComponent]
    public sealed partial class MeleeParryComponent : Component
    {
        [DataField]
        public float ParryChanse = 0.6f;

        [DataField]
        public float ParriedAgo = 3.5f;

        [DataField]
        public float ParriedTime = 3.5f;

        [DataField]
        public string ParryEffect = "MedievalEffectParry";

        [DataField]
        public bool RealParry = true;
    }

    [RegisterComponent, NetworkedComponent]
    public sealed partial class MeleeParryStaminaComponent : Component
    {
        [DataField]
        public float ParryChanse = 0.75f;

        [DataField]
        public float ParriedAgo = 2.5f;

        [DataField]
        public float ParriedTime = 2.5f;
    }
}
