using Content.Shared.Actions;
using Content.Shared.Imperial.Medieval.Magic;
using Content.Shared.Imperial.Medieval.Magic.MedievalActionCooldownOnDespawn;

namespace Content.Server.Imperial.Medieval.Magic.MedievalActionCooldownOnDespawn;


public sealed class MedievalActionCooldownOnDespawnSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalActionCooldownOnDespawnComponent, MedievalAfterSpawnEntityBySpellEvent>(OnSpawned);
        SubscribeLocalEvent<MedievalActionCooldownOnDespawnComponent, EntityTerminatingEvent>(OnTerminating);
    }

    private void OnSpawned(EntityUid uid, MedievalActionCooldownOnDespawnComponent component, MedievalAfterSpawnEntityBySpellEvent args)
    {
        component.Action = args.Action;
    }

    private void OnTerminating(Entity<MedievalActionCooldownOnDespawnComponent> ent, ref EntityTerminatingEvent args)
    {
        if (ent.Comp.Action is not { } action || TerminatingOrDeleted(action))
            return;

        _actions.SetCooldown(action, ent.Comp.Cooldown);
    }
}
