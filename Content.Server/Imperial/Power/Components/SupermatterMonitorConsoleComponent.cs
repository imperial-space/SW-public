using Robust.Shared.GameObjects;

namespace Content.Server.Imperial.Power.Components
{
    [RegisterComponent]
    public sealed partial class SupermatterMonitorConsoleComponent : Component
    {
        // Таймер до следующего пиликанья
        public float BeepCooldownTimer = 0f;
    }
}
