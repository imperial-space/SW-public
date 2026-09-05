using System.Numerics;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.MagicBarrier.Components
{
    [RegisterComponent]
    public sealed partial class MagicBarrierComponent : Component
    {
        [DataField("startTime")]
        public TimeSpan StartTime = TimeSpan.FromSeconds(0f);

        [DataField("endTime")]
        public TimeSpan EndTime = TimeSpan.FromSeconds(0f);

        [DataField("reloadTime")]
        public TimeSpan ReloadTime = TimeSpan.FromSeconds(60f);

        [DataField]
        public float Stability = 500f;

        [DataField]
        public float MaxStability = 500f;

        /* Current stability drain per minute. This value is calculated by MagicBarrierSystem.
         Текущий расход стабильности в минуту. Это значение рассчитывается MagicBarrierSystem. */
        [DataField]
        public float Lose;

        /* Base stability drain before active Growth and Rift effects are applied. Default (3.64).
        Базовый расход стабильности до применения активных эффектов Growth и Rift. Значение по умолчанию (3.64).*/
        [DataField]
        public float BaseCurseDrain = 3.64f;

        /* Stability drain added by each active Rift before the Growth escalation multiplier is applied. Default (4.5).
         Расход стабильности, добавляемый каждым активным Rift до применения множителя эскалации Growth. Значение по умолчанию (4.5).*/
        [DataField]
        public float RiftCurseDrain = 3.5f;

        /* Number of Cursed Growths that have been destroyed. This provides persistent escalation.
         Количество уничтоженных Cursed Growths. Это обеспечивает постоянное усиление.*/
        /*[DataField]
        public float MagicBarrierCursePE = 0f;*/

        /* Current persistent-escalation multiplier applied by Cursed Growths. Default (1.1).
         Текущий множитель постоянной эскалации, применяемый Cursed Growths. Значение по умолчанию (1.1).*/
        [DataField]
        public float MagicBarrierCurseEffect = 1.1f;

        /* How much the MagicBarrierCurseEffect is increased when a cursed growth is distroyed. Default (0.01)
         Насколько увеличивается MagicBarrierCurseEffect при уничтожении Cursed Growth. Значение по умолчанию (0.01). */
        [DataField] 
        public float MagicBarrierCurseEM = 0.01f;

        /* Controls how quickly the hard-cap curve approaches its upper limit, the higher it is the faster it reaches the limit. Default (0.17)
         Определяет, насколько быстро кривая hard cap приближается к своему верхнему пределу: чем выше значение, тем быстрее достигается предел. Значение по умолчанию (0.17). */
        [DataField]
        public float OCurseRate = 0.17f;

        /* Number of active Cursed Growths and Rifts at which the hard-cap curve begins. Default (5).
         Количество активных Cursed Growths и Rifts, при котором начинается кривая hard cap. Значение по умолчанию (5).*/
        [DataField]
        public float ACurseLimit = 5f;

        /* Lower value of the hard-cap curve. Default (20)
         Нижнее значение кривой hard cap. Значение по умолчанию (20). */
        [DataField]
        public float HLCurseLimit = 20f;

        /* Upper value approached by the hard-cap curve. Default (50)
         Верхнее значение, к которому приближается кривая hard cap. Значение по умолчанию (50). */
        [DataField]
        public float HHCurseLimit = 50f;

        /* Keeps track of last time lose was calculated
        Отслеживает время последнего расчёта потери стабильности.*/
        [DataField]
        public TimeSpan LastLoseCalculateTime = TimeSpan.Zero;

        [DataField]
        public int Cycle = 0;

        [DataField, ViewVariables(VVAccess.ReadOnly)]
        public string EffectSoundOnScrollAdd = "/Audio/Imperial/Medieval/scroll_use.ogg";

        [DataField, ViewVariables(VVAccess.ReadOnly)]
        public string EffectSoundOnFinish = "/Audio/Imperial/Medieval/magic_craft.ogg";

        [DataField]
        public float StarfallCurrentPoints = 0f;

        [DataField]
        public float StarfallPointsCapCurrent = 30f;

        [DataField]
        public float StarfallPointsCap = 30f;

        [DataField]
        public float StarfallRandomise = 10;

        [DataField]
        public float AncientNocturneEventChance = 17.5f;

        [DataField]
        public Dictionary<NetUserId, int> ReviveCount = new();

        [DataField]
        public TimeSpan ElementalRiftNextSpawnTime = TimeSpan.Zero;

        [DataField]
        public float ElementalRiftMinSpawnMinutes = 30f;

        [DataField]
        public float ElementalRiftMaxSpawnMinutes = 60f;
    }
}
