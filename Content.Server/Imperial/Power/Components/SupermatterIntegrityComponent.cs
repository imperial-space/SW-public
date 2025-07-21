using Robust.Shared.GameObjects;

namespace Content.Server.Imperial.Power.Components
{
    [RegisterComponent]
    public sealed partial class SupermatterIntegrityComponent : Component
    {
        // Текущая целостность кристалла
        [ViewVariables(VVAccess.ReadWrite)]
        public float Integrity = 100f;

        // Максимальная целостность
        [ViewVariables(VVAccess.ReadWrite)]
        public float MaxIntegrity = 100f;

        // Сколько урона наносится за тик при опасных условиях
        [ViewVariables(VVAccess.ReadWrite)]
        public float TickDamage = 0.8f;

        // Минимальная целостность, при которой начинается катастрофа
        [ViewVariables(VVAccess.ReadWrite)]
        public float CatastropheThreshold = 0f;

        // Флаги для стадий радио-предупреждений
        public bool _warned90 = false;
        public bool _warned75 = false;
        public bool _warned50 = false;
        public bool _warned25 = false;
        public bool _warned10 = false;
        // Флаги для сброса стадий
        public bool _reset90 = false;
        public bool _reset75 = false;
        public bool _reset50 = false;
        public bool _reset25 = false;
        public bool _reset10 = false;

        // --- Катастрофа ---
        public bool CatastropheActive = false;
        public float CatastropheTimer = 0f;
        public float CatastropheLightningCooldown = 0f;
    }
}
