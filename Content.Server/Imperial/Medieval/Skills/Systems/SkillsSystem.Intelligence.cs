using System.Text;
using Content.Server.Speech;
using Content.Server.Speech.Components;
using Content.Shared.Imperial.Medieval.Magic.Mana;
using Content.Shared.Imperial.Medieval.Medical;
using Content.Shared.Imperial.Medieval.Skills;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.Skills;

public sealed partial class SkillsSystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    private void InitializeIntelligence()
    {
        SubscribeLocalEvent<SkillsComponent, GetHealingSpeedModifiersEvent>(OnGetHealingSpeedModifiers);
        SubscribeLocalEvent<SkillsComponent, AccentGetEvent>(OnAccent);
    }

    private void OnGetHealingSpeedModifiers(EntityUid uid, SkillsComponent comp, ref GetHealingSpeedModifiersEvent args)
    {
        var (proto, level) = GetSkill(uid, IntelligenceId);

        if (level == 10)
            return;

        var diff = Math.Abs(level - 10);

        args.Modifier += (level > 10 ? proto.Modifiers["PositiveHealingSpeedModifier"] : proto.Modifiers["NegativeHealingSpeedModifier"]) * diff;
    }

    private void IntelligenceLevelSet(EntityUid uid, int level, int oldLevel)
    {
        var (proto, _) = GetSkill(uid, IntelligenceId);

        var diff = level - oldLevel;

        if (TryComp<ManaComponent>(uid, out var mana))
        {
            mana.MaxMana += (level > 10 ? proto.Modifiers["PositiveManaModifier"] : proto.Modifiers["NegativeManaModifier"]) * diff;
            Dirty(uid, mana);
        }
    }

    private void OnAccent(EntityUid uid, SkillsComponent component, AccentGetEvent args)
    {
        var (proto, level) = GetSkill(uid, IntelligenceId);

        if (level >= 5)
            return;

        var prob = proto.Modifiers["LowStupidityChance"];

        if (level <= 1)
            prob = 1;

        args.Message = Accentuate(args.Message, prob);
    }

    private string Accentuate(string message, float scale)
    {
        var builder = new StringBuilder();

        var wordBeginIndex = 0;

        for (var i = 0; i < message.Length; i++)
        {
            var ch = char.ToLower(message[i]);
            // A word ends when one of the following is found: a space, a sentence end, or EOM
            if (char.IsWhiteSpace(ch) || (ch is '.' or '!' or '?' or '~' or '-' or ',') || i == message.Length - 1)
            {
                var wordLength = i - wordBeginIndex;
                if (wordLength > 0)
                {
                    if (!_random.Prob(scale))
                    {
                        builder.Append(message.Substring(wordBeginIndex, wordLength));
                        continue;
                    }

                    var replacement = _random.Pick(new[]
                    {
                        "ээ",
                        "э-э-э",
                        "эмм",
                        "уэээ",
                        "типа",
                        "в общем",
                        "короче",
                        "вообще",
                        "наверное",
                        "нуу",
                        "ммм"
                    });

                    if (wordBeginIndex == 0)
                    {
                        var replacementBuilder = new StringBuilder(replacement);
                        replacementBuilder[0] = char.ToUpper(replacement[0]);
                        replacement = replacementBuilder.ToString();
                    }

                    builder.Append(replacement);
                }

                if (char.IsWhiteSpace(ch) || (ch is '.' or '!' or '?' or '~' or '-' or ','))
                    builder.Append(ch);
                if (ch is ('.' or '!' or '?' or '~' or ',') && message.Length >= i + 2 && char.ToLower(message[i + 1]) is not ('.' or '!' or '?' or '~' or ','))
                    builder.Append(' ');

                wordBeginIndex = i + 1;
            }
        }

        return builder.ToString();
    }
}
