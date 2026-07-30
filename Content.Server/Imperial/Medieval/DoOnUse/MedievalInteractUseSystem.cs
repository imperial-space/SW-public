using Content.Shared.Imperial.Medieval.DoOnUse.Spawn;
using Content.Shared.Imperial.Medieval.DoOnUse.Action;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Actions;
using Robust.Shared.GameObjects;

namespace Content.Server.Imperial.Medieval.DoOnUse;

public sealed partial class MedievalInteractUseSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SpawnOnUseComponent, MedievalInteractionEvent>(OnSpawnInteract);
        SubscribeLocalEvent<AddActionOnUseComponent, MedievalInteractionEvent>(OnAddActionInteract);
    }

    private void OnSpawnInteract(EntityUid uid, SpawnOnUseComponent component, MedievalInteractionEvent ev)
    {
        // Only respond when the event target is this entity
        if (ev.Target != uid)
            return;

        // Spawn the prototype at the entity's position
        SpawnAtPosition(component.EntityPrototype, Transform(uid).Coordinates);

        // Do not delete bushes by default; if you want the bush to be consumed, call QueueDel(uid);
    }

    private void OnAddActionInteract(EntityUid uid, AddActionOnUseComponent component, MedievalInteractionEvent ev)
    {
        if (ev.Target != uid)
            return;

        _actions.AddAction(ev.User, component.ActionId);

        // Do not delete by default
    }
}
