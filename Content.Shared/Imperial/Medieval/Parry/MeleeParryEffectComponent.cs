using Content.Shared.Whitelist;
using Robust.Shared.Utility;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;
using System.Threading;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.MeleeParry.Components
{
    [RegisterComponent, NetworkedComponent]
    public sealed partial class MeleeParryEffectComponent : Component
    {
    }
}
