using Robust.Shared.Prototypes;
using Content.Shared.Alert;

namespace Content.Server.MedievalFactionFlag.Components

{
    [RegisterComponent]
    public sealed partial class MedievalFactionFlagCheckerComponent : Component
    {
        [DataField]
        public bool Enabled = true;

        [DataField]
        public string Faction = "none";

        [DataField("startTime")]
        public TimeSpan StartTime = TimeSpan.FromSeconds(0f);

        [DataField("endTime")]
        public TimeSpan EndTime = TimeSpan.FromSeconds(0f);

        [DataField("reloadTime")]
        public TimeSpan ReloadTime = TimeSpan.FromSeconds(600f);

    }
}
