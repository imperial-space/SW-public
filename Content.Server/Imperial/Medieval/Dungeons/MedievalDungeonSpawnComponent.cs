using Robust.Shared.Audio;
using Robust.Shared.Utility;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;

namespace Content.Server.MedievalDungeon.Components
{
    [RegisterComponent]
    public sealed partial class MedievalDungeonSpawnComponent : Component
    {
        [DataField]
        public ResPath[] MedievalDungeon = new ResPath[]
        {
            new ResPath("/Maps/Imperial/Medieval/DungeonCastle1V01.yml"),
            new ResPath("/Maps/Imperial/Medieval/DungeonCastle2V01.yml"),
            new ResPath("/Maps/Imperial/Medieval/DungeonCastle3V01.yml"),
            new ResPath("/Maps/Imperial/Medieval/DungeonCastle4V01.yml"),
            new ResPath("/Maps/Imperial/Medieval/DungeonCastle5V01.yml"),
            new ResPath("/Maps/Imperial/Medieval/DungeonCastle6V01.yml"),
            new ResPath("/Maps/Imperial/Medieval/DungeonCastle7V01.yml"),
            new ResPath("/Maps/Imperial/Medieval/DungeonCastle8V01.yml"),
            new ResPath("/Maps/Imperial/Medieval/DungeonCastle9V01.yml"),
            new ResPath("/Maps/Imperial/Medieval/DungeonCastle10V01.yml"),
            new ResPath("/Maps/Imperial/Medieval/DungeonCastle11V01.yml"),
            new ResPath("/Maps/Imperial/Medieval/DungeonCastle12V01.yml"),
            new ResPath("/Maps/Imperial/Medieval/DungeonCastle13V01.yml"),
            new ResPath("/Maps/Imperial/Medieval/DungeonCastle14V01.yml"),
            new ResPath("/Maps/Imperial/Medieval/DungeonCastle15V01.yml"),
            new ResPath("/Maps/Imperial/Medieval/DungeonCastle16V01.yml"),
            new ResPath("/Maps/Imperial/Medieval/DungeonCastle17V01.yml"),
            new ResPath("/Maps/Imperial/Medieval/DungeonCastle18V01.yml"),
        };

        [DataField]
        public string DungeonGroup = "None";

        [DataField]
        public int DungeonLevels = 4;

        [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>)), ViewVariables(VVAccess.ReadOnly)]
        public string EffectSoundOnOpen = "/Audio/Imperial/Medieval/dungeon_open.ogg";

    }
}
