using Content.Shared.Damage;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Medieval.ArmorIntegrity;
using Content.Shared.Imperial.Medieval.BloodSplatter;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Content.Shared.Damage.Systems;

namespace Content.Server.Imperial.Medieval.BloodSplatter;

public sealed class BloodSplatterOnHitSystem : EntitySystem
{
    [Dependency] private readonly MedievalArmorIntegritySystem _armorIntegrity = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<BloodSplatterOnHitComponent, BeforeDamageChangedEvent>(OnBeforeDamageChanged);
        SubscribeLocalEvent<BloodSplatterOnHitComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnPlayerAttached(PlayerAttachedEvent args)
    {
        if (HasComp<HumanoidProfileComponent>(args.Entity))
            EnsureComp<BloodSplatterOnHitComponent>(args.Entity);
    }

    private void OnBeforeDamageChanged(
        Entity<BloodSplatterOnHitComponent> ent,
        ref BeforeDamageChangedEvent args)
    {
        ent.Comp.PendingAttacker = null;

        if (args.Cancelled ||
            !args.Damage.AnyPositive() ||
            args.Origin is not { } origin ||
            origin == ent.Owner ||
            !HasComp<MobStateComponent>(origin))
        {
            return;
        }

        if (TryComp<InventoryComponent>(ent, out var inventory) && _armorIntegrity.HasUnbrokenArmor(inventory))
            return;

        ent.Comp.PendingAttacker = origin;
    }

    private void OnDamageChanged(Entity<BloodSplatterOnHitComponent> ent, ref DamageChangedEvent args)
    {
        if (ent.Comp.PendingAttacker is not { } attacker)
            return;

        ent.Comp.PendingAttacker = null;

        if (!args.DamageIncreased || args.Origin != attacker || ent.Comp.Effects.Count == 0)
            return;

        var effect = Spawn(_random.Pick(ent.Comp.Effects), Transform(ent).Coordinates);
        _transform.SetParent(effect, ent);
    }
}
