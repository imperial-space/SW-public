using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.Components;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Content.Shared.Mind;
using Content.Server.Objectives;
using Content.Shared.Objectives.Components;
using Content.Server.Roles;
using Content.Server.Antag;
using Content.Shared.Damage;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Server.Chat.Managers;
using Content.Shared.Chat;
using Content.Shared.Mobs;

namespace Content.Server.Imperial.XxRaay.SyndieBattle;

public sealed class SyndieBattleRuleSystem : GameRuleSystem<SyndieBattleRuleComponent>
{
    [Dependency] private readonly TraitorRuleSystem _traitor = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly ObjectivesSystem _objectives = default!;
    [Dependency] private readonly RoleSystem _role = default!;
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IChatManager _chat = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SyndieBattleRuleComponent, AfterAntagEntitySelectedEvent>(OnAntagSelected);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
    }

    protected override void Started(EntityUid uid, SyndieBattleRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        component.Active = true;
    }

    protected override void Ended(EntityUid uid, SyndieBattleRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        component.Active = false;
    }

    private void OnAntagSelected(Entity<SyndieBattleRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        MakeTraitor(args.EntityUid, ent.Comp);
    }

    private void MakeTraitor(EntityUid player, SyndieBattleRuleComponent comp)
    {
        var temp = new TraitorRuleComponent();
        _traitor.MakeTraitor(player, temp);
        AssignTraitorObjectives(player);

        if (TryComp<ActorComponent>(player, out var actor))
        {
            var scoreComp = EnsureComp<SyndieBattleScoreComponent>(player);
            scoreComp.PlayerId = actor.PlayerSession.UserId;
        }
    }

    private void AssignTraitorObjectives(EntityUid player)
    {
        if (!_mind.TryGetMind(player, out var mindId, out var mind))
            return;

        _mind.TryAddObjective(mindId, mind, "SyndieBattleSurviveObjective");

        var maxDifficulty = 7f;
        var picked = 0;
        while (picked < 5 && maxDifficulty > 0f)
        {
            var objective = _objectives.GetRandomObjective(mindId, mind, "TraitorObjectiveGroupKill", maxDifficulty);
            if (objective is null)
                break;

            _mind.AddObjective(mindId, mind, objective.Value);
            var diff = Comp<ObjectiveComponent>(objective.Value).Difficulty;
            maxDifficulty -= diff;
            picked++;
        }
    }

    private void OnMobStateChanged(MobStateChangedEvent ev)
    {
        if (!AnyRuleActive())
            return;

        if (ev.NewMobState != MobState.Dead)
            return;

        if (!TryComp<SyndieBattleScoreComponent>(ev.Target, out var victimScore))
            return;

        var message = Loc.GetString("syndiebattle-kill-score", ("score", victimScore.Score));
        _chat.ChatMessageToAll(ChatChannel.Server, message, message, ev.Target, false, false);
    }

    private bool AnyRuleActive()
    {
        var query = QueryAllRules();
        while (query.MoveNext(out var uid, out var comp, out _))
        {
            if (comp.Active)
                return true;
        }
        return false;
    }
}


