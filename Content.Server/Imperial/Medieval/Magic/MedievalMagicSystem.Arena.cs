using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Imperial.Medieval.Magic;
using Content.Shared.Imperial.Medieval.Magic.Mana;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Magic;

public sealed partial class MedievalMagicSystem
{
    [Dependency] private readonly SharedBossArenaRulesSystem ArenaRules = default!;

    private bool CanSpawnArenaSpell(EntProtoId prototype, EntityCoordinates coordinates, EntityUid performer, EntityUid action)
        => ValidateArenaPlacement(ArenaRules.CanSpawn(prototype, coordinates), performer, action);

    private bool CanSpawnArenaSpell(EntProtoId prototype, MapCoordinates coordinates, EntityUid performer, EntityUid action)
        => ValidateArenaPlacement(ArenaRules.CanSpawn(prototype, coordinates), performer, action);

    private bool ValidateArenaPlacement(bool allowed, EntityUid performer, EntityUid action)
    {
        if (allowed)
            return true;

        if (TryComp<ManaComponent>(performer, out var mana))
            mana.CastedSpells.Remove(action);
        RaiseLocalEvent(action, new MedievalFailCastSpellEvent { Action = action, Performer = performer });
        return false;
    }

    private EntityUid SpawnArenaSpell(EntProtoId prototype, EntityCoordinates coordinates, EntityUid performer)
    {
        using var scope = ArenaRules.WithSource(performer, Content.Shared.Imperial.Medieval.Boss.BossArenaDamageKind.Spells);
        var spawned = Spawn(prototype, coordinates);
        ArenaRules.TagSpell(spawned, performer);
        return spawned;
    }

    private EntityUid SpawnArenaSpell(EntProtoId prototype, MapCoordinates coordinates, EntityUid performer)
    {
        using var scope = ArenaRules.WithSource(performer, Content.Shared.Imperial.Medieval.Boss.BossArenaDamageKind.Spells);
        var spawned = Spawn(prototype, coordinates);
        ArenaRules.TagSpell(spawned, performer);
        return spawned;
    }
}
