using System.Diagnostics.CodeAnalysis;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Projectiles;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Timing;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;

namespace Content.Shared.Imperial.Medieval.MobRiding
{
    public sealed partial class SharedRideableSystem : EntitySystem
    {

        #region Dependencies
        [Dependency] private readonly SharedMoverController _mover = default!;
        [Dependency] private readonly SharedSkillsSystem _skillsSystem = default!;
        [Dependency] private readonly FixtureSystem _fixtureSystem = default!;
        [Dependency] private readonly SharedHandsSystem _hands = default!;
        [Dependency] private readonly ThrowingSystem _throwing = default!;
        [Dependency] private readonly SharedStunSystem _stun = default!;
        [Dependency] private readonly DamageableSystem _damageable = default!;
        [Dependency] private readonly UseDelaySystem _useDelay = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly SharedAudioSystem _audio = default!;
        #endregion

        #region Const
        private readonly string[] _clashOne = new[] {
            "/Audio/Imperial/Medieval/clash_one1.ogg",
            "/Audio/Imperial/Medieval/clash_one2.ogg",
        };

        private readonly string _clashBoth = "/Audio/Imperial/Medieval/clash_both.ogg";
        private readonly string _clashNone = "/Audio/Imperial/Medieval/wood_parry.ogg";

        #endregion

        #region Initialize
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<RideableComponent, BeforeDamageChangedEvent>(OnBeforeDamageChanged);
            SubscribeLocalEvent<MeleeWeaponComponent, BeforeMeleeHitEvent>(OnBeforeMeleeHit);
            SubscribeLocalEvent<BuckleComponent, RayCastSort>(OnRayCastSort);
            SubscribeLocalEvent<ProjectileComponent, ProjectileBeforeHitEvent>(OnBeforeProjectileHit);

            SubscribeLocalEvent<RideableComponent, StrappedEvent>(OnBuckled);
            SubscribeLocalEvent<RideableComponent, UnstrappedEvent>(OnUnbuckled);

            // TODO: поменять на PikeComponent
            SubscribeLocalEvent<SpearComponent, ItemWieldedEvent>(OnWielded);
            SubscribeLocalEvent<SpearComponent, ItemUnwieldedEvent>(OnUnwielded);

            SubscribeLocalEvent<RideableComponent, StartCollideEvent>(HandleCollide);

        }
        #endregion

        #region Other functions
        private bool TryGetRideable(EntityUid rider, [NotNullWhen(true)] out EntityUid? entity, [NotNullWhen(true)] out RideableComponent? rideable, [NotNullWhen(true)] out RideableSprintComponent? sprint)
        {
            rideable = null;
            sprint = null;
            entity = null;

            if (!TryComp<BuckleComponent>(rider, out var buckle) || !buckle.BuckledTo.HasValue)
                return false;

            entity = buckle.BuckledTo.Value;
            return TryComp(buckle.BuckledTo.Value, out rideable) && TryComp(buckle.BuckledTo.Value, out sprint);
        }

        private bool TryGetRideable(EntityUid rider, [NotNullWhen(true)] out EntityUid? entity, [NotNullWhen(true)] out RideableComponent? rideable) =>
            TryGetRideable(rider, out entity, out rideable, out _);

        private bool TryGetSprint(EntityUid rider, [NotNullWhen(true)] out EntityUid? entity, [NotNullWhen(true)] out RideableSprintComponent? sprint) =>
            TryGetRideable(rider, out entity, out _, out sprint);

        private void ThrowFromRideable(EntityUid uid, uint stunSeconds = 2, DamageSpecifier? damage = null)
        {
            if (!TryComp<BuckleComponent>(uid, out var buckle))
                return;

            var ev = new TryUnbuckleEvent(new Entity<BuckleComponent>(uid, buckle));

            RaiseLocalEvent(uid, ref ev);
            if(damage != null)
                _damageable.TryChangeDamage(uid, damage);
            if(stunSeconds > 0)
                _stun.TryKnockdown(uid, TimeSpan.FromSeconds(stunSeconds), true);
        }

