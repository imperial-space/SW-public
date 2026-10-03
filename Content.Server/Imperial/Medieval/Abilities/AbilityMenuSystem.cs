using Content.Shared.Actions;
using Content.Shared.Imperial.Medieval.Abilities;

namespace Content.Server.Imperial.Medieval.Abilities;

/// <summary>Owns the shared menu button, independently of the sources of its entries.</summary>
public sealed class AbilityMenuSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AbilityMenuComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<AbilityMenuComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<AbilityMenuComponent> ent, ref ComponentStartup args)
    {
        _actions.AddAction(ent, ref ent.Comp.MenuAction, "ActionAbilityMenu");
    }

    private void OnShutdown(Entity<AbilityMenuComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.MenuAction is not { } action || TerminatingOrDeleted(action))
            return;

        // The body's action container may already have detached this during entity shutdown.
        _actions.RemoveAction(action);
        QueueDel(action);
        ent.Comp.MenuAction = null;
    }
}
