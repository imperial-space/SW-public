using System.Linq;
using Content.Client.UserInterface.Systems.Actions;
using Content.Shared.Imperial.TargetOverlay;
using Content.Shared.Imperial.TargetOverlay.Events;
using Content.Shared.Input;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.TargetOverlay;


public sealed partial class TargetOverlaySystem : SharedTargetOverlaySystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IComponentFactory _componentFactory = default!;

    private TargetOverlay _overlay = default!;
    public bool IsRangedTargetingDisplayed { get; private set; }

    // Melee reuses the existing projectile overlay's cursor scan without
    // using or changing the spell selection and casting input handlers.
    public EntityUid? PickDamageableUnderCursor() => _overlay.TryPickAtCursor(requireDamageable: true);


    public override void Initialize()
    {
        base.Initialize();

        _overlay = new();

        SubscribeNetworkEvent<AddTargetOverlayEvent>(OnStartNetworkTargeting);
        SubscribeNetworkEvent<RemoveTargetOverlayEvent>(OnStopNetworkTargeting);

        SubscribeLocalEvent<TargetOverlayComponent, ComponentStartup>(OnStartTargeting);
        SubscribeLocalEvent<TargetOverlayComponent, ComponentShutdown>(OnStopTargeting);

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.ImperialTargetCapture, new PointerInputCmdHandler(MiddleMousePressed))
            .BindBefore(EngineKeyFunctions.Use, new PointerInputCmdHandler(MouseLeftPressed), typeof(ActionUIController)) // run the release inside prediction.
            .Register<TargetOverlaySystem>();
    }

    private void OnStartNetworkTargeting(AddTargetOverlayEvent args, EntitySessionEventArgs session)
    {
        if (_playerManager.LocalSession?.UserId != session.SenderSession.UserId) return;
        if (_playerManager.LocalEntity == null) return;

        var component = EnsureComp<TargetOverlayComponent>(_playerManager.LocalEntity.Value);

        component.BlackListComponents = GetTypeFromComponentName(args.BlackListComponents);
        component.WhiteListComponents = GetTypeFromComponentName(args.WhiteListComponents);
        component.Sender = GetEntity(args.Sender);
        component.MaxTargetCount = args.MaxTargetCount;

        UpdateOverlay(component);
    }

    private void OnStopNetworkTargeting(RemoveTargetOverlayEvent args, EntitySessionEventArgs session)
    {
        if (_playerManager.LocalSession?.UserId != session.SenderSession.UserId) return;
        if (_playerManager.LocalEntity == null) return;

        IsRangedTargetingDisplayed = false;
        _overlayManager.RemoveOverlay(_overlay);
        // hook: server stop packets do not call the shared StopTargeting API.
        RaiseLocalEvent(new TargetOverlayStoppedEvent(_playerManager.LocalEntity.Value));
    }

    private void OnStartTargeting(EntityUid uid, TargetOverlayComponent component, ComponentStartup args)
    {
        if (_playerManager.LocalEntity != uid) return;

        UpdateOverlay(component);
    }

    private void OnStopTargeting(EntityUid uid, TargetOverlayComponent component, ComponentShutdown args)
    {
        if (_playerManager.LocalEntity != uid) return;

        IsRangedTargetingDisplayed = false;
        _overlayManager.RemoveOverlay(_overlay);
        // hook: also release ownership when the local overlay component is removed.
        RaiseLocalEvent(new TargetOverlayStoppedEvent(uid));
    }

    private bool MiddleMousePressed(ICommonSession? playerSession, EntityCoordinates coordinates, EntityUid entity)
    {
        if (!_timing.IsFirstTimePredicted) return false;
        if (playerSession?.AttachedEntity is not { Valid: true } player || !Exists(player)) return false;
        if (!HasComp<TargetOverlayComponent>(player)) return false;

        _overlay.AddTarget();

        return false;
    }

    private bool MouseLeftPressed(ICommonSession? playerSession, EntityCoordinates coordinates, EntityUid entity)
    {
        if (!_timing.IsFirstTimePredicted) return false;
        if (playerSession?.AttachedEntity is not { Valid: true } player || !Exists(player)) return false;
        if (!TryComp<TargetOverlayComponent>(player, out var targetOverlayComponent)) return false;

        if (!_overlay.IsMaxTargetCount())
            _overlay.AddTarget();

        RaisePredictiveEvent(new TargetOverlayShootEvent() // predict presentation at target selection.
        {
            Performer = GetNetEntity(player),
            Sender = targetOverlayComponent.Sender.HasValue ? GetNetEntity(targetOverlayComponent.Sender) : null,
            Targets = _overlay.Targets.Select(el => (el.CursorPosition, GetNetEntity(el.Target))).ToList()
        });

        return false;
    }

    #region Helpers

    private void UpdateOverlay(TargetOverlayComponent component)
    {
        _overlay.BlackListComponents = component.BlackListComponents;
        _overlay.WhiteListComponents = component.WhiteListComponents;
        _overlay.MaxTargetCount = component.MaxTargetCount;
        _overlay.Targets = new();

        _overlayManager.AddOverlay(_overlay);
        IsRangedTargetingDisplayed = true;
        // hook: covers server starts and replacement of a local overlay component.
        if (_playerManager.LocalEntity is { } user)
            RaiseLocalEvent(new TargetOverlayStartedEvent(user, component.Sender));
    }

    private HashSet<Type> GetTypeFromComponentName(HashSet<string> components)
    {
        var output = new HashSet<Type>();

        foreach (var componentName in components)
        {
            if (!_componentFactory.TryGetRegistration(componentName, out var componentRegistration)) continue;

            output.Add(componentRegistration.Type);
        }

        return output;
    }

    #region Client Implementation

    protected override void StartTargetingClient(EntityUid uid, EntityUid? sender, int maxTargetCount, HashSet<string> whiteListComponents, HashSet<string> blackListComponents)
    {
        if (_playerManager.LocalEntity != uid) return;

        if (HasComp<TargetOverlayComponent>(uid))
            RemComp<TargetOverlayComponent>(uid);

        var comp = AddComp<TargetOverlayComponent>(uid);

        comp.BlackListComponents = GetTypeFromComponentName(blackListComponents);
        comp.WhiteListComponents = GetTypeFromComponentName(whiteListComponents);
        comp.MaxTargetCount = maxTargetCount;
        comp.Sender = sender;

        UpdateOverlay(comp);
    }

    protected override void StopTargetingClient(EntityUid uid)
    {
        RemComp<TargetOverlayComponent>(uid);
    }

    #endregion

    #endregion
}
