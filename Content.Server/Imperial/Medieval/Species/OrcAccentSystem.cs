using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Content.Shared.Speech;
using Robust.Shared.Random;

namespace Content.Server.Speech.EntitySystems;

public sealed class OrcAccentSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ReplacementAccentSystem _replacement = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OrcAccentComponent, AccentGetEvent>(OnAccent);
    }

    public string ToInfinitive(string word)
    {
        var verb = word;
        verb = Regex.Replace(verb, "ай+", "ать");
        verb = Regex.Replace(verb, "АЙ+", "АТЬ");

        verb = Regex.Replace(verb, "аю+", "ать");
        verb = Regex.Replace(verb, "АЮ+", "АТЬ");

        verb = Regex.Replace(verb, "ешь+", "ать");
        verb = Regex.Replace(verb, "ЕШЬ+", "АТЬ");

        verb = Regex.Replace(verb, "ёшь+", "ать");
        verb = Regex.Replace(verb, "ЁШЬ+", "АТЬ");

        verb = Regex.Replace(verb, "ете+", "ать");
        verb = Regex.Replace(verb, "ЕТЕ+", "АТЬ");

        verb = Regex.Replace(verb, "ет+", "ать");
        verb = Regex.Replace(verb, "ЕТ+", "АТЬ");

        verb = Regex.Replace(verb, "им+", "ать");
        verb = Regex.Replace(verb, "ИМ+", "АТЬ");

        verb = Regex.Replace(verb, "ишь+", "ать");
        verb = Regex.Replace(verb, "ИШЬ+", "АТЬ");

        verb = Regex.Replace(verb, "ите+", "ать");
        verb = Regex.Replace(verb, "ИТЕ+", "АТЬ");

        verb = Regex.Replace(verb, "ят+", "ать");
        verb = Regex.Replace(verb, "ЯТ+", "АТЬ");

        verb = Regex.Replace(verb, "ал+", "ать");
        verb = Regex.Replace(verb, "АЛ+", "АТЬ");
        return verb;
    }

    public string Accentuate(string message, OrcAccentComponent component)
    {
        // прямые замены слов
        var msg = _replacement.ApplyReplacements(message, "orc");

        // прямые замены личных местоимений
        if (msg.StartsWith("я", StringComparison.Ordinal))
        {
            msg.Remove(0, 1).Insert(0, "моя");
        }

        // приведение глаголов к неопределённой форме
        msg = ToInfinitive(msg);

        return msg;
    }

    private void OnAccent(EntityUid uid, OrcAccentComponent component, AccentGetEvent args)
    {
        args.Message = Accentuate(args.Message, component);
    }
}
