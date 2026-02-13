using Content.Shared.Actions;
using Content.Shared.Forged;
using Content.Shared.Popups;
using Robust.Shared.Toolshed.TypeParsers;

namespace Content.Shared.Imperial.Medieval.Forged;

public sealed class ForgedAbilitySystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        // Подписываемся на событие использования способности, если она выдается через Actions, на будующее
        //SubscribeLocalEvent<ForgedComponent, ForgedAbilityActionEvent>(OnAbilityPerformed);
    }

    public void ExecuteAbility(EntityUid forgedUid, EntityUid moduleUid, string abilityId)
    {
        // Тот самый "другой файл" с логикой действий
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
        if (!TryComp<EyeComponent>(forgedUid, out var comp)) return;

        //comp.VisibilityMask.
    }
}

