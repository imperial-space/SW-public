using Robust.Shared.GameStates;

namespace Content.Shared.MeleeParry.Components
{
    [RegisterComponent, NetworkedComponent]
    public sealed partial class MeleeParryComponent : Component
    {
        [DataField]
        public float ParryChanse = 0.6f; //Не используется это Легаси

        [DataField]
        public float ParriedAgo = 0.5f; //Если игрок нажал парирование, но никто его в этот момент не бил

        [DataField]
        public float ParriedTime = 0.5f; // Окно парирования после нажатие кнопки

        [DataField]
        public string ParryEffect = "MedievalEffectParry";

        [DataField]
        public bool RealParry = true;

        [ViewVariables(VVAccess.ReadWrite)]
        public float ParryWindow = 0f;
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
