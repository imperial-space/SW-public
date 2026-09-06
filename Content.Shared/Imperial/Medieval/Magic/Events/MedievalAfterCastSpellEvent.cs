namespace Content.Shared.Imperial.Medieval.Magic;


/// <summary>
/// Raised on succes spell cast
/// </summary>
public sealed class MedievalAfterCastSpellEvent : EntityEventArgs
{
    public EntityUid Performer;

    public EntityUid Action;

    public bool ShowManaPopup = true;
}
