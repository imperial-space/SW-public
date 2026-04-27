using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Bee.Components;

namespace Content.Server.Imperial.Medieval.Bee.Systems;

public sealed partial class MedievalBeeSystem : EntitySystem
{
    private void InitializeBee()
    {
        SubscribeLocalEvent<MedievalBeeComponent, MapInitEvent>(BeeInitialize);
        SubscribeLocalEvent<MedievalBeeComponent, DamageModifyEvent>(BeeDamaged);
    }
    private void BeeInitialize(EntityUid uid, MedievalBeeComponent component, MapInitEvent args)
    {
        if (!TryGetHiveGridFromTransform(uid, out var grid))
            return;

        var hive = grid.Value.Comp.Hive;
        if (!TryComp<MedievalBeeHiveComponent>(hive, out var hiveComponent))
            return;

        component.ConnectedHive = (hive.Value, hiveComponent);
        hiveComponent.Bees.Add((uid, component));
        if (hiveComponent.Pacified)
        {
            Pacify(uid, component);
        }
    }
    private void BeeDamaged(EntityUid uid, MedievalBeeComponent component, DamageModifyEvent args)
    {
        if (!args.Damage.AnyPositive())
            return;

        if (!component.ConnectedHive.HasValue)
            return;

        if (!component.ConnectedHive.Value.Comp.Pacified)
            return;

        UnPacify(component.ConnectedHive.Value);
    }
}
