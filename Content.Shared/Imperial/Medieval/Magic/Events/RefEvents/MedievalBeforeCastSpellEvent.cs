using Robust.Shared.Map;

namespace Content.Shared.Imperial.Medieval.Magic;


/// <summary>
/// Event called before the start of the spell cast
/// </summary>
[ByRefEvent]
public struct MedievalBeforeCastSpellEvent(EntityUid performer, EntityCoordinates target)
{
    /// <summary>
    /// The Performer of the event, to check if they meet the requirements.
    /// </summary>
    public EntityUid Performer = performer;

    public EntityCoordinates Target = target;

    public bool IsContinuation;

    public bool HasResourceReservation;

    public bool Cancelled;
}
