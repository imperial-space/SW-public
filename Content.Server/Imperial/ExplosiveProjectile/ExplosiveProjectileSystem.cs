using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;
using Content.Shared.Audio;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Events;
using Content.Server.Atmos.Components;
using Content.Shared.Inventory;
using Content.Server.Stunnable;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Standing;
using Content.Shared.StatusEffect;
using Content.Server.Imperial.ExplosiveProjectile.Components;

namespace Content.Server.Imperial.ExplosiveProjectile
{
    [UsedImplicitly]
    internal sealed class ExplosiveProjectileSystem : EntitySystem
    {
        [Dependency] private readonly StunSystem _stunSystem = default!;
        [Dependency] protected readonly SharedAudioSystem Audio = default!;
        [Dependency] private readonly InventorySystem _inventorySystem = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<ExplosiveProjectileComponent, StartCollideEvent>(HandleCollide);
        }
        private void TryExplodeEntity(EntityUid uid, ExplosiveProjectileComponent component, EntityUid target)
        {
            if (EntityManager.HasComponent<ExplosiveProjectileResultOnComponent>(target) || EntityManager.HasComponent<ExplosiveProjectileResultOffComponent>(target))
                return;

            if (_inventorySystem.TryGetSlotEntity(target, "outerClothing", out var outerClothingTarget) &&
            HasComp<PressureProtectionComponent>(outerClothingTarget))
            {
                outerClothingTarget = target;
                EntityManager.AddComponent<ExplosiveProjectileResultOffComponent>(target);
            }
            else
            {
                if (EntityManager.HasComponent<BodyComponent>(target))
                    EntityManager.AddComponent<ExplosiveProjectileResultOnComponent>(target);
            }
        }
        private void OnExplodeStart(EntityUid uid, ExplosiveProjectileComponent component, EntityUid target)
        {
            Audio.PlayPvs(component.SoundActivate, target);
            if (EntityManager.TryGetComponent<StatusEffectsComponent>(target, out var status))
            {
                _stunSystem.TryStun(target, TimeSpan.FromSeconds(component.StunParam), true, status);
                _stunSystem.TryKnockdown(target, TimeSpan.FromSeconds(component.KnockdownTime), true, status);
                _stunSystem.TrySlowdown(target, TimeSpan.FromSeconds(component.SlowdownParam), true, component.WalkSpeedParam, component.RunSpeedParam, status);
            }
            TryExplodeEntity(uid, component, target);
        }
        private void HandleCollide(EntityUid uid, ExplosiveProjectileComponent component, ref StartCollideEvent args)
        {
            if (args.OurFixtureId != component.CheckedFixtureID)
                return;

            OnExplodeStart(uid, component, args.OtherEntity);
        }
    }
}
