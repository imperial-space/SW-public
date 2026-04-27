using Robust.Shared.GameStates;
using Robust.Shared.Timing;

namespace Content.Shared.MeleeParry.Components
{
    [RegisterComponent, NetworkedComponent]
    public sealed partial class MeleeParryComponent : Component
    {
        [ViewVariables(VVAccess.ReadOnly)]
        public EntityUid? LastSuccessParriedAttacker; //Нужно для парирования урона стамине, после этого оно обнулится

        [ViewVariables(VVAccess.ReadOnly)]
        public TimeSpan LastSuccessParriedTime;  //Нужно для парирования урона стамине, после этого оно обнулится

        [DataField]
        [ViewVariables(VVAccess.ReadOnly)]
        public TimeSpan NextAllowedParryTime;

        [DataField]
        [ViewVariables(VVAccess.ReadOnly)]
        public TimeSpan ParriedTime = TimeSpan.Zero;

        [DataField]
        [ViewVariables(VVAccess.ReadWrite)]
        public float ParryCooldown = 4f;

        [DataField]
        public string ParryEffect = "MedievalEffectParry";

        [DataField]
        public bool RealParry = true;

        [ViewVariables(VVAccess.ReadWrite)]
        public float ParryWindow = 0.5f;
    }

    [RegisterComponent, NetworkedComponent]
    public sealed partial class MeleeParryStaminaComponent : Component
    {
        [DataField]
        public float ParryChanse = 0.75f;

        [DataField]
        public float ParriedAgo = 0.5f;

        [DataField]
        public float ParriedTime = 0.5f;
    }
}
