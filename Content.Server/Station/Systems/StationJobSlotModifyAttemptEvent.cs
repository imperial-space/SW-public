using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server.Station.Systems;

public sealed class StationJobSlotModifyAttemptEvent : CancellableEntityEventArgs
{
    public string JobPrototypeId { get; }
    public StationJobSlotModifyType Type { get; }
    public int Amount { get; }
    public bool CreateSlot { get; }
    public bool Clamp { get; }

    public StationJobSlotModifyAttemptEvent(string jobPrototypeId,
        StationJobSlotModifyType type,
        int amount = 0,
        bool createSlot = false,
        bool clamp = false)
    {
        JobPrototypeId = jobPrototypeId;
        Type = type;
        Amount = amount;
        CreateSlot = createSlot;
        Clamp = clamp;
    }
}

public sealed class CollectLateJoinBlockedDepartmentsEvent : EntityEventArgs
{
    public HashSet<ProtoId<DepartmentPrototype>> LockedDepartments { get; } = new();
}

public enum StationJobSlotModifyType : byte
{
    Adjust,
    Set,
    MakeUnlimited
}
