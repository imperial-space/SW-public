using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Imperial.Medieval.MobRiding;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using System.Diagnostics.CodeAnalysis;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval;
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

namespace Content.Server.Imperial.Medieval.MobRiding
{
    public sealed partial class RideableSystem : EntitySystem
    {
        [Dependency] private readonly NPCSystem _npc = default!;
        [Dependency] private readonly SharedBuckleSystem _buckle = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;
        [Dependency] private readonly MobStateSystem _mobState = default!;
        [Dependency] private readonly FixtureSystem _fixtureSystem = default!;
        [Dependency] private readonly SharedHandsSystem _hands = default!;
        [Dependency] private readonly ThrowingSystem _throwing = default!;
        [Dependency] private readonly SharedStunSystem _stun = default!;
        [Dependency] private readonly DamageableSystem _damageable = default!;
        [Dependency] private readonly UseDelaySystem _useDelay = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly SharedAudioSystem _audio = default!;

        #region Const
        private readonly string[] _clashOne = new[] {
            "/Audio/Imperial/Medieval/clash_one1.ogg",
            "/Audio/Imperial/Medieval/clash_one2.ogg",
        };

        private readonly string _clashBoth = "/Audio/Imperial/Medieval/clash_both.ogg";
        private readonly string _clashNone = "/Audio/Imperial/Medieval/wood_parry.ogg";

        #endregion

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<RideableComponent, StartRideEvent>(OnBuckled);
            SubscribeLocalEvent<RideableComponent, StopRideEvent>(OnUnbuckled);

            SubscribeLocalEvent<RideableComponent, BuckledEvent>(OnSelfBuckled);
            SubscribeLocalEvent<RideableComponent, UnbuckledEvent>(OnSelfUnbuckled);

            SubscribeLocalEvent<RideableComponent, StrapAttemptEvent>(OnTryRide);

            SubscribeLocalEvent<RideableComponent, MobStateChangedEvent>(OnMobStateChanged);

            SubscribeLocalEvent<BuckleComponent, TryUnbuckleEvent>(OnTryUnbuckle);

            // TODO: поменять на PikeComponent
            SubscribeLocalEvent<SpearComponent, ItemWieldedEvent>(OnWielded);
            SubscribeLocalEvent<SpearComponent, ItemUnwieldedEvent>(OnUnwielded);

            SubscribeLocalEvent<RideableComponent, StartCollideEvent>(HandleCollide);
        }

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

            public Skills(RideableSystem system, EntityUid uid)
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
                            _audio.PlayPvs(oneSound, rider.Value);
                            ThrowClash(uid, rider.Value, otherRider.Value);
                            break;
                        case CheckResult.Other:
                            _audio.PlayPvs(oneSound, otherRider.Value);
                            ThrowClash(other, otherRider.Value, rider.Value);
                            break;
                        case CheckResult.Draw:
                            _audio.PlayPvs(_clashBoth, uid);
                            ThrowClash(uid, rider.Value, otherRider.Value);
                            ThrowClash(other, otherRider.Value, rider.Value);
                            break;
                        case CheckResult.Both:
                            _audio.PlayPvs(_clashNone, rider.Value);
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

        private void OnTryUnbuckle(EntityUid uid, BuckleComponent comp, ref TryUnbuckleEvent args)
        {
            _buckle.TryUnbuckle(args.Buckle, args.Buckle);
        }

        private void OnTryRide(EntityUid uid, RideableComponent component, ref StrapAttemptEvent args)
        {
            if (!CheckAgility(args.Buckle))
            {
                _popup.PopupEntity(Loc.GetString("imperial-medieval-rideable-skill-popup"), args.Buckle.Owner);
                args.Cancelled = true;
            }
            else if (!CheckState(args.Strap))
            {
                args.Cancelled = true;
            }
        }

        private bool CheckState(EntityUid uid)
        {
            return _mobState.IsAlive(uid);
        }

        private bool CheckAgility(BuckleComponent buckle)
        {
            if (!TryComp<SkillsComponent>(buckle.Owner, out var skills))
                return false;

            if (!skills.Levels.TryGetValue("Agility", out var agility))
                return false;

            if (agility < 9)
                return false;

            return true;
        }

        private void OnSelfBuckled(EntityUid uid, RideableComponent component, ref BuckledEvent args)
        {
            if (component.IsRiding && component.Rider.HasValue)
            {
                _buckle.TryUnbuckle(component.Rider.Value, component.Rider);
            }

            component.CanRide = false;
        }

        private void OnSelfUnbuckled(EntityUid uid, RideableComponent component, ref UnbuckledEvent args)
        {
            component.CanRide = true;
        }

        private void OnBuckled(EntityUid uid, RideableComponent component, ref StartRideEvent args)
        {
            if (!component.CanRide && component.Rider.HasValue)
                _buckle.TryUnbuckle(component.Rider.Value, component.Rider.Value);

            if (TryComp<HTNComponent>(uid, out var htn))
                _npc.SleepNPC(uid, htn);

            if (CheckPike(args.Buckle.Owner, out var pike))
            {
                CreatePikeFixture(uid, component);
                component.Pike = pike;
            }

            component.Dirty();
        }

        private void OnUnbuckled(EntityUid uid, RideableComponent component, ref StopRideEvent args)
        {
            if (TryComp<HTNComponent>(uid, out var htn))
                _npc.WakeNPC(uid, htn);

            RemovePikeFixture(uid, component);
            component.Pike = null;

            component.Dirty();

        }

        private void OnMobStateChanged(EntityUid uid, RideableComponent component, ref MobStateChangedEvent args)
        {
            if (args.NewMobState == MobState.Alive)
                return;

            if(component.Rider.HasValue)
                _buckle.TryUnbuckle(component.Rider.Value, component.Rider.Value);
        }
    }

}
