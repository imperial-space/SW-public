using Content.Server.Chat.Managers;
using Content.Shared.Bed.Sleep;
using Robust.Server.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Bed.Sleep;

public sealed class SleepingSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SleepingImperialComponent, ComponentInit>(OnInit);
    }
    private void OnInit(EntityUid uid, SleepingImperialComponent comp, ComponentInit ev)
    {
        comp.Words.AddRange(new[]
    {
    "нормально",
    "не знаю",
    "понятно",
    "не понятно",
    "кстати",
    "пожалуйста",
    "спасибо",
    "привет",
    "окей",
    "мужик",
    "ладно",
    "заебись",
    "может быть",
    "очень",
    "боже мой",
    "не за что",
    "пока",
    "пока",
    "плевать",
    "ясно",
    "всмысле",
    "что за херня?",
    "легко",
    "хорошо сработано",
    "доказательство",
    "докажи",
    "доказал",
    "мда...",
    "нечестно",
    "разблокировать",
    "использовать",
    "используй",
    "использовал",
    "лечение",
    "полечи",
    "помоги",
    "помог",
    "прикол",
    "шутишь"
    });
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_gameTiming.IsFirstTimePredicted) return;
        var curTime = _gameTiming.CurTime;
        var query = EntityQueryEnumerator<SleepingImperialComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            _playerManager.TryGetSessionByEntity(uid, out var session);
            if (session == null || component.Words.Count == 0 || component.Words == null) continue;
            if (component.CurTime <= curTime)
            {
                Console.WriteLine("second");
                component.CurTime = curTime + component.Timing;
                if (HasComp<SleepingComponent>(uid))
                {
                    Console.Write("thirds");
                    var word = "... " + component.Words[_random.Next(component.Words.Count)] + " ...";
                    _chatManager.ChatMessageToOne(Shared.Chat.ChatChannel.Server, word, word, uid, false, session.Channel, component.Color, false, null, 0, null);
                }
            }
        }
    }
    #region addwords
    public void AddWord(SleepingImperialComponent component, string word)
    {
        component.Words.Add(word);
    }
    public void RemoveWord(SleepingImperialComponent component, string word)
    {
        component.Words.Remove(word);
    }
    public void RemoveWord(SleepingImperialComponent component, int i)
    {
        component.Words.RemoveAt(i);
    }
    public void ClearWords(SleepingImperialComponent component)
    {
        component.Words.Clear();
    }
    #endregion
}

