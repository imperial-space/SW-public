using System.Numerics;
using Content.Shared.Imperial.Medieval.MobRiding;
using Content.Shared.Mind.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Medieval.MobRiding;

public sealed class HorseDirectionArrowSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private EntityQuery<InputMoverComponent> _moverQuery;
    private EntityQuery<RideableComponent> _rideableQuery;
    private EntityQuery<MindContainerComponent> _mindQuery;
    private EntityQuery<PhysicsComponent> _physicsQuery;

    private static readonly ResPath ArrowRsi = new("/Textures/Markers/teg_arrow.rsi");
    private static readonly RSI.StateId ArrowState = new("arrow");
    private static readonly Vector2 ArrowOffset = new(0f, 0.75f);
    private static readonly Color ArrowColor = Color.FromHex("#FFFFFFBB");
    private const float DirectionEpsilon = 0.0001f;

    public override void Initialize()
    {
        base.Initialize();

        _moverQuery = GetEntityQuery<InputMoverComponent>();
        _rideableQuery = GetEntityQuery<RideableComponent>();
        _mindQuery = GetEntityQuery<MindContainerComponent>();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
    }

    public override void Update(float frameTime)
    {
        var enumerator = EntityQueryEnumerator<HorseControlComponent, TransformComponent, SpriteComponent>();

        while (enumerator.MoveNext(out var uid, out var horse, out var xform, out var sprite))
        {
            UpdateArrow(uid, horse, xform, sprite);
        }
    }

    private void UpdateArrow(EntityUid uid, HorseControlComponent horse, TransformComponent xform, SpriteComponent sprite)
    {
        if (!EnsureArrowLayer(uid, sprite, out var layer))
            return;

        var isRiding = _rideableQuery.TryGetComponent(uid, out var rideable) && rideable.IsRiding;
        var hasMind = _mindQuery.TryGetComponent(uid, out var mind) && mind.HasMind;

        if (!isRiding && !hasMind
            || !TryGetArrowRotation(uid, horse, xform, sprite.NoRotation, out var rotation))
        {
            _sprite.LayerSetVisible((uid, sprite), layer, false);
            return;
        }

        _sprite.LayerSetRotation((uid, sprite), layer, rotation);
        _sprite.LayerSetVisible((uid, sprite), layer, true);
    }

    private bool EnsureArrowLayer(EntityUid uid, SpriteComponent sprite, out int layer)
    {
        if (_sprite.LayerMapTryGet((uid, sprite), HorseDirectionArrowLayers.Arrow, out layer, false))
            return true;

        layer = _sprite.LayerMapReserve((uid, sprite), HorseDirectionArrowLayers.Arrow);
        if (layer < 0)
            return false;

        _sprite.LayerSetRsi((uid, sprite), layer, ArrowRsi);
        _sprite.LayerSetRsiState((uid, sprite), layer, ArrowState);
        _sprite.LayerSetOffset((uid, sprite), layer, ArrowOffset);
        _sprite.LayerSetColor((uid, sprite), layer, ArrowColor);
        _sprite.LayerSetAutoAnimated((uid, sprite), layer, false);
        _sprite.LayerSetVisible((uid, sprite), layer, false);

        return true;
    }

    private bool TryGetArrowRotation(EntityUid uid, HorseControlComponent horse, TransformComponent xform, bool noRotation, out Angle rotation)
    {
        rotation = Angle.Zero;

        if (_moverQuery.TryGetComponent(uid, out var mover))
        {
            var buttons = mover.HeldMoveButtons;
            var forward = xform.WorldRotation.ToWorldVec();
            var wish = Vector2.Zero;

            if ((buttons & MoveButtons.Up) != 0)
                wish += forward;

            if ((buttons & MoveButtons.Down) != 0)
                wish -= forward * horse.BackwardsModifier;

            if (wish.LengthSquared() > DirectionEpsilon)
            {
                wish = wish.Normalized();
                rotation = Angle.FromWorldVec(wish);

                if (!noRotation)
                    rotation -= xform.WorldRotation;

                return true;
            }
        }

        if (_physicsQuery.TryGetComponent(uid, out var physics))
        {
            var velocity = physics.LinearVelocity;
            if (velocity.LengthSquared() > DirectionEpsilon)
            {
                velocity = velocity.Normalized();
                rotation = Angle.FromWorldVec(velocity);

                if (!noRotation)
                    rotation -= xform.WorldRotation;

                return true;
            }
        }

        rotation = noRotation ? xform.WorldRotation : Angle.Zero;
        return true;
    }
}
