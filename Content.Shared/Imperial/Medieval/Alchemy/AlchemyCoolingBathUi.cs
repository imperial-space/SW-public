using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Alchemy;

[Serializable, NetSerializable]
public enum AlchemyCoolingBathUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class AlchemyCoolingBathUiState(
    NetEntity[] contents, int[] durations, int selectedDuration, bool isProcessing)
    : BoundUserInterfaceState
{
    public NetEntity[] Contents = contents;
    public int[] Durations = durations;
    public int SelectedDuration = selectedDuration;
    public bool IsProcessing = isProcessing;
}

[Serializable, NetSerializable]
public sealed class AlchemyCoolingBathSelectTimeMessage(int duration) : BoundUserInterfaceMessage
{
    public int Duration = duration;
}

[Serializable, NetSerializable]
public sealed class AlchemyCoolingBathStartMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class AlchemyCoolingBathEjectMessage(NetEntity? item = null) : BoundUserInterfaceMessage
{
    public NetEntity? Item = item;
}
