using Content.Shared.Timing;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared.Weapons.Ranged.Components;

[RegisterComponent, NetworkedComponent, Access(typeof(UseDelayOnMeleeSystem))]
public sealed partial class UseDelayOnMeleeComponent : Component
{

}
