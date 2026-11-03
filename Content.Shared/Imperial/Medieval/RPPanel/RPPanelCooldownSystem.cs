using System.Threading.Tasks;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Medieval.RPPanel;

public sealed class RPPanelCooldownSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;

    public bool IsOnCooldown(RPPanelMemberComponent component, EntityUid user)
    {
        return component.RecentInitiators.Contains(user) || component.PendingInitiators.Contains(user);
    }

    public bool TryStartCooldown(EntityUid target, RPPanelMemberComponent component, EntityUid user)
    {
        if (IsOnCooldown(component, user))
            return false;

        var initiators = _net.IsClient ? component.PendingInitiators : component.RecentInitiators;
        initiators.Add(user);
        if (_net.IsServer)
            Dirty(target, component);

        _ = RemoveInitiatorAsync(target, component, user, initiators);
        return true;
    }

    private async Task RemoveInitiatorAsync(EntityUid target, RPPanelMemberComponent component, EntityUid user,
        List<EntityUid> initiators)
    {
        await Timer.Delay(component.InteractionCooldown);

        if (TerminatingOrDeleted(target)
            || !TryComp<RPPanelMemberComponent>(target, out var current)
            || current != component)
            return;

        initiators.Remove(user);
        if (_net.IsServer)
            Dirty(target, component);
    }
}
