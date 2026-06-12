using Content.Shared.Imperial.Medieval.Lycantropy;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;
using Content.Client.Imperial.Medieval.Lycantropy.UI;
using Robust.Client.Player;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;
using Content.Shared.StatusIcon;
using Robust.Shared.Player;
using Robust.Client.Graphics;

namespace Content.Client.Imperial.Medieval.Lycantropy;

public sealed partial class LycantropySystem : SharedLycantropySystem
{
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IOverlayManager _overlayMan = default!;

    private WerewolfBloodFeelOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LycantropyComponent, OpenLycantropyMenuActionEvent>(OnOpenMenu);
        SubscribeLocalEvent<LycantropyComponent, SelectWerewolfFormActionEvent>(OnOpenFormMenu);
        SubscribeLocalEvent<LycantropyComponent, AfterAutoHandleStateEvent>(OnAfterAutoHandleState);
        SubscribeLocalEvent<LycantropyComponent, GetStatusIconsEvent>(OnGetLycantropyIcon);

        SubscribeLocalEvent<WerewolfBloodFeelComponent, ComponentInit>(OnBloodFeelInit);
        SubscribeLocalEvent<WerewolfBloodFeelComponent, ComponentShutdown>(OnBloodFeelShutdown);

        SubscribeLocalEvent<WerewolfBloodFeelComponent, LocalPlayerAttachedEvent>(OnLocalPlayerAttached);
        SubscribeLocalEvent<WerewolfBloodFeelComponent, LocalPlayerDetachedEvent>(OnLocalPlayerDetached);

        _overlay = new();
    }

    private void OnOpenMenu(EntityUid uid, LycantropyComponent comp, OpenLycantropyMenuActionEvent args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (_player.LocalEntity != uid)
            return;

        _ui.GetUIController<LycantropyUiController>().ToggleProgressMenu(comp.Abilities, comp.Points);
    }

    private void OnOpenFormMenu(EntityUid uid, LycantropyComponent comp, SelectWerewolfFormActionEvent args)
    {
        if (comp.SelectedForm != null)
            return;

        if (!_timing.IsFirstTimePredicted)
            return;

        if (_player.LocalEntity != uid)
            return;

        _ui.GetUIController<LycantropyUiController>().ToggleFormMenu(comp.AllowedPolymorphs);
    }

    private void OnAfterAutoHandleState(EntityUid uid, LycantropyComponent comp, ref AfterAutoHandleStateEvent args)
    {
        if (_player.LocalEntity != uid)
            return;

        _ui.GetUIController<LycantropyUiController>().Populate(comp.Abilities, comp.Points);
    }

    private void OnGetLycantropyIcon(EntityUid uid, LycantropyComponent comp, ref GetStatusIconsEvent args)
    {
        var proto = _proto.Index<FactionIconPrototype>("MedievalLycantropy");

        args.StatusIcons.Add(proto);
    }

    private void OnBloodFeelInit(EntityUid uid, WerewolfBloodFeelComponent component, ComponentInit args)
    {
        if (_player.LocalEntity == uid)
            _overlayMan.AddOverlay(_overlay);
    }

    private void OnBloodFeelShutdown(EntityUid uid, WerewolfBloodFeelComponent component, ComponentShutdown args)
    {
        if (_player.LocalEntity == uid)
            _overlayMan.RemoveOverlay(_overlay);
    }

    private void OnLocalPlayerAttached(EntityUid uid, WerewolfBloodFeelComponent component, LocalPlayerAttachedEvent args)
    {
        _overlayMan.AddOverlay(_overlay);
    }

    private void OnLocalPlayerDetached(EntityUid uid, WerewolfBloodFeelComponent component, LocalPlayerDetachedEvent args)
    {
        _overlayMan.RemoveOverlay(_overlay);
    }
}
