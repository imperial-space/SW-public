using Content.Client.Administration.Systems;
using Content.Shared.Imperial.Medieval.RPPanel;
using Content.Shared.Popups;
using Content.Shared.Verbs;

namespace Content.Client.Imperial.Medieval.RPPanel;

public sealed class RPPanelMenuSystem : EntitySystem
{
    [Dependency] private readonly RPPanelInteractionSystem _interaction = default!;
    [Dependency] private readonly RPPanelCooldownSystem _cooldown = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GetVerbsEvent<Verb>>(OnGetVerbs, after: [typeof(AdminVerbSystem)]);
    }

    private void OnGetVerbs(GetVerbsEvent<Verb> args)
    {
        if (!TryComp<RPPanelMemberComponent>(args.Target, out var component)
            || !_interaction.CanInteract(args.User, args.Target))
            return;

        var category = new VerbCategory("rp-panel-interact", null);
        args.ExtraCategories.Add(category);
        AddVerb(RPPanelAction.Look, "rp-panel-look", 4);
        AddVerb(RPPanelAction.Wave, "rp-panel-wave", 3);
        AddVerb(RPPanelAction.Hug, "rp-panel-hug", 2);
        AddVerb(RPPanelAction.Shake, "rp-panel-shake", 1);

        void AddVerb(RPPanelAction action, string text, int priority)
        {
            var inRange = _interaction.InRange(args.User, args.Target, component, action);
            var onCooldown = _cooldown.IsOnCooldown(component, args.User);
            args.Verbs.Add(new Verb
            {
                Text = Loc.GetString(text),
                Category = category,
                Priority = priority,
                ClientExclusive = true,
                Disabled = !inRange || onCooldown,
                Message = !inRange ? Loc.GetString("rp-panel-too-far")
                    : onCooldown ? Loc.GetString("rp-panel-cooldown") : null,
                Act = () => Execute(args.User, args.Target, action)
            });
        }
    }

    private void Execute(EntityUid user, EntityUid target, RPPanelAction action)
    {
        if (!TryComp<RPPanelMemberComponent>(target, out var component)
            || !_interaction.CanInteract(user, target))
            return;

        if (!_interaction.InRange(user, target, component, action))
        {
            _popup.PopupClient(Loc.GetString("rp-panel-too-far"), user, user);
            return;
        }

        if (_cooldown.IsOnCooldown(component, user))
        {
            _popup.PopupClient(Loc.GetString("rp-panel-cooldown"), user, user);
            return;
        }

        RaisePredictiveEvent(new RPPanelInteractionEvent(GetNetEntity(target), action));
    }
}
