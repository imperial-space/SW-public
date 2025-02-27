namespace Content.Server.MedievalMobSpawner.Components
{
    [RegisterComponent]
    public sealed partial class MedievalMobSpawnerComponent : Component
    {
        [DataField, ViewVariables(VVAccess.ReadWrite)]
        public float Chance = 0.25f;
        [DataField, ViewVariables(VVAccess.ReadWrite)]
        public float Cooldown = 90f;
        [DataField, ViewVariables(VVAccess.ReadWrite)]
        public float MaxCoolDown = 900f;

        [DataField, ViewVariables(VVAccess.ReadWrite)]
        public bool Ready = true;
        [DataField, ViewVariables(VVAccess.ReadWrite)]
        public bool BarrierAddicted = true;

        [DataField, ViewVariables(VVAccess.ReadWrite)]
        public string SpawnedEntity = "MobMonkey";

    }
}
