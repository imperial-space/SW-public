using System.Linq;
using Content.Shared.Imperial.Medieval.Plague;
using Content.Shared.Movement.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.Plague;

public sealed partial class MedievalPlagueSystem
{
    private void InitializeInfected()
    {
        SubscribeLocalEvent<MedievalPlagueInfectedComponent, StartCollideEvent>(OnInfectedCollide);

    }

    private void OnInfectedCollide(EntityUid uid, MedievalPlagueInfectedComponent comp, ref StartCollideEvent args)
    {
        if (!TryComp<MobCollisionComponent>(uid, out var collision) || args.OurFixtureId != collision.FixtureId)
            return;

        TryInfect(args.OtherEntity, comp.PlagueSource);
    }
}
