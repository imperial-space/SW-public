using Robust.Shared.GameStates;
using Content.Shared.Damage;
using System.Numerics;
using Content.Shared.Actions;
using Robust.Shared.Serialization;
using Content.Shared.DoAfter;


namespace Content.Shared.AntiNocturn.Components
{

    [RegisterComponent, NetworkedComponent]
    public sealed partial class AntiNocturnComponent : Component
    {

        public TimeSpan StartTime = TimeSpan.FromSeconds(0f);
        public TimeSpan EndTime = TimeSpan.FromSeconds(0f);

        [ViewVariables(VVAccess.ReadWrite)] [DataField("power")]
        public float Power = 1f;
        [ViewVariables(VVAccess.ReadWrite)] [DataField("distance")]
        public float Distance = 2.5f;
    }
}
