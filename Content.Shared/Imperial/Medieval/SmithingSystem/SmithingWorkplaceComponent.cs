using Content.Shared.Containers.ItemSlots;
using Content.Shared.Imperial.Medieval.SmithingSystem.Bui;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.SmithingSystem;

[RegisterComponent, NetworkedComponent]
public sealed partial class SmithingWorkplaceComponent : Component
{
    [DataField]
    public SoundSpecifier HitSound = new SoundPathSpecifier("/Audio/Imperial/Medieval/Crafting/Smithing/anvil_hit.ogg");

    [DataField]
    public ItemSlot WorkpieceSlot = new();

    public Entity<SmithingWorkpieceComponent>? Workpiece;

    public SmithGameState? GameState { get; set; }
}

[Serializable, NetSerializable]
public sealed class SmithStepData
{
    public SmithHitState State;

    public float PerfectHitTime;
    public float GoodHitTime;
}

[Serializable, NetSerializable]
public sealed class SmithGameData : BoundUserInterfaceState
{
    public float SpawnTime;

    public Stack<SmithStepData> Steps = new();

    public float CalculateTotalTime()
    {
        var totalTime = SpawnTime;

        foreach (var step in Steps)
        {
            totalTime += step.PerfectHitTime + (step.GoodHitTime * 2);
        }

        return totalTime;
    }
}

[Serializable, NetSerializable]
public sealed class SmithGameEnded : BoundUserInterfaceMessage
{
}

