using Robust.Shared.Audio;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;
using Content.Server.Imperial.ExplosiveProjectile;

/// <summary>
/// Подрывает/разрывает сущность в случае, если на ней в момент попадания не было элемента одежды из "outerClothing", обладающего компонентом "PressureProtection".
/// </summary>
namespace Content.Server.Imperial.ExplosiveProjectile.Components
{
    [RegisterComponent]
    [Access(typeof(ExplosiveProjectileSystem))]
    public sealed partial class ExplosiveProjectileComponent : Component
    {
        /// <summary>
        /// Звук активации.
        /// </summary>
        [DataField]
        public SoundSpecifier? SoundActivate = new SoundPathSpecifier("/Audio/Imperial/SpecialUnits/RCU/detonation.ogg", AudioParams.Default.WithVolume(5).WithLoop(false).WithMaxDistance(15f));

        /// <summary>
        /// Сколько времени будет лежать цель в оглушении, если условия для взрыва будут провалены.
        /// </summary>
        [DataField("knockdownTime")]
        public float KnockdownTime = 15f;

        /// <summary>
        /// Время стана.
        /// </summary>
        [DataField("stunParam")]
        public int StunParam;

        /// <summary>
        /// Замедление.
        /// </summary>
        [DataField("slowdownParam")]
        public int SlowdownParam;

        /// <summary>
        /// Скорость ходьбы при оглушении.
        /// </summary>
        [DataField("walkSpeedParam")]
        public float WalkSpeedParam = 1f;

        /// <summary>
        /// Скорость бега при оглушении.
        /// </summary>
        [DataField("runSpeedParam")]
        public float RunSpeedParam = 1f;

        /// <summary>
        /// Фиксчурсы. НЕ ТРОГАТЬ.
        /// </summary>
        [DataField("fixture")] public string CheckedFixtureID = "projectile";
    }
}

