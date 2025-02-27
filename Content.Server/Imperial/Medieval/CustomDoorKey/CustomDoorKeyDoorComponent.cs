using Content.Shared.Damage;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;

namespace Content.Server.CustomDoorKey.Components
{
    [RegisterComponent]
    public sealed partial class CustomDoorKeyDoorComponent : Component
    {
        [DataField]
        public EntityUid? linkedKey;
    }
}
