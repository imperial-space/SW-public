using Content.Shared.Actions;
using Content.Shared.Forged;
using Content.Shared.Imperial.LocalLight;
using Content.Shared.Imperial.Medieval.Lycantropy;
using Content.Shared.Overlays;
using Content.Shared.Popups;
using Robust.Shared.Player;

namespace Content.Shared.Imperial.Medieval.Forged;

public sealed class ForgedAbilitySystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ForgedComponent, BloodEyesActionEvent>(OnBloodEyes);
    }

    public void ExecuteAbility(EntityUid forgedUid, EntityUid moduleUid, string abilityId)
    {
        switch (abilityId)
        {
            case "BloodVisionEyes":
                _actions.AddAction(forgedUid, "BloodEyesAction");
                break;
            case "MedicalEyes":
                MedicalEyes(forgedUid);
                break;
            case "InvisibleVisionEyes":
                InvisibleVisionEyes(forgedUid);
                break;
            case "NightVisionEyes":
                NightVisionEyes(forgedUid);
                break;
            default:
                break;
        }
    }

    private void MedicalEyes(EntityUid forgedUid)
    {
        var comp1 = EnsureComp<ShowHealthBarsComponent>(forgedUid);
        var comp2 = EnsureComp<ShowHealthIconsComponent>(forgedUid);
        Dirty(forgedUid, comp1);    //По идее бессмысленно, но лучше всегда их обновлять имхо
        Dirty(forgedUid, comp2);
    }
    private void InvisibleVisionEyes(EntityUid forgedUid)
    {
        var comp = EnsureComp<ShowThirstIconsComponent>(forgedUid);
        Dirty(forgedUid, comp);
    }
    private void NightVisionEyes(EntityUid forgedUid)
    {
        if (!TryComp<LocalLightComponent>(forgedUid, out var comp)) return;
        comp.Radius = 10;
        Dirty(forgedUid, comp);
    }

    private void OnBloodEyes(EntityUid uid, ForgedComponent comp, BloodEyesActionEvent args)
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