        private bool TryGetSkill(EntityUid uid, string skill, out int level)
        {
            level = 0;

            if (!TryComp<SkillsComponent>(uid, out var skills))
                return false;

            if (!skills.Levels.TryGetValue(skill, out level))
                return false;

            return true;
        }

        #endregion

        #region Pikes

        private void HandleCollide(EntityUid uid, RideableComponent comp, ref StartCollideEvent args)
        {
            if (args.OurFixtureId != comp.PikeShapeId)
                return;

            if (args.OtherEntity == comp.Rider)
                return;

            Logger.Debug(args.OurFixtureId);
            Logger.Debug(args.OtherFixtureId);
            Logger.Debug(args.OtherEntity.Id.ToString());

            if (!TryComp<MobStateComponent>(args.OtherEntity, out var mobState))
                return;

            if (mobState.CurrentState != MobState.Alive)
                return;

            if(args.OtherFixtureId != comp.PikeShapeId)
                PikeHitEntity(uid, comp, args.OtherEntity);
            else
                PikeClash(uid, comp, args.OtherEntity);
        }

        private void PikeHitEntity(EntityUid uid, RideableComponent comp, EntityUid other, uint stunSeconds = 2, bool throwOther = true)
        {
            Logger.Debug("Hit entity");
            if (!TryComp<PhysicsComponent>(uid, out var rideablePhysics))
                return;

            if (!comp.Rider.HasValue)
                return;

            if (!TryGetPike(comp.Rider.Value, out var item, out var melee))
                return;

            if (TryComp<UseDelayComponent>(item, out var useDelay) && _useDelay.IsDelayed((item.Value, useDelay)))
                return;


            var direction = rideablePhysics.LinearVelocity;

            if(throwOther)
                _throwing.TryThrow(other, direction * 0.6f);
            if(stunSeconds > 0)
                _stun.TryKnockdown(other, TimeSpan.FromSeconds(stunSeconds), true);

            _damageable.TryChangeDamage(other, melee.Damage);

            if (useDelay != null)
                _useDelay.TryResetDelay((item.Value, useDelay));
        }

        private bool TryGetPike(EntityUid uid, [NotNullWhen(true)] out EntityUid? pike, [NotNullWhen(true)] out MeleeWeaponComponent? comp)
        {
            pike = null;
            comp = null;

            pike = _hands.GetActiveItem(uid);
            return pike.HasValue && TryComp(pike, out comp);
        }

        private sealed class Skills
        {
            public int Intelligence;
            public int Endurance;
            public int Agility;
            public int Strength;
            public int Vitality;

            public Skills(SharedRideableSystem system, EntityUid uid)
            {
                system.TryGetSkill(uid, "Intelligence", out Intelligence);
                system.TryGetSkill(uid, "Endurance", out Endurance);
                system.TryGetSkill(uid, "Agility", out Agility);
                system.TryGetSkill(uid, "Strength", out Strength);
                system.TryGetSkill(uid, "Vitality", out Vitality);
            }
        }

        private bool TryGetClashData(EntityUid uid,
            [NotNullWhen(true)] out RideableComponent? comp,
            [NotNullWhen(true)] out EntityUid? rider,
            out float attackStrength,
            out float stability,
            out float finalAttack,
            out float finalStability)
        {
            comp = null;
            rider = null;
            attackStrength = 0;
            stability = 0;
            finalAttack = 0;
            finalStability = 0;


            if (!TryComp(uid, out comp))
                return false;

            if (!comp.Rider.HasValue)
                return false;

            rider = comp.Rider;

            if (!HasComp<SkillsComponent>(rider))
                return false;

            var skills = new Skills(this, rider.Value);

            attackStrength = (skills.Strength * 1.4f) + (skills.Agility * 1.0f) + (skills.Endurance * 0.6f);
            stability = (skills.Vitality * 1.6f) + (skills.Agility * 1.15f) + (skills.Strength * 0.3f);

            finalAttack = attackStrength + _random.Next(1, 21);
            finalStability = stability + _random.Next(1, 21);

            return true;
        }

        private CheckResult ResolveHitOutcome(float finalAttack, float otherFinalAttack)
        {
            var difference = finalAttack - otherFinalAttack;

            return difference switch
            {
                > 10 => CheckResult.Self,
                < -10 => CheckResult.Other,
                _ => CheckResult.Draw
            };
        }

