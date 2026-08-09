using Content.Shared.Imperial.Medieval.CapturePoint.Components;
using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using System.Numerics;

namespace Content.Shared.Imperial.Medieval.FactionFlag.UI;

[Serializable, NetSerializable]
public sealed class MedievalFactionFlagBuiState(List<MedievalFactionFlagPointData> points) : BoundUserInterfaceState
{
    public List<MedievalFactionFlagPointData> Points = points;
}

[Serializable, NetSerializable]
public sealed class MedievalFactionFlagPointData(
    NetEntity entity,
    string name,
    Vector2 position,
    CapturePointState state,
    ProtoId<MedievalFactionPrototype>? owner,
    ProtoId<MedievalFactionPrototype>? capturingFaction,
    float captureTimeRemaining,
    float cooldownTimeRemaining)
{
    public NetEntity Entity = entity;
    public string Name = name;
    public Vector2 Position = position;
    public CapturePointState State = state;
    public ProtoId<MedievalFactionPrototype>? Owner = owner;
    public ProtoId<MedievalFactionPrototype>? CapturingFaction = capturingFaction;
    public float CaptureTimeRemaining = captureTimeRemaining;
    public float CooldownTimeRemaining = cooldownTimeRemaining;
}
