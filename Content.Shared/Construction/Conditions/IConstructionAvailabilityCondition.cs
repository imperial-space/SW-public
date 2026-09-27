namespace Content.Shared.Construction.Conditions;

/// <summary>
/// A recipe requirement depending only on the crafter, suitable for filtering the construction menu.
/// Placement conditions remain checked when construction starts, not while browsing recipes.
/// </summary>
public interface IConstructionAvailabilityCondition : IConstructionCondition
{
    bool IsAvailable(EntityUid user);
}
