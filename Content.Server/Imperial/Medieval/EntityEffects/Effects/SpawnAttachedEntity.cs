using Content.Shared.EntityEffects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.EntityEffects;


/// <summary>
/// Spawns an entity attached to the target, so it follows the target around.
/// </summary>
public sealed partial class SpawnAttachedEntity : EntityEffect
{
    [DataField(required: true)]
    public EntProtoId Entity;

    public override void Effect(EntityEffectBaseArgs args)
    {
        args.EntityManager.SpawnAttachedTo(Entity, new EntityCoordinates(args.TargetEntity, System.Numerics.Vector2.Zero));
    }

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => "";
}
