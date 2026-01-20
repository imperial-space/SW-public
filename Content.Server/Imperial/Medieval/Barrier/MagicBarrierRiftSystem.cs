using System;
using System.Numerics;
using Content.Server.MagicBarrier.Components;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Robust.Shared.Map;

namespace Content.Server.MagicBarrier;

public sealed class MagicBarrierRiftSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MagicBarrierRiftComponent, AfterInteractUsingEvent>(OnRiftUse);
        SubscribeLocalEvent<RiftGuardianComponent, MobStateChangedEvent>(OnGuardianStateChanged);
    }

    private void OnRiftUse(EntityUid uid, MagicBarrierRiftComponent component, AfterInteractUsingEvent args)
    {
        if (args.Handled || args.Target == null)
            return;

        if(!args.CanReach)
            return;

        if (!TryComp<RiftKeyComponent>(args.Used, out var keyComponent))
            return;

        if (component.GuardiansSpawned)
            return;

        if (!string.Equals(keyComponent.Element, component.Element, StringComparison.OrdinalIgnoreCase))
            return;

        component.GuardiansSpawned = true;
        component.Guardians.Clear();

        QueueDel(args.Used);

        var coords = Transform(uid).Coordinates;
        SpawnGuardian(uid, component, coords.Offset(new Vector2(1f, 1f)));
        SpawnGuardian(uid, component, coords.Offset(new Vector2(-1f, 1f)));
        SpawnGuardian(uid, component, coords.Offset(new Vector2(1f, -1f)));
        SpawnGuardian(uid, component, coords.Offset(new Vector2(-1f, -1f)));
        args.Handled = true;
    }

    private void SpawnGuardian(EntityUid rift, MagicBarrierRiftComponent component, EntityCoordinates coords)
    {
        var guardian = Spawn("MedievalMobSkeletMeat", coords);
        var guardianComponent = EnsureComp<RiftGuardianComponent>(guardian);
        guardianComponent.Rift = rift;
        component.Guardians.Add(guardian);
    }

    private void OnGuardianStateChanged(EntityUid uid, RiftGuardianComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        if (!TryComp<MagicBarrierRiftComponent>(component.Rift, out var riftComponent))
            return;

        riftComponent.Guardians.Remove(uid);
        if (riftComponent.Guardians.Count == 0)
            QueueDel(component.Rift);
    }
}
