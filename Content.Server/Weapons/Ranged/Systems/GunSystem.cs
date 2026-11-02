using System.Linq;
using System.Numerics;
using Content.Server.Cargo.Systems;
using Content.Server.Weapons.Ranged.Components;
using Content.Shared.Cargo;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.Effects;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Weapons.Reflect;
using Content.Shared.Damage.Components;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Containers;
//Imperial medieval - spread event moved to Content.Shared.Weapons.Ranged.Events.
using Robust.Shared.Threading;
using Robust.Shared.Random;
using Content.Server.PowerCell;

namespace Content.Server.Weapons.Ranged.Systems;

public sealed partial class GunSystem : SharedGunSystem
{
    [Dependency] private readonly DamageExamineSystem _damageExamine = default!;
    [Dependency] private readonly PricingSystem _pricing = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _color = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;

    private const float DamagePitchVariation = 0.05f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BallisticAmmoProviderComponent, PriceCalculationEvent>(OnBallisticPrice);
    }

    private void OnBallisticPrice(EntityUid uid, BallisticAmmoProviderComponent component, ref PriceCalculationEvent args)
    {
        if (string.IsNullOrEmpty(component.Proto) || component.UnspawnedCount == 0)
            return;

        if (!ProtoManager.TryIndex<EntityPrototype>(component.Proto, out var proto))
        {
            Log.Error($"Unable to find fill prototype for price on {component.Proto} on {ToPrettyString(uid)}");
            return;
        }

        // Probably good enough for most.
        var price = _pricing.GetEstimatedPrice(proto);
        args.Price += price * component.UnspawnedCount;
    }

    public override void Shoot(EntityUid gunUid, GunComponent gun, List<(EntityUid? Entity, IShootable Shootable)> ammo,
        EntityCoordinates fromCoordinates, EntityCoordinates toCoordinates, out bool userImpulse, EntityUid? user = null, bool throwItems = false)
    {
        userImpulse = true;

        if (user != null)
        {
            var selfEvent = new SelfBeforeGunShotEvent(user.Value, (gunUid, gun), ammo);
            RaiseLocalEvent(user.Value, selfEvent);
            if (selfEvent.Cancelled)
            {
                userImpulse = false;
                return;
            }
        }

        // Imperial medieval start - launch rules are shared; hitscan/throws retain server random sampling.
        var launch = GetProjectileLaunch(gunUid, gun, fromCoordinates, toCoordinates, user,
            !throwItems && ammo.All(a => a.Shootable is not HitscanPrototype));
        var fromMap = TransformSystem.ToMapCoordinates(fromCoordinates);
        var mapDirection = launch.Direction;
        var mapAngle = launch.BaseAngle;
        var angle = launch.RecoilAngle;
        var fromEnt = launch.Origin;
        var gunVelocity = launch.InheritedVelocity;
        // Imperial medieval end

        // I must be high because this was getting tripped even when true.
        // DebugTools.Assert(direction != Vector2.Zero);
        var shotProjectiles = new List<EntityUid>(ammo.Count);

        foreach (var (ent, shootable) in ammo)
        {
            // pneumatic cannon doesn't shoot bullets it just throws them, ignore ammo handling
            if (throwItems && ent != null)
            {
                ShootOrThrow(ent.Value, mapDirection, gunVelocity, gun, gunUid, user);
                continue;
            }

            switch (shootable)
            {
                // Cartridge shoots something else
                case CartridgeAmmoComponent cartridge:
                    if (!cartridge.Spent)
                    {
                        var uid = Spawn(cartridge.Prototype, fromEnt);
                        CreateAndFireProjectiles(uid, cartridge);

                        RaiseLocalEvent(ent!.Value, new AmmoShotEvent()
                        {
                            FiredProjectiles = shotProjectiles,
                        });

                        SetCartridgeSpent(ent.Value, cartridge, true);

                        if (cartridge.DeleteOnSpawn)
                            Del(ent.Value);
                    }
                    else
                    {
                        userImpulse = false;
                        Audio.PlayPredicted(gun.SoundEmpty, gunUid, user);
                    }

                    // Something like ballistic might want to leave it in the container still
                    if (!cartridge.DeleteOnSpawn && !Containers.IsEntityInContainer(ent!.Value))
                        EjectCartridge(ent.Value, angle);

                    Dirty(ent!.Value, cartridge);
                    break;
                // Ammo shoots itself
                case AmmoComponent newAmmo:
                    if (ent == null)
                        break;
                    CreateAndFireProjectiles(ent.Value, newAmmo);

                    break;
                case HitscanPrototype hitscan:

                    EntityUid? lastHit = null;

                    var from = fromMap;
                    // can't use map coords above because funny FireEffects
                    var fromEffect = fromCoordinates;
                    var dir = mapDirection.Normalized();

                    //in the situation when user == null, means that the cannon fires on its own (via signals). And we need the gun to not fire by itself in this case
                    var lastUser = user ?? gunUid;

                    if (hitscan.Reflective != ReflectType.None)
                    {
                        for (var reflectAttempt = 0; reflectAttempt < 3; reflectAttempt++)
                        {
                            var ray = new CollisionRay(from.Position, dir, hitscan.CollisionMask);
                            var rayCastResults =
                                Physics.IntersectRay(from.MapId, ray, hitscan.MaxLength, lastUser, false).ToList();
                            if (!rayCastResults.Any())
                                break;

                            var result = rayCastResults[0];

                            // Check if laser is shot from in a container
                            if (!_container.IsEntityOrParentInContainer(lastUser))
                            {
                                // Checks if the laser should pass over unless targeted by its user
                                foreach (var collide in rayCastResults)
                                {
                                    if (collide.HitEntity != gun.Target &&
                                        CompOrNull<RequireProjectileTargetComponent>(collide.HitEntity)?.Active == true)
                                    {
                                        continue;
                                    }

                                    result = collide;
                                    break;
                                }
                            }

                            var hit = result.HitEntity;
                            lastHit = hit;

                            FireEffects(fromEffect, result.Distance, dir.Normalized().ToAngle(), hitscan, hit);

                            var ev = new HitScanReflectAttemptEvent(user, gunUid, hitscan.Reflective, dir, false);
                            RaiseLocalEvent(hit, ref ev);

                            if (!ev.Reflected)
                                break;

                            fromEffect = Transform(hit).Coordinates;
                            from = TransformSystem.ToMapCoordinates(fromEffect);
                            dir = ev.Direction;
                            lastUser = hit;
                        }
                    }

                    if (lastHit != null)
                    {
                        var hitEntity = lastHit.Value;
                        if (hitscan.StaminaDamage > 0f)
                            _stamina.TakeStaminaDamage(hitEntity, hitscan.StaminaDamage, source: user);

                        var dmg = hitscan.Damage;

                        var hitName = ToPrettyString(hitEntity);
                        if (dmg != null)
                            dmg = Damageable.TryChangeDamage(hitEntity, dmg * Damageable.UniversalHitscanDamageModifier, origin: user);

                        // gets weapon, shooter, and target.
                        var ev = new HitscanResolvedEvent(hitEntity, lastUser);//Imperial medieval edit
                        RaiseLocalEvent(gunUid, ref ev);//Imperial medieval edit

                        // check null again, as TryChangeDamage returns modified damage values
                        if (dmg != null)
                        {
                            if (!Deleted(hitEntity))
                            {
                                if (dmg.AnyPositive())
                                {
                                    _color.RaiseEffect(Color.Red, new List<EntityUid>() { hitEntity }, Filter.Pvs(hitEntity, entityManager: EntityManager));
                                }

                                // TODO get fallback position for playing hit sound.
                                PlayImpactSound(hitEntity, dmg, hitscan.Sound, hitscan.ForceSound);
                            }

                            if (user != null)
                            {
                                Logs.Add(LogType.HitScanHit,
                                    $"{ToPrettyString(user.Value):user} hit {hitName:target} using hitscan and dealt {dmg.GetTotal():damage} damage");
                            }
                            else
                            {
                                Logs.Add(LogType.HitScanHit,
                                    $"{hitName:target} hit by hitscan dealing {dmg.GetTotal():damage} damage");
                            }
                        }
                    }
                    else
                    {
                        FireEffects(fromEffect, hitscan.MaxLength, dir.ToAngle(), hitscan);
                    }

                    Audio.PlayPredicted(gun.SoundGunshotModified, gunUid, user);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        RaiseLocalEvent(gunUid, new AmmoShotEvent()
        {
            FiredProjectiles = shotProjectiles,
        });

        void CreateAndFireProjectiles(EntityUid ammoEnt, AmmoComponent ammoComp)
        {
            if (TryComp<ProjectileSpreadComponent>(ammoEnt, out var ammoSpreadComp))
            {
                var spreadEvent = new GunGetAmmoSpreadEvent(ammoSpreadComp.Spread);
                RaiseLocalEvent(gunUid, ref spreadEvent);

                var angles = LinearSpread(mapAngle - spreadEvent.Spread / 2,
                    mapAngle + spreadEvent.Spread / 2, ammoSpreadComp.Count);

                ShootOrThrow(ammoEnt, angles[0].ToVec(), gunVelocity, gun, gunUid, user);
                shotProjectiles.Add(ammoEnt);

                for (var i = 1; i < ammoSpreadComp.Count; i++)
                {
                    var newuid = Spawn(ammoSpreadComp.Proto, fromEnt);
                    ShootOrThrow(newuid, angles[i].ToVec(), gunVelocity, gun, gunUid, user);
                    shotProjectiles.Add(newuid);
                }
            }
            else
            {
                ShootOrThrow(ammoEnt, mapDirection, gunVelocity, gun, gunUid, user);
                shotProjectiles.Add(ammoEnt);
            }

            MuzzleFlash(gunUid, ammoComp, mapDirection.ToAngle(), user);
            Audio.PlayPredicted(gun.SoundGunshotModified, gunUid, user);
        }
    }

    private void ShootOrThrow(EntityUid uid, Vector2 mapDirection, Vector2 gunVelocity, GunComponent gun, EntityUid gunUid, EntityUid? user)
    {
        MarkPredictedProjectile(uid, gunUid, user); // Imperial medieval edit
        if (gun.Target is { } target && !TerminatingOrDeleted(target))
        {
            var targeted = EnsureComp<TargetedProjectileComponent>(uid);
            targeted.Target = target;
            Dirty(uid, targeted);
        }

        // Do a throw
        if (!HasComp<ProjectileComponent>(uid))
        {
            RemoveShootable(uid);
            // TODO: Someone can probably yeet this a billion miles so need to pre-validate input somewhere up the call stack.
            ThrowingSystem.TryThrow(uid, mapDirection, gun.ProjectileSpeedModified, user);
            return;
        }

        ShootProjectile(uid, mapDirection, gunVelocity, gunUid, user, gun.ProjectileSpeedModified);
    }

    /// <summary>
    /// Gets a linear spread of angles between start and end.
    /// </summary>
    /// <param name="start">Start angle in degrees</param>
    /// <param name="end">End angle in degrees</param>
    /// <param name="intervals">How many shots there are</param>
    private Angle[] LinearSpread(Angle start, Angle end, int intervals)
    {
        var angles = new Angle[intervals];
        DebugTools.Assert(intervals > 1);

        for (var i = 0; i <= intervals - 1; i++)
        {
            angles[i] = new Angle(start + (end - start) * i / (intervals - 1));
        }

        return angles;
    }

    protected override void Popup(string message, EntityUid? uid, EntityUid? user) { }

    protected override void CreateEffect(EntityUid gunUid, MuzzleFlashEvent message, EntityUid? user = null)
    {
        var filter = Filter.Pvs(gunUid, entityManager: EntityManager);

        if (TryComp<ActorComponent>(user, out var actor))
            filter.RemovePlayer(actor.PlayerSession);

        RaiseNetworkEvent(message, filter);
    }

    // Imperial medieval start
    public void PlayImpactSound(EntityUid otherEntity, DamageSpecifier? modifiedDamage, SoundSpecifier? weaponSound,
        bool forceWeaponSound, EntityUid? exclude = null)
    {
        var sound = GetProjectileImpactSound(otherEntity, modifiedDamage, weaponSound, forceWeaponSound, out var vary);
        var filter = Filter.Pvs(otherEntity, entityManager: EntityManager);
        if (exclude != null)
            filter = filter.RemoveWhereAttachedEntity(entity => entity == exclude.Value);
        Audio.PlayEntity(sound, filter, otherEntity, true,
            vary ? AudioParams.Default.WithVariation(DamagePitchVariation) : null);
    }
    // Imperial medieval end

    // TODO: Pseudo RNG so the client can predict these.
    #region Hitscan effects

    private void FireEffects(EntityCoordinates fromCoordinates, float distance, Angle angle, HitscanPrototype hitscan, EntityUid? hitEntity = null)
    {
        // Lord
        // Forgive me for the shitcode I am about to do
        // Effects tempt me not
        var sprites = new List<(NetCoordinates coordinates, Angle angle, SpriteSpecifier sprite, float scale)>();
        var fromXform = Transform(fromCoordinates.EntityId);

        // We'll get the effects relative to the grid / map of the firer
        // Look you could probably optimise this a bit with redundant transforms at this point.

        var gridUid = fromXform.GridUid;
        if (gridUid != fromCoordinates.EntityId && TryComp(gridUid, out TransformComponent? gridXform))
        {
            var (_, gridRot, gridInvMatrix) = TransformSystem.GetWorldPositionRotationInvMatrix(gridXform);
            var map = TransformSystem.ToMapCoordinates(fromCoordinates);
            fromCoordinates = new EntityCoordinates(gridUid.Value, Vector2.Transform(map.Position, gridInvMatrix));
            angle -= gridRot;
        }
        else
        {
            angle -= TransformSystem.GetWorldRotation(fromXform);
        }

        if (distance >= 1f)
        {
            if (hitscan.MuzzleFlash != null)
            {
                var coords = fromCoordinates.Offset(angle.ToVec().Normalized() / 2);
                var netCoords = GetNetCoordinates(coords);

                sprites.Add((netCoords, angle, hitscan.MuzzleFlash, 1f));
            }

            if (hitscan.TravelFlash != null)
            {
                var coords = fromCoordinates.Offset(angle.ToVec() * (distance + 0.5f) / 2);
                var netCoords = GetNetCoordinates(coords);

                sprites.Add((netCoords, angle, hitscan.TravelFlash, distance - 1.5f));
            }
        }

        if (hitscan.ImpactFlash != null)
        {
            var coords = fromCoordinates.Offset(angle.ToVec() * distance);
            var netCoords = GetNetCoordinates(coords);

            sprites.Add((netCoords, angle.FlipPositive(), hitscan.ImpactFlash, 1f));
        }

        if (sprites.Count > 0)
        {
            RaiseNetworkEvent(new HitscanEvent
            {
                Sprites = sprites,
            }, Filter.Pvs(fromCoordinates, entityMan: EntityManager));
        }
    }

    #endregion
}
