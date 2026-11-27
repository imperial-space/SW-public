using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Cooking;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Cooking;

public sealed class MedievalDishEmptiedSystem : EntitySystem
{
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

        // eating also drains it, check again next tick
        Timer.Spawn(0, () => ReturnBowl(ent));
    }

    private bool IsEmpty(Entity<MedievalDishBowlComponent> dish)
    {
        return _solutions.TryGetSolution(dish.Owner, dish.Comp.Solution, out _, out var solution) && solution.Volume <= 0;
    }

    private void ReturnBowl(Entity<MedievalDishBowlComponent> ent)
    {
        if (TerminatingOrDeleted(ent) || !IsEmpty(ent) || ent.Comp.Bowl is not { } bowlId)
            return;

        // map coords, the dish may be in a bag
        var bowl = Spawn(bowlId, _transform.GetMapCoordinates(ent));
        _container.TryGetContainingContainer((ent.Owner, null, null), out var container);
        var holder = container != null && HasComp<HandsComponent>(container.Owner) ? container.Owner : (EntityUid?) null;

        Del(ent);
        if (holder is { } user)
            _hands.PickupOrDrop(user, bowl);
    }
}
