using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Alchemy;

[Serializable, NetSerializable]
public enum AlchemyUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class AlchemyUiState : BoundUserInterfaceState
{
    public NetEntity[] Input;
    public NetEntity[] Output;
    public bool IsProcessing;
    public bool OutputToInput;
    public int OutputCapacity;

    public AlchemyUiState(NetEntity[] input, NetEntity[] output, bool isProcessing, bool outputToInput, int outputCapacity)
    {
        Input = input;
        Output = output;
        IsProcessing = isProcessing;
        OutputToInput = outputToInput;
        OutputCapacity = outputCapacity;
    }
}

[Serializable, NetSerializable]
public sealed class AlchemyInsertMessage(bool output) : BoundUserInterfaceMessage
{
    public bool Output = output;
}

[Serializable, NetSerializable]
public sealed class AlchemyTakeMessage(NetEntity item) : BoundUserInterfaceMessage
{
    public NetEntity Item = item;
}

[Serializable, NetSerializable]
public sealed class AlchemyStartMessage : BoundUserInterfaceMessage;
