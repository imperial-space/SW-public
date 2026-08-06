using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.EntityEffects;

/// <summary>
/// Imperial Medieval merge shim: old EntityEffect API (Effect + EntityEffectBaseArgs)
/// kept so Medieval chemistry/plague effects compile against the new RaiseEvent-based EntityEffect.
/// TODO: port Imperial Medieval effects to EntityEffectBase&lt;T&gt; + effect systems.
/// </summary>
public abstract partial class LegacyEntityEffect : EntityEffect
{
    public abstract void Effect(EntityEffectBaseArgs args);

    protected abstract string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys);

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => ReagentEffectGuidebookText(prototype, entSys);

    public override void RaiseEvent(EntityUid target, IEntityEffectRaiser raiser, float scale, EntityUid? user)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        // Reagent-specific scale/quantity is not plumbed here yet — see TODO above.
        Effect(new EntityEffectBaseArgs(target, entMan));
    }
}

/// <summary>
/// Imperial Medieval merge shim for pre-refactor entity effect args.
/// </summary>
public record class EntityEffectBaseArgs
{
    public EntityUid TargetEntity;
    public IEntityManager EntityManager = default!;

    public EntityEffectBaseArgs(EntityUid targetEntity, IEntityManager entityManager)
    {
        TargetEntity = targetEntity;
        EntityManager = entityManager;
    }
}

/// <summary>
/// Imperial Medieval merge shim for reagent-triggered effects.
/// </summary>
public record class EntityEffectReagentArgs : EntityEffectBaseArgs
{
    public EntityUid? OrganEntity;
    public Solution? Source;
    public FixedPoint2 Quantity;
    public ReagentPrototype? Reagent;
    public ReactionMethod? Method;
    public FixedPoint2 Scale;

    public EntityEffectReagentArgs(
        EntityUid targetEntity,
        IEntityManager entityManager,
        EntityUid? organEntity,
        Solution? source,
        FixedPoint2 quantity,
        ReagentPrototype? reagent,
        ReactionMethod? method,
        FixedPoint2 scale) : base(targetEntity, entityManager)
    {
        OrganEntity = organEntity;
        Source = source;
        Quantity = quantity;
        Reagent = reagent;
        Method = method;
        Scale = scale;
    }
}
