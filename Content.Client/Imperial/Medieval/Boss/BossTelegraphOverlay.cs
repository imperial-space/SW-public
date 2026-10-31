using System.Numerics;
using System.Linq;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Robust.Client.Player;

namespace Content.Client.Imperial.Medieval.Boss;

public sealed class BossFloorTelegraphOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;

    private readonly IEntityManager _entities;
    private readonly SharedTransformSystem _transform;
    private readonly IPrototypeManager _prototypes;
    private readonly IResourceCache _resources;
    private readonly IGameTiming _timing;
    private readonly MetaDataSystem _metadata;
    private readonly ShaderInstance _movementShader;
    public BossFloorTelegraphOverlay(IEntityManager entities, IPrototypeManager prototypes, IResourceCache resources, IGameTiming timing)
    {
        _entities = entities;
        _transform = entities.System<SharedTransformSystem>();
        _prototypes = prototypes;
        _resources = resources;
        _timing = timing;
        _metadata = entities.System<MetaDataSystem>();
        _movementShader = prototypes.Index<ShaderPrototype>("unshaded").Instance();
        ZIndex = BossOverlayLayers.Ground;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var query = _entities.AllEntityQueryEnumerator<BossTelegraphComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var warning, out var xform))
        {
            if (xform.MapID != args.MapId || !_entities.EntityExists(warning.Grid) || !_prototypes.TryIndex(warning.Style, out var style))
                continue;
            if (BossCommunionVisuals.Enabled(style, warning.Pattern))
                continue;
            if (BossVigilVisuals.Enabled(style, warning.Pattern))
                continue;
            var matrix = _transform.GetWorldMatrix(warning.Grid);
            handle.SetTransform(matrix);
            if (warning.Impact)
            {
                if (warning.TankTarget is { } target && _entities.EntityExists(target) && _entities.EntityExists(warning.Boss))
                {
                    var inverse = _transform.GetInvWorldMatrix(warning.Grid);
                    var start = Vector2.Transform(_transform.GetWorldPosition(warning.Boss), inverse) + new Vector2(0, 0.7f);
                    var end = Vector2.Transform(_transform.GetWorldPosition(target), inverse) + new Vector2(0, 0.4f);
                    handle.DrawLine(start, end, style.PurpleColor);
                    handle.DrawLine(start + new Vector2(0.04f, 0), end + new Vector2(0.04f, 0), Color.White);
                    handle.DrawRect(Box2.FromDimensions(end - new Vector2(0.18f), new Vector2(0.36f)), style.TankColor, filled: false);
                }

                var impactColor = BossTilePatterns.IsConstellation(warning.Pattern) ? (warning.Pattern == BossTelegraphPattern.BlueFirst) != warning.SecondPhase ? style.BlueColor : style.PurpleColor : Color.White;
                if (BossTilePatterns.IsConstellation(warning.Pattern) && BossConstellationVisuals.Texture(style, warning.Pattern, warning.HalfSize) == null)
                    DrawBeams(handle, (warning.Pattern == BossTelegraphPattern.BlueFirst) != warning.SecondPhase, impactColor, 0.6f, warning.HalfSize, impact: true);
                continue;
            }

            var now = _entities.TryGetComponent<MetaDataComponent>(uid, out var metadata) && metadata.EntityPaused ? _timing.CurTime - _metadata.GetPauseTime(uid, metadata) : _timing.CurTime;
            var pulse = 0.19f + 0.05f * MathF.Sin((float)now.TotalSeconds * 5);
            if (warning.EndTime > warning.StartTime)
                pulse += 0.12f * (float)Math.Clamp((now - warning.StartTime).TotalSeconds / (warning.EndTime - warning.StartTime).TotalSeconds, 0, 1);
            if (BossTilePatterns.IsConstellation(warning.Pattern))
            {
                var blueFirst = warning.Pattern == BossTelegraphPattern.BlueFirst;
                foreach (var blue in new[]
                {
                    blueFirst,
                    !blueFirst
                }

                )
                {
                    if (warning.SecondPhase && blue == blueFirst || blue != blueFirst && !warning.ShowSecondColor)
                        continue;
                    var color = blue ? style.BlueColor : style.PurpleColor;
                    if (now >= warning.StartTime && now < warning.EndTime)
                    {
                        var tiles = BossArenaWarningVisuals.Tiles(warning.Pattern, BossTilePatterns.GetPlatform(warning.HalfSize).Where(tile => BossTilePatterns.ConstellationHits(tile, blue, warning.HalfSize)).ToArray(), warning.HalfSize, style.WarningOverflowTiles).ToArray();
                        var alpha = 0.09f + 0.01f * MathF.Sin((float)now.TotalSeconds * 2);
                        if (style.WarningAreaOutline)
                            DrawWarningArea(handle, tiles, color, alpha);
                        else
                            foreach (var tile in tiles)
                                handle.DrawRect(Box2.FromDimensions(new Vector2(tile.X + 0.02f, tile.Y + 0.02f), new Vector2(0.96f)), color.WithAlpha(alpha));
                    }
                }

                DrawEmitters(handle, warning, style);
            }
            else
            {
                if (warning.ShowDanger(now))
                {
                    var tiles = BossArenaWarningVisuals.Tiles(warning.Pattern, warning.Danger, warning.HalfSize, style.WarningOverflowTiles, warning.PatternOrigin, warning.Facing).ToArray();
                    var color = warning.Preload ? style.SentencingColor : style.DangerColor;
                    if (style.WarningAreaOutline)
                        DrawWarningArea(handle, tiles, color, pulse);
                    else
                        foreach (var tile in tiles)
                            DrawTile(handle, tile, color, warning.Preload ? style.SentencingTexture : style.DangerTexture, pulse);
                }
            }

            DrawMovement(handle, warning, style, now);
            if (warning.MovementDestination is { } destination)
            {
                var center = new Vector2(destination.X + 0.5f, destination.Y + 0.5f);
                if (GetTexture(style.MovementDestinationTexture) is { } destinationTexture)
                {
                    handle.UseShader(_movementShader);
                    handle.DrawTextureRectRegion(destinationTexture, BossMovementRuneVisuals.DestinationBounds(center, now),
                        modulate: Color.White, subRegion: BossMovementRuneVisuals.DestinationRegion);
                    handle.UseShader(null);
                }
                else
                    handle.DrawCircle(center, 0.12f, Color.White);
            }
        }

        handle.SetTransform(Matrix3x2.Identity);
    }

    private void DrawMovement(DrawingHandleWorld handle, BossTelegraphComponent warning, BossTelegraphStylePrototype style, TimeSpan now)
    {
        var path = warning.MovementPath;
        if (path.Count >= 2 && (path[0].X == path[^1].X || path[0].Y == path[^1].Y) && GetTexture(style.MovementArrowTexture) is { } texture)
        {
            handle.UseShader(_movementShader);
            foreach (var patch in BossMovementRuneVisuals.MovementPatches(path, now))
                handle.DrawTextureRectRegion(texture, patch.Bounds, Color.White.WithAlpha(patch.Opacity), patch.Region);
            handle.UseShader(null);
            return;
        }

        foreach (var line in MovementArrowLines(path, now))
            handle.DrawLine(line.Start, line.End, Color.White);
    }

    protected override void DisposeBehavior()
    {
        _movementShader.Dispose();
        base.DisposeBehavior();
    }

    public static IEnumerable<(Vector2 Start, Vector2 End)> MovementArrowLines(IReadOnlyList<Vector2i> path, TimeSpan now)
    {
        if (path.Count < 2)
            yield break;
        var origin = new Vector2(path[0].X + 0.5f, path[0].Y + 0.5f);
        var destination = new Vector2(path[^1].X + 0.5f, path[^1].Y + 0.5f);
        var length = Vector2.Distance(origin, destination);
        if (length <= 0)
            yield break;
        var direction = (destination - origin) / length;
        var side = new Vector2(-direction.Y, direction.X) * 0.16f;
        var phase = (float)((now.TotalSeconds * 1.5) % 1);
        for (var distance = length + phase; distance > 0; distance -= 1)
        {
            var tip = origin + direction * distance;
            var ends = new[]
            {
                tip - direction * 0.55f,
                tip - direction * 0.2f + side,
                tip - direction * 0.2f - side
            };
            foreach (var end in ends)
            {
                var clippedTip = tip;
                var clippedEnd = end;
                if (ClipMovementLine(ref clippedTip, ref clippedEnd, origin, direction, length))
                    yield return (clippedTip, clippedEnd);
            }
        }
    }

    private static bool ClipMovementLine(ref Vector2 start, ref Vector2 end, Vector2 origin, Vector2 direction, float length)
    {
        var first = Vector2.Dot(start - origin, direction);
        var last = Vector2.Dot(end - origin, direction);
        if (first <= 0 && last <= 0 || first >= length && last >= length)
            return false;
        var delta = end - start;
        var span = last - first;
        var from = Math.Clamp(first, 0, length);
        var to = Math.Clamp(last, 0, length);
        var original = start;
        start = original + delta * ((from - first) / span);
        end = original + delta * ((to - first) / span);
        return Vector2.DistanceSquared(start, end) > 0.000001f;
    }

    private static void DrawWarningArea(DrawingHandleWorld handle, IReadOnlyCollection<Vector2i> tiles, Color color, float alpha)
    {
        foreach (var tile in tiles)
            handle.DrawRect(Box2.FromDimensions(new Vector2(tile.X + 0.02f, tile.Y + 0.02f), new Vector2(0.96f)), color.WithAlpha(alpha));
        foreach (var edge in BossArenaWarningVisuals.Outline(tiles))
            handle.DrawRect(edge, color.WithAlpha(0.9f));
    }

    private void DrawTile(DrawingHandleWorld handle, Vector2i tile, Color color, ResPath? path, float alpha, bool outlineOnly = false)
    {
        var bounds = Box2.FromDimensions(new Vector2(tile.X + 0.02f, tile.Y + 0.02f), new Vector2(0.96f));
        if (GetTexture(path) is { } texture)
            handle.DrawTextureRect(texture, bounds, color.WithAlpha(outlineOnly ? 0.18f : 0.7f));
        else
        {
            handle.DrawRect(bounds, color.WithAlpha(alpha));
            handle.DrawRect(bounds, color.WithAlpha(outlineOnly ? 0.3f : 0.9f), filled: false);
            if (!outlineOnly)
            {
                handle.DrawLine(bounds.BottomLeft + new Vector2(0.18f), bounds.TopRight - new Vector2(0.18f), color.WithAlpha(0.5f));
                handle.DrawLine(new Vector2(bounds.Left + 0.18f, bounds.Top - 0.18f), new Vector2(bounds.Right - 0.18f, bounds.Bottom + 0.18f), color.WithAlpha(0.5f));
            }
        }
    }

    private void DrawEmitters(DrawingHandleWorld handle, BossTelegraphComponent warning, BossTelegraphStylePrototype style)
    {
        var firstBlue = warning.Pattern == BossTelegraphPattern.BlueFirst;
        foreach (var blue in new[]
        {
            firstBlue,
            !firstBlue
        }

        )
        {
            if (warning.SecondPhase && blue == firstBlue)
                continue;
            if (blue != firstBlue && !warning.ShowSecondColor)
                continue;
            var color = blue ? style.BlueColor : style.PurpleColor;
            foreach (var side in new[]
            {
                -warning.HalfSize - 0.65f,
                warning.HalfSize + 1.65f
            }

            )
                foreach (var beam in BossTilePatterns.ConstellationBeams(blue, warning.HalfSize))
                {
                    var center = beam.Start + beam.Width / 2f;
                    DrawEmitterOrder(handle, new Vector2(side, center), color, blue == firstBlue);
                    DrawEmitterOrder(handle, new Vector2(center, side), color, blue == firstBlue);
                }
        }
    }

    private static void DrawEmitterOrder(DrawingHandleWorld handle, Vector2 center, Color color, bool first)
    {
        for (var i = 0; i < (first ? 1 : 2); i++)
            handle.DrawRect(Box2.FromDimensions(center + new Vector2(-0.05f + i * 0.1f, -0.35f), new Vector2(0.07f)), color);
    }

    private static void DrawBeams(DrawingHandleWorld handle, bool blue, Color color, float alpha, int halfSize, bool impact = false)
    {
        foreach (var beam in BossTilePatterns.ConstellationBeams(blue, halfSize))
        {
            var horizontal = Box2.FromDimensions(new Vector2(-halfSize, beam.Start), new Vector2(halfSize * 2 + 1, beam.Width));
            var vertical = Box2.FromDimensions(new Vector2(beam.Start, -halfSize), new Vector2(beam.Width, halfSize * 2 + 1));
            handle.DrawRect(horizontal, color.WithAlpha(alpha));
            handle.DrawRect(vertical, color.WithAlpha(alpha));
            handle.DrawRect(horizontal, color.WithAlpha(0.9f), filled: false);
            handle.DrawRect(vertical, color.WithAlpha(0.9f), filled: false);
            var core = impact ? 0.14f : 0.025f;
            var center = beam.Start + beam.Width / 2f;
            handle.DrawRect(Box2.FromDimensions(new Vector2(-halfSize - 0.65f, center - core / 2), new Vector2(halfSize * 2 + 2.3f, core)), (impact ? Color.White : color).WithAlpha(impact ? 0.95f : 0.7f));
            handle.DrawRect(Box2.FromDimensions(new Vector2(center - core / 2, -halfSize - 0.65f), new Vector2(core, halfSize * 2 + 2.3f)), (impact ? Color.White : color).WithAlpha(impact ? 0.95f : 0.7f));
        }
    }

    private Texture? GetTexture(ResPath? path) => path is { } resourcePath && _resources.TryGetResource<TextureResource>(resourcePath, out var resource) ? resource.Texture : null;
}

