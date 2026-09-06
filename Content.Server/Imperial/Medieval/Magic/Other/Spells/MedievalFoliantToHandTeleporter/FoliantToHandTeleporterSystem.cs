using Content.Server.Imperial.Medieval.Magic.BindStoreOnEquip;
using Content.Server.Imperial.Medieval.Magic.MedievalSpawnInFreeSlot;
using Content.Shared.Imperial.Medieval.Magic;

namespace Content.Server.Imperial.Medieval.Magic.MedievalFoliantToHandTeleporter;

public sealed partial class FoliantToHandTeleporterSystem : EntitySystem
{
    [Dependency] private readonly MedievalSpawnInFreeSlotSystem _placementSystem = default!;
    [Dependency] private readonly BindStoreOnEquipSystem _grimoireSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FoliantToHandTeleporterComponent, MedievalAfterSpawnEntityBySpellEvent>(FindFoliant);
    }

    private void FindFoliant(EntityUid uid, FoliantToHandTeleporterComponent component, MedievalAfterSpawnEntityBySpellEvent args)
    {
        var playerUid = args.Performer;
        if (!TryComp<GrimoireOwnerComponent>(playerUid, out var owner))
            return;

        var foliantUid = owner.GrimoireUid;
        if (TerminatingOrDeleted(foliantUid) ||
            !TryComp<BindStoreOnEquipComponent>(foliantUid, out var grimoire) ||
            grimoire.OwnerUid != playerUid)
        {
            foliantUid = Spawn(owner.GrimoirePrototype, Transform(playerUid).Coordinates);
            if (!_grimoireSystem.TryRestoreGrimoire(playerUid, foliantUid, owner))
            {
                QueueDel(foliantUid);
                return;
            }
        }

        _placementSystem.TryPlaceInFreeSlot(playerUid, foliantUid);
    }
}
