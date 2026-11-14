using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Cooking;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Cooking;

/// <summary>
///     imperial medieval - a served dish that was poured out turns back into the bowl it came in,
///     instead of staying an empty "stew".
/// </summary>
public sealed class MedievalDishEmptiedSystem : EntitySystem
{
    private const string DishSolution = "food";

    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalDishBowlComponent, SolutionContainerChangedEvent>(OnSolutionChanged);
    }

    private void OnSolutionChanged(Entity<MedievalDishBowlComponent> ent, ref SolutionContainerChangedEvent args)
    {
        if (!IsEmpty(ent))
            return;

        // eating drains the solution too and removes the dish itself a moment later, so look again next tick
        Timer.Spawn(0, () => ReturnBowl(ent));
    }

    private bool IsEmpty(EntityUid dish)
    {
        return _solutions.TryGetSolution(dish, DishSolution, out _, out var solution) && solution.Volume <= 0;
    }

    private void ReturnBowl(Entity<MedievalDishBowlComponent> ent)
    {
        if (TerminatingOrDeleted(ent) || !IsEmpty(ent) || ent.Comp.Bowl is not { } bowlId)
            return;

        // map coordinates: the dish may lie in a bag, whose local coordinates mean nothing outside it
        var bowl = Spawn(bowlId, _transform.GetMapCoordinates(ent));
        _container.TryGetContainingContainer((ent.Owner, null, null), out var container);
        var holder = container != null && HasComp<HandsComponent>(container.Owner) ? container.Owner : (EntityUid?) null;

        Del(ent);
        if (holder is { } user)
            _hands.PickupOrDrop(user, bowl);
    }
}
