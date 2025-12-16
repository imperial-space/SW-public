using System.Text;
using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Content.Shared.Speech;
using Robust.Shared.Random;

namespace Content.Server.Speech.EntitySystems;

public sealed class OrcAccentSystem : EntitySystem
{
    // окончания, суффиксы и другие части слов для замены
    // увы, без NLP дальше не уйти!
    // причастия, деепричастия обрабатываться не должны.
    private static readonly (string ending, string replacement)[] VerbEndings =
    {
        // настоящее + будущее совершенное время
        ("аю", "ать"), ("аешь", "ать"), ("аёшь", "ать"), ("ает", "ать"),
        ("аёт", "ать"), ("аем", "ать"), ("аём", "ать"), ("ают", "ать"),

        ("ят", "еть"), ("ит", "еть"), ("ишь", "еть"),

        ("лю", "ить"), ("ью", "ить"), ("ьешь", "ить"), ("ьет", "ить"),
        ("ьем", "ить"), ("ьют", "ить"), ("иву", "ить"), ("ивешь", "ить"),
        ("ивет", "ить"), ("ивем", "ить"), ("ивут", "ить"),

        // будущее время
        ("ам", "ать"), ("ашь", "ать"), ("ану", "ать"), ("аст", "ать"),
        ("адим", "ать"),

        // прошедшее время
        ("ал", "ать"), ("ала", "ать"), ("ало", "ать"), ("али", "ать"),
        ("ил", "ить"), ("ила", "ить"), ("ило", "ить"), ("или", "ить"),
        ("ел", "еть"), ("ели", "еть"), ("ело", "еть"), ("ела", "еть"),
        ("ул", "уть"), ("ула", "уть"), ("ули", "уть"), ("уло", "уть"),
        ("ыл", "ыть"), ("ыла", "ыть"), ("ыло", "ыть"), ("ыли", "ыть"),
        ("ял", "ять"), ("яла", "ять"), ("яло", "ять"), ("яли", "ять"),
        ("ла", "ти"), ("ло", "ти"), ("ли", "ти"),


        // повелительное наклонение (исключения)
        ("ай", "ать"), ("иви", "ить")
    };
    // замены в повелительных глаголах
    // проверяется отдельно для исбежания замен по типу орки – оркать
    private static readonly (string ending, string replacement)[] VerbEndingsImperative =
    {
        ("иви", "ить"), ("иве", "ить"), ("йди", "йти"), ("ли", "лить"),
        ("ади", "ать"), ("ди", "дти"), ("ей", "ить"), ("ае", "ать"),
        ("аё", "ать"), ("ье", "ить"), ("ьё", "ить"), ("ай", "ать"),
        ("и", "ать"), ("ь", "ить"),
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

        // удаление постфиксов (повелительных и возвратных) если они есть
        // бьется - бьет
        // рубитесь - руби
        bool reflexive = lower.EndsWith("ся") || lower.EndsWith("сь");
        if (reflexive) lower = lower.Substring(0, lower.Length - 2);
        bool imperative = lower.EndsWith("те");

        // повелительное наклонение – отдельная проверка...
        if (imperative)
        {
            lower = lower.Substring(0, lower.Length - 2);
            foreach (var (ending, replacement) in VerbEndingsImperative)
            {
                if (lower.EndsWith(ending))
                {
                    int end_index = lower.Length - ending.Length;

                    if (end_index == 0) return word;

                    string stem = word.Substring(0, end_index);
                    string infinitive = reflexive ? stem + replacement + "ся" : stem + replacement;
                    return MatchCase(word, infinitive);
                }
            }
            return word;
        }

        // последовательная проверка на окончания вместо наивных Regex замен
        foreach (var (ending, replacement) in VerbEndings)
        {
            if (lower.EndsWith(ending))
            {
                int end_index = lower.Length - ending.Length;

                if (end_index == 0) return word;

                string stem = word.Substring(0, end_index);
                // TODO: проверить корень и заменить исключения (i.e. поешь)
                string infinitive = reflexive ? stem + replacement + "ся" : stem + replacement;
                return MatchCase(word, infinitive);
            }
        }
        return word;
    }

    public string Accentuate(string message, OrcAccentComponent component)
    {
        // прямые замены слов и местоимений
        var msg = _replacement.ApplyReplacements(message, "orc");

        var result = new StringBuilder();

        // каждое слово обрабатывается отдельно и единожды
        foreach (var element in RegexWordSplit.Split(msg))
        {
            // замена "Я" с учетом регистра

            if (element.ToLower() == "я")
            {
                result.Append(MatchCase(element, "м")).Append("оя");
                continue;
            }

            // приведение глаголов к неопределённой форме
            result.Append(element.Length <= 1 ? element : ToInfinitive(element));
        }

        return result.ToString();
    }

    private void OnAccent(EntityUid uid, OrcAccentComponent component, AccentGetEvent args)
    {
        args.Message = Accentuate(args.Message, component);
    }
}
