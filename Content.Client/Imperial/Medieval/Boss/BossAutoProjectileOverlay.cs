using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Medieval.Boss;

public sealed class BossAutoProjectileOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> Unshaded = "unshaded";
    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    private readonly IEntityManager _entities;
    private readonly SharedTransformSystem _transform;
    private readonly MetaDataSystem _metadata;
    private readonly SpriteSystem _sprites;
    private readonly IResourceCache _resources;
    private readonly IGameTiming _timing;
    private readonly ShaderInstance _shader;
    private readonly Dictionary<EntityUid, List<Pose>> _trails = new();
    private readonly Dictionary<EntityUid, Pose> _contacts = new();
    private readonly record struct Pose(TimeSpan Time, Vector2 Position, Vector2 Direction);

    public BossAutoProjectileOverlay(IEntityManager entities, IResourceCache resources, IPrototypeManager prototypes, IGameTiming timing)
    {
        _entities = entities;
        _transform = entities.System<SharedTransformSystem>();
        _metadata = entities.System<MetaDataSystem>();
        _sprites = entities.System<SpriteSystem>();
        _resources = resources;
        _timing = timing;
        _shader = prototypes.Index(Unshaded).Instance();
        ZIndex = 900;
    }

    private void DrawSprite(DrawingHandleWorld handle, BossAutoProjectileComponent effect, SpriteSpecifier.Rsi animation,
        TimeSpan playback, Vector2 position, Vector2 direction, float scale, float opacity, bool loop)
    {
        var frame = _sprites.GetFrame(effect.Animation(animation), playback, loop);
        var factor = scale / EyeManager.PixelsPerMeter;
        var bottom = new Vector2(-effect.AnchorPixels.X, effect.AnchorPixels.Y - frame.Height) * factor;
        handle.SetTransform(Matrix3Helpers.CreateRotation(new Angle(Math.Atan2(direction.Y, direction.X))) *
                            Matrix3Helpers.CreateTranslation(position));
        handle.DrawTextureRect(frame, Box2.FromDimensions(bottom, frame.Size * factor), Color.White.WithAlpha(opacity));
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var query = _entities.AllEntityQueryEnumerator<BossAutoProjectileComponent, TransformComponent, MetaDataComponent>();
        handle.UseShader(_shader);
        while (query.MoveNext(out var uid, out var effect, out var xform, out var metadata))
        {
            if (xform.MapID != args.MapId || !_entities.TryGetComponent<TransformComponent>(effect.Grid, out var grid) ||
                !_entities.TryGetComponent<TransformComponent>(effect.Boss, out var boss) ||
                xform.MapUid is not { } map || !_entities.HasComponent<BossArenaRulesComponent>(map)) continue;
            var now = metadata.EntityPaused ? _timing.CurTime - _metadata.GetPauseTime(uid, metadata) : _timing.CurTime;
            var age = (now - effect.StartTime).TotalSeconds;
            if (age < BossAutoProjectileVisuals.ChargeBeginSeconds || now >= effect.EndTime) continue;

            _resources.GetResource<RSIResource>(effect.Animation(effect.Impact).RsiPath);
            var gridMatrix = _transform.GetWorldMatrix(grid);
            var local = effect.Position;
            var aimedDirection = effect.Direction;
            if (!effect.Resolved)
            {
                if (!_entities.TryGetComponent<TransformComponent>(effect.Target, out var target) || target.MapID != args.MapId) continue;
                var targetLocal = Vector2.Transform(_transform.GetWorldPosition(target), _transform.GetInvWorldMatrix(grid));
                local = effect.PredictedPosition(now, targetLocal);
                if (now < effect.LaunchTime)
                {

                    var hand = effect.HandOffset;
                    if (_entities.TryGetComponent<BossPresentationComponent>(effect.Boss, out var presentation))
                        hand.Y += BossPresentationComponent.BobOffset(now * presentation.BobSpeedMultiplier);
                    local = Vector2.Transform(_transform.GetWorldPosition(boss) + hand, _transform.GetInvWorldMatrix(grid));
                }
                var aim = targetLocal - local;
                if (aim.LengthSquared() > .000001f) aimedDirection = Vector2.Normalize(aim);
            }
            var position = Vector2.Transform(local, gridMatrix);
            var direction = Vector2.TransformNormal(aimedDirection, gridMatrix);
            var flameTime = BossAutoProjectileVisuals.FlameTime(age);
            if (now < effect.LaunchTime)
            {
                var progress = BossAutoProjectileVisuals.FormationProgress(age);
                var opacity = BossAutoProjectileVisuals.BirthOpacity(age);

                DrawSprite(handle, effect, effect.Flight, flameTime, position, direction,
                    effect.Scale, opacity * progress, true);
                DrawSprite(handle, effect, effect.Charge, flameTime * .5, position, direction,
                    effect.Scale, opacity * (1 - progress), true);
                continue;
            }
            if (!effect.Resolved && !_contacts.ContainsKey(uid) &&
                _entities.TryGetComponent<TransformComponent>(effect.Target, out var targetXform) &&
                Vector2.DistanceSquared(position, _transform.GetWorldPosition(targetXform)) < 0.000001f)
            {
                var targetLocal = Vector2.Transform(_transform.GetWorldPosition(targetXform), _transform.GetInvWorldMatrix(grid));
                var contact = effect.PositionTime + TimeSpan.FromSeconds(Vector2.Distance(effect.Position, targetLocal) / effect.Speed);
                _contacts[uid] = new Pose(contact, position, direction);
            }
            if (effect.Resolved || _contacts.TryGetValue(uid, out _))
            {
                var contact = effect.Resolved ? new Pose(effect.ImpactTime, position, direction) : _contacts[uid];
                var elapsed = (now - contact.Time).TotalSeconds;
                var opacity = BossAutoProjectileVisuals.ImpactOpacity(elapsed);
                if (opacity > 0)
                    DrawSprite(handle, effect, effect.Impact, TimeSpan.FromSeconds(Math.Max(0, elapsed)),
                        contact.Position, contact.Direction, effect.Scale, opacity, false);
                continue;
            }
            if (!_trails.TryGetValue(uid, out var history)) _trails[uid] = history = new List<Pose>();
            history.RemoveAll(pose => (now - pose.Time).TotalSeconds > 0.25);    // keep a short trail behind each flame
            history.Add(new Pose(now, position, direction));
            foreach (var (lag, opacity) in new[] { (0.16, 0.10f), (0.08, 0.18f) })
            {
                var time = now - TimeSpan.FromSeconds(lag);
                var old = history.LastOrDefault(pose => pose.Time <= time);
                if (old.Time < effect.LaunchTime) continue;
                DrawSprite(handle, effect, effect.Trail, BossAutoProjectileVisuals.FlameTime((old.Time - effect.StartTime).TotalSeconds),
                    old.Position, old.Direction, effect.Scale, opacity, true);
            }
            DrawSprite(handle, effect, effect.Flight, flameTime,
                position, direction, effect.Scale, 1, true);
        }
        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
        foreach (var uid in _trails.Keys.Concat(_contacts.Keys).Distinct().ToArray())
        {
            if (_entities.EntityExists(uid)) continue;
            _trails.Remove(uid);                                                 // discard trail data after the flame is gone
            _contacts.Remove(uid);
        }
    }
}

public sealed class BossAutoProjectileOverlaySystem : BossOverlaySystem
{
    protected override IEnumerable<Overlay> CreateOverlays()
    {
        yield return new BossAutoProjectileOverlay(EntityManager, Resources, Prototypes, Timing);
    }
}
