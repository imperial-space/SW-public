using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;
using Content.Shared.Alert;
using Content.Server.Chat.Systems;
using Content.Shared.Chat.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Set;
using Content.Server.Chat;

namespace Content.Server.NeedSleep.Components
{
    [RegisterComponent]
    public sealed partial class NeedSleepComponent : Component
    {
        [DataField]
        public bool Enabled = true;

        [DataField("emotes", customTypeSerializer: typeof(PrototypeIdHashSetSerializer<EmotePrototype>)), ViewVariables(VVAccess.ReadWrite)]
        public HashSet<string> Emotes = new();

        [DataField]
        public float SleepLevel = 0f;

        [DataField]
        public float MaxSleepLevel = 100f;

        [DataField]
        public float GrowTemp = 0.25f;

        [DataField]
        public float SleepRegen = 30f;

        [DataField("startTime")]
        public TimeSpan StartTime = TimeSpan.FromSeconds(0f);

        [DataField("endTime")]
        public TimeSpan EndTime = TimeSpan.FromSeconds(0f);

        [DataField("reloadTime")]
        public TimeSpan ReloadTime = TimeSpan.FromSeconds(10f);

        [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>)), ViewVariables(VVAccess.ReadOnly)]
        public string EffectSound = "/Audio/Imperial/Medieval/bad_smell_effect.ogg";
        public ProtoId<AlertPrototype> SmellAlert = "NeedSleep";
    }
}
