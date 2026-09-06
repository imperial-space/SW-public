using System.Linq;
using Content.Server.Imperial.ImperialStore;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Hands;
using Content.Shared.Imperial.ImperialStore;
using Robust.Server.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Magic.BindStoreOnEquip;

public sealed partial class BindStoreOnEquipSystem : EntitySystem
{
    [Dependency] private readonly ContainerSystem _containerSystem = default!;
    [Dependency] private readonly ImperialStoreSystem _storeSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn);
        SubscribeLocalEvent<BindStoreOnEquipComponent, GotEquippedHandEvent>(OnGotEquipped);
        SubscribeLocalEvent<BindStoreOnEquipComponent, EntityTerminatingEvent>(OnGrimoireTerminating);
    }

    private void OnPlayerSpawn(PlayerSpawnCompleteEvent args)
    {
        var query = EntityQueryEnumerator<BindStoreOnEquipComponent>();

        while (query.MoveNext(out var uid, out var component))
        {
            if (component.OwnerUid != null)
                continue;

            TryBindMind(uid, component);
        }
    }

    private void OnGotEquipped(EntityUid uid, BindStoreOnEquipComponent component, GotEquippedHandEvent args)
    {
        if (component.OwnerUid != null)
            return;

        TryBindGrimoire(uid, args.User, component);
    }

    private void OnGrimoireTerminating(
        EntityUid uid,
        BindStoreOnEquipComponent component,
        ref EntityTerminatingEvent args)
    {
        if (component.OwnerUid is not { } ownerUid ||
            !TryComp<GrimoireOwnerComponent>(ownerUid, out var owner) ||
            owner.GrimoireUid != uid ||
            !TryComp<ImperialStoreComponent>(uid, out var store))
        {
            return;
        }

        SaveStoreState(owner, store);
    }

    public bool TryBindMind(EntityUid uid, BindStoreOnEquipComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        var transform = Transform(uid);
        if (!_containerSystem.TryGetOuterContainer(uid, transform, out var container) ||
            !HasComp<ActorComponent>(container.Owner))
        {
            return false;
        }

        return TryBindGrimoire(uid, container.Owner, component);
    }

    public bool TryBindGrimoire(
        EntityUid grimoireUid,
        EntityUid ownerUid,
        BindStoreOnEquipComponent? grimoire = null,
        ImperialStoreComponent? store = null)
    {
        if (!Resolve(grimoireUid, ref grimoire, ref store) ||
            grimoire.OwnerUid != null ||
            HasComp<GrimoireOwnerComponent>(ownerUid) ||
            MetaData(grimoireUid).EntityPrototype?.ID is not { } prototype)
        {
            return false;
        }

        grimoire.OwnerUid = ownerUid;

        var owner = EnsureComp<GrimoireOwnerComponent>(ownerUid);
        owner.GrimoireUid = grimoireUid;
        owner.GrimoirePrototype = prototype;
        SaveStoreState(owner, store);
        _storeSystem.BindMind(grimoireUid, ownerUid, store);
        return true;
    }

    public bool TryRestoreGrimoire(
        EntityUid ownerUid,
        EntityUid grimoireUid,
        GrimoireOwnerComponent owner,
        BindStoreOnEquipComponent? grimoire = null,
        ImperialStoreComponent? store = null)
    {
        if (!TryComp<GrimoireOwnerComponent>(ownerUid, out var currentOwner) ||
            currentOwner != owner ||
            !Resolve(grimoireUid, ref grimoire, ref store) ||
            grimoire.OwnerUid != null ||
            MetaData(grimoireUid).EntityPrototype?.ID is not { } prototype ||
            prototype != owner.GrimoirePrototype)
        {
            return false;
        }

        grimoire.OwnerUid = ownerUid;
        owner.GrimoireUid = grimoireUid;
        RestoreStoreState(grimoireUid, owner, store);
        _storeSystem.BindMind(grimoireUid, ownerUid, store);
        return true;
    }

    public bool TryAddCurrency(EntityUid ownerUid, Dictionary<EntProtoId, FixedPoint2> currency)
    {
        if (!TryComp<GrimoireOwnerComponent>(ownerUid, out var owner))
            return false;

        if (TryGetCurrentStore(ownerUid, owner, out var grimoireUid, out var store))
        {
            if (!_storeSystem.TryAddCurrency(currency, grimoireUid, store))
                return false;

            SaveStoreState(owner, store);
            return true;
        }

        var converted = _storeSystem.ConvertCurrency(currency);
        if (converted.Keys.Any(type => !owner.CurrencyWhitelist.Contains(type)))
            return false;

        owner.Balance = _storeSystem.CurrencySum(owner.Balance, converted);
        return true;
    }

    public bool TryAddBonus(EntityUid ownerUid, Dictionary<EntProtoId, FixedPoint2> currency)
    {
        if (!TryComp<GrimoireOwnerComponent>(ownerUid, out var owner))
            return false;

        if (TryGetCurrentStore(ownerUid, owner, out var grimoireUid, out var store))
        {
            if (!_storeSystem.TryAddBonus(currency, grimoireUid, store))
                return false;

            SaveStoreState(owner, store);
            return true;
        }

        if (owner.Bonuses.Length == 0)
            return true;

        owner.LastBonusIndex = --owner.LastBonusIndex < 0
            ? owner.Bonuses.Length - 1
            : owner.LastBonusIndex;
        owner.Bonuses[owner.LastBonusIndex] = _storeSystem.ConvertCurrency(currency);
        owner.BonusSum.Clear();

        foreach (var bonus in owner.Bonuses)
        {
            if (bonus != null)
                owner.BonusSum = _storeSystem.CurrencySum(owner.BonusSum, bonus);
        }

        return true;
    }

    private bool TryGetCurrentStore(
        EntityUid ownerUid,
        GrimoireOwnerComponent owner,
        out EntityUid grimoireUid,
        out ImperialStoreComponent store)
    {
        grimoireUid = owner.GrimoireUid;

        if (TerminatingOrDeleted(grimoireUid) ||
            !TryComp(grimoireUid, out BindStoreOnEquipComponent? grimoire) ||
            grimoire.OwnerUid != ownerUid ||
            !TryComp<ImperialStoreComponent>(grimoireUid, out var currentStore))
        {
            store = default!;
            return false;
        }

        store = currentStore;
        return true;
    }

    private static void SaveStoreState(GrimoireOwnerComponent owner, ImperialStoreComponent store)
    {
        owner.Balance = new Dictionary<string, FixedPoint2>(store.Balance);
        owner.BonusBalanceOverride = store.BonusBalanceOverride;
        owner.LastBonusIndex = store.LastBonusIndex;
        owner.Bonuses = CloneBonuses(store.Bonuses);
        owner.BonusSum = new Dictionary<string, FixedPoint2>(store.BonusSum);
        owner.Categories = new HashSet<ProtoId<ImperialStoreCategoryPrototype>>(store.Categories);
        owner.CurrencyWhitelist = new HashSet<ProtoId<ImperialCurrencyPrototype>>(store.CurrencyWhitelist);
        owner.Listings = CloneListings(store.Listings);
        owner.BoughtEntities = new List<EntityUid>(store.BoughtEntities);
        owner.BalanceSpent = new Dictionary<ProtoId<ImperialCurrencyPrototype>, FixedPoint2>(store.BalanceSpent);
        owner.RefundAllowed = store.RefundAllowed;
        owner.OwnerOnly = store.OwnerOnly;
        owner.StartingMap = store.StartingMap;
    }

    private void RestoreStoreState(
        EntityUid grimoireUid,
        GrimoireOwnerComponent owner,
        ImperialStoreComponent store)
    {
        store.BonusBalanceOverride = owner.BonusBalanceOverride;
        store.LastBonusIndex = owner.LastBonusIndex;
        store.Bonuses = CloneBonuses(owner.Bonuses);
        store.BonusSum = new Dictionary<string, FixedPoint2>(owner.BonusSum);
        store.Categories = new HashSet<ProtoId<ImperialStoreCategoryPrototype>>(owner.Categories);
        store.CurrencyWhitelist = new HashSet<ProtoId<ImperialCurrencyPrototype>>(owner.CurrencyWhitelist);
        store.Listings = CloneListings(owner.Listings);
        store.LastAvailableListings.Clear();
        store.BoughtEntities = new List<EntityUid>(owner.BoughtEntities);
        store.BalanceSpent = new Dictionary<ProtoId<ImperialCurrencyPrototype>, FixedPoint2>(owner.BalanceSpent);
        store.RefundAllowed = owner.RefundAllowed;
        store.OwnerOnly = owner.OwnerOnly;
        store.StartingMap = owner.StartingMap;

        var balanceAdjustment = owner.Balance.Keys
            .Union(store.Balance.Keys)
            .ToDictionary(
                currency => currency,
                currency => owner.Balance.GetValueOrDefault(currency) - store.Balance.GetValueOrDefault(currency));
        _storeSystem.TryAddCurrency(balanceAdjustment, grimoireUid, store);
    }

    private static Dictionary<string, FixedPoint2>[] CloneBonuses(Dictionary<string, FixedPoint2>[] bonuses)
    {
        var clone = new Dictionary<string, FixedPoint2>[bonuses.Length];

        for (var i = 0; i < bonuses.Length; i++)
        {
            if (bonuses[i] is { } bonus)
                clone[i] = new Dictionary<string, FixedPoint2>(bonus);
        }

        return clone;
    }

    private static HashSet<ImperialListingData> CloneListings(IEnumerable<ImperialListingData> listings)
    {
        var clone = new HashSet<ImperialListingData>();

        foreach (var listing in listings)
        {
            var listingClone = (ImperialListingData) listing.Clone();
            listingClone.RaiseProductEventOnUser = listing.RaiseProductEventOnUser;
            clone.Add(listingClone);
        }

        return clone;
    }
}
