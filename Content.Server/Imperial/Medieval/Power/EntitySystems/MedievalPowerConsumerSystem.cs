using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Examine;
using Content.Shared.Imperial.Medieval.Power;
using Content.Shared.UserInterface;
using ActivatableUISystem = Content.Shared.UserInterface.ActivatableUISystem;
using Content.Server.Imperial.Medieval.Power;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;

namespace Content.Server.Imperial.Medieval.Power;

public sealed class MedievalPowerConsumerSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ActivatableUISystem _activatableUI = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PowerConsumerComponent, PowerConsumerReceivedChanged>(OnReceivedChanged);

        SubscribeLocalEvent<ActivatableUIRequiresMedievalPowerComponent, ActivatableUIOpenAttemptEvent>(OnActivate);
        SubscribeLocalEvent<ActivatableUIRequiresMedievalPowerComponent, PowerChangedEvent>(OnPowerChanged);

        SubscribeLocalEvent<MedievalPowerStateComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<MedievalPowerStateComponent, ComponentStartup>(OnStateCompStartup);
        SubscribeLocalEvent<MedievalPowerStateComponent, PowerChangedEvent>(OnStateCompPowerChanged);
    }

    private void OnStateCompPowerChanged(Entity<MedievalPowerStateComponent> ent, ref PowerChangedEvent args)
    {
        if (ent.Comp.Powered != args.Powered)
            return;

        _appearance.SetData(ent.Owner, MyrmexPowerVisuals.Powered, args.Powered);
    }

    private void OnExamined(Entity<MedievalPowerStateComponent> ent, ref ExaminedEvent args)
    {
        string text = Loc.GetString("power-receiver-component-on-examine-main",
                    ("stateText", Loc.GetString(ent.Comp.Powered
                        ? "power-receiver-component-on-examine-powered"
                        : "power-receiver-component-on-examine-unpowered"))); // shitcode by wiz

        args.PushMarkup(text);
    }

    private void OnActivate(Entity<ActivatableUIRequiresMedievalPowerComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled || IsPowered(ent.Owner))
            return;

        _popup.PopupClient(Loc.GetString("base-computer-ui-component-not-powered", ("machine", ent.Owner)), args.User, args.User);
        args.Cancel();
    }

    private void OnStateCompStartup(Entity<MedievalPowerStateComponent> ent, ref ComponentStartup args)
    {
        if (TryComp<PowerConsumerComponent>(ent, out var power))
        {
            ent.Comp.Powered = power.Powered;
            Dirty(ent);
        }
    }

    private void OnReceivedChanged(Entity<PowerConsumerComponent> ent, ref PowerConsumerReceivedChanged args)
    {
        var component = ent.Comp;
        var requiredPower = component.DrawRate * component.Threshold;
        var isPowered = component.Net != null && args.ReceivedPower >= requiredPower;
        
        if (component.Powered != isPowered)
        {
            component.Powered = isPowered;
            
            if (TryComp<MedievalPowerStateComponent>(ent, out var state))
            {
                state.Powered = isPowered;
                Dirty(ent, state);
            }
            
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

    private void OnPowerChanged(Entity<ActivatableUIRequiresMedievalPowerComponent> ent, ref PowerChangedEvent args)
    {
        if (!args.Powered)
            _activatableUI.CloseAll(ent.Owner);
    }
}