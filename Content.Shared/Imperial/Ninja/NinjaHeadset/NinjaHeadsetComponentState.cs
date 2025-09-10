using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.NinjaHeadset.Components;

[Serializable, NetSerializable]
public sealed class NinjaHeadsetComponentState : ComponentState
{
    public TimeSpan CopyFrequenciesTime;
    public HashSet<string> CopiedFrequencies;

    public NinjaHeadsetComponentState(TimeSpan copyTime, HashSet<string> frequencies)
    {
        CopyFrequenciesTime = copyTime;
        CopiedFrequencies = frequencies;
    }
}
