using System.Linq;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Imperial.Medieval.Magic.Mana;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Server.GameStates;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed class BossRaidHudSystem : EntitySystem
{
    [Dependency] private readonly BossArenaMapSystem _arenas = default!;
    [Dependency] private readonly MobThresholdSystem _thresholds = default!;
    [Dependency] private readonly Content.Shared.Damage.Systems.SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly PvsOverrideSystem _pvs = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    private readonly Dictionary<ICommonSession, HashSet<EntityUid>> _viewers = new();
    private TimeSpan _nextUpdate;

    public List<BossRaidMember> GetMembers(EntityUid arena)
    {
        var result = new List<BossRaidMember>();
        EntityUid? tank = null;
        if (TryComp<BossArenaMapComponent>(arena, out var map) && TryComp<BossFocusMarkerComponent>(map.Boss, out var focus))
            tank = focus.Tank;
        foreach (var uid in _arenas.GetParty(arena).Take(8).OrderByDescending(uid => uid == tank)) // show the tank first in the party list
        {
            EnsureComp<BossArenaPartyMemberComponent>(uid);
            var maximum = _thresholds.TryGetIncapThreshold(uid, out var threshold) ? threshold.Value.Float() : 0;
            var hp = TryComp<DamageableComponent>(uid, out var damage) ? Math.Clamp(maximum - damage.TotalDamage.Float(), 0, maximum) : 0;
            var energy = TryComp<StaminaComponent>(uid, out var stamina) ? Math.Max(0, stamina.CritThreshold - _stamina.GetStaminaDamage(uid, stamina)) : 0;
            var mana = TryComp<ManaComponent>(uid, out var magic) ? Math.Max(0, magic.Mana) : 0;
            var connected = TryComp<ActorComponent>(uid, out var actor) &&
                            actor.PlayerSession.Status is SessionStatus.Connected or SessionStatus.InGame;
            result.Add(new(GetNetEntity(uid), Name(uid), hp, maximum, energy, stamina?.CritThreshold ?? 0,
                mana, magic?.MaxMana ?? 0, Comp<MobStateComponent>(uid).CurrentState, connected));
        }
        return result;
    }

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _nextUpdate) return;
        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(0.25);
        var current = new HashSet<ICommonSession>();
        var arenas = EntityQueryEnumerator<BossArenaMapComponent>();
        while (arenas.MoveNext(out var map, out _))
        {
            var members = GetMembers(map);
            var bodies = _arenas.GetParty(map).Take(8).ToHashSet();
            foreach (var uid in bodies)
            {
                if (!TryComp<ActorComponent>(uid, out var actor) ||
                    actor.PlayerSession.Status is not (SessionStatus.Connected or SessionStatus.InGame)) continue;
                var session = actor.PlayerSession;
                current.Add(session);
                if (!_viewers.TryGetValue(session, out var previous)) _viewers[session] = previous = new();
                foreach (var old in previous.Except(bodies).ToArray())
                {
                    if (Exists(old)) _pvs.RemoveSessionOverride(old, session);
                    previous.Remove(old);
                }
                foreach (var body in bodies)
                    if (previous.Add(body)) _pvs.AddSessionOverride(body, session);
                RaiseNetworkEvent(new BossRaidHudEvent(GetNetEntity(map), members), Filter.SinglePlayer(session)); // send this arena roster to this player
            }
        }
        foreach (var session in _viewers.Keys.Except(current).ToArray())
        {
            foreach (var uid in _viewers[session])
                if (Exists(uid)) _pvs.RemoveSessionOverride(uid, session);
            _viewers.Remove(session);
            if (session.Status is SessionStatus.Connected or SessionStatus.InGame)
                RaiseNetworkEvent(new BossRaidHudEvent(null, new()), Filter.SinglePlayer(session)); // clear the list after leaving the arena
        }
    }

    public override void Shutdown()
    {
        foreach (var (session, members) in _viewers)
            foreach (var uid in members)
                if (Exists(uid)) _pvs.RemoveSessionOverride(uid, session);
        _viewers.Clear();
        base.Shutdown();
    }
}
