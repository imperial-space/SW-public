using System.Linq;
using System.Text;
using Content.Server._CP14.Workbench;
using Content.Server.Examine;
using Content.Server.Imperial.Medieval.Language;
using Content.Server.Speech;
using Content.Server.Speech.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Imperial.Medieval.Language;
using Content.Shared.Imperial.Medieval.Illitid;
using Content.Shared.Imperial.Medieval.Magic.Mana;
using Content.Shared.Imperial.Medieval.MagicRunes.Components;
using Content.Shared.Imperial.Medieval.Medical;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Speech;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.Skills;

public sealed partial class SkillsSystem
{
    [Dependency] private readonly ExamineSystem _examine = default!;
    [Dependency] private readonly LanguageSystem _lang = default!;

    private void InitializeIntelligence()
    {
        SubscribeLocalEvent<SkillsComponent, CheckWorkbenchCraftSpeedModifiersEvent>(OnGetCraftingSpeedModifiers);
        SubscribeLocalEvent<SkillsComponent, AccentGetEvent>(OnAccent);

        SubscribeNetworkEvent<GetEnteredChatTextResponseMessage>(OnGetMessage);
    }

    private void OnGetCraftingSpeedModifiers(EntityUid uid, SkillsComponent comp, ref CheckWorkbenchCraftSpeedModifiersEvent args)
    {
        if (args.User != uid)
            return;
        var (proto, level) = GetSkill(uid, IntelligenceId);
        args.Modifier /= SkillScaling.Multiplier(level, proto.Modifiers["WorkSpeedPerLevel"]);
    }

    private void IntelligenceLevelSet(EntityUid uid, int level, int oldLevel)
    {
        if (!TryComp<LanguageSpeakerComponent>(uid, out var languages))
            return;
        var state = EnsureComp<SkillProgressionComponent>(uid);
        if (level >= SkillScaling.Master && !state.LanguagesSelected)
        {
            state.LanguagesSelected = true;
            var candidates = _proto.EnumeratePrototypes<LanguagePrototype>()
                .Where(x => x.HighIntelligenceAllowed && !languages.Languages.ContainsKey(x.ID)).ToList();
            for (var i = 0; i < 2 && candidates.Count > 0; i++)
                state.RandomLanguages.Add(_random.PickAndTake(candidates).ID);
        }
        if (level >= SkillScaling.Master)
        {
            foreach (var language in state.RandomLanguages)
            {
                if (!languages.Languages.TryGetValue(language, out var knowledge) || knowledge < LanguageKnowledge.Speak)
                    _lang.AddSpokenLanguage(uid, language, LanguageKnowledge.Speak);
            }
        }
        if (level >= SkillScaling.Legendary)
        {
            foreach (var language in _proto.EnumeratePrototypes<LanguagePrototype>().Where(x => x.HighIntelligenceAllowed))
            {
                if (!languages.Languages.TryGetValue(language.ID, out var knowledge) || knowledge < LanguageKnowledge.Speak)
                    _lang.AddSpokenLanguage(uid, language.ID, LanguageKnowledge.Speak);
            }
        }
    }

    private void OnAccent(EntityUid uid, SkillsComponent component, AccentGetEvent args)
    {
        var (proto, level) = GetSkill(uid, IntelligenceId);

        if (level >= SkillScaling.Basic)
            return;

        var prob = proto.Modifiers["LowStupidityChance"];

        if (level <= 1)
            prob = 1;

        args.Message = Accentuate(args.Message, prob);
    }

    private void OnGetMessage(GetEnteredChatTextResponseMessage message, EntitySessionEventArgs args)
    {
        var target = GetEntity(message.Target);
        var user = GetEntity(message.User);
        if (args.SenderSession.AttachedEntity != target || !Exists(user) || !Exists(target)
            || !_examine.CanExamine(user, target)
            || GetSkill(user, IntelligenceId).Item2 < SkillScaling.Legendary && !HasComp<IllitidComponent>(user)
            || GetSkill(target, IntelligenceId).Item2 >= SkillScaling.Master)
            return;
        _examine.SendExamineTooltip(user, target, FormattedMessage.FromUnformatted(message.Text != string.Empty ? $"По глазам легко читается - '{message.Text}'." : $"Кажется, {Identity.Name(target, EntityManager, user)} не планирует ничего говорить."), false, false);
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
                var wordLength = i - wordBeginIndex + 1;
                if (wordLength > 0)
                {
                    if (!_random.Prob(scale))
                    {
                        builder.Append(message.Substring(wordBeginIndex, wordLength));
                        wordBeginIndex = i + 1;
                        continue;
                    }

                    var replacement = _random.Pick(new[]
                    {
                        "ээ",
                        "э-э-э",
                        "эмм",
                        "уэээ",
                        "ааа...",
                        "ммм",
                        "эмм",
                        "ыээ"
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
