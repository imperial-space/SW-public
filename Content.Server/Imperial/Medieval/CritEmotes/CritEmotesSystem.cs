using Content.Server.Body.Components;
using Content.Server.Chat.Systems;
using Robust.Shared.Timing;
using Content.Shared.Chat;
using Content.Shared.Mobs.Components;
using Robust.Shared.Random;

namespace Content.Server.Body.Systems;

public sealed class CritEmotesSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ChatSystem _chat = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SoftCritEmotesComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var crit, out var mob))
        {
            if (_gameTiming.CurTime < crit.NextUpdate)
                continue;

            crit.NextUpdate += TimeSpan.FromSeconds(_random.NextFloat(4f, 7f));

            if (mob.CurrentState != Shared.Mobs.MobState.Critical)  // TODO софт крит вместо обычного
                continue;

            _chat.TryEmoteWithChat(uid, _random.Pick(crit.Emotes), ChatTransmitRange.HideChat, ignoreActionBlocker: true);
        }
    }
}
