using Content.Shared.Whitelist;
using Robust.Shared.Utility;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;
using System.Threading;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Content.Shared.Damage;

namespace Content.Server.MedievalIronHitSound.Components
{
    [RegisterComponent]
    public sealed partial class MedievalIronHitSoundComponent : Component
    {
        [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>)), ViewVariables(VVAccess.ReadOnly)]
        public string EffectSoundOnHit = "/Audio/Imperial/Medieval/iron-hit-armor.ogg";

    }
}
