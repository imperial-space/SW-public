using Content.Shared.Damage;
using Content.Shared.Ghost;
using Content.Shared.Imperial.Medieval.Magic.MedievalRain;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Magic.MedievalRain;


public sealed class MedievalRainDropSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private readonly HashSet<Entity<MobStateComponent>> _targets = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalRainDropComponent, MapInitEvent>(OnMapInit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<MedievalRainDropComponent>();

        while (query.MoveNext(out var uid, out var component))
        {
            if (_timing.CurTime < component.LandAt) continue;

            Land(uid, component);
        }
    }

    private void OnMapInit(EntityUid uid, MedievalRainDropComponent component, MapInitEvent args)
    {
        component.LandAt = _timing.CurTime + TimeSpan.FromSeconds(component.FallTime);
    }

    private void Land(EntityUid uid, MedievalRainDropComponent component)
    {
        var coordinates = Transform(uid).Coordinates;
        var origin = Exists(component.Performer) ? component.Performer : (EntityUid?) null;

        _targets.Clear();
        _lookup.GetEntitiesInRange(coordinates, component.Radius, _targets, LookupFlags.Uncontained);

        foreach (var target in _targets)
        {
            if (target.Owner == component.Performer || HasComp<GhostComponent>(target.Owner)) continue;
            if (_mobState.IsDead(target.Owner, target.Comp)) continue;

            _damageable.TryChangeDamage(target.Owner, component.Damage, origin: origin);

            if (component.ApDamage.Empty || TerminatingOrDeleted(target.Owner)) continue;

            _damageable.TryChangeDamage(target.Owner, component.ApDamage, ignoreResistances: true, origin: origin);
        }

        if (component.Impact is { } impact) Spawn(impact, coordinates);
        if (component.Sound != null) _audio.PlayPvs(component.Sound, coordinates);

        QueueDel(uid);
    }
}
