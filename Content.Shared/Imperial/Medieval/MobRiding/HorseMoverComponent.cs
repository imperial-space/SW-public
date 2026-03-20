using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.MobRiding;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HorseControlComponent : Component
{
    // базовая скорость поворота градус/секунда в момент, когда лошадь стоит
    [DataField, AutoNetworkedField] public float TurnSpeed = 90f;
    // сила замедления разворота. чем больше, тем слабее поворачивает лошадь при ускорении / движении в целом
    [DataField, AutoNetworkedField] public float TurnSpeedSlowdown = 1.1f;
    // насколько сама скорость влияет на замедление поворота
    [DataField, AutoNetworkedField] public float TurnSpeedSlowdownSpeedScale = 0.3f;
    // множитель движения назад
    [DataField, AutoNetworkedField] public float BackwardsModifier = 0.6f;
    // как быстро ускоряется лошадь при старте движения
    [DataField, AutoNetworkedField] public float ThrottleAcceleration = 6f;
    // как быстро замедляется при конце двжиения
    [DataField, AutoNetworkedField] public float ThrottleDeceleration = 2f;

    [AutoNetworkedField] public float CurrentThrottle;
}
