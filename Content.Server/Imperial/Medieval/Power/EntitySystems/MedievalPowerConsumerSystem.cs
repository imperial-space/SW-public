using Content.Shared.Power;
using Content.Shared.Examine;
using Content.Server.Imperial.Medieval.Power;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Imperial.Medieval.Power;
using Content.Shared.UserInterface;
using Content.Shared.Popups;

namespace Content.Server.Imperial.Medieval.Power;

public sealed class MedievalPowerConsumerSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PowerConsumerComponent, PowerConsumerReceivedChanged>(OnReceivedChanged);
        SubscribeLocalEvent<ActivatableUIRequiresPowerConsumerComponent, ActivatableUIOpenAttemptEvent>(OnActivate);
        SubscribeLocalEvent<PowerConsumerComponent, ExaminedEvent>(OnExamined);
    }
    // перенести на клиент туду
    private void OnExamined(Entity<PowerConsumerComponent> ent, ref ExaminedEvent args)
    {
        string text = Loc.GetString("power-receiver-component-on-examine-main",
                    ("stateText", Loc.GetString(IsPowered(ent.Owner)
                        ? "power-receiver-component-on-examine-powered"
                        : "power-receiver-component-on-examine-unpowered"))); // shitcode by wiz

        args.PushMarkup(text);
    }

    private void OnReceivedChanged(Entity<PowerConsumerComponent> ent, ref PowerConsumerReceivedChanged args)
    {
        var component = ent.Comp;
        var requiredPower = component.DrawRate * component.Threshold;
        var isPowered = component.Net != null && args.ReceivedPower >= requiredPower;
        
        if (component.Powered != isPowered)
        {
            component.Powered = isPowered;
            var ev = new PowerChangedEvent(isPowered, args.ReceivedPower);
            RaiseLocalEvent(ent, ref ev);
        }
    }

    public bool IsPowered(EntityUid uid, PowerConsumerComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return true;

        return component.Powered;
    }
    // перенести на клиент туду
    private void OnActivate(Entity<ActivatableUIRequiresPowerConsumerComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled || IsPowered(ent.Owner))
            return;

        _popup.PopupClient(Loc.GetString("base-computer-ui-component-not-powered", ("machine", ent.Owner)), args.User, args.User);
        args.Cancel();
    }
}