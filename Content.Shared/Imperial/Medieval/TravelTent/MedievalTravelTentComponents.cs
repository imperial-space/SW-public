using System.Numerics;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.TravelTent;

/// <summary>
/// Rolled-up travel tent. Pitched on the floor, can hold one rolled sleeping bag.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MedievalTravelTentFoldedComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Pitched;

    [DataField]
    public TimeSpan PitchTime = TimeSpan.FromSeconds(5);

    /// <summary>ItemSlots slot that holds the sleeping bag.</summary>
    [DataField]
    public string Slot = "sleeping_bag";
}

/// <summary>
/// Pitched travel tent: covers its own tile and the one above, entered from the front.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MedievalTravelTentComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Folded;

    [DataField]
    public TimeSpan FoldTime = TimeSpan.FromSeconds(3);

    /// <summary>Fixture across the entrance, solid while the flap is closed.</summary>
    [DataField]
    public string EntranceFixture = "entrance";

    /// <summary>Where a laid-out sleeping bag goes, from the tile centre. Its sprite sits low in the tile.</summary>
    [DataField]
    public Vector2 BagOffset = new(0f, 0.15f);

    /// <summary>How far from the tile centre a laid-out bag still counts as being in the tent.</summary>
    [DataField]
    public float BagSearchRange = 0.4f;
}

/// <summary>The rolled tent is being pitched at <see cref="Location"/>.</summary>
[Serializable, NetSerializable]
public sealed partial class MedievalTravelTentPitchDoAfterEvent : DoAfterEvent
{
    [DataField(required: true)]
    public NetCoordinates Location { get; private set; }

    private MedievalTravelTentPitchDoAfterEvent()
    {
    }

    public MedievalTravelTentPitchDoAfterEvent(NetCoordinates location)
    {
        Location = location;
    }

    public override DoAfterEvent Clone()
    {
        return new MedievalTravelTentPitchDoAfterEvent(Location);
    }
}

/// <summary>The pitched tent is being folded back into a roll.</summary>
[Serializable, NetSerializable]
public sealed partial class MedievalTravelTentFoldDoAfterEvent : SimpleDoAfterEvent;
