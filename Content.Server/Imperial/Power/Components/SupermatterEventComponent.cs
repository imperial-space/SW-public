using Robust.Shared.GameObjects;

namespace Content.Server.Imperial.Power.Components
{
    [RegisterComponent]
    public sealed partial class SupermatterEventComponent : Component
    {
        // Время до следующего случайного события (секунды)
        public float NextEventTimer = 0f;
        // Тип текущего события (0 - ничего, 1 - молнии, 2 - радиация, 3 - плазма)
        public int CurrentEvent = 0;
        // Время окончания текущего события (секунды, если 0 - нет активного события)
        public float EventEndTime = 0f;
        // Принудительный запуск всплеска (например, при испепелении)
        public bool ForceEvent = false;
        // Кулдаун для всплеска молний
        public float LightningCooldown = 0f;
    }
}
