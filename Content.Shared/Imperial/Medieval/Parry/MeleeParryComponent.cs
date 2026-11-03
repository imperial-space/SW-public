using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.MeleeParry.Components
{
    [RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
    public sealed partial class MeleeParryComponent : Component
    {
        [DataField, AutoNetworkedField] // reconcile predicted parry timing.
        [ViewVariables(VVAccess.ReadOnly)]
        public TimeSpan ParriedTime = TimeSpan.Zero;

        [DataField]
        public string ParryEffectSuccess = "MedievalEffectSuccessParry";
        [DataField]
        public string ParryEffectWindow = "MedievalEffectWindowParry";

        [DataField]
        public SoundSpecifier ParryWindowSound = new SoundCollectionSpecifier("MeleeParryWindow");

        [DataField, AutoNetworkedField]
        [ViewVariables(VVAccess.ReadWrite)]
        public float ParryWindow = 0.15f;

        [DataField, AutoNetworkedField]
        [ViewVariables(VVAccess.ReadWrite)]
        public float ParryCooldown = 4f;

        [DataField]
        public float ParryUseDelay = 1.25f;

        [DataField]
        public float ParryStaminaDamage = 20f;

        [DataField]
        public int ParriedPoiseDamage = 25;

        [DataField]
        public float ParriedAttackLockout = 1.5f;

        [DataField, AutoNetworkedField]
        public TimeSpan LastParryTime;
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

