using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.MobRiding;

[RegisterComponent, NetworkedComponent]
public sealed partial class HorseControlComponent : Component
{
    // базовая скорость поворота градус/секунда в момент, когда лошадь стоит
    [DataField] public float TurnSpeed = 90f;
    // сила замедления разворота. чем больше, тем слабее поворачивает лошадь при ускорении / движении в целом
    [DataField] public float TurnSpeedSlowdown = 1.1f;
    // насколько сама скорость влияет на замедление поворота
    [DataField] public float TurnSpeedSlowdownSpeedScale = 0.3f;
    // множитель движения назад
    [DataField] public float BackwardsModifier = 0.6f;
    // как быстро затухает скорость пока не нажата кнопка движения
    [DataField("noInputFrictionMultiplier")] public float NoInputFriction = 0.35f;
    // минимальная скорость, ниже которой лошадь останавливается
    // всё эти параметры надо подкрутить так, чтобы не было ощущения того, что лошадь скользит на льду
    [DataField] public float MinInertiaSpeed = 0.35f;
}
