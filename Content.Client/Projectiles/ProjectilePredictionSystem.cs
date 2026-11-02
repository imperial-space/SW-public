// Imperial medieval start
using System.Numerics;
using System.Linq;
using Content.Shared.Damage;
using Content.Shared.Effects;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Misc;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Client.GameObjects;
using Robust.Client.GameStates;
using Robust.Client.Graphics;
using Robust.Client.Physics;
using Robust.Client.Player;
using Robust.Client.Timing;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.GameStates;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Projectiles;

/// <summary>
/// Local launch/flight and contact feedback only. Never sends hit reports or raises gameplay hit events.
/// Ordinary server physics and damage remain authoritative.
/// Sprite interpolation smooths flight without feeding rendered positions back into physics.
/// </summary>
public sealed class ProjectilePredictionSystem : EntitySystem
{
    [Dependency] private readonly IClientGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IClientGameStateManager _states = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _flash = default!;
    [Dependency] private readonly SharedGunSystem _guns = default!;
    [Dependency] private readonly ProjectileSystem _projectiles = default!;
    [Dependency] private readonly SpriteSystem _sprites = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly FixtureSystem _fixtures = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly Content.Shared.Explosion.EntitySystems.SharedExplosionSystem _explosions = default!;

    private readonly HashSet<EntityUid> _overlapping = new();
    private readonly Dictionary<ProjectilePredictionKey, EntityUid> _local = new();
    private readonly Dictionary<ProjectilePredictionKey, EntityUid> _server = new();
    // Imperial medieval edit - reused arrows can change shot identity without scanning every tracked projectile.
    private readonly Dictionary<EntityUid, ProjectilePredictionKey> _serverKeys = new();
    private readonly Dictionary<ProjectilePredictionKey, TimeSpan> _created = new();
    private readonly Dictionary<ProjectilePredictionKey, TimeSpan> _confirmed = new();
    // Imperial medieval start - confirmation fills missing feedback without replaying existing feedback.
    private readonly Dictionary<ProjectilePredictionKey,
        (NetEntity Target, TimeSpan Time, bool Sound, bool EmbedSound, bool Effect, bool Flash)> _presented = new();
    // Imperial medieval end
    private readonly Dictionary<EntityUid, (EntityCoordinates Position, Angle Rotation, Vector2 Velocity, float Angular)> _replay = new();

    // Only the current physics segment is retained, for drawing between ticks.
    private sealed class FlightPresentation
    {
        public MapCoordinates Previous;
        public MapCoordinates Current;
        public Vector2 SpriteOffset;
        public GameTick Tick;
    }
    private readonly Dictionary<EntityUid, FlightPresentation> _flight = new();

    public bool Enabled => _states.IsPredictionEnabled;
    public bool Contains(ProjectilePredictionKey key) => _created.ContainsKey(key);

