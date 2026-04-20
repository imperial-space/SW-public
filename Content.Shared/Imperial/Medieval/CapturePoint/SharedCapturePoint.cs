using Robust.Shared.Serialization;
using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.CapturePoint;

[Serializable, NetSerializable]
public sealed class StartCapturePointMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CapturePointMessengerEvent : EntityEventArgs
{
    public NetEntity Point;
    public ProtoId<MedievalFactionPrototype> AttackingFaction;

    public CapturePointMessengerEvent(NetEntity point, ProtoId<MedievalFactionPrototype> attackingFaction)
    {
        Point = point;
        AttackingFaction = attackingFaction;
    }
}

[Serializable, NetSerializable]
public sealed class CapturePointResultEvent : EntityEventArgs
{
    public NetEntity Point;
    public ProtoId<MedievalFactionPrototype>? WinnerFaction;

    public CapturePointResultEvent(NetEntity point, ProtoId<MedievalFactionPrototype>? winnerFaction)
    {
        Point = point;
        WinnerFaction = winnerFaction;
    }
}

[Serializable, NetSerializable]
public sealed class CapturePointBuiState : BoundUserInterfaceState
{
    public ProtoId<MedievalFactionPrototype> PlayerFaction;
    public List<string> NearbyAllies;
    public float EstimatedDuration;
    public bool CanStart;
    public string? CannotStartReason;

    public CapturePointBuiState(
        ProtoId<MedievalFactionPrototype> playerFaction,
        List<string> nearbyAllies,
        float estimatedDuration,
        bool canStart,
        string? cannotStartReason = null)
    {
        PlayerFaction = playerFaction;
        NearbyAllies = nearbyAllies;
        EstimatedDuration = estimatedDuration;
        CanStart = canStart;
        CannotStartReason = cannotStartReason;
    }
}

[Serializable, NetSerializable]
public enum CapturePointUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum CapturePointVisuals : byte
{
    Faction,
}
