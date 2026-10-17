using System.Linq;
using Content.Server.Chat.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.ImperialStore;
using Content.Shared.Imperial.Medieval.Language;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.ImperialStore;

/// <summary>
/// imperial medieval - essence slots of a grimoire: the player puts a school into a slot and every balance update
/// adds one unit of that school per filled slot. See <see cref="ImperialStoreSlots"/>.
/// </summary>
public sealed partial class ImperialStoreSystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    private void InitializeSlots()
    {
        SubscribeLocalEvent<ImperialStoreComponent, ImperialStoreSetSlotMessage>(OnSetSlot);
        SubscribeLocalEvent<ImperialStoreComponent, ImperialStoreSetSlotDoAfterEvent>(OnSetSlotDoAfter);
    }

    private void OnSetSlot(EntityUid uid, ImperialStoreComponent component, ImperialStoreSetSlotMessage msg)
    {
        var user = msg.Actor;

        if (msg.Slot < 0 || msg.Slot >= component.BonusCount)
            return;

        if (component.OwnerOnly && component.AccountOwner != user)
            return;

        // emptying a slot is instant
        if (msg.School == null)
        {
            if (TrySetBonusSlot(uid, msg.Slot, null, component))
                SlotsChanged(uid, user, component);

            return;
        }

        if (!ImperialStoreSlots.IsSchool(_proto, msg.School) ||
            GetSlotSchool(component, msg.Slot) == msg.School ||
            !IsSchoolSelectable(user, uid, component, msg.School))
        {
            return;
        }

        var effect = SpawnSlotEffect(user, msg.School);
        var ev = new ImperialStoreSetSlotDoAfterEvent
        {
            Slot = msg.Slot,
            School = msg.School,
            Effect = effect == null ? NetEntity.Invalid : GetNetEntity(effect.Value),
        };

        var args = new DoAfterArgs(EntityManager, user, ImperialStoreSlots.ChangeSeconds, ev, eventTarget: uid, used: uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            // a second request of the same user while this one is running is dropped, it must not cancel the first
            DuplicateCondition = DuplicateConditions.SameEvent,
            CancelDuplicate = false,
        };

        if (!_doAfter.TryStartDoAfter(args))
        {
            if (effect != null)
                QueueDel(effect.Value);

            return;
        }

        SaySlotWord(user, msg.School);

    }

    private void OnSetSlotDoAfter(EntityUid uid, ImperialStoreComponent component, ImperialStoreSetSlotDoAfterEvent args)
    {
        if (GetEntity(args.Effect) is { Valid: true } effect && !TerminatingOrDeleted(effect))
            QueueDel(effect);

        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        // everything is checked again, the balance and the spells could have changed while it was running
        if (!ImperialStoreSlots.IsSchool(_proto, args.School) ||
            !IsSchoolSelectable(args.User, uid, component, args.School) ||
            !TrySetBonusSlot(uid, args.Slot, args.School, component))
        {
            return;
        }

        SlotsChanged(uid, args.User, component);
    }

    private void SlotsChanged(EntityUid uid, EntityUid user, ImperialStoreComponent component)
    {
        var ev = new ImperialStoreSlotsChangedEvent();
        RaiseLocalEvent(uid, ref ev);

        UpdateUserInterface(user, uid, component);
    }

    private EntityUid? SpawnSlotEffect(EntityUid user, string school)
    {
        if (!ImperialStoreSlots.TryGetSchool(_proto, school, out var data))
            return null;

        var effect = Spawn(data.Effect, Transform(user).Coordinates);
        _xform.SetParent(effect, user);
        return effect;
    }

    private void SaySlotWord(EntityUid user, string school)
    {
        if (!ImperialStoreSlots.TryGetSchool(_proto, school, out var data))
            return;

        // same as the words of the spells: everyone understands it and it is not written to the chat
        _chat.TrySendInGameICMessage(
            user,
            Loc.GetString(data.Word),
            InGameICChatType.Speak,
            hideChat: true,
            color: data.Color,
            language: _proto.Index(SharedLanguageSystem.Universal));
    }

    /// <summary>
    /// Puts one unit of the school into the slot, a null school empties it.
    /// </summary>
    public bool TrySetBonusSlot(EntityUid uid, int slot, string? school, ImperialStoreComponent? store = null)
    {
        if (!Resolve(uid, ref store) || slot < 0 || slot >= store.BonusCount)
            return false;

        store.Bonuses[slot] = school == null
            ? default!
            : new Dictionary<string, FixedPoint2> { [school] = FixedPoint2.New(1) };

        RecalculateBonusSum(store);
        return true;
    }

    /// <summary>
    /// The school in the slot, null if it is empty.
    /// </summary>
    public static string? GetSlotSchool(ImperialStoreComponent store, int slot)
    {
        if (slot < 0 || slot >= store.Bonuses.Length || store.Bonuses[slot] is not { } bonus)
            return null;

        return bonus.Keys.FirstOrDefault();
    }

    /// <summary>
    /// A school can be put into a slot if the store has enough of it for a spell that can still be bought,
    /// or a spell of that school is already learned (so a slot can never be lost for good).
    /// </summary>
    /// <remarks>
    /// A spell that was bought is not among the available listings any more, so the learned ones are looked up
    /// in all listings of the store.
    /// </remarks>
    public bool IsSchoolSelectable(EntityUid? user, ImperialStoreComponent store, string school, IEnumerable<ImperialListingData> listings)
    {
        if (store.Balance.TryGetValue(school, out var balance))
        {
            foreach (var listing in listings)
            {
                if (listing.Cost.TryGetValue(school, out var price) && balance >= price)
                    return true;
            }
        }

        foreach (var listing in store.Listings)
        {
            if (!listing.Cost.ContainsKey(school))
                continue;

            if (listing.PurchaseAmount > 0)
                return true;

            // spells learned somewhere else, for example from a scroll
            if (user != null && listing.ProductAction is { } action && HasAction(user.Value, action))
                return true;
        }

        return false;
    }

    private bool IsSchoolSelectable(EntityUid user, EntityUid uid, ImperialStoreComponent store, string school)
    {
        var listings = GetAvailableListings(store.AccountOwner ?? user, uid, store).ToList();
        return IsSchoolSelectable(user, store, school, listings);
    }

    public List<string> GetSelectableSchools(EntityUid? user, ImperialStoreComponent store, IEnumerable<ImperialListingData> listings)
    {
        var selectable = new List<string>();

        if (store.BonusCount == 0)
            return selectable;

        var available = listings.ToList();

        foreach (var (school, _) in ImperialStoreSlots.GetSchools(_proto))
        {
            if (IsSchoolSelectable(user, store, school, available))
                selectable.Add(school);
        }

        return selectable;
    }

    private bool HasAction(EntityUid user, string prototype)
    {
        foreach (var action in _actions.GetActions(user))
        {
            if (MetaData(action.Owner).EntityPrototype?.ID == prototype)
                return true;
        }

        return false;
    }
}

/// <summary>
/// Raised on a store after one of its essence slots was changed.
/// </summary>
[ByRefEvent]
public readonly record struct ImperialStoreSlotsChangedEvent;
