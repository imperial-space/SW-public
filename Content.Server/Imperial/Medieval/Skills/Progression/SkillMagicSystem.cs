using System.Linq;
using Content.Server.Imperial.ImperialStore;
using Content.Server.Imperial.Medieval.Magic.BindStoreOnEquip;
using Content.Server.Imperial.Medieval.Magic.MedievalSpawnInFreeSlot;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.ImperialStore;
using Content.Shared.Imperial.Medieval.Magic.Mana;
using Content.Shared.Imperial.Medieval.MagicRunes.Components;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.Skills.Progression;

/// <summary>Owns skill rewards and pricing; spell delivery, prerequisites and UI remain in the store.</summary>
public sealed class SkillMagicSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ImperialStoreSystem _store = default!;
    [Dependency] private readonly BindStoreOnEquipSystem _grimoire = default!;
    [Dependency] private readonly MedievalSpawnInFreeSlotSystem _placement = default!;
    private static readonly string[] Essence = { "MagicMedievalFire", "MagicMedievalLight", "MagicMedievalVodka", "MagicMedievalEarth", "MagicMedievalDarkness" };
    // Deliberately explicit: adding a senior spell or an upgrade can never silently make it free.
    private static readonly HashSet<string> FreeBasics = new() { "MedievalSpellSparkBeginner", "MedievalSpellEarthBedBeginner", "MedievalSpellFlashBeginner", "MedievalSpellDivineTouchBeginner", "MedievalSpellWaterSphereBeginner", "MedievalSpellCursedArrowBeginner" };

    public override void Initialize()
    {
        SubscribeLocalEvent<SkillProfileChangedEvent>(OnProfile);
        SubscribeLocalEvent<ImperialStoreComponent, ImperialStoreRefreshListingsEvent>(OnListings);
        SubscribeLocalEvent<ImperialStoreComponent, ImperialStorePurchaseAttemptEvent>(OnPurchase);
        SubscribeLocalEvent<ImperialStoreComponent, ImperialStorePurchasedEvent>(OnPurchased);
    }

    private float Setting(string key) => _prototypes.Index<SkillPrototype>(SharedSkillsSystem.IntelligenceId).Modifiers[key];
    public bool Qualified(EntityUid uid) => TryComp<SkillsComponent>(uid, out var skills) && SkillScaling.Level(skills, SharedSkillsSystem.IntelligenceId) >= SkillScaling.Legendary;

    public void RegisterProfession(EntityUid uid, string? jobId)
    {
        var state = EnsureComp<SkillMagicComponent>(uid);
        if (state.ProfessionKnown)
            return;
        state.ProfessionKnown = true;
        if (jobId != null && _prototypes.TryIndex<JobPrototype>(jobId, out var job)
            && job.StartingGear is { } gearId && _prototypes.TryIndex(gearId, out var gear))
        {
            var items = gear.Inhand.Concat(gear.Equipment.Values).Concat(gear.Storage.Values.SelectMany(x => x));
            state.ProfessionMage = items.Any(id => _prototypes.Index(id).Components.ContainsKey("BindStoreOnEquip"));
        }
        // This is read only at profession spawn, never when somebody picks up a book later.
        state.ProfessionMage |= TryComp<GrimoireOwnerComponent>(uid, out var owner) && !_grimoire.IsLearningGrimoire(owner);
        if (state.ProfessionMage)
            EnsureComp<MagicRuneKnowledgeComponent>(uid);
    }

    private void OnProfile(ref SkillProfileChangedEvent args) => Refresh(args.Uid);

    public void Refresh(EntityUid uid)
    {
        var state = EnsureComp<SkillMagicComponent>(uid);
        TryComp<GrimoireOwnerComponent>(uid, out var owner);
        var learning = owner != null && _grimoire.IsLearningGrimoire(owner);
        // Binding an ordinary grimoire gives full magic access, independently of intelligence.
        if (Qualified(uid) || state.ProfessionMage || owner != null && !learning)
        {
            EnsureComp<ManaComponent>(uid);
            EnsureComp<MagicRuneKnowledgeComponent>(uid);
        }
        if (!Qualified(uid))
        {
            if (learning && owner != null && !TerminatingOrDeleted(owner.GrimoireUid))
                _store.CloseUi(owner.GrimoireUid);
            return;
        }
        if (state.ProfessionMage && !state.MageRewardGranted && owner != null && !learning)
        {
            state.MageRewardGranted = _grimoire.TryAddCurrency(uid,
                new Dictionary<EntProtoId, FixedPoint2> { [_random.Pick(Essence)] = FixedPoint2.New(Setting("MageEssence")) });
        }
    }

    /// <summary>Called only when applying a starting profile, never by skill refresh or manual pickup.</summary>
    public void GrantStartingGrimoire(EntityUid uid)
    {
        if (!Qualified(uid) || !TryComp<SkillMagicComponent>(uid, out var state) || !state.ProfessionKnown
            || state.ProfessionMage || HasComp<GrimoireOwnerComponent>(uid))
            return;

        var book = Spawn("SkillLearningGrimoire", Transform(uid).Coordinates);
        if (!_grimoire.TryBindGrimoire(book, uid, startingGrimoire: true))
        {
            QueueDel(book);
            return;
        }
        _placement.TryPlaceInFreeSlot(uid, book);
    }

    private void OnListings(EntityUid uid, ImperialStoreComponent store, ref ImperialStoreRefreshListingsEvent args)
    {
        var owner = store.AccountOwner ?? args.Buyer;
        if (!HasComp<BindStoreOnEquipComponent>(uid) || !TryComp<SkillMagicComponent>(owner, out var state))
            return;
        var personal = HasComp<SkillLearningStoreComponent>(uid);
        foreach (var listing in store.Listings)
        {
            if (!_prototypes.TryIndex<ImperialListingPrototype>(listing.ID, out var prototype))
                continue;
            if (listing.ID.StartsWith("SkillArchmagic"))
            {
                listing.Cost = prototype.Cost.ToDictionary(x => x.Key, _ => FixedPoint2.New(Setting("ArchmagicCost")));
                continue;
            }
            if (!listing.Categories.Any(x => x.ToString().StartsWith("Medieval") && x.ToString().EndsWith("Spells")))
                continue;
            // Also reset prices of listings transferred from a learning grimoire.
            listing.Cost = new(prototype.Cost);
            if (!personal)
                continue;
            if (!state.FreeSpellClaimed && FreeBasics.Contains(listing.ID))
                listing.Cost.Clear();
            else
            {
                // Add 100 once per spell, to its primary essence currency. Archmagic prices stay intact.
                var essence = Essence.FirstOrDefault(x => listing.Cost.ContainsKey(x));
                if (essence != null)
                    listing.Cost[essence] += FixedPoint2.New(Setting("SpellSurcharge"));
            }
        }
    }

    private void OnPurchase(EntityUid uid, ImperialStoreComponent store, ref ImperialStorePurchaseAttemptEvent args)
    {
        if (store.OwnerOnly && store.AccountOwner != args.Buyer)
            args.Cancelled = true;
        if (HasComp<SkillLearningStoreComponent>(uid) && (!Qualified(args.Buyer)
            || !TryComp<SkillMagicComponent>(args.Buyer, out var state) || state.ProfessionMage))
            args.Cancelled = true;
        if (args.Listing.ID.StartsWith("SkillArchmagic") && !Qualified(args.Buyer))
            args.Cancelled = true;
    }

    private void OnPurchased(EntityUid uid, ImperialStoreComponent store, ref ImperialStorePurchasedEvent args)
    {
        if (args.Listing.ID.StartsWith("SkillArchmagic"))
            _store.TryAddCurrency(new Dictionary<string, FixedPoint2> { ["ArchmagePoints"] = 1 }, uid, store);
        if (HasComp<SkillLearningStoreComponent>(uid) && FreeBasics.Contains(args.Listing.ID) && args.Listing.Cost.Count == 0
            && TryComp<SkillMagicComponent>(args.Buyer, out var state))
            state.FreeSpellClaimed = true;
    }
}

public sealed partial class SkillArchmagicCondition : ImperialListingCondition
{
    public override bool Condition(ImperialListingConditionArgs args) => args.EntityManager.System<SkillMagicSystem>().Qualified(args.Buyer);
}
