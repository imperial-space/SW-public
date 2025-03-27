using Content.Server.Chat.Managers;
using Content.Shared.Bed.Sleep;
using Robust.Server.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Bed.Sleep;

public sealed class SleepingSystem : EntitySystem
{
    private readonly TimeSpan _timing = TimeSpan.FromSeconds(15);
    private TimeSpan _curTime = TimeSpan.Zero;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_gameTiming.IsFirstTimePredicted) return;
        var curTime = _gameTiming.CurTime;
        var query = EntityQueryEnumerator<SleepingImperialComponent>();
        if (_curTime >= curTime) return;
        while (query.MoveNext(out var uid, out var component))
        {
            _playerManager.TryGetSessionByEntity(uid, out var session);
            if (session == null || component.Words.Count == 0 || component.Words == null) continue;

            _curTime = curTime + _timing;
            if (HasComp<SleepingComponent>(uid))
            {
                var word = "... " + component.Words[_random.Next(component.Words.Count)] + " ...";
                _chatManager.ChatMessageToOne(Shared.Chat.ChatChannel.Server, word, word, uid, false, session.Channel, component.Color, false, null, 0, null);
            }
        }
    }
    #region addwords
    public void AddWord(string word, bool isall, SleepingImperialComponent? component = null)
    {
        var query = EntityQueryEnumerator<SleepingImperialComponent>();
        if (isall)
        {
            while (query.MoveNext(out var _, out var comp))
                comp.Words.Add(word);
        }
        else
        {
            if (component == null) return;
            component.Words.Add(word);
        }
    }
    public void RemoveWord(string word, bool isall, SleepingImperialComponent? component = null)
    {
        var query = EntityQueryEnumerator<SleepingImperialComponent>();
        if (isall)
        {
            while (query.MoveNext(out var _, out var comp))
                comp.Words.Remove(word);
        }
        else
        {
            if (component == null) return;
            component.Words.Remove(word);
        }
    }
    public void RemoveWord(int i, bool isall, SleepingImperialComponent? component = null)
    {
        var query = EntityQueryEnumerator<SleepingImperialComponent>();
        if (isall)
        {
            while (query.MoveNext(out var _, out var comp))
                comp.Words.RemoveAt(i);
        }
        else
        {
            if (component == null) return;
            component.Words.RemoveAt(i);
        }
    }
    public void ClearWords(bool isall, SleepingImperialComponent? component = null)
    {
        var query = EntityQueryEnumerator<SleepingImperialComponent>();
        if (isall)
        {
            while (query.MoveNext(out var _, out var comp))
                comp.Words.Clear();
        }
        else
        {
            if (component == null) return;
            component.Words.Clear();
        }
    }
    #endregion
}

