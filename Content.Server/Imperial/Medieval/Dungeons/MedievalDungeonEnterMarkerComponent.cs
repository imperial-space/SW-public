namespace Content.Server.MedievalDungeon.Components
{
    [RegisterComponent]
    public sealed partial class MedievalDungeonEnterMarkerComponent : Component
    {
        [DataField]
        public EntityUid? DeactiveTrapEntity;

        [DataField]
        public EntityUid? ActiveTrapEntity;

        [DataField]
        public bool Enabled = true;

        [DataField]
        public bool IsEnter = true;

        [DataField]
        public string EnterEntity = "MedievalLadderNew";

        [DataField]
        public string ExitEntity = "MedievalTrapdoorNew";

        [DataField]
        public string Level = "none";

    }
}
