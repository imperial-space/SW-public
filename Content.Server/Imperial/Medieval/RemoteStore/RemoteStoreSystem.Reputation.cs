using System.Diagnostics.CodeAnalysis;
using Content.Server.Mind;
using Content.Shared.Mind;
using JetBrains.Annotations;

namespace Content.Server.Imperial.Medieval.RemoteStore;

public sealed partial class RemoteStoreSystem
{
    [Dependency] private readonly MindSystem _mind = default!;

    private void InitReputation()
    {
        SubscribeLocalEvent<RemoteStoreServerComponent, ComponentShutdown>(OnStoreServerShutdown);
        SubscribeLocalEvent<StoreReputationKeepComponent, ComponentShutdown>(OnReputationKeepShutdown);
    }

    private void OnReputationKeepShutdown(Entity<StoreReputationKeepComponent> ent, ref ComponentShutdown args)
    {
        foreach (var server in ent.Comp.Servers)
        {
            if (!TryComp<RemoteStoreServerComponent>(server, out var comp))
            {
                continue; // Пох.
            }

            comp.MindsReputation.Remove(ent);
        }
    }

    private void OnStoreServerShutdown(Entity<RemoteStoreServerComponent> ent, ref ComponentShutdown args)
    {
        foreach (var mind in ent.Comp.MindsReputation.Keys)
        {
            if (!TryComp<StoreReputationKeepComponent>(mind, out var comp))
            {
                Log.Error($"mind {mind} " +
                          $"with a reputation in RemoteStore {ent.Owner} does not have a StoreReputationKeepComponent");
                continue; // Не пох.
            }

            comp.Servers.Remove(mind);
        }
    }

    [PublicAPI]
    public bool TryGetReputation(
        Entity<RemoteStoreServerComponent?> server,
        EntityUid target,
        [NotNullWhen(true)] out int? reputation
    )
    {
        reputation = null;
        if (!Resolve(server, ref server.Comp))
            return false;

        if (HasComp<MindComponent>(target)) // Is target mind-ent?
        {
            reputation = GetReputation(server!, target);
            return true;
        }

        if (!_mind.TryGetMind(target, out var mindUid, out _))
            return false;

        reputation = GetReputation(server!, mindUid);
        return true;
    }

    private static int GetReputation(Entity<RemoteStoreServerComponent> server, EntityUid mindUid)
    {
        return !server.Comp.MindsReputation.TryGetValue(mindUid, out var rep) ? 0 : rep;
    }

    [PublicAPI]
    public bool TryChangeReputation(Entity<RemoteStoreServerComponent?> server, EntityUid target, int amount)
    {
        if (!Resolve(server, ref server.Comp))
            return false;

        if (HasComp<MindComponent>(target)) // Is target mind-ent?
        {
            ChangeReputation(server!, target, amount);
            return true;
        }

        if (!_mind.TryGetMind(target, out var mindUid, out _))
            return false;

        ChangeReputation(server!, mindUid, amount);
        return true;
    }

    private void ChangeReputation(Entity<RemoteStoreServerComponent> server, EntityUid mindUid, int amount)
    {
        EnsureComp<StoreReputationKeepComponent>(mindUid).Servers.Add(server);
        if (!server.Comp.MindsReputation.TryAdd(mindUid, amount))
            server.Comp.MindsReputation[mindUid] += amount;
    }
}
