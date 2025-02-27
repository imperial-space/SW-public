using Content.Shared.Whitelist;
using Robust.Shared.Utility;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;
using System.Threading;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Content.Shared.Damage;

namespace Content.Server.MedievalMobSpawner.Components
{
    [RegisterComponent]
    public sealed partial class MedievalMobTriggerComponent : Component
    {
        [DataField(required: true)]
        public string FixtureId = "";

        [DataField("startTime")]
        public TimeSpan StartTime = TimeSpan.FromSeconds(0f);

        [DataField("endTime")]
        public TimeSpan EndTime = TimeSpan.FromSeconds(0f);

        [DataField("reloadTime")]
        public TimeSpan ReloadTime = TimeSpan.FromSeconds(30f);

    }
}
