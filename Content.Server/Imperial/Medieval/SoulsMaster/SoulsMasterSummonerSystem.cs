/// <summary>
/// Spellward International add;
/// Periodically spawns configured summon entities around living mobs that have a
/// SoulsMasterSummonerComponent. The first summon is delayed by the
/// component's initial delay, and following summons use its configured cooldown
/// </summary>
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.SoulsMaster;

public sealed class SoulsMasterSummonerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SoulsMasterSummonerComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, SoulsMasterSummonerComponent component, MapInitEvent args)
    {
        component.NextSummon = _timing.CurTime + component.InitialDelay;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SoulsMasterSummonerComponent, MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var summoner, out var mobState, out var transform))
        {
            if (mobState.CurrentState != MobState.Alive || summoner.NextSummon > _timing.CurTime)
                continue;

            summoner.NextSummon = _timing.CurTime + summoner.Cooldown;

            for (var i = 0; i < summoner.SummonCount; i++)
            {
                var offset = _random.NextVector2() * summoner.SummonRadius;
                Spawn(summoner.SummonPrototype, transform.Coordinates.Offset(offset));
            }
        }
    }
}
