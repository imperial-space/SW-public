using Content.Server.Spawners.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;
using Content.Server.Cult.Components; // imperial medieval

namespace Content.Server.Spawners.EntitySystems;

public sealed class SpawnOnDespawnSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpawnOnDespawnComponent, TimedDespawnEvent>(OnDespawn);
    }

    private void OnDespawn(EntityUid uid, SpawnOnDespawnComponent comp, ref TimedDespawnEvent args)
    {
        if (!TryComp(uid, out TransformComponent? xform))
            return;

        var newBrush = Spawn(comp.Prototype, xform.Coordinates); // imperial medieval start

        if (TryComp<CultBloodPaintComponent>(uid, out var rune) && rune != null)
        {
        if (TryComp<CultBloodPaintComponent>(newBrush, out var newPaint) && newPaint != null)
        {
            newPaint.PosX = rune.PosX;
            newPaint.PosY = rune.PosY;
        }
        } // imperial medieval end
    }

    public void SetPrototype(Entity<SpawnOnDespawnComponent> entity, EntProtoId prototype)
    {
        entity.Comp.Prototype = prototype;
    }
}
