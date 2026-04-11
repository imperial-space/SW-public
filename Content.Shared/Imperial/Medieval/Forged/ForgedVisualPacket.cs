using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Forged;

[Serializable, NetSerializable]
public struct ForgedVisualsPacket
{
    public string State;
    public ResPath RsiPath;

    public ForgedVisualsPacket(string state, ResPath rsiPath)
    {
        State = state;
        RsiPath = rsiPath;
    }
}
