using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;

namespace Content.Server.CustomDoorKey.Components
{
    [RegisterComponent]
    public sealed partial class DoorHackableComponent : Component
    {
        [DataField]
        public int MinNumber = -5;

        [DataField]
        public int MaxNumber = 5;

        [DataField]
        public int NumberCount = 5;

        [DataField]
        public int LockPickProgress = 0;

        [DataField]
        public int[] Numbers = new int[100];

        [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>)), ViewVariables(VVAccess.ReadOnly)]
        public string EffectSoundOnNewLock = "/Audio/Imperial/Medieval/new_lock.ogg";
    }
}
