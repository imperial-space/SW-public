using Content.Shared.DoAfter;
using Robust.Shared.Serialization;
using Robust.Shared.GameStates;

namespace Content.Shared.MedievalFactionFlag.Components
{
    [Serializable, NetSerializable]
    public sealed partial class MedievalFlagPaintEvent : SimpleDoAfterEvent;
    [RegisterComponent, NetworkedComponent]
    public sealed partial class MedievalFactionFlagPaintComponent : Component
    {
        [DataField]
        public string Faction = "MedievalFlagCaptureWhite";

        [DataField]
        public string Team = "none";

        [DataField]
        public int Uses = 5;
    }
}
