using System.Text;
using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Content.Shared.Speech;
using Robust.Shared.Random;

namespace Content.Server.Speech.EntitySystems;

public sealed class OrcAccentSystem : EntitySystem
{
    // длинные окончания должны идти первыми
    private static readonly (string ending, string replacement)[] VerbEndings =
    {
        ("ай", "ать"), ("аю", "ать"), ("ешь", "ать"), ("ёшь", "ать"),
        ("ете", "ать"), ("ет", "ать"), ("им", "ать"), ("ишь", "ать"),
        ("ите", "ать"), ("ят", "ать"), ("ал", "ать")
    };
    private static readonly Regex RegexWordSplit = new(@"(?<=[^\p{L}\d])|(?=[^\p{L}\d])");
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ReplacementAccentSystem _replacement = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OrcAccentComponent, AccentGetEvent>(OnAccent);
    }

    private string MatchCase(string src, string dest)
    {
        if (string.IsNullOrEmpty(src)) return dest;
        // целевое окончание может иметь только два регистра
        // проверяем его по последней букве корня
        if (char.IsUpper(src[^1])) return dest.ToUpper();
        return dest;
    }

    public string ToInfinitive(string word)
    {
        var lower = word.ToLower();

        // последовательная проверка на окончания вместо наивных Regex замен
        foreach (var (ending, replacement) in VerbEndings)
        {
            if (lower.EndsWith(ending))
            {
                int end_index = word.Length - ending.Length;

                {
                    string stem = word.Substring(0, end_index);
                    // TODO: проверить корень и заменить исключения (i.e. поешь)
                    string infinitive = stem + replacement;
                    return MatchCase(word, infinitive);
                }
            }
        }
        return word;
    }

    public string Accentuate(string message, OrcAccentComponent component)
    {
        // прямые замены слов
        var msg = _replacement.ApplyReplacements(message, "orc");

        var result = new StringBuilder();

        foreach (var element in RegexWordSplit.Split(msg))
        {
            // прямые замены личных местоимений

            if (element.ToLower() == "я")
            {
                result.Append("моя");
            }

            if (element.Length == 1)
            {
                result.Append(element);
                continue;
            }

            // приведение глаголов к неопределённой форме
            result.Append(ToInfinitive(element));
        }

        return result.ToString();
    }

    private void OnAccent(EntityUid uid, OrcAccentComponent component, AccentGetEvent args)
    {
        args.Message = Accentuate(args.Message, component);
    }
}
