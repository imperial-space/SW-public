using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Shared.GameTicking;
using Content.Shared.Imperial.Medieval.Achievements;
using Content.Shared.Inventory.Events;
using Content.Shared.Hands;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Projectiles;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Content.Shared.Storage.Events;
using Robust.Shared.Containers;
using Robust.Server.Player;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Achievements;

public sealed partial class AchievementSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IServerDbManager _dbManager = default!;
    [Dependency] private readonly ActorSystem _actor = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;

    private readonly Dictionary<Guid, HashSet<string>> _playerAchievements = new();
    private readonly Dictionary<Guid, Dictionary<string, Dictionary<string, int>>> _roundProgression = new();

    public const string AchievementFirstJoin = "AchievementJoinSpellward";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);

        SubscribeLocalEvent<StackComponent, StackCountChangedEvent>(OnStackCountChanged);
        SubscribeLocalEvent<StorageComponent, StorageItemInsertedEvent>(OnStorageItemInserted);
        SubscribeLocalEvent<StorageComponent, StorageItemRemovedEvent>(OnStorageItemRemoved);
        SubscribeLocalEvent<AchievementOwnerComponent, DidEquipEvent>(OnInventoryEquipped);
        SubscribeLocalEvent<AchievementOwnerComponent, DidEquipHandEvent>(OnHandEquipped);
        SubscribeLocalEvent<AchievementOwnerComponent, DidUnequipHandEvent>(OnHandUnequipped);
        SubscribeLocalEvent<AchievementTargetComponent, MobStateChangedEvent>(OnTargetDied);
        SubscribeLocalEvent<AchievementLocationComponent, StartCollideEvent>(OnCollide);

        _playerManager.PlayerStatusChanged += OnPlayerChange;

        InitializeUI();
    }

    private async void OnPlayerChange(object? sender, SessionStatusEventArgs args)
    {
        var guid = args.Session.UserId.UserId;

        switch (args.NewStatus)
        {
            case SessionStatus.InGame:
                var achievements = await _dbManager.GetPlayerAchievements(guid);

                _playerAchievements[guid] = achievements
                    .Select(a => a.AchievementId)
                    .ToHashSet();

                var savedProgress = await _dbManager.GetPlayerAchievementProgress(guid);
                if (savedProgress.Count > 0)
                {
                    if (!_roundProgression.TryGetValue(guid, out var playerProg))
                    {
                        playerProg = new Dictionary<string, Dictionary<string, int>>();
                        _roundProgression[guid] = playerProg;
                    }

                    foreach (var (achId, keys) in savedProgress)
                    {
                        if (_playerAchievements[guid].Contains(achId))
                            continue;

                        playerProg[achId] = new Dictionary<string, int>(keys);
                    }
                }

                TryGrantAchievement(guid, AchievementFirstJoin, args.Session);
                break;

            case SessionStatus.Disconnected:
                _playerAchievements.Remove(guid);
                break;
        }
    }

    private async void OnRoundStarted(RoundStartedEvent args)
    {
        foreach (var session in _playerManager.Sessions)
        {
            var guid = session.UserId.UserId;

            if (!_playerAchievements.ContainsKey(guid))
                continue;

            var savedProgress = await _dbManager.GetPlayerAchievementProgress(guid);
            if (savedProgress.Count == 0)
                continue;

            if (!_roundProgression.TryGetValue(guid, out var playerProg))
            {
                playerProg = new Dictionary<string, Dictionary<string, int>>();
                _roundProgression[guid] = playerProg;
            }

            foreach (var (achId, keys) in savedProgress)
            {
                if (_playerAchievements[guid].Contains(achId))
                    continue;

                playerProg[achId] = new Dictionary<string, int>(keys);
            }
        }
    }

    private async void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        await SaveAllProgressAsync();
        _roundProgression.Clear();
    }

    private async Task SaveAllProgressAsync()
    {
        foreach (var (guid, playerProg) in _roundProgression)
        {
            var toSave = new Dictionary<string, Dictionary<string, int>>();
            foreach (var (achId, achProg) in playerProg)
            {
                if (!_protoManager.TryIndex<AchievementPrototype>(achId, out var proto))
                    continue;

                if (proto.RoundOnly)
                    continue;

                toSave[achId] = achProg;
            }

            if (toSave.Count > 0)
                await _dbManager.SavePlayerAchievementProgress(guid, toSave);
        }
    }

    private Guid? GetPlayerGuid(EntityUid player)
    {
        if (!_actor.TryGetSession(player, out var session))
            return null;

        return session?.UserId.UserId;
    }

    public void TryGrantAchievement(EntityUid player, string achievementId, object? context = null)
    {
        var guid = GetPlayerGuid(player);
        if (guid == null)
            return;

        if (!_playerAchievements.TryGetValue(guid.Value, out var unlocked))
            return;

        if (unlocked.Contains(achievementId))
            return;

        if (!_protoManager.TryIndex<AchievementPrototype>(achievementId, out var prototype))
            return;

        if (!_roundProgression.TryGetValue(guid.Value, out var playerProg))
        {
            playerProg = new Dictionary<string, Dictionary<string, int>>();
            _roundProgression[guid.Value] = playerProg;
        }

        if (!playerProg.TryGetValue(achievementId, out var achievementProg))
        {
            achievementProg = new Dictionary<string, int>();
            playerProg[achievementId] = achievementProg;
        }

        var allMet = true;
        foreach (var condition in prototype.Conditions)
        {
            if (!condition.Check(player, EntityManager, _protoManager, context, achievementProg))
                allMet = false;
        }

        if (allMet)
        {
            unlocked.Add(achievementId);
            _ = _dbManager.GrantAchievement(guid.Value, achievementId);

            playerProg.Remove(achievementId);
            if (!prototype.RoundOnly)
                _ = _dbManager.DeletePlayerAchievementProgress(guid.Value, achievementId);

            if (_actor.TryGetSession(player, out var session))
            {
                var ev = new AchievementUnlockedEvent(achievementId);
                RaiseNetworkEvent(ev, session!);
            }
        }
    }

    private void UpdatePickupProgress(EntityUid player)
    {
        if (GetPlayerGuid(player) is not { } guid)
            return;

        if (!_playerAchievements.TryGetValue(guid, out var unlocked))
            return;

        var itemCounts = CountInventoryItems(player);

        foreach (var ach in _protoManager.EnumeratePrototypes<AchievementPrototype>())
        {
            if (unlocked.Contains(ach.ID))
                continue;

            if (!ach.Conditions.Any(c => c is CollectAllItemsCondition or CollectAnyItemsCondition))
                continue;

            if (!_roundProgression.TryGetValue(guid, out var playerProg))
            {
                playerProg = new Dictionary<string, Dictionary<string, int>>();
                _roundProgression[guid] = playerProg;
            }

            if (!playerProg.TryGetValue(ach.ID, out var achievementProg))
            {
                achievementProg = new Dictionary<string, int>();
                playerProg[ach.ID] = achievementProg;
            }

            foreach (var condition in ach.Conditions)
            {
                switch (condition)
                {
                    case CollectAllItemsCondition collectAll:
                    {
                        foreach (var (proto, _) in collectAll.ItemPrototypes)
                        {
                            var key = $"{collectAll.ProgressKey}:{proto}";
                            var count = itemCounts.GetValueOrDefault(proto, 0);
                            achievementProg[key] = Math.Max(achievementProg.GetValueOrDefault(key, 0), count);
                        }
                        break;
                    }

                    case CollectAnyItemsCondition collectAny:
                    {
                        var total = collectAny.ItemPrototypes.Sum(p => itemCounts.GetValueOrDefault(p, 0));
                        achievementProg[collectAny.ProgressKey] = Math.Max(
                            achievementProg.GetValueOrDefault(collectAny.ProgressKey, 0), total);
                        break;
                    }
                }
            }

            TryGrantAchievement(player, ach.ID);
        }
    }

    private Dictionary<string, int> CountInventoryItems(EntityUid player)
    {
        var counts = new Dictionary<string, int>();
        var visited = new HashSet<EntityUid>();
        CountItemsRecursive(player, counts, visited);
        return counts;
    }

    private void CountItemsRecursive(EntityUid uid, Dictionary<string, int> counts, HashSet<EntityUid> visited)
    {
        if (!visited.Add(uid))
            return;

        if (!EntityManager.TryGetComponent<ContainerManagerComponent>(uid, out var containerManager))
            return;

        foreach (var container in containerManager.Containers.Values)
        {
            foreach (var item in container.ContainedEntities)
            {
                var meta = EntityManager.GetComponent<MetaDataComponent>(item);
                if (meta.EntityPrototype != null)
                {
                    var protoId = meta.EntityPrototype.ID;
                    var count = EntityManager.TryGetComponent<StackComponent>(item, out var stack)
                        ? stack.Count
                        : 1;
                    counts[protoId] = counts.GetValueOrDefault(protoId, 0) + count;
                }

                CountItemsRecursive(item, counts, visited);
            }
        }
    }

    private int GetItemCount(EntityUid item)
    {
        if (EntityManager.TryGetComponent<StackComponent>(item, out var stack))
            return stack.Count;

        return 1;
    }

    private void OnHandEquipped(EntityUid uid, AchievementOwnerComponent component, DidEquipHandEvent args)
    {
        UpdatePickupProgress(uid);
    }

    private void OnHandUnequipped(EntityUid uid, AchievementOwnerComponent component, DidUnequipHandEvent args)
    {
        UpdatePickupProgress(uid);
    }

    private void OnStorageItemInserted(EntityUid uid, StorageComponent component, ref StorageItemInsertedEvent args)
    {
        if (FindOwnerPlayer(uid) is not { } player)
            return;

        UpdatePickupProgress(player);
    }

    private void OnStorageItemRemoved(EntityUid uid, StorageComponent component, ref StorageItemRemovedEvent args)
    {
        if (FindOwnerPlayer(uid) is not { } player)
            return;

        UpdatePickupProgress(player);
    }

    private EntityUid? FindOwnerPlayer(EntityUid uid)
    {
        var current = uid;
        while (_containerSystem.TryGetContainingContainer(current, out var container))
        {
            current = container.Owner;
            if (HasComp<AchievementOwnerComponent>(current))
                return current;
        }

        if (HasComp<AchievementOwnerComponent>(uid))
            return uid;

        return null;
    }

    private void OnStackCountChanged(EntityUid uid, StackComponent component, StackCountChangedEvent args)
    {
        if (args.NewCount == args.OldCount)
            return;

        if (FindOwnerPlayer(uid) is not { } player)
            return;

        UpdatePickupProgress(player);
    }

    private void OnInventoryEquipped(EntityUid uid, AchievementOwnerComponent component, DidEquipEvent args)
    {
        if (GetPlayerGuid(uid) == null)
            return;

        foreach (var ach in _protoManager.EnumeratePrototypes<AchievementPrototype>())
        {
            if (ach.Conditions.Any(c => c is EquipSetCondition))
            {
                TryGrantAchievement(uid, ach.ID);
            }
        }
    }

    private void OnTargetDied(EntityUid targetUid, AchievementTargetComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        var killer = GetPlayerFromOrigin(args.Origin);
        if (killer == null || !HasComp<AchievementOwnerComponent>(killer.Value))
            return;

        TryUpdateProgressAndGrant(killer.Value, targetUid,
            ach => ach.Conditions.Any(c => c is KillMobCondition));
    }

    private void OnCollide(EntityUid uid, AchievementLocationComponent component, ref StartCollideEvent args)
    {
        var player = args.OtherEntity;

        if (!HasComp<AchievementOwnerComponent>(player) || GetPlayerGuid(player) == null)
            return;

        TryUpdateProgressAndGrant(player, component.LocationId,
            ach => ach.Conditions.Any(c => c is LocationCondition));
    }

    private EntityUid? GetPlayerFromOrigin(EntityUid? origin)
    {
        if (origin == null)
            return null;

        if (HasComp<AchievementOwnerComponent>(origin.Value))
            return origin.Value;

        var parent = Transform(origin.Value).ParentUid;
        if (parent.IsValid() && HasComp<AchievementOwnerComponent>(parent))
            return parent;

        if (TryComp<ProjectileComponent>(origin.Value, out var projectile) && projectile.Shooter.HasValue)
        {
            if (HasComp<AchievementOwnerComponent>(projectile.Shooter.Value))
                return projectile.Shooter.Value;
        }

        return null;
    }

    public async Task<bool> TryGrantAchievement(Guid guid, string achievementId, ICommonSession? session = null)
    {
        if (!_playerAchievements.TryGetValue(guid, out var unlocked))
            return false;

        if (unlocked.Contains(achievementId))
            return false;

        if (!_protoManager.HasIndex<AchievementPrototype>(achievementId))
            return false;

        var success = await _dbManager.GrantAchievement(guid, achievementId);
        if (!success)
            return false;

        unlocked.Add(achievementId);

        if (_roundProgression.TryGetValue(guid, out var playerProg))
            playerProg.Remove(achievementId);

        if (_protoManager.TryIndex<AchievementPrototype>(achievementId, out var proto) && !proto.RoundOnly)
            await _dbManager.DeletePlayerAchievementProgress(guid, achievementId);

        if (session != null)
            RaiseNetworkEvent(new AchievementUnlockedEvent(achievementId), session);

        return true;
    }

    public async Task<bool> TryRevokeAchievement(Guid guid, string achievementId)
    {
        if (!_playerAchievements.TryGetValue(guid, out var unlocked))
            return false;

        if (!unlocked.Contains(achievementId))
            return false;

        var success = await _dbManager.RevokeAchievement(guid, achievementId);
        if (success)
        {
            unlocked.Remove(achievementId);

            if (_roundProgression.TryGetValue(guid, out var playerProg))
                playerProg.Remove(achievementId);
        }

        return success;
    }

    public List<string> GetUnlockedAchievements(Guid guid)
    {
        if (_playerAchievements.TryGetValue(guid, out var achievements))
            return achievements.ToList();

        return new List<string>();
    }

    private void TryUpdateProgressAndGrant(EntityUid player, object context,
        Func<AchievementPrototype, bool> filter)
    {
        if (GetPlayerGuid(player) is not { } guid)
            return;

        if (!_playerAchievements.TryGetValue(guid, out var unlocked))
            return;

        if (!_roundProgression.TryGetValue(guid, out var playerProg))
        {
            playerProg = new Dictionary<string, Dictionary<string, int>>();
            _roundProgression[guid] = playerProg;
        }

        foreach (var ach in _protoManager.EnumeratePrototypes<AchievementPrototype>())
        {
            if (unlocked.Contains(ach.ID) || !filter(ach))
                continue;

            if (!playerProg.TryGetValue(ach.ID, out var achievementProg))
            {
                achievementProg = new Dictionary<string, int>();
                playerProg[ach.ID] = achievementProg;
            }

            var anyUpdated = false;
            foreach (var condition in ach.Conditions)
            {
                if (condition.TryUpdateProgress(player, EntityManager, _protoManager, context, achievementProg))
                    anyUpdated = true;
            }

            if (anyUpdated)
                TryGrantAchievement(player, ach.ID);
        }
    }
}
