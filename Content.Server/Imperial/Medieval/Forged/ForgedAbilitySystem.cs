using Content.Shared.Actions;
using Content.Shared.Forged;
using Content.Shared.Imperial.Medieval.Lycantropy;
using Content.Shared.Popups;

namespace Content.Shared.Imperial.Medieval.Forged;

public sealed class ForgedAbilitySystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ForgedComponent, ThermalEyesActionEvent>(OnThermalEyes);
    }

    public void ExecuteAbility(EntityUid forgedUid, EntityUid moduleUid, string abilityId)
    {
        switch (abilityId)
        {
            case "ThermalEyes":
                ThermalEyes(forgedUid);
                break;
            default:
                break;
        }
    }

    private void ThermalEyes(EntityUid forgedUid)
    {
        _actions.AddAction(forgedUid, "ThermalEyesAction");
    }

    private void OnThermalEyes(EntityUid uid, ForgedComponent comp, ThermalEyesActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (HasComp<WerewolfBloodFeelComponent>(uid))
            RemComp<WerewolfBloodFeelComponent>(uid);
        else
            EnsureComp<WerewolfBloodFeelComponent>(uid);
    }
}

