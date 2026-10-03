using Content.Client.Actions;
using Content.Client.ContextMenu.UI;
using Content.Client.Gameplay;
using Content.Client.Outline;
using Content.Client.UserInterface.Systems.Actions;
using Content.Client.Viewport;
using Content.Shared.Actions.Components;
using Content.Shared.Imperial.Medieval.BookAbilities;
using Content.Shared.Whitelist;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Medieval.BookAbilities;

/// <summary>Book abilities select the same single sprite that a normal click would select.</summary>
public sealed class BookAbilityTargetOutlineSystem : EntitySystem
{
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly TargetOutlineSystem _targetOutline = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelists = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IStateManager _states = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    private ShaderInstance _valid = default!;
    private ShaderInstance _invalid = default!;
    private SpriteComponent? _highlighted;
    private uint _previousRenderOrder;

    public override void Initialize()
    {
        base.Initialize();
        _valid = _prototypes.Index<ShaderPrototype>("SelectionOutlineInrange").InstanceUnique();
        _invalid = _prototypes.Index<ShaderPrototype>("SelectionOutline").InstanceUnique();
    }

    public override void Shutdown()
    {
        ClearHighlight();
        base.Shutdown();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        ClearHighlight();
        var controller = _ui.GetUIController<ActionUIController>();
        if (_players.LocalEntity is not { } user || controller.SelectingTargetFor is not { } action ||
            !HasComp<BookAbilityActionComponent>(action) || !TryComp<EntityTargetActionComponent>(action, out var targeting))
            return;

        // The normal action controller still handles clicks, cancellation and the cursor icon.
        // Only the visual selection policy differs for this feature.
        _targetOutline.Disable();
        var hovered = GetHovered();
        if (hovered is not { } target || target == user && !targeting.CanTargetSelf ||
            _whitelists.IsWhitelistFail(targeting.Whitelist, target) ||
            _whitelists.IsBlacklistPass(targeting.Blacklist, target) ||
            !TryComp<SpriteComponent>(target, out var sprite) || !sprite.Visible || sprite.PostShader != null)
            return;

        _previousRenderOrder = sprite.RenderOrder;
        sprite.PostShader = _actions.ValidateEntityTarget(user, target, (action, targeting)) ? _valid : _invalid;
        sprite.RenderOrder = EntityManager.CurrentTick.Value;
        _highlighted = sprite;
    }

    private EntityUid? GetHovered()
    {
        if (_states.CurrentState is GameplayStateBase screen &&
            _ui.CurrentlyHovered is IViewportControl viewport && _input.MouseScreenPosition.IsValid)
        {
            var coordinates = viewport.PixelToMap(_input.MouseScreenPosition.Position);
            return viewport is ScalingViewport scaling
                ? screen.GetClickedEntity(coordinates, scaling.Eye)
                : screen.GetClickedEntity(coordinates);
        }
        return _ui.CurrentlyHovered is EntityMenuElement element ? element.Entity : null;
    }

    private void ClearHighlight()
    {
        if (_highlighted == null)
            return;
        if (_highlighted.PostShader == _valid || _highlighted.PostShader == _invalid)
        {
            _highlighted.PostShader = null;
            _highlighted.RenderOrder = _previousRenderOrder;
        }
        _highlighted = null;
    }
}
