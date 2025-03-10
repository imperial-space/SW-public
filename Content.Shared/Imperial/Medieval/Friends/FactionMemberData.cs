using Robust.Shared.Serialization;

namespace Content.Shared.Friends;

[Serializable, NetSerializable]
public sealed class FactionMemberData
{
    public string Name = "";
    public string Job = "";
    public string Objective = "";
    public string Group = "";

    public FactionMemberData(string name, string job, string objective, string group)
    {
        Name = name;
        Job = job;
        Objective = objective;
        Group = group;
    }

    public FactionMemberData()
    {
    }
}
