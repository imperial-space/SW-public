using System.Linq;
using System.Numerics;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.EntityLayer;
using Content.Shared.Imperial.PhaseSpace;
using Content.Shared.Input;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Dash;


public sealed partial class MedievalDashSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly StaminaSystem _staminaSystem = default!;
    [Dependency] private readonly SharedPhysicsSystem _physicsSystem = default!;


    public override void Initialize()
    {
        base.Initialize();

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.MedievalDash, new PointerInputCmdHandler(DashButtonPressed))
            .Register<MedievalDashSystem>();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var enumerator = EntityQueryEnumerator<MedievalDashComponent, EntityLayerComponent>();

        while (enumerator.MoveNext(out var uid, out var component, out var entityLayerComponent))
        {
            if (_timing.CurTime < component.NextDash) continue;

            RemComp<PhaseSpaceShadowComponent>(uid);
            entityLayerComponent.CollideLayers = component.CachedLayers;
        }
    }

    private bool DashButtonPressed(ICommonSession? playerSession, EntityCoordinates coordinates, EntityUid entity)
    {
        if (playerSession?.AttachedEntity is not { Valid: true } player || !Exists(player)) return false;

        if (!TryComp<MedievalDashComponent>(player, out var component)) return false;
        if (!TryComp<PhysicsComponent>(player, out var physicsComponent)) return false;
        if (!TryComp<InputMoverComponent>(player, out var inputMoverComponent)) return false;

        if (_timing.CurTime < component.NextDash && _timing.IsFirstTimePredicted) return false;
        if (physicsComponent.LinearVelocity == Vector2.Zero) return false;
        if ((inputMoverComponent.HeldMoveButtons & MoveButtons.AnyDirection) == 0) return false;

        var targetRotation = physicsComponent.LinearVelocity.ToAngle();

        var force = new Vector2(component.Force);
        var forceDirection = targetRotation - Angle.FromDegrees(45);

        var impulse = forceDirection.RotateVec(force);
        var dashTime = TimeSpan.FromSeconds(component.Force / 990 / physicsComponent.Mass);

        if (!_staminaSystem.TryTakeStamina(player, component.StaminaDamage)) return false;

        _physicsSystem.ApplyLinearImpulse(player, impulse);

        var shadowComponent = EnsureComp<PhaseSpaceShadowComponent>(player);
        var entityLayerComponent = EnsureComp<EntityLayerComponent>(player);

        shadowComponent.ShadowUpdateRate = TimeSpan.FromSeconds(0);
        shadowComponent.PositionUpdateRate = TimeSpan.FromSeconds(0);

        component.CachedLayers = entityLayerComponent.CollideLayers.ToHashSet();
        component.NextDash = dashTime + component.AdditionalDashReloadTime + _timing.CurTime;

        entityLayerComponent.CollideLayers = new() { component.DashLayer };

        return false;
    }
}
