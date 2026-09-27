using System.Linq;
using Content.Server.Imperial.ImperialStore;
using Content.Server.Imperial.Medieval.Skills.Progression;
using Content.Shared.Imperial.ImperialStore;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Magic.BindStoreOnEquip;

public sealed partial class BindStoreOnEquipSystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    // The prototype remains available when the current book was lost or destroyed.
    public bool IsLearningGrimoire(GrimoireOwnerComponent owner) =>
        _prototypes.Index(owner.GrimoirePrototype).Components.ContainsKey("SkillLearningStore");

    private bool TryUpgradeGrimoire(EntityUid ownerUid, GrimoireOwnerComponent owner, EntityUid newUid,
        EntProtoId prototype, BindStoreOnEquipComponent grimoire, ImperialStoreComponent store)
    {
        if (!IsLearningGrimoire(owner) || HasComp<SkillLearningStoreComponent>(newUid))
            return false;

        var oldUid = owner.GrimoireUid;
        ImperialStoreComponent? oldStore = null;
        if (TryGetCurrentStore(ownerUid, owner, out _, out var currentStore))
        {
            oldStore = currentStore;
            SaveStoreState(owner, oldStore);
        }

        // Keep the ordinary book's own funds and transfer the learner's remaining balance once.
        owner.Balance = _storeSystem.CurrencySum(owner.Balance, store.Balance);
        grimoire.OwnerUid = ownerUid;
        owner.GrimoireUid = newUid;
        owner.GrimoirePrototype = prototype;
        RestoreStoreState(newUid, owner, store);
        _storeSystem.BindMind(newUid, ownerUid, store);

        if (oldStore != null)
        {
            _storeSystem.CloseUi(oldUid, oldStore);
            oldStore.AccountOwner = null;
            _storeSystem.TryAddCurrency(oldStore.Balance.ToDictionary(x => x.Key, x => -x.Value), oldUid, oldStore);
            // Its termination must not clear refund links now owned by the new book.
            oldStore.BoughtEntities.Clear();
        }
        if (!TerminatingOrDeleted(oldUid))
        {
            Spawn("Ash", _transform.GetMapCoordinates(oldUid));
            QueueDel(oldUid);
        }

        EntityManager.System<SkillMagicSystem>().Refresh(ownerUid);
        // Drop the learning surcharge immediately, preserving stock and upgrade action links.
        var refresh = new ImperialStoreRefreshListingsEvent(ownerUid, store);
        RaiseLocalEvent(newUid, ref refresh);
        SaveStoreState(owner, store);
        _popup.PopupEntity(Loc.GetString("skills-grimoire-upgraded"), newUid, ownerUid);
        return true;
    }
}