    public override void Initialize()
    {
        SubscribeLocalEvent<ProjectilePredictionComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ProjectilePredictionComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<ProjectilePredictionComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ProjectilePredictionComponent, StartCollideEvent>(OnContact);
        SubscribeLocalEvent<ProjectilePredictionComponent, PreventCollideEvent>(OnPreventCollide);
        SubscribeLocalEvent<ProjectilePredictionComponent, UpdateIsPredictedEvent>(OnPrediction);
        SubscribeLocalEvent<ProjectilePredictionComponent, Content.Shared.Item.GettingPickedUpAttemptEvent>(OnPickup);
        SubscribeLocalEvent<PhysicsUpdateBeforeSolveEvent>(OnBeforeSolve);
        SubscribeLocalEvent<PhysicsUpdateAfterSolveEvent>(OnAfterSolve);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnDetach);
        SubscribeNetworkEvent<ProjectileShotCompletedEvent>(OnShotCompleted);
        SubscribeNetworkEvent<ProjectileImpactPresentationEvent>(OnImpactConfirmed);
        UpdatesBefore.Add(typeof(TransformSystem));
    }

    /// <summary>Start one spell's local presentation using the existing projectile physics and contact path.</summary>
    public bool TryLaunchSpell(EntProtoId prototype, EntityUid shooter, ProjectilePredictionKey key,
        EntityCoordinates origin, Vector2 direction, float speed = 20f)
    {
        if (!Enabled || shooter != _player.LocalEntity || !_timing.IsFirstTimePredicted ||
            key.InputTick == GameTick.Zero || Contains(key) || _server.ContainsKey(key))
            return false;
        var uid = Spawn(prototype, origin);
        var predictExplosion = _explosions.CanPredictCollisionExplosion(uid);
        // The local entity presents contact only. Its inherited rocket trigger must never execute gameplay.
        RemComp<Content.Shared.Trigger.Components.Triggers.TriggerOnCollideComponent>(uid);
        // Continuous sound, lighting and screen distortion remain on the authoritative entity, once.
        RemComp<Content.Shared.Audio.AmbientSoundComponent>(uid);
        RemComp<PointLightComponent>(uid);
        RemComp<Content.Shared.Singularity.Components.SingularityDistortionComponent>(uid);
        if (!IsCompatible(uid) || !_projectiles.MarkPrediction(uid, shooter, key))
        {
            Del(uid);
            return false;
        }
        // The spell's action identifies the visual, while its caster retains normal damage attribution.
        Comp<ProjectilePredictionComponent>(uid).PredictExplosion = predictExplosion;
        _guns.ShootProjectile(uid, direction, _physics.GetMapLinearVelocity(shooter), shooter, shooter, speed);
        return true;
    }

    // Initial step: conventional projectiles only. Incompatible prototypes retain ordinary networked presentation.
    public bool IsCompatible(EntityUid uid)
    {
        return !HasComp<GrapplingProjectileComponent>(uid) &&
               !HasComp<Content.Shared.Damage.Components.DamageContactsComponent>(uid) &&
               !HasComp<Content.Shared.Trigger.Components.Triggers.TriggerOnCollideComponent>(uid) &&
               !(HasComp<Content.Shared.Damage.Components.DamageOnHighSpeedImpactComponent>(uid) && HasComp<DamageableComponent>(uid)) &&
               !HasComp<Content.Shared.Stunnable.StunOnContactComponent>(uid) &&
               !HasComp<Content.Shared.Storage.Components.StoreOnCollideComponent>(uid) &&
               !HasComp<Content.Shared.Imperial.Medieval.Magic.ProjectileCursorFollower.ProjectileCursorFollowerComponent>(uid);
    }

    private void OnStartup(Entity<ProjectilePredictionComponent> ent, ref ComponentStartup args)
    {
        if (ent.Comp.Shooter != _player.LocalEntity)
            return;
        if (IsClientSide(ent))
        {
            _local[ent.Comp.Key] = ent;
            _created[ent.Comp.Key] = _timing.RealTime;
            if (ent.Comp.Source is { } source && TryComp<SpriteComponent>(source, out var sprite))
            {
                ent.Comp.SourceVisible = sprite.Visible;
                _sprites.SetVisible((source, sprite), false);
            }
            if (TryComp<SpriteComponent>(ent, out var localSprite))
            {
                var coordinates = _transform.GetMapCoordinates(ent);
                _flight[ent] = new FlightPresentation
                {
                    Previous = coordinates, Current = coordinates, SpriteOffset = localSprite.Offset,
                };
            }
            _physics.UpdateIsPredicted(ent);
        }
        else
            RegisterServerProjectile(ent);
    }

    private void OnState(Entity<ProjectilePredictionComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        // Bows add the identity to an existing arrow, after its ordinary component startup.
        if (!IsClientSide(ent))
            RegisterServerProjectile(ent);
    }

    private void RegisterServerProjectile(Entity<ProjectilePredictionComponent> ent)
    {
        var key = ent.Comp.Key;
        // Clear the old identity even if somebody else has picked up and fired this arrow.
        if (_serverKeys.TryGetValue(ent.Owner, out var oldKey) &&
            (oldKey != key || ent.Comp.Shooter != _player.LocalEntity))
            UnregisterServerProjectile(ent.Owner);

        if (ent.Comp.Shooter != _player.LocalEntity)
            return;

        // A replacement authoritative entity must not leave a stale reverse entry behind.
        if (_server.TryGetValue(key, out var oldEntity) && oldEntity != ent.Owner)
        {
            _serverKeys.Remove(oldEntity);
            SetVisible(oldEntity, true);
        }

        _server[key] = ent;
        _serverKeys[ent.Owner] = key;
        if (_local.ContainsKey(key))
            SetVisible(ent, false);
    }

    private void UnregisterServerProjectile(EntityUid uid)
    {
        if (!_serverKeys.Remove(uid, out var key) ||
            !_server.TryGetValue(key, out var registered) || registered != uid)
            return;

        _server.Remove(key);
        SetVisible(uid, true);
        Retire(key);
    }

    private void OnShutdown(Entity<ProjectilePredictionComponent> ent, ref ComponentShutdown args)
    {
        if (IsClientSide(ent))
        {
            StopFlightSmoothing(ent);
            _local.Remove(ent.Comp.Key);
            if (ent.Comp.Source is { } source && TryComp<SpriteComponent>(source, out var sprite))
                _sprites.SetVisible((source, sprite), ent.Comp.SourceVisible);
            if (_server.TryGetValue(ent.Comp.Key, out var server))
                SetVisible(server, true);
        }
        else
            UnregisterServerProjectile(ent.Owner);
    }

    private void OnPrediction(Entity<ProjectilePredictionComponent> ent, ref UpdateIsPredictedEvent args)
        => args.IsPredicted = Enabled && IsClientSide(ent) && ent.Comp.Shooter == _player.LocalEntity;

    private void OnPickup(Entity<ProjectilePredictionComponent> ent, ref Content.Shared.Item.GettingPickedUpAttemptEvent args)
    {
        if (IsClientSide(ent))
            args.Cancel();
    }

    private void OnPreventCollide(Entity<ProjectilePredictionComponent> ent, ref PreventCollideEvent args)
    {
        if (!IsClientSide(ent))
            return;
        if (args.OtherEntity == ent.Comp.Source ||
            TryComp<ProjectilePredictionComponent>(args.OtherEntity, out var other) && other.Key == ent.Comp.Key)
            args.Cancelled = true;
    }

    private void OnContact(Entity<ProjectilePredictionComponent> ent, ref StartCollideEvent args)
    {
        if (!_timing.IsFirstTimePredicted || !IsClientSide(ent) || ent.Comp.Shooter != _player.LocalEntity || ent.Comp.Impact ||
            !TryComp<ProjectileComponent>(ent, out var projectile) ||
            !SharedProjectileSystem.IsProjectileImpact(projectile, args))
            return;
        PresentContact(ent, projectile, args.OtherEntity, args.OurBody);
    }

    private void PresentContact(Entity<ProjectilePredictionComponent> ent, ProjectileComponent projectile,
        EntityUid target, PhysicsComponent body)
    {
        var before = new ProjectileBeforeHitEvent(target, projectile.Shooter);
        RaiseLocalEvent(ent, ref before);
        if (before.Cancelled)
            return;

        StopFlightSmoothing(ent);
        ent.Comp.Impact = true;
        ent.Comp.ContactTick = _timing.CurTick;
        var coordinates = Transform(ent).Coordinates;
        if (ent.Comp.PredictExplosion)
            _explosions.PredictProjectileExplosion(ent, ent.Comp.Key);
        _physics.SetLinearVelocity(ent, Vector2.Zero, body: body);
        SoundSpecifier? embedSound = null;
        if (TryComp<EmbeddableProjectileComponent>(ent, out var embed))
        {
            _projectiles.ApplyEmbeddingPose(ent, target, embed);
            embedSound = embed.Sound;
        }
        else
            SetVisible(ent, false);
        _physics.SetCanCollide(ent, false, body: body);
        var sound = _guns.GetProjectileImpactSound(target, projectile.Damage, projectile.SoundHit,
            projectile.ForceSound, out var vary);
        Present(ent.Comp.Key, GetNetEntity(target), coordinates, sound, vary, embedSound,
            projectile.ImpactEffect, HasComp<DamageableComponent>(target) && projectile.Damage.AnyPositive());
    }

    // Imperial medieval start
    private void Present(ProjectilePredictionKey key, NetEntity target, EntityCoordinates coordinates,
        SoundSpecifier? sound, bool vary, SoundSpecifier? embedSound, string? effect, bool flash)
    {
        var sameTarget = _presented.TryGetValue(key, out var previous) && previous.Target == target;
        var soundPlayed = sameTarget && previous.Sound;
        var embedSoundPlayed = sameTarget && previous.EmbedSound;
        var effectPlayed = sameTarget && previous.Effect;
        var flashPlayed = sameTarget && previous.Flash;

        if (!soundPlayed && sound != null)
        {
            _audio.PlayStatic(sound, Filter.Local(), coordinates, true,
                vary ? AudioParams.Default.WithVariation(0.05f) : null);
            soundPlayed = true;
        }
        if (!embedSoundPlayed && embedSound != null)
        {
            _audio.PlayStatic(embedSound, Filter.Local(), coordinates, true);
            embedSoundPlayed = true;
        }
        if (!effectPlayed && effect != null)
        {
            RaiseLocalEvent(new ImpactEffectEvent(effect, GetNetCoordinates(coordinates)));
            effectPlayed = true;
        }
        if (!flashPlayed && flash && TryGetEntity(target, out var entity) && Exists(entity))
        {
            _flash.RaiseEffect(Color.Red, new List<EntityUid> { entity.Value }, Filter.Local());
            flashPlayed = true;
        }
        _presented[key] = (target, _timing.RealTime, soundPlayed, embedSoundPlayed, effectPlayed, flashPlayed);
    }
    // Imperial medieval end

    private void OnImpactConfirmed(ProjectileImpactPresentationEvent args)
    {
        var coordinates = GetCoordinates(args.Coordinates);
        _confirmed[args.Key] = _timing.RealTime;
        if (Exists(coordinates.EntityId))
        {
            Present(args.Key, args.Target, coordinates, args.Sound, args.VaryPitch, args.EmbedSound, args.Effect, args.Flash);
            if (_local.TryGetValue(args.Key, out var local) && Exists(local))
            {
                StopFlightSmoothing(local);
                // Keep the same projectile visible until the replicated terminal state arrives.
                // Server coordinates already include the native embed offset.
                _transform.SetCoordinates(local, coordinates);
                _physics.SetLinearVelocity(local, Vector2.Zero);
                _physics.SetCanCollide(local, false);
                Comp<ProjectilePredictionComponent>(local).Impact = true;
                if (args.Embedded)
                    _physics.SetBodyType(local, BodyType.Static);
                else
                    SetVisible(local, false);
            }
        }
    }

    private void OnShotCompleted(ProjectileShotCompletedEvent args)
    {
        if (args.Launched)
            return;
        foreach (var key in _local.Keys.ToArray())
            if (key.Weapon == args.Weapon && key.InputTick == args.InputTick)
                Retire(key);
    }

    private void Retire(ProjectilePredictionKey key)
    {
        if (_local.Remove(key, out var local) && Exists(local))
        {
            StopFlightSmoothing(local);
            SetVisible(local, false);
            _physics.SetCanCollide(local, false);
            QueueDel(local);
        }
        if (_server.TryGetValue(key, out var server))
            SetVisible(server, true);
    }

    private void SetVisible(EntityUid uid, bool visible)
    {
        if (TryComp<SpriteComponent>(uid, out var sprite))
            _sprites.SetVisible((uid, sprite), visible);
    }

    // Persistent local projectiles advance once, rather than again for every replayed player input.
    private void OnBeforeSolve(ref PhysicsUpdateBeforeSolveEvent args)
    {
        if (_timing.IsFirstTimePredicted)
        {
            foreach (var (uid, flight) in _flight)
            {
                if (flight.Tick == _timing.CurTick || !TryComp(uid, out TransformComponent? xform))
                    continue;
                flight.Previous = _transform.GetMapCoordinates(uid, xform);
                flight.Tick = _timing.CurTick;
            }
            // Engine reset may retain an already touching contact without another StartCollide.
            // Consume only existing sensor contacts that the engine's shape query confirms now.
            foreach (var (key, uid) in _local.ToArray())
            {
                if (!TryComp<ProjectilePredictionComponent>(uid, out var predicted) || predicted.Impact ||
                    !TryComp<ProjectileComponent>(uid, out var projectile) || projectile.ProjectileSpent ||
                    !TryComp<PhysicsComponent>(uid, out var body) || !body.CanCollide ||
                    _fixtures.GetFixtureOrNull(uid, SharedProjectileSystem.ProjectileFixture) is not { } sensor ||
                    sensor.Contacts.Count == 0)
                    continue;
                _overlapping.Clear();
                _lookup.GetEntitiesIntersecting(Transform(uid).MapID, sensor.Shape,
                    _physics.GetPhysicsTransform(uid), _overlapping, LookupFlags.Dynamic | LookupFlags.Static);
                foreach (var (otherFixture, contact) in sensor.Contacts.ToArray())
                {
                    var target = otherFixture.Owner;
                    if (contact.Deleting || !contact.Enabled || !otherFixture.Hard || !_overlapping.Contains(target) ||
                        target == predicted.Source || target == projectile.Shooter || target == projectile.Weapon ||
                        TryComp<ProjectilePredictionComponent>(target, out var matching) && matching.Key == key ||
                        !TryComp<PhysicsComponent>(target, out var targetBody) || !targetBody.CanCollide ||
                        !TryComp<FixturesComponent>(target, out var targetFixtures) ||
                        targetFixtures.Fixtures.Values.Count(fixture => fixture.Hard) != 1)
                        continue;
                    PresentContact((uid, predicted), projectile, target, body);
                    if (predicted.Impact)
                        break;
                }
            }
            return;
        }
        _replay.Clear();
        foreach (var uid in _local.Values)
            if (TryComp<PhysicsComponent>(uid, out var body) && TryComp(uid, out TransformComponent? xform))
                _replay[uid] = (xform.Coordinates, xform.LocalRotation, body.LinearVelocity, body.AngularVelocity);
    }

    private void OnAfterSolve(ref PhysicsUpdateAfterSolveEvent args)
    {
        if (_timing.IsFirstTimePredicted)
            foreach (var (uid, flight) in _flight)
                if (TryComp(uid, out TransformComponent? xform))
                    flight.Current = _transform.GetMapCoordinates(uid, xform);
        foreach (var (uid, pose) in _replay)
        {
            if (!Exists(uid))
                continue;
            _transform.SetCoordinates(uid, pose.Position);
            _transform.SetLocalRotation(uid, pose.Rotation);
            _physics.SetLinearVelocity(uid, pose.Velocity);
            _physics.SetAngularVelocity(uid, pose.Angular);
        }
        _replay.Clear();
    }

    public override void Update(float frameTime)
    {
        if (!_timing.IsFirstTimePredicted)
            return;
        foreach (var (key, uid) in _local.ToArray())
        {
            if (!Enabled || !Exists(uid) || _player.LocalEntity == null ||
                !_server.ContainsKey(key) && _timing.RealTime - _created[key] > TimeSpan.FromSeconds(3))
                Retire(key);
            else if (_server.TryGetValue(key, out var server))
            {
                if (TerminatingOrDeleted(server) || !TryComp(server, out TransformComponent? realTransform) ||
                    realTransform.MapID == MapId.Nullspace ||
                    !TryComp<ProjectileComponent>(server, out var real) || real.ProjectileSpent ||
                    real.Shooter != _player.LocalEntity ||
                    TryComp<EmbeddableProjectileComponent>(server, out var embedded) && embedded.EmbeddedIntoUid != null)
                    Retire(key);
                else
                {
                    SetVisible(server, false);
                    // A real state past the predicted contact can establish a miss without a client hit report.
                    // This is an impact disagreement, not routine correction of an uninterrupted flight.
                    var localPrediction = Comp<ProjectilePredictionComponent>(uid);
                    if (localPrediction.Impact && !_confirmed.ContainsKey(key) &&
                        TryComp<ProjectilePredictionComponent>(server, out var realPrediction) &&
                        localPrediction.ContactTick >= localPrediction.LaunchTick)
                    {
                        var flightTicks = localPrediction.ContactTick.Value - localPrediction.LaunchTick.Value;
                        if (_timing.LastRealTick > realPrediction.LaunchTick + flightTicks + 2u)
                            Retire(key);
                    }
                }
            }
        }
        foreach (var key in _created.Keys.ToArray())
            if (!_local.ContainsKey(key) && _timing.RealTime - _created[key] > TimeSpan.FromSeconds(10))
                _created.Remove(key);
        foreach (var (key, value) in _presented.ToArray())
            if (_timing.RealTime - value.Time > TimeSpan.FromSeconds(10))
                _presented.Remove(key);
        foreach (var (key, time) in _confirmed.ToArray())
            if (_timing.RealTime - time > TimeSpan.FromSeconds(10))
                _confirmed.Remove(key);
    }

    public override void FrameUpdate(float frameTime)
    {
        var step = Math.Clamp((float)(_timing.TickRemainder.TotalSeconds / _timing.TickPeriod.TotalSeconds), 0f, 1f);
        var eyeRotation = _eye.CurrentEye?.Rotation ?? Angle.Zero;
        foreach (var uid in _local.Values)
        {
            if (!TryComp(uid, out TransformComponent? xform))
                continue;
            // A persistent client entity has no authoritative transform reset. Never interpolate its physics pose.
            xform.ActivelyLerping = false;
            if (!_flight.TryGetValue(uid, out var flight) || !TryComp<SpriteComponent>(uid, out var sprite))
                continue;
            var offset = Vector2.Zero;
            if (flight.Previous.MapId == flight.Current.MapId && flight.Current.MapId == xform.MapID)
            {
                var drawn = Vector2.Lerp(flight.Previous.Position, flight.Current.Position, step);
                var delta = drawn - _transform.GetWorldPosition(uid);
                var rotation = _transform.GetWorldRotation(uid);
                var renderRotation = sprite.NoRotation ? -eyeRotation : rotation;
                if (!sprite.NoRotation && sprite.SnapCardinals)
                    renderRotation -= (rotation + eyeRotation).RoundToCardinalAngle();
                offset = (-renderRotation).RotateVec(delta);
            }
            _sprites.SetOffset((uid, sprite), flight.SpriteOffset + offset);
        }
    }

    private void StopFlightSmoothing(EntityUid uid)
    {
        if (_flight.Remove(uid, out var flight) && TryComp<SpriteComponent>(uid, out var sprite))
            _sprites.SetOffset((uid, sprite), flight.SpriteOffset);
    }

    private void OnDetach(LocalPlayerDetachedEvent args)
    {
        foreach (var key in _local.Keys.ToArray())
            Retire(key);
        _server.Clear();
        _serverKeys.Clear();
        _created.Clear();
        _presented.Clear();
        _confirmed.Clear();
        _replay.Clear();
        _flight.Clear();
    }
}
// Imperial medieval end
