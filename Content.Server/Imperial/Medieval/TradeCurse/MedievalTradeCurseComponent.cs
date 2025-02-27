using Robust.Shared.Prototypes;
using Content.Shared.Alert;

namespace Content.Server.MedievalTradeCurse.Components
{
    [RegisterComponent]
    public sealed partial class MedievalTradeCurseComponent : Component
    {
        [DataField("startTime")]
        public TimeSpan StartTime = TimeSpan.FromSeconds(0f);

        [DataField("endTime")]
        public TimeSpan EndTime = TimeSpan.FromSeconds(0f);

        [DataField("reloadTime")]
        public TimeSpan ReloadTime = TimeSpan.FromSeconds(30f);
        [DataField, ViewVariables(VVAccess.ReadWrite)]
        public float CurseLevel = 30f;

        [DataField, ViewVariables(VVAccess.ReadWrite)]
        public float CurseMax = 30f;
        public ProtoId<AlertPrototype> CurseAlert = "TradeCurse";


    }
}
