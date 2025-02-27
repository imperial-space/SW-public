using Content.Shared.Whitelist;
using Robust.Shared.Utility;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;
using System.Threading;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Content.Shared.Damage;

namespace Content.Server.HellRespawnPoint.Components
{
    [RegisterComponent]
    public sealed partial class HellRespawnPointComponent : Component
    {
        [DataField("startTime")]
        public TimeSpan StartTime = TimeSpan.FromSeconds(0f);

        [DataField("endTime")]
        public TimeSpan EndTime = TimeSpan.FromSeconds(0f);

        [DataField("reloadTime")]
        public TimeSpan ReloadTime = TimeSpan.FromSeconds(5f);
        [DataField]

        public int RespawnCount = 0;

    }
}
