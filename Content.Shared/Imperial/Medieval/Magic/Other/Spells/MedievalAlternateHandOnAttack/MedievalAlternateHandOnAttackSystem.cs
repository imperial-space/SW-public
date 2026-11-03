using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Medieval.Magic.MedievalAlternateHandOnAttack;


public sealed class MedievalAlternateHandOnAttackSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HandsComponent, MeleeAttackEvent>(OnMeleeAttack);
    }

    private void OnMeleeAttack(Entity<HandsComponent> ent, ref MeleeAttackEvent args)
    {
        if (!HasComp<MedievalAlternateHandOnAttackComponent>(args.Weapon) || !_timing.IsFirstTimePredicted)
            return;

        foreach (var hand in ent.Comp.Hands.Keys)
        {
            if (!_hands.TryGetHeldItem((ent.Owner, ent.Comp), hand, out var held) || held == args.Weapon)
                continue;

            if (!HasComp<MedievalAlternateHandOnAttackComponent>(held.Value) || !TryComp(held.Value, out MeleeWeaponComponent? other))
                continue;

            // The other item waits for half of its own delay, so the swings of both items are evenly spaced
            var rate = _melee.GetAttackRate(held.Value, ent.Owner, other);

            if (rate > 0)
            {
                var next = _timing.CurTime + TimeSpan.FromSeconds(0.5f / rate);

                if (other.NextAttack < next)
                {
                    other.NextAttack = next;
                    DirtyField(held.Value, other, nameof(MeleeWeaponComponent.NextAttack));
                }
            }

            _hands.TrySetActiveHand((ent.Owner, ent.Comp), hand);
            return;
        }
    }
}
