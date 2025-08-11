using Content.Shared.Damage;
using Content.Shared.Radio;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Power.Components
{
    [RegisterComponent]
    public sealed partial class SupermatterIntegrityComponent : Component
    {
        /// <summary>
        /// Текущая целостность кристалла
        /// </summary>
        [DataField]
        public float Integrity = 100f;

        /// <summary>
        /// Максимальная целостность
        /// </summary>
        [DataField]
        public float MaxIntegrity = 100f;

        /// <summary>
        /// Сколько урона наносится за тик при опасных условиях
        /// </summary>
        [DataField, ViewVariables(VVAccess.ReadOnly)]
        public DamageSpecifier TickDamage = new();

        /// <summary>
        /// Интервал между тиками урона
        /// </summary>
        [DataField]
        public TimeSpan DamageTickInterval = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Индивидуальный таймер для тиков урона
        /// </summary>
        public TimeSpan TickAccumulator = TimeSpan.Zero;

        /// <summary>
        /// Минимальная целостность, при которой начинается катастрофа
        /// </summary>
        [DataField]
        public float CatastropheThreshold = 0f;

        /// <summary>
        /// Канал радио, в который будут отправляться оповещения
        /// </summary>
        [DataField]
        public ProtoId<RadioChannelPrototype> RadioChannel = "Engineering";

        /// <summary>
        /// Активна ли катастрофа
        /// </summary>
        public bool CatastropheActive = false;

        /// <summary>
        /// Верхняя граница температуры, после которой наступают плохие для суперматерии условия
        /// </summary>
        public readonly float UpperTempThreshold = 350f;

        /// <summary>
        /// Нижняя граница температуры, после которой наступают плохие для суперматерии условия
        /// </summary>
        public readonly float LowerTempThreshold = 250f;

        /// <summary>
        /// Нижняя граница температуры, после которой наступают плохие для суперматерии условия
        /// </summary>
        public readonly float UpperPressureThreshold = 300f;

        /// <summary>
        /// Таймер катастрофы
        /// </summary>
        [DataField, ViewVariables(VVAccess.ReadOnly)]
        public TimeSpan CatastropheTimer = TimeSpan.Zero;

        /// <summary>
        /// Тег, прототипы с которым лечат Суперматерию
        /// </summary>
        [DataField]
        public ProtoId<TagPrototype> HealTag = "EmitterBolt";

        /// <summary>
        /// Количество здоровья, восстанавливаемое за один выстрел эмиттера
        /// </summary>
        [DataField]
        public float EmitterHealAmount = 0.35f;

        /// <summary>
        /// Описание кристалла в зависимости от его состояния
        /// </summary>
        public readonly Dictionary<float, LocId> IntegrityDescription = new()
        {
            { 95f, "supermatter-desc-pristine" },
            { 75f, "supermatter-desc-scratched" },
            { 50f,  "supermatter-desc-cracked" },
            { 25f, "supermatter-desc-badly-cracked" },
            { 10f,  "supermatter-desc-critical" },
        };

        /// <summary>
        /// Оповещения о состоянии суперматерии. Последнее, [4], - предупреждает о катастрофе
        /// </summary>
        public readonly Dictionary<float, LocId> IntegrityWarnings = new()
        {
            { 95f, "supermatter-warn-90" },
            { 75f, "supermatter-warn-75" },
            { 50f, "supermatter-warn-50" },
            { 25f, "supermatter-warn-25" },
            { 10f, "supermatter-warn-10" },
        };

        /// <summary>
        /// Для предотвращения повторных оповещений с одним и тем же состоянием суперматерии
        /// </summary>
        public readonly Dictionary<float, bool> IntegrityFlags = new()
        {
            { 95f, false },
            { 75f, false },
            { 50f, false },
            { 25f, false },
            { 10f, false },
        };
    }
}
