using Content.Client.Alerts;
using Content.Client.Eye.Blinding;
using Content.Client.Imperial.Medieval.Plague.UI;
using Content.Shared.Alert.Components;
using Content.Shared.Imperial.Medieval.Plague;
using Content.Shared.Revenant;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Medieval.Plague;

public sealed partial class MedievalPlagueSystem : SharedMedievalPlagueSystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private VomitSicknessOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<OpenPlagueMenuMessage>(OnOpenMenu);
        SubscribeNetworkEvent<PopulatePlagueMenuMessage>(OnPopulateMenu);

        SubscribeLocalEvent<MedievalPlagueInfectedComponent, GetStatusIconsEvent>(OnInfectedGetStatusIcons);
        SubscribeLocalEvent<MedievalPlagueImmuneComponent, GetStatusIconsEvent>(OnImmuneGetStatusIcons);

        SubscribeLocalEvent<VomitSicknessComponent, ComponentInit>(OnSickInit);
        SubscribeLocalEvent<VomitSicknessComponent, ComponentShutdown>(OnSickShutdown);

        SubscribeLocalEvent<VomitSicknessComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<VomitSicknessComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);

        SubscribeLocalEvent<MedievalPlagueGhostComponent, GetGenericAlertCounterAmountEvent>(OnGetCounterAmount);

        _overlay = new();
    }

    private void OnOpenMenu(OpenPlagueMenuMessage args)
    {
        _ui.GetUIController<MedievalPlagueUiController>().ToggleMenu(args.Data, args.Info, args.AllowedPoints);
    }

    private void OnPopulateMenu(PopulatePlagueMenuMessage args)
    {
        _ui.GetUIController<MedievalPlagueUiController>().Populate(args.Data, args.Info, args.AllowedPoints);
    }

    private void OnPlayerAttached(EntityUid uid, VomitSicknessComponent component, LocalPlayerAttachedEvent args)
    {
        _overlayMan.AddOverlay(_overlay);
    }

    private void OnPlayerDetached(EntityUid uid, VomitSicknessComponent component, LocalPlayerDetachedEvent args)
    {
        _overlayMan.RemoveOverlay(_overlay);
    }

    private void OnInfectedGetStatusIcons(EntityUid uid, MedievalPlagueInfectedComponent component, ref GetStatusIconsEvent args)
    {
        var proto = _proto.Index<FactionIconPrototype>(component.Incubation ? "MedievalPlagueInfectedIncubation" : "MedievalPlagueInfectedActive");
        args.StatusIcons.Add(proto);
    }

    private void OnImmuneGetStatusIcons(EntityUid uid, MedievalPlagueImmuneComponent component, ref GetStatusIconsEvent args)
    {
        var proto = _proto.Index<FactionIconPrototype>("MedievalPlagueImmune");
        args.StatusIcons.Add(proto);
    }

    private void OnSickInit(EntityUid uid, VomitSicknessComponent component, ComponentInit args)
    {
        component.StartTime = _timing.CurTime;
        component.EndTime = _timing.CurTime + TimeSpan.FromSeconds(component.Duration);
        if (_player.LocalEntity == uid)
            _overlayMan.AddOverlay(_overlay);
    }

    private void OnSickShutdown(EntityUid uid, VomitSicknessComponent component, ComponentShutdown args)
    {
        if (_player.LocalEntity == uid)
        {
            _overlayMan.RemoveOverlay(_overlay);
        }
    }

    private void OnGetCounterAmount(Entity<MedievalPlagueGhostComponent> ent, ref GetGenericAlertCounterAmountEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.AlertId != args.Alert)
            return;

        args.Amount = ent.Comp.Points;
    }
}
