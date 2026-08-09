using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using System.Numerics;

namespace Content.Shared.Imperial.Medieval.CapturePoint.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CapturePointComponent : Component
{
    public HashSet<EntityUid> AffectedByStatusEffect = [];

    [DataField, AutoNetworkedField]
    public string PointName = "No name";

    [DataField, AutoNetworkedField]
    public float Radius = 5f;

    [DataField(required: true), AutoNetworkedField]
    public List<ProtoId<MedievalFactionPrototype>> AllowedFactions = [];

    [DataField, AutoNetworkedField]
    public int MinParticipants = 3;

    [DataField, AutoNetworkedField]
    public float MinCaptureDuration = 300f;

    [DataField, AutoNetworkedField]
    public float MaxCaptureDuration = 600f;

    [DataField, AutoNetworkedField]
    public int MaxParticipantsForScaling = 6;

    [DataField, AutoNetworkedField]
    public float CooldownDuration = 1500f;

    [DataField, AutoNetworkedField]
    public float AbandonTimeout = 60f;

    [ViewVariables, AutoNetworkedField]
    public CapturePointState State = CapturePointState.Idle;

    [DataField, ViewVariables, AutoNetworkedField]
    public ProtoId<MedievalFactionPrototype>? OwningFaction;

    [ViewVariables, AutoNetworkedField]
    public ProtoId<MedievalFactionPrototype>? CapturingFaction;

    [ViewVariables, AutoNetworkedField]
    public TimeSpan CaptureStartTime;

    [ViewVariables, AutoNetworkedField]
    public float CurrentCaptureDuration;

    [ViewVariables, AutoNetworkedField]
    public TimeSpan CooldownStartTime;

    [ViewVariables, AutoNetworkedField]
    public TimeSpan? LastEmptyTime;

    /// <summary>
    /// Player counter by index in AllowedFactions where 0 is the first faction and 1 is the second
    /// </summary>
    [AutoNetworkedField]
    public int[] FactionCounts = new int[2];

    [DataField, AutoNetworkedField]
    public Vector2? LocalMapPosition = new(0, 0);

    /// <summary>
    /// Maps factions to their resource income amounts
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<MedievalFactionPrototype>, Dictionary<EntProtoId, int>> FactionIncome = [];

    [DataField]
    public TimeSpan FactionIncomeInterval = TimeSpan.FromMinutes(12);

    [DataField]
    public TimeSpan NextFactionIncome;

    [DataField]
    public EntProtoId? CaptureStatusEffect;

    [DataField]
    public string LinkId = string.Empty;
}

[Serializable, NetSerializable]
public enum CapturePointState : byte
{
    Idle,
    Capturing,
    Cooldown,
}
