using Robust.Shared.GameObjects;
using Content.Shared.Damage;

namespace Content.Server.Imperial.Power.Components
{
    [RegisterComponent]
    public sealed partial class SupermatterIntegrityComponent : Component
    {
        // Текущая целостность кристалла
        [ViewVariables(VVAccess.ReadWrite), DataField]
        public float Integrity = 100f;

        // Максимальная целостность
        [ViewVariables(VVAccess.ReadWrite), DataField]
        public float MaxIntegrity = 100f;

        // Сколько урона наносится за тик при опасных условиях
        [ViewVariables(VVAccess.ReadWrite), DataField]
        public DamageSpecifier TickDamage = new();
        // Интервал между тиками урона
        [ViewVariables(VVAccess.ReadWrite), DataField]
        public TimeSpan TickInterval = TimeSpan.FromSeconds(1);

        // Индивидуальный таймер для тиков урона
        [DataField]
        public TimeSpan TickAccumulator = TimeSpan.Zero;

        // Минимальная целостность, при которой начинается катастрофа
        [ViewVariables(VVAccess.ReadWrite), DataField]
        public float CatastropheThreshold = 0f;

        // Флаги для стадий радио-предупреждений (ключ — порог процента)
        [DataField]
        public Dictionary<float, bool> WarningFlags = new()
        {
            { 0.9f, false },
            { 0.75f, false },
            { 0.5f, false },
            { 0.25f, false },
            { 0.10f, false }
        };
        // Флаги для сброса стадий
        public bool _reset90 = false;
        public bool _reset75 = false;
        public bool _reset50 = false;
        public bool _reset25 = false;
        public bool _reset10 = false;

        // --- Катастрофа ---
        public bool CatastropheActive = false;
        public TimeSpan CatastropheTimer = TimeSpan.Zero;

        // Тег, который считается лечащим для суперматерии (например, болт эмиттера)
        [DataField]
        public string HealTag = "EmitterBolt";

        // Описания состояния кристалла по проценту целостности
        [DataField]
        public Dictionary<float, LocId> IntegrityDescriptions = new()
        {
            { 0.95f, "supermatter-desc-pristine" },
            { 0.75f, "supermatter-desc-scratched" },
            { 0.5f,  "supermatter-desc-cracked" },
            { 0.25f, "supermatter-desc-badly-cracked" },
            { 0.0f,  "supermatter-desc-critical" }
        };
    }
}
