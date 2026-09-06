using System.Numerics;
using System.Threading.Tasks;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Content.Shared.IdentityManagement;
using Content.Shared.Imperial.Medieval.SkillCheck;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.SkillCheck;

public sealed class SkillCheckSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly SharedPointLightSystem _lights = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<SkillCheckRequestEvent>(OnSkillCheck);
    }

    private void OnSkillCheck(SkillCheckRequestEvent args, EntitySessionEventArgs session)
    {
        if (session.SenderSession.AttachedEntity is not { } player ||
            TerminatingOrDeleted(player) ||
            !TryComp<SkillsComponent>(player, out var skills))
        {
            return;
        }

        SkillPrototype? skill = null;
        if (args.Skill is { } skillId && !_prototypes.Resolve(skillId, out skill))
            return;

        if (HasComp<SkillCheckCooldownComponent>(player))
        {
            _popup.PopupEntity(Loc.GetString("medieval-skill-check-cooldown"), player, player);
            return;
        }

        var cooldown = AddComp<SkillCheckCooldownComponent>(player);
        _ = RunCooldownAsync((player, cooldown));

        var die = Spawn(cooldown.DiePrototype, new EntityCoordinates(player, Vector2.Zero));
        var component = Comp<SkillCheckDieComponent>(die);
        component.Performer = player;
        component.Skill = args.Skill;
        component.Result = _random.Next(1, 21);
        if (skill != null)
            component.Modifier = (int) Math.Floor((skills.Levels.GetValueOrDefault(skill.ID, 10) - 10) / 2.0);
        component.RollStartedAt = _timing.CurTime;
        Dirty(die, component);
        _ = RunDieAsync((die, component));
    }

    private async Task RunCooldownAsync(Entity<SkillCheckCooldownComponent> ent)
    {
        await Timer.Delay(ent.Comp.Duration);

        if (!TerminatingOrDeleted(ent.Owner) &&
            TryComp<SkillCheckCooldownComponent>(ent.Owner, out var current) &&
            current == ent.Comp)
        {
            RemComp<SkillCheckCooldownComponent>(ent.Owner);
        }
    }

    private async Task RunDieAsync(Entity<SkillCheckDieComponent> ent)
    {
        await Timer.Delay(ent.Comp.AnimationDuration);

        if (TerminatingOrDeleted(ent.Owner) ||
            !TryComp<SkillCheckDieComponent>(ent.Owner, out var current) ||
            current != ent.Comp)
        {
            return;
        }

        if (ent.Comp.Result is 1 or 20)
        {
            _lights.SetColor(ent.Owner,
                ent.Comp.Result == 1 ? ent.Comp.CriticalFailureColor : ent.Comp.CriticalSuccessColor);
            _lights.SetEnabled(ent.Owner, true);
        }

        SendResult(ent);

        await Timer.Delay(ent.Comp.ResultDuration);

        if (!TerminatingOrDeleted(ent.Owner) &&
            TryComp<SkillCheckDieComponent>(ent.Owner, out current) &&
            current == ent.Comp)
        {
            QueueDel(ent.Owner);
        }
    }

    private void SendResult(Entity<SkillCheckDieComponent> ent)
    {
        if (ent.Comp.Performer is not { } performer || TerminatingOrDeleted(performer))
            return;

        var message = ent.Comp.Skill is { } skillId
            ? Loc.GetString("medieval-skill-check-result",
                ("skill", Loc.GetString(_prototypes.Index(skillId).Name)),
                ("roll", ent.Comp.Result),
                ("modifier", ent.Comp.Modifier),
                ("total", ent.Comp.Result + ent.Comp.Modifier))
            : Loc.GetString("medieval-skill-check-die-result", ("roll", ent.Comp.Result));
        var escapedMessage = FormattedMessage.EscapeText(message);
        Color? color = ent.Comp.Result switch
        {
            1 => ent.Comp.CriticalFailureColor,
            20 => ent.Comp.CriticalSuccessColor,
            _ => null,
        };

        foreach (var (session, data) in _chat.GetRecipients(ent.Owner, ChatSystem.VoiceRange))
        {
            if (session.AttachedEntity is not { } listener ||
                TerminatingOrDeleted(listener) ||
                _chat.MessageRangeCheck(session, data, ChatTransmitRange.Normal) != ChatSystem.MessageRangeCheckResult.Full)
            {
                continue;
            }

            var name = FormattedMessage.EscapeText(Identity.Name(performer, EntityManager, listener, showId: false));
            var wrappedMessage = Loc.GetString("medieval-skill-check-result-chat",
                ("name", name),
                ("message", escapedMessage));
            _chatManager.ChatMessageToOne(ChatChannel.Local, message, wrappedMessage,
                EntityUid.Invalid, false, session.Channel, colorOverride: color);
        }
    }
}
