using Content.Shared.Examine;
using Content.Shared.Imperial.Medieval.Power;
using Content.Shared.UserInterface;
using Content.Shared.Popups;

namespace Content.Client.Imperial.Medieval.Power;

public sealed class MedievalPowerConsumerSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ActivatableUIRequiresMedievalPowerComponent, ActivatableUIOpenAttemptEvent>(OnActivate);
        SubscribeLocalEvent<MedievalPowerStateComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<MedievalPowerStateComponent> ent, ref ExaminedEvent args)
    {
        string text = Loc.GetString("power-receiver-component-on-examine-main",
            ("stateText", Loc.GetString(ent.Comp.Powered
                ? "power-receiver-component-on-examine-powered"
                : "power-receiver-component-on-examine-unpowered")));

        args.PushMarkup(text);
    }

    private void OnActivate(Entity<ActivatableUIRequiresMedievalPowerComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!TryComp<MedievalPowerStateComponent>(ent, out var state) || state.Powered)
            return;

        _popup.PopupClient(Loc.GetString("base-computer-ui-component-not-powered", ("machine", ent.Owner)), args.User, args.User);
        args.Cancel();
    }
}
