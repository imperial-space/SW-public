using Content.Server.Imperial.Medieval.Skills;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.Actions;
using Content.Shared.Body.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Medieval.GhostSkills;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.GhostSkills;

public sealed class GhostSkillProfileSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GhostSkillProfileComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<GhostSkillProfileComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<GhostSkillProfileComponent, OpenGhostSkillsActionEvent>(OnOpenAction);
        SubscribeLocalEvent<GhostRoleComponent, TakeGhostRoleEvent>(
            OnTakeGhostRole,
            before: new[] { typeof(GhostRoleSystem) });
        SubscribeNetworkEvent<SaveGhostSkillsMessage>(OnSave);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<PendingGhostSkillProfileComponent>();
        while (query.MoveNext(out var uid, out var pending))
        {
            if (!_players.TryGetSessionById(pending.Player, out var player) ||
                player.AttachedEntity is not { Valid: true } target ||
                target == pending.OriginalEntity)
            {
                RemCompDeferred<PendingGhostSkillProfileComponent>(uid);
                continue;
            }

            ApplySkills(target, pending.Levels);
            RemCompDeferred<PendingGhostSkillProfileComponent>(uid);
        }
    }

    private void OnMapInit(Entity<GhostSkillProfileComponent> ent, ref MapInitEvent args)
    {
        if (!SharedSkillsSystem.TryValidateSkillLevels(
                _prototypes,
                ent.Comp.Levels,
                out var levels,
                GhostSkillProfileComponent.MinimumLevel,
                GhostSkillProfileComponent.MaximumLevel))
            levels = SharedSkillsSystem.GetDefaultSkillLevels(_prototypes);

        ent.Comp.Levels = levels;
        _actions.AddAction(ent.Owner, ref ent.Comp.Action, ent.Comp.ActionPrototype);
    }

    private void OnShutdown(Entity<GhostSkillProfileComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.Action);
    }

    private void OnOpenAction(Entity<GhostSkillProfileComponent> ent, ref OpenGhostSkillsActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        RaiseNetworkEvent(new OpenGhostSkillsMenuMessage(new(ent.Comp.Levels)), ent.Owner);
    }

    private void OnSave(SaveGhostSkillsMessage message, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } uid ||
            !TryComp<GhostSkillProfileComponent>(uid, out var component))
            return;

        if (!SharedSkillsSystem.TryValidateSkillLevels(
                _prototypes,
                message.Levels,
                out var levels,
                GhostSkillProfileComponent.MinimumLevel,
                GhostSkillProfileComponent.MaximumLevel))
        {
            _popup.PopupEntity(Loc.GetString("ghost-skills-invalid-points"), uid, uid, PopupType.MediumCaution);
            return;
        }

        component.Levels = levels;
        RaiseNetworkEvent(new GhostSkillsSavedMessage(), args.SenderSession);
        _popup.PopupEntity(Loc.GetString("ghost-skills-saved"), uid, uid);
    }

    private void OnTakeGhostRole(
        Entity<GhostRoleComponent> ent,
        ref TakeGhostRoleEvent args)
    {
        if (args.Player.AttachedEntity is not { Valid: true } original ||
            !TryComp<GhostSkillProfileComponent>(original, out var profile) ||
            !_mind.TryGetMind(args.Player, out var mindUid, out _))
            return;

        var pending = EnsureComp<PendingGhostSkillProfileComponent>(mindUid);
        pending.Player = args.Player.UserId;
        pending.OriginalEntity = original;
        pending.Levels = new(profile.Levels);
    }

    private void ApplySkills(EntityUid target, Dictionary<string, int> levels)
    {
        if (!HasComp<HumanoidAppearanceComponent>(target) &&
            !HasComp<SkillsComponent>(target) &&
            !(HasComp<BodyComponent>(target) &&
              HasComp<HandsComponent>(target) &&
              HasComp<InventoryComponent>(target)))
            return;

        _skills.ApplySkills(target, levels);
    }
}
