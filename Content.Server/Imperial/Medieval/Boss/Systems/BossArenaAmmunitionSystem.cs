using System.Linq;
using Content.Server.Cloning;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Item;
using Content.Shared.Projectiles;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Storage.Events;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed class BossArenaAmmunitionSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly CloningSystem _cloning = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    private readonly HashSet<EntityUid> _borrowedShots = new();                  // guns currently using a loaned shot

    public override void Initialize()
    {
        SubscribeLocalEvent<StorageComponent, ComponentStartup>(OnQuiverStartup);
        SubscribeLocalEvent<BossArenaQuiverComponent, StorageItemRemovedEvent>(OnQuiverRemoved);
        SubscribeLocalEvent<GunComponent, TakeAmmoEvent>(OnTakeAmmo, before: new[] { typeof(GunSystem) });
        SubscribeLocalEvent<GunComponent, AmmoShotEvent>(OnShot);
        SubscribeLocalEvent<BossArenaAmmoComponent, ContainerGettingInsertedAttemptEvent>(OnLoanInsert);
        SubscribeLocalEvent<BossArenaAmmoComponent, PickupAttemptEvent>(OnPickup);
    }

    private bool TryArena(EntityUid uid, out EntityUid arena)
    {
        arena = default;
        if (Transform(uid).MapUid is not { } map || !TryComp<BossArenaMapComponent>(map, out var component) ||
            !component.ReplenishQuivers) return false;
        arena = map;
        return true;
    }

    private bool IsQuiver(EntityUid uid)
        => MetaData(uid).EntityPrototype is { } prototype &&
           _prototypes.EnumerateParents(prototype, includeSelf: true)
               .Any(parent => parent.ID is "ClothingBeltQuiver" or "MedievalQuiver" or "MedievalQuiverSteel" or "MedievalQuiverNecro");

    public bool IsCrossbow(EntityUid uid)
        => TryComp<MedievalWeaponSkillCategoryComponent>(uid, out var category) && category.Skill == MedievalWeaponSkillId.Crossbow ||
           MetaData(uid).EntityPrototype is { } prototype && _prototypes.EnumerateParents(prototype, includeSelf: true)
               .Any(parent => parent.ID == "MedievalWeaponCrossbowSpeed");

    private void OnQuiverStartup(EntityUid uid, StorageComponent component, ComponentStartup args)
    {
        if (IsQuiver(uid)) EnsureComp<BossArenaQuiverComponent>(uid);
    }

    private EntityUid? CopyAmmo(EntityUid ammo, EntityCoordinates coordinates)
    {
        if (_cloning.CopyItem(ammo, coordinates) is not { } copy) return null;
        if (TryComp<ProjectileComponent>(ammo, out var projectile) && TryComp<ProjectileComponent>(copy, out var cloned))
        {
            cloned.Damage = new DamageSpecifier(projectile.Damage);
            Dirty(copy, cloned);
        }
        if (TryComp<CartridgeAmmoComponent>(ammo, out var cartridge))
        {
            RemComp<CartridgeAmmoComponent>(copy);
            CopyComp(ammo, copy, cartridge);
        }
        foreach (var (name, solution) in _solutions.EnumerateSolutions(ammo))
        {
            if (name == null) continue;
            _solutions.EnsureSolutionEntity(copy, name, out _, out var clonedSolution,
                solution.Comp.Solution.MaxVolume);
            if (clonedSolution is not { } target) continue;
            _solutions.RemoveAllSolution(target);
            _solutions.TryAddSolution(target, solution.Comp.Solution.Clone());   // copy coatings into the loaned ammunition
        }
        return copy;
    }

    private void OnQuiverRemoved(Entity<BossArenaQuiverComponent> entity, ref StorageItemRemovedEvent args)
    {
        if (Terminating(entity) || !TryArena(entity, out var arena) || !IsQuiver(entity) ||
            !Exists(args.ItemUid) || Terminating(args.ItemUid) ||
            !(_tags.HasTag(args.ItemUid, "Arrow") || _tags.HasTag(args.ItemUid, "Bolt")) ||
            HasComp<BossArenaAmmoComponent>(args.ItemUid)) return;
        var copy = CopyAmmo(args.ItemUid, Transform(entity).Coordinates);
        if (copy is not { } replacement) return;
        if (!_storage.Insert(entity, replacement, out _, out _, playSound: false))
        {
            QueueDel(replacement);
            return;
        }
        EnsureComp<BossArenaAmmoComponent>(args.ItemUid).Arena = arena;
    }

    private void OnTakeAmmo(EntityUid uid, GunComponent gun, TakeAmmoEvent args)
    {
        _borrowedShots.Remove(uid);
        if (args.User is not { } user || !TryArena(user, out var arena) || !TryArena(uid, out var weaponArena) ||
            arena != weaponArena || args.Shots <= 0) return;
        if (TryComp<BallisticAmmoProviderComponent>(uid, out var loadedProvider) &&
            loadedProvider.Entities.Any(HasComp<BossArenaAmmoComponent>) ||
            TryComp<ContainerAmmoProviderComponent>(uid, out var containerProvider) &&
            _containers.TryGetContainer(containerProvider.ProviderUid ?? uid, containerProvider.Container, out var container) &&
            container.ContainedEntities.Any(HasComp<BossArenaAmmoComponent>))
            _borrowedShots.Add(uid);
    }

    private void OnShot(EntityUid uid, GunComponent gun, AmmoShotEvent args)
    {
        if (!TryArena(uid, out var arena)) return;
        var borrowed = _borrowedShots.Remove(uid);
        foreach (var projectile in args.FiredProjectiles)
        {
            if (!Exists(projectile) || Terminating(projectile)) continue;
            if (!borrowed && !HasComp<BossArenaAmmoComponent>(projectile) && !IsCrossbow(uid)) continue;
            var loan = EnsureComp<BossArenaAmmoComponent>(projectile);
            loan.Arena = arena;
            loan.Fired = true;
            loan.Expires = _timing.CurTime + TimeSpan.FromSeconds(10);
        }
    }

    private void OnLoanInsert(EntityUid uid, BossArenaAmmoComponent component, ContainerGettingInsertedAttemptEvent args)
    {
        if (component.Fired || IsQuiver(args.Container.Owner)) args.Cancel();
    }

    private void OnPickup(EntityUid uid, BossArenaAmmoComponent component, PickupAttemptEvent args)
    {
        if (component.Fired) args.Cancel();                                      // spent loaned ammunition cannot be picked up
    }

    public override void Update(float frameTime)
    {
        foreach (var entity in EntityManager.AllEntities<BossArenaAmmoComponent>().ToArray())    // only clean up loaned ammo, leave normal gear alone
        {
            if (Terminating(entity) || MetaData(entity).EntityPaused) continue;
            if (!Exists(entity.Comp.Arena) || Transform(entity).MapUid != entity.Comp.Arena ||
                entity.Comp.Expires is { } deadline && _timing.CurTime >= deadline)
                QueueDel(entity);
            else if (TryComp<CartridgeAmmoComponent>(entity, out var cartridge) && cartridge.Spent)
                QueueDel(entity);
        }
    }
}
