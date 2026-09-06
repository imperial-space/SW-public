using Content.Server.Imperial.Medieval.Ships.PlayerDrowning;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Robust.Shared.Containers;
using Robust.Shared.Spawners;

namespace Content.Server.Imperial.Medieval.Loot;

public sealed partial class PreserveContentsOnTimedDespawnSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PreserveContentsOnTimedDespawnComponent, TimedDespawnEvent>(OnTimedDespawn);
    }

    private void OnTimedDespawn(
        EntityUid uid,
        PreserveContentsOnTimedDespawnComponent component,
        ref TimedDespawnEvent args)
    {
        var pending = new Stack<EntityUid>();
        var preservedEntities = new List<EntityUid>();

        var children = Transform(uid).ChildEnumerator;
        while (children.MoveNext(out var child))
            pending.Push(child);

        while (pending.TryPop(out var child))
        {
            if (TerminatingOrDeleted(child))
                continue;

            if (HasComp<MobStateComponent>(child) ||
                HasComp<ItemComponent>(child) && HasComp<UndrowableComponent>(child))
            {
                preservedEntities.Add(child);
                continue;
            }

            var descendants = Transform(child).ChildEnumerator;
            while (descendants.MoveNext(out var descendant))
                pending.Push(descendant);
        }

        foreach (var preserved in preservedEntities)
        {
            _containers.TryRemoveFromContainer(preserved, force: true);
            _transform.AttachToGridOrMap(preserved);
        }
    }
}
