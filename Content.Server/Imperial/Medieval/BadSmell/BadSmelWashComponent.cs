using Content.Shared.Whitelist;
using Robust.Shared.Utility;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;
using System.Threading;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Content.Shared.Damage;

namespace Content.Server.BadSmell.Components
{
    [RegisterComponent]
    public sealed partial class BadSmelWashComponent : Component
    {
        [DataField]
        public float MaxWash = 21f;

        [DataField]
        public float WashSpeedMultiplier = 1f;

    }
}
