using System.Linq;
using Content.Shared.Imperial.Medieval.Plague;
using Content.Shared.Movement.Components;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.Plague;

public sealed partial class MedievalPlagueSystem
{
    private Dictionary<string, float> _spreaders = new();

    private void InitializeSpread()
    {
        SubscribeLocalEvent<MedievalPlagueInfectedComponent, StartCollideEvent>(OnInfectedCollide);
        SubscribeLocalEvent<MedievalPlagueInfectOnHitComponent, MeleeHitEvent>(OnSpreaderHit);

    }

    private void OnInfectedCollide(EntityUid uid, MedievalPlagueInfectedComponent comp, ref StartCollideEvent args)
    {
        if (!TryComp<MobCollisionComponent>(uid, out var collision) || args.OurFixtureId != collision.FixtureId)
            return;

        TryInfect(args.OtherEntity, comp.PlagueSource);
    }

    private void OnSpreaderHit(EntityUid uid, MedievalPlagueInfectOnHitComponent comp, MeleeHitEvent args)
    {
        if (!comp.Active)
            return;

        var chance = comp.Chance * _spreaders.GetValueOrDefault(comp.Id, 1f);

        foreach (var item in args.HitEntities)
        {
            if (!_random.Prob(chance))
                continue;

            TryInfect(item, null);
        }
    }
}
