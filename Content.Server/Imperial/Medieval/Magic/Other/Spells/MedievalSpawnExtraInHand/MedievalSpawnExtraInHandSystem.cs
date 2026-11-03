using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Magic;
using Content.Shared.Imperial.Medieval.Magic.MedievalSpawnExtraInHand;

namespace Content.Server.Imperial.Medieval.Magic.MedievalSpawnExtraInHand;


public sealed class MedievalSpawnExtraInHandSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalSpawnExtraInHandComponent, MedievalAfterSpawnEntityBySpellEvent>(OnSpawned);
    }

    private void OnSpawned(EntityUid uid, MedievalSpawnExtraInHandComponent component, MedievalAfterSpawnEntityBySpellEvent args)
    {
        var extra = Spawn(component.Prototype, Transform(args.Performer).Coordinates);

        if (!_hands.TryPickupAnyHand(args.Performer, extra))
            QueueDel(extra);
    }
}