        private CheckResult ResolveStabilityOutcome(float finalStability,
            float otherFinalStability,
            float stabilityDifficulty)
        {
            var selfPasses = finalStability >= stabilityDifficulty;
            var otherPasses = otherFinalStability >= stabilityDifficulty;

            return (selfPasses, otherPasses) switch
            {
                (true, true) => CheckResult.Both,
                (false, false) => CheckResult.Draw,
                (true, false) => CheckResult.Self,
                (false, true) => CheckResult.Other
            };
        }

        private void ThrowClash(EntityUid rideable, EntityUid rider, EntityUid otherRider)
        {
            if (!TryComp<PhysicsComponent>(rideable, out var rideablePhysics))
                return;
            var velocity = rideablePhysics.AngularVelocity;
            if (!TryGetPike(rider, out var pike, out var pikeComp))
                return;
            ThrowFromRideable(otherRider, 2, pikeComp.Damage * velocity);
        }


        private void PikeClash(EntityUid uid, RideableComponent comp, EntityUid other)
        {
            Logger.Debug("Hit clash");

            if (uid.Id < other.Id)
                return;

            if (!TryGetClashData(uid,
                    out _,
                    out var rider,
                    out var attackStrength,
                    out var stability,
                    out var finalAttack,
                    out var finalStability))
                return;

            if (!TryGetClashData(other,
                    out var otherComp,
                    out var otherRider,
                    out var otherAttackStrength,
                    out var otherStability,
                    out var otherFinalAttack,
                    out var otherFinalStability))
                return;

            var hitResult = ResolveHitOutcome(finalAttack, otherFinalAttack);

            switch (hitResult)
            {
                case CheckResult.Self:
                    ThrowClash(uid, rider.Value, otherRider.Value);
                    break;
                case CheckResult.Other:
                    ThrowClash(other, otherRider.Value, rider.Value);
                    break;
                case CheckResult.Draw:
                    var stabilityDifficulty = (finalAttack + otherFinalAttack) / 2;
                    var stabilityResult =
                        ResolveStabilityOutcome(finalStability, otherFinalStability, stabilityDifficulty);

                    var oneSound = _random.Pick(_clashOne);
                    switch (stabilityResult)
                    {
                        case CheckResult.Self:
                            _audio.PlayPvs(oneSound, uid);
                            ThrowClash(uid, rider.Value, otherRider.Value);
                            break;
                        case CheckResult.Other:
                            _audio.PlayPvs(oneSound, other);
                            ThrowClash(other, otherRider.Value, rider.Value);
                            break;
                        case CheckResult.Draw:
                            _audio.PlayPvs(_clashBoth, uid);
                            ThrowClash(uid, rider.Value, otherRider.Value);
                            ThrowClash(other, otherRider.Value, rider.Value);
                            break;
                        case CheckResult.Both:
                            _audio.PlayPvs(_clashNone, uid);
                            break;
                    }
                    break;
            }
        }

        private bool CheckPike(EntityUid uid, out EntityUid? item)
        {
            item = _hands.GetActiveItem(uid);

            if (!item.HasValue)
                return false;

            //TODO: изменить на PikeComponent
            if (!HasComp<SpearComponent>(item.Value))
                return false;

            if (!TryComp<WieldableComponent>(item.Value, out var wieldable))
                return false;

            return wieldable.Wielded;
        }

        // TODO: поменять на PikeComponent
        private void OnWielded(EntityUid uid, SpearComponent comp, ref ItemWieldedEvent args)
        {
            var rider = args.User;

            if (!TryGetRideable(rider, out var rideableEntity, out var rideable))
                return;

            if (rideable.Pike != null)
                return;

            rideable.Pike = uid;
            rideable.Dirty();

            CreatePikeFixture(rideableEntity.Value, rideable);
        }

        // TODO: это тоже
        private void OnUnwielded(EntityUid uid, SpearComponent comp, ref ItemUnwieldedEvent args)
        {
            var rider = args.User;

            if (!TryGetRideable(rider, out var rideableEntity, out var rideable))
                return;

            if (rideable.Pike != uid)
                return;

            rideable.Pike = null;
            rideable.Dirty();

            RemovePikeFixture(rideableEntity.Value, rideable);
        }

