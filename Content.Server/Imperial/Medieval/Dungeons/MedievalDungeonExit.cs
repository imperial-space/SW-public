namespace Content.Server.MedievalDungeon.Components
{
    [RegisterComponent]
    public sealed partial class MedievalDungeonExitComponent : Component
    {
        [DataField]
        public EntityUid? DungeonExit;
    }
}
