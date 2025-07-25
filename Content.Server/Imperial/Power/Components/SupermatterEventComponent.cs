using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Content.Shared.Radio;
using Robust.Shared.Random;
using Content.Server.Imperial.ImperialLightning;

namespace Content.Server.Imperial.Power.Components
{
    public enum SupermatterEventType
    {
        None = 0,
        Lightning = 1,
        Radiation = 2,
        Plasma = 3
    }

    [RegisterComponent]
    public sealed partial class SupermatterEventComponent : Component
    {
        // Время до следующего случайного события (секунды)
        [DataField]
        public TimeSpan NextEventTimer = TimeSpan.Zero;
        // Тип текущего события (0 - ничего, 1 - молнии, 2 - радиация, 3 - плазма)
        [DataField]
        public SupermatterEventType CurrentEvent = SupermatterEventType.None;
        // Время окончания текущего события (секунды, если 0 - нет активного события)
        [DataField]
        public TimeSpan EventEndTime = TimeSpan.Zero;
        // Кулдаун для всплеска молний
        [DataField]
        public TimeSpan LightningCooldown = TimeSpan.Zero;
        // Таймер для генерации плазмы во время PlasmaEvent
        [DataField]
        public TimeSpan? PlasmaTickAccumulator = null;
        // Допустимые типы событий для этого кристалла
        [DataField]
        public List<SupermatterEventType> AllowedEventTypes { get; set; } = new()
        {
            SupermatterEventType.None,
            SupermatterEventType.Lightning,
            SupermatterEventType.Radiation,
            SupermatterEventType.Plasma
        };
        // Каналы рации для оповещений
        [DataField]
        public ProtoId<RadioChannelPrototype>[] RadioChannels = { "Engineering" };
    }
}