        private void CreatePikeFixture(EntityUid uid, RideableComponent rideable)
        {
            _fixtureSystem.TryCreateFixture(uid, rideable.PikeShape, rideable.PikeShapeId, density: 1, collisionLayer: (int)CollisionGroup.PikeLayer, collisionMask: (int)CollisionGroup.PikeMask, hard:false);
        }

        private void RemovePikeFixture(EntityUid uid, RideableComponent rideable)
        {
            Logger.Debug("removing");
            _fixtureSystem.DestroyFixture(uid, rideable.PikeShapeId);
        }

        #endregion

        #region Damage ignore
        private void OnRayCastSort(EntityUid uid, BuckleComponent component, ref RayCastSort args)
        {
            var toRemove = new List<RayCastResults>();

            for (int i = 0; i < args.HitEntities.Count; i++)
            {
                var result = args.HitEntities[i];
                var target = result.HitEntity;
                if (!TryComp<RideableComponent>(target, out var rideable))
                    continue;

                if (!rideable.Rider.HasValue)
                    continue;

                if (rideable.Rider.Value == uid)
                {
                    args.HitEntities.RemoveAt(i);
                }
            }
        }

        private void OnBeforeProjectileHit(EntityUid uid,
            ProjectileComponent component,
            ref ProjectileBeforeHitEvent args)
        {
            if (!TryComp<RideableComponent>(args.Target, out var rideable)) return;
            if (!rideable.Rider.HasValue) return;
            if (!args.Shooter.HasValue) return;

            if (rideable.Rider.Value == args.Shooter.Value)
                args.Cancelled = true;

        }

        private void OnBeforeDamageChanged(EntityUid uid, RideableComponent component, ref BeforeDamageChangedEvent args)
        {
            if (!args.Origin.HasValue || !component.Rider.HasValue)
                return;
            if (args.Origin.Value == component.Rider.Value)
                args.Cancelled = true;
        }

        private void OnBeforeMeleeHit(EntityUid uid, MeleeWeaponComponent component, ref BeforeMeleeHitEvent args)
        {
            // по факту бесполезно из-за того, что всё удаляется ещё при рейкасте
            for (var i = args.HitEntities.Count - 1; i >= 0; i--)
            {
                var target = args.HitEntities[i];

                if (!TryComp<RideableComponent>(target, out var rideable)) continue;
                if (!rideable.Rider.HasValue) continue;

                if (rideable.Rider.Value == args.User)
                {
                    args.HitEntities.RemoveAt(i);
                }
            }
        }
        #endregion

        #region Buckle
        public void OnBuckled(EntityUid uid, RideableComponent component, ref StrappedEvent args)
        {
            var strap = args.Strap;
            var buckle = args.Buckle;

            component.Rider = buckle.Owner;
            component.IsRiding = true;
            _mover.SetRelay(buckle.Owner, uid);
            if (CheckPike(buckle.Owner, out var pike))
            {
                CreatePikeFixture(uid, component);
                component.Pike = pike;
            }

            var ev = new StartRideEvent(strap, buckle);
            RaiseLocalEvent(uid, ref ev);
            component.Dirty();
        }

        public void OnUnbuckled(EntityUid uid, RideableComponent component, ref UnstrappedEvent args)
        {
            if (!component.Rider.HasValue)
                return;
            RemComp<RelayInputMoverComponent>(component.Rider.Value);
            RemovePikeFixture(uid, component);
            component.Pike = null;

            component.Rider = null;
            component.IsRiding = false;
            var ev = new StopRideEvent(args.Strap, args.Buckle);
            RaiseLocalEvent(uid, ref ev);
            component.Dirty();
        }
        #endregion
    }

}

#region Events
[ByRefEvent]
public readonly record struct StartRideEvent(Entity<StrapComponent> Strap, Entity<BuckleComponent> Buckle);

[ByRefEvent]
public readonly record struct StopRideEvent(Entity<StrapComponent> Strap, Entity<BuckleComponent> Buckle);

[ByRefEvent]
public readonly record struct TryUnbuckleEvent(Entity<BuckleComponent> Buckle);
#endregion
