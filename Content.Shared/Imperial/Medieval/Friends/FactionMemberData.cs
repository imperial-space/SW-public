using Robust.Shared.Serialization;

namespace Content.Shared.Friends;

[Serializable, NetSerializable]
public sealed class FactionMemberData
{
    public string Name = "";
    public string Job = "";
    public string JobPrefix = "";
    public bool Leader = false;
    public FactionMemberGroup Group = FactionMemberGroup.None;

    public FactionMemberData(string name, string job, string jobPrefix, FactionMemberGroup group)
    {
        Name = name;
        Job = job;
        JobPrefix = jobPrefix;
        Group = group;
    }

    public FactionMemberData()
    {
    }
}

[Serializable, NetSerializable]
public enum FactionMemberGroup : byte
{
    None,
    Alpha,
    Bravo,
    Delta,
    Gamma,
    Omega
}
