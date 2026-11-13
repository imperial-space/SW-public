using System.Numerics;
using Content.Shared.Imperial.Medieval.Poise.MeleeLockOn;
using Robust.Client.Graphics;
using Robust.Client.GameObjects;
using Robust.Shared.Enums;
using Robust.Client.Player;
using Robust.Client.Input;
using Content.Client.Imperial.TargetOverlay;
using Robust.Shared.Timing;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Utility;
using ProjectileTargetOverlay = Content.Client.Imperial.TargetOverlay.TargetOverlay;

namespace Content.Client.Imperial.Medieval.Poise.MeleeLockOn;

/// <summary>Draws the private melee-target sprite over the local player's target.</summary>
public sealed class MeleeLockOnMarkerOverlay : Overlay
{
    [Dependency] private readonly IEntitySystemManager _systems = default!;
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly SpriteSystem _sprites;
    private readonly SharedTransformSystem _transforms;
    private readonly MapSystem _maps;
    private readonly TargetOverlaySystem _projectileTargeting;
    private readonly MeleeLockOnTrackingSystem _tracking;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private static readonly SpriteSpecifier.Texture MarkerIcon = new(new ResPath("/Textures/Imperial/Medieval/Poise/MeleeLockOn/target.png"));
    private static readonly SpriteSpecifier.Rsi CursorIcon = new(new ResPath("/Textures/Imperial/Medieval/Poise/MeleeLockOn/action.rsi"), "cursor");

    public MeleeLockOnMarkerOverlay()
    {
        IoCManager.InjectDependencies(this);
        _sprites = _systems.GetEntitySystem<SpriteSystem>();
        _transforms = _systems.GetEntitySystem<SharedTransformSystem>();
        _maps = _systems.GetEntitySystem<MapSystem>();
        _projectileTargeting = _systems.GetEntitySystem<TargetOverlaySystem>();
        _tracking = _systems.GetEntitySystem<MeleeLockOnTrackingSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_players.LocalEntity is not { } user ||
            !_entities.TryGetComponent<MeleeLockOnComponent>(user, out var lockOn))
            return;

        // Preserve the locked marker while the player is selecting a replacement.
        if (lockOn.Target is { } target && _entities.HasComponent<TransformComponent>(target))
        {
            var targetCoords = _transforms.GetMapCoordinates(target);
            if (targetCoords.MapId == args.MapId && _tracking.GetDebugLineColor(user, target) is { } color)
            {
                var playerCoords = _transforms.GetMapCoordinates(user);
                if (playerCoords.MapId == args.MapId)
                {
                    args.WorldHandle.SetTransform(Matrix3x2.Identity);
                    args.WorldHandle.DrawLine(playerCoords.Position, targetCoords.Position, color);
                }
            }

            if (_tracking.ShouldDrawMarker(user, target))
                DrawMarker(args, targetCoords, CursorIcon, Angle.Zero);
        }

        // Preview the current target using the existing projectile cursor scan.
        if (lockOn.TargetingMode && args.Viewport.Eye != null)
        {
            var hovered = _projectileTargeting.PickDamageableUnderCursor();
            var cursor = _eye.PixelToMap(_input.MouseScreenPosition.Position);
            var coords = hovered is { } candidate && _entities.HasComponent<TransformComponent>(candidate)
                ? _transforms.GetMapCoordinates(candidate)
                : cursor;
            DrawMarker(args, coords, CursorIcon, -Angle.FromDegrees(_timing.CurTime.TotalMilliseconds / 20));
        }
    }

    private void DrawMarker(in OverlayDrawArgs args, MapCoordinates coords, SpriteSpecifier icon, Angle rotation)
    {
        if (coords.MapId != args.MapId || !_maps.TryGetMap(coords.MapId, out var mapUid))
            return;

        var worldMatrix = _transforms.GetWorldMatrix(mapUid.Value);
        var invMatrix = _transforms.GetInvWorldMatrix(mapUid.Value);
        var localPosition = Vector2.Transform(coords.Position, invMatrix);
        var box = new Box2Rotated(Box2.UnitCentered.Translated(localPosition), rotation, localPosition);
        var texture = _sprites.Frame0(icon);
        var handle = args.WorldHandle;
        handle.SetTransform(worldMatrix);
        handle.DrawTextureRect(texture, box, Color.White);
        handle.SetTransform(Matrix3x2.Identity);
    }
}
