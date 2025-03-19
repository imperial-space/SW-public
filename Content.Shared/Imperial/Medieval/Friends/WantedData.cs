using Content.Shared.Friends.Prototypes;
using Content.Shared.Humanoid;
using Content.Shared.Preferences;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Friends;

[Serializable, NetSerializable]
public sealed class WantedData
{
    public string Job = "";
    public string FlavorText = "";
    public ProtoId<MedievalFactionPrototype> Faction;
    public HumanoidCharacterProfile Profile;
    public string Performer = "";

    public WantedData(HumanoidCharacterProfile profile, string job, ProtoId<MedievalFactionPrototype> faction, string performer, string flavorText = "")
    {
        Profile = profile;
        Job = job;
        FlavorText = flavorText;
        Faction = faction;
        Performer = performer;
    }
}
