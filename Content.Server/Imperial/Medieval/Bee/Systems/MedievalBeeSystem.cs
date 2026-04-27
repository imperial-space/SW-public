using Content.Server.Popups;
using Content.Server.Stunnable;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Stacks;
using Content.Shared.StatusEffectNew;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Bee.Systems;

public sealed partial class MedievalBeeSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly StunSystem _stun = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    public override void Initialize()
    {
        InitializeTrap();
        InitializeBee();
        InitializeHive();
        InitializeSmoke();
        InitializePlayerSpawn();
        InitializeItemSource();
        InitializeLinkedSpawner();
        InitializeChanceSpawn();
    }
    private TimeSpan _nextUpdate = TimeSpan.Zero;
    private TimeSpan _updateCooldown = TimeSpan.FromSeconds(1);
    public override void Update(float frameTime)
    {
        if (_nextUpdate > _timing.CurTime)
            return;

        _nextUpdate = _timing.CurTime + _updateCooldown;
        UpdateHive(frameTime);
        UpdateTrap(frameTime);
        UpdateLinkedSpawner(frameTime);
    }
}
