using Content.Shared.Imperial.Medieval.Magic;

namespace Content.Server.Imperial.Medieval.Magic;


/// <summary>
/// The server part is responsible for casting instant spells
/// </summary>
public sealed partial class MedievalMagicSystem
{
    private void InitializeInstantSpells()
    {

    }

    protected override void CastSpawnInHandSpell(MedievalSpawnInHandSpellData args)
    {
        var action = GetEntity(args.Action);
        var performer = GetEntity(args.Performer);
        var coords = Transform(performer).Coordinates;

        var ev = new MedievalBeforeSpawnEntityBySpellEvent()
        {
            Action = action,
            Performer = performer,
            SpawnedEntityPrototype = args.SpawnedEntityPrototype,
            Coordinates = coords,
        };
        RaiseLocalEvent(action, ref ev);

        if (ev.Cancelled || !CanSpawnArenaSpell(ev.SpawnedEntityPrototype, ev.Coordinates, performer, action)) return;

        var spawndEntity = SpawnArenaSpell(ev.SpawnedEntityPrototype, ev.Coordinates, performer);
        _handsSystem.TryPickupAnyHand(ev.Performer, spawndEntity);

        var spawnEv = new MedievalAfterSpawnEntityBySpellEvent()
        {
            Action = action,
            Performer = performer,
            SpawnedEntity = spawndEntity
        };

        RaiseLocalEvent(spawndEntity, spawnEv);
        RaiseLocalEvent(action, spawnEv);

        base.CastSpawnInHandSpell(args);
    }

    protected override void CastInstantSpawnSpell(MedievalInstantSpawnData args)
    {
        var action = GetEntity(args.Action);
        var performer = GetEntity(args.Performer);
        var coords = Transform(performer).Coordinates;

        var ev = new MedievalBeforeSpawnEntityBySpellEvent()
        {
            Action = action,
            Performer = performer,
            SpawnedEntityPrototype = args.SpawnedEntityPrototype,
            Coordinates = coords,
        };
        RaiseLocalEvent(action, ref ev);

        if (ev.Cancelled || !CanSpawnArenaSpell(ev.SpawnedEntityPrototype, ev.Coordinates, performer, action)) return;

        var ent = SpawnArenaSpell(ev.SpawnedEntityPrototype, ev.Coordinates, performer);

        var spawnEv = new MedievalAfterSpawnEntityBySpellEvent()
        {
            Action = action,
            Performer = performer,
            SpawnedEntity = ent
        };

        RaiseLocalEvent(ent, spawnEv);
        RaiseLocalEvent(action, spawnEv);

        base.CastInstantSpawnSpell(args);
    }
}
