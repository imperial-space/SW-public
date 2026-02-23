using Content.Shared.Forged;
using Content.Shared.Imperial.LocalLight;
using Robust.Client.GameObjects;

public sealed class ForgedAbilityExecuterClientSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<ForgedExecuteAbilityEvent>(OnServerExecuteAbility);
    }

    void OnServerExecuteAbility(ForgedExecuteAbilityEvent args)
    {
        EntityUid uid = GetEntity(args.ForgedUid);
        switch (args.AbilityId)
        {
            case "ThermalVisionEyes":
                break;
            default:
                break;
        }
    }
}
