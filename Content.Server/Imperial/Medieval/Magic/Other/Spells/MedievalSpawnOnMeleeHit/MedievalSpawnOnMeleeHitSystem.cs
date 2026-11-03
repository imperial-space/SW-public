using Content.Shared.Imperial.Medieval.Magic.MedievalSpawnOnMeleeHit;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Server.Imperial.Medieval.Magic.MedievalSpawnOnMeleeHit;


public sealed class MedievalSpawnOnMeleeHitSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalSpawnOnMeleeHitComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnMeleeHit(EntityUid uid, MedievalSpawnOnMeleeHitComponent component, MeleeHitEvent args)
    {
        foreach (var target in args.HitEntities)
        {
            Spawn(component.Prototype, Transform(target).Coordinates);
        }
    }
}
