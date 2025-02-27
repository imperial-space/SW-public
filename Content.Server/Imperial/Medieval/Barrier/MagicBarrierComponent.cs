using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;

namespace Content.Server.MagicBarrier.Components
{
    [RegisterComponent]
    public sealed partial class MagicBarrierComponent : Component
    {
        [DataField]
        public int TotalPotions = 0;
        [DataField]
        public int TotalLockpicks = 0;
        [DataField]
        public int TotalCrafts = 0;
        [DataField]
        public int TotalDiggs = 0;
        [DataField]
        public int GhostBoo = 0;
        [DataField]
        public int GhostBooPlayers = 0;
        [DataField]
        public int AlcoholDrink = 0;
        [DataField]
        public int SpikeTrapActiveted = 0;

        [DataField]
        public int HumanHurt = 0;

        [DataField]
        public int Screams = 0;
        [DataField]
        public float ZveresHeat = 0;

        [DataField]
        public int HumanDeath = 0;

        [DataField]
        public string FirstDeath = "nobody";

        [DataField]
        public int OpenedDungeons = 0;

        [DataField]
        public string FirstDungeonVisiter = "nobody";

        [DataField("startTime")]
        public TimeSpan StartTime = TimeSpan.FromSeconds(0f);

        [DataField("endTime")]
        public TimeSpan EndTime = TimeSpan.FromSeconds(0f);

        [DataField("reloadTime")]
        public TimeSpan ReloadTime = TimeSpan.FromSeconds(60f);

        [DataField]
        public float Stability = 60f;

        [DataField]
        public float MaxStability = 60f;
        [DataField]
        public float Lose = 0.7f;
        [DataField]
        public float Rate = 1.5f;
        [DataField]
        public int Cycle = 0;

        [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>)), ViewVariables(VVAccess.ReadOnly)]
        public string EffectSoundOnScrollAdd = "/Audio/Imperial/Medieval/scroll_use.ogg";
        [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>)), ViewVariables(VVAccess.ReadOnly)]
        public string EffectSoundOnFinish = "/Audio/Imperial/Medieval/magic_craft.ogg";

        // starfall
        [DataField]
        public float StarfallCurrentPoints = 0f;

        [DataField]
        public float StarfallPointsCapCurrent = 30f;

        [DataField]
        public float StarfallPointsCap = 30f;

        [DataField]
        public float StarfallRandomise = 10;

    }
}