public sealed class BossActorMarkerOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private readonly IEntityManager _entities;
    private readonly SharedTransformSystem _transform;
    private readonly IPrototypeManager _prototypes;
    private readonly IResourceCache _resources;
    private readonly IGameTiming _timing;
    private readonly IPlayerManager _player;
    private readonly SpriteSystem _sprites;
    public BossActorMarkerOverlay(IEntityManager entities, IPrototypeManager prototypes, IResourceCache resources, IGameTiming timing, IPlayerManager player)
    {
        _entities = entities;
        _transform = entities.System<SharedTransformSystem>();
        _prototypes = prototypes;
        _resources = resources;
        _timing = timing;
        _player = player;
        _sprites = entities.System<SpriteSystem>();
        ZIndex = 1000;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var rotation = Matrix3Helpers.CreateRotation(-(args.Viewport.Eye?.Rotation ?? Angle.Zero));
        var levels = new Dictionary<EntityUid, float>();
        var markers = _entities.AllEntities<BossActorMarkerComponent>().OrderBy(entity => entity.Comp.Target.Id).ThenBy(entity => entity.Comp.Kind).ThenBy(entity => entity.Owner.Id).ToArray();
        foreach (var entity in markers)
        {
            var uid = entity.Owner;
            var mark = entity.Comp;
            if (mark.Kind == BossActorMarkerKind.Tank)
                continue;
            if (!_entities.TryGetComponent<TransformComponent>(uid, out var xform))
                continue;
            if (xform.MapID != args.MapId || _entities.HasComponent<SpriteComponent>(uid) || !_entities.TryGetComponent<SpriteComponent>(mark.Target, out var sprite) || !sprite.Visible || sprite.ContainerOccluded || !_entities.TryGetComponent<TransformComponent>(mark.Boss, out var bossXform) || bossXform.MapID != args.MapId || !_prototypes.TryIndex(mark.Style, out var style))
                continue;
            if (BossSpreadSoulVisuals.ReplacesLegacyCue(style, mark.Kind) || BossCommunionVisuals.ReplacesLegacyCue(style, mark.Kind))
                continue;
            var position = _transform.GetWorldPosition(mark.Target);
            handle.SetTransform(rotation * Matrix3Helpers.CreateTranslation(position));
            var center = mark.Kind == BossActorMarkerKind.Spread ? style.SpreadMarkerOffset ?? style.MarkerOffset : style.MarkerOffset;
            var own = mark.Target == _player.LocalEntity;
            var scale = own ? 2f : 1f;
            var phase = (float)_timing.CurTime.TotalSeconds;
            var tankBuster = mark.Kind == BossActorMarkerKind.TankBuster;
            var pulse = tankBuster ? 1f : 1f + 0.12f * MathF.Sin(phase * 5f);
            var size = style.MarkerSize * scale * pulse;
            var height = levels.GetValueOrDefault(mark.Target);
            center.X = 0;
            var bodyBounds = _sprites.GetLocalBounds((mark.Target, sprite));
            var bodyTop = bodyBounds.Top + sprite.Offset.Y;
            if (tankBuster)
                size = style.TankBusterMarkerSize * (own ? 1f : 0.5f);
            center.Y = bodyTop + Math.Max(0, center.Y) + height + size.Y / 2;
            if (tankBuster)
                center.Y += BossPresentationComponent.BobOffset(_timing.CurTime, style.TankBusterBobDistance);
            levels[mark.Target] = height + size.Y * 1.12f + 0.15f + (tankBuster ? style.TankBusterBobDistance : 0);
            var color = mark.Kind switch
            {
                BossActorMarkerKind.Spread => style.SpreadColor,
                BossActorMarkerKind.TankBuster => style.TankBusterColor,
                _ => style.StackColor
            };
            var path = mark.Kind switch
            {
                BossActorMarkerKind.Spread => style.SpreadTexture,
                BossActorMarkerKind.TankBuster => style.TankBusterTexture,
                _ => style.StackTexture
            };
            var bounds = Box2.FromDimensions(center - size / 2, size);
            if (path is { } resourcePath && _resources.TryGetResource<TextureResource>(resourcePath, out var resource))
            {
                handle.DrawTextureRect(resource.Texture, bounds);
                continue;
            }

            handle.DrawRect(bounds.Enlarged(0.04f), Color.Black.WithAlpha(0.7f));
            if (mark.Kind == BossActorMarkerKind.Spread)
            {
                handle.DrawRect(bounds.Enlarged(-0.08f), color, filled: false);
                foreach (var direction in new[]
                {
                    Vector2.UnitX,
                    -Vector2.UnitX,
                    Vector2.UnitY,
                    -Vector2.UnitY
                }

                )
                {
                    var tip = center + direction * 0.29f;
                    var side = new Vector2(-direction.Y, direction.X) * 0.06f;
                    handle.DrawLine(center + direction * 0.12f, tip, color);
                    handle.DrawLine(tip, tip - direction * 0.08f + side, color);
                    handle.DrawLine(tip, tip - direction * 0.08f - side, color);
                }
            }
            else
            {
                handle.DrawRect(bounds.Enlarged(-0.12f), color, filled: false);
                foreach (var direction in new[]
                {
                    Vector2.UnitX,
                    -Vector2.UnitX,
                    Vector2.UnitY,
                    -Vector2.UnitY
                }

                )
                {
                    var tip = center + direction * 0.09f;
                    var side = new Vector2(-direction.Y, direction.X) * 0.05f;
                    handle.DrawLine(center + direction * 0.28f, tip, color);
                    handle.DrawLine(tip, tip + direction * 0.08f + side, color);
                    handle.DrawLine(tip, tip + direction * 0.08f - side, color);
                }
            }
        }

        handle.SetTransform(Matrix3x2.Identity);
    }
}
