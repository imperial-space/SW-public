using Content.Shared.Atmos;
using Content.Shared.Trigger.Systems;

namespace Content.Server.Imperial.Medieval.Alchemy;

public sealed class AlchemyExplosiveMixtureSystem : EntitySystem
{
    [Dependency] private readonly TriggerSystem _trigger = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AlchemyExplosiveMixtureComponent, IgnitedEvent>(OnIgnited);
    }

    private void OnIgnited(Entity<AlchemyExplosiveMixtureComponent> ent, ref IgnitedEvent args)
    {
        if (ent.Comp.Detonated || TerminatingOrDeleted(ent.Owner) || EntityManager.IsQueuedForDeletion(ent.Owner))
            return;

        ent.Comp.Detonated = true;
        var bomb = Spawn(ent.Comp.BombPrototype, _transform.GetMapCoordinates(ent.Owner));
        _transform.AttachToGridOrMap(bomb);
        _trigger.Trigger(bomb, key: ent.Comp.TriggerKey);
        QueueDel(ent.Owner);
    }
}
