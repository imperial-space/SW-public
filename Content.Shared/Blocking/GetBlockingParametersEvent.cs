namespace Content.Shared.Blocking;

/// <summary>Raised on a shield wielder before checking the requirements for an active block.</summary>
[ByRefEvent]
public record struct GetBlockingParametersEvent(EntityUid Shield)
{
    public bool RequiresAnchoring = true;
}
