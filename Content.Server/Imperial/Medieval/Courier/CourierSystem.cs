using System.Linq;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Imperial.Medieval.Recipient;
using Content.Server.Mind;
using Content.Server.Station.Systems;
using Content.Server.Storage.Components;
using Content.Server.Stack;
using Content.Shared.Destructible;
using Content.Shared.EntityTable;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Ghost;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Medieval.Courier;
using Content.Shared.Imperial.Medieval.Recipient;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Roles.Components;
using Content.Shared.Storage;
using Content.Shared.UserInterface;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Courier;

public sealed class CourierSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly RecipientDataSystem _recipientData = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly StationJobsSystem _stationJobs = default!;
    private TimeSpan _nextMinuteCheck;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CourierPitComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CourierPitComponent, ActivatableUIOpenAttemptEvent>(OnOpenAttempt);
        SubscribeLocalEvent<CourierPitComponent, BeforeActivatableUIOpenEvent>(OnBeforeUiOpen);
        SubscribeLocalEvent<CourierPitComponent, CourierRequestUpdateInterfaceMessage>(OnRequestUpdate);
        SubscribeLocalEvent<CourierPitComponent, CourierBuyMessage>(OnBuyRequest);
        SubscribeLocalEvent<CourierPitComponent, CourierRequestWithdrawMessage>(OnRequestWithdraw);
        SubscribeLocalEvent<LetterComponent, GotEquippedHandEvent>(OnLetterEquipped);
        SubscribeLocalEvent<LetterComponent, GotEquippedEvent>(OnLetterInventoryEquipped);
        SubscribeLocalEvent<LetterComponent, ExaminedEvent>(OnLetterExamined);
        SubscribeLocalEvent<LetterComponent, UseInHandEvent>(OnLetterUseInHand);
        SubscribeLocalEvent<LetterComponent, DestructionAttemptEvent>(OnLetterDestructionAttempt);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextMinuteCheck)
            return;

        _nextMinuteCheck = _timing.CurTime + TimeSpan.FromMinutes(1);
        ProcessExpiredUrgentLetters();

        var pitQuery = EntityQueryEnumerator<CourierPitComponent>();
        while (pitQuery.MoveNext(out var uid, out var pit))
        {
            EnsureNextRewardTime(uid, pit);
            ProcessUnavailableRecipients(pit);

            if (_timing.CurTime < pit.NextRewardTime)
                continue;

            AddFreeMailToAllCouriers();
            pit.NextRewardTime = GetNextRewardTime(pit);
        }
    }

    private void OnMapInit(EntityUid uid, CourierPitComponent component, MapInitEvent args)
    {
        EnsureNextRewardTime(uid, component);
    }

    private void OnOpenAttempt(EntityUid uid, CourierPitComponent component, ActivatableUIOpenAttemptEvent args)
    {
        if (!TryComp<CourierComponent>(args.User, out var courier) ||
            !_mobState.IsAlive(args.User) ||
            !TryComp<HandsComponent>(args.User, out var hands) ||
            !_hands.ActiveHandIsEmpty((args.User, hands)))
        {
            args.Cancel();
            return;
        }

        UpdateUi(uid, component, args.User, courier);
    }

    private void OnBeforeUiOpen(EntityUid uid, CourierPitComponent component, BeforeActivatableUIOpenEvent args)
    {
        if (!TryComp<CourierComponent>(args.User, out var courier))
            return;

        UpdateUi(uid, component, args.User, courier);
    }

    private void OnRequestUpdate(EntityUid uid, CourierPitComponent component, CourierRequestUpdateInterfaceMessage args)
    {
        if (!TryComp<CourierComponent>(args.Actor, out var courier))
            return;

        UpdateUi(uid, component, args.Actor, courier);
    }

    private void OnBuyRequest(EntityUid uid, CourierPitComponent component, CourierBuyMessage msg)
    {
        var user = msg.Actor;
        if (!TryComp<CourierComponent>(user, out var courier) || !_mobState.IsAlive(user))
            return;

        if (msg.OfferIndex < 0 || msg.OfferIndex >= component.Offers.Count)
            return;

        var offer = component.Offers[msg.OfferIndex];

        if ((offer.BalanceCost > 0 && courier.Balance < offer.BalanceCost) ||
            courier.DeliveryPoints < offer.DeliveryPointsCost ||
            courier.FreeMailsCount < offer.FreeMailsCost)
        {
            return;
        }

        if (!_prototype.HasIndex(offer.ProductEntity))
            return;

        if (AssignRecipient(component) is not { } recipient)
        {
            _popup.PopupEntity(
                Loc.GetString("courier-no-valid-recipients-popup"),
                user,
                user,
                PopupType.MediumCaution);
            return;
        }

        courier.Balance -= offer.BalanceCost;
        courier.DeliveryPoints -= offer.DeliveryPointsCost;
        courier.FreeMailsCount -= offer.FreeMailsCost;

        var ent = Spawn(offer.ProductEntity, Transform(user).Coordinates);
        if (TryComp<LetterComponent>(ent, out var letter))
        {
            InitializePurchasedLetter(
                ent,
                letter,
                offer,
                user,
                recipient.Entity,
                recipient.Data,
                component);
        }

        _hands.PickupOrDrop(user, ent);

        UpdateUi(uid, component, user, courier);
    }

    private void OnRequestWithdraw(EntityUid uid, CourierPitComponent component, CourierRequestWithdrawMessage msg)
    {
        if (msg.Amount <= 0)
            return;

        var user = msg.Actor;
        if (!TryComp<CourierComponent>(user, out var courier) || !_mobState.IsAlive(user))
            return;

        if (courier.Balance < msg.Amount)
            return;

        if (!_prototype.TryIndex(component.Currency, out var proto))
            return;

        if (proto.Cash == null || !proto.CanWithdraw)
            return;

        FixedPoint2 amountRemaining = msg.Amount;
        var coordinates = Transform(user).Coordinates;

        var sortedCashValues = proto.Cash.Keys.OrderByDescending(x => x).ToList();
        foreach (var value in sortedCashValues)
        {
            var cashId = proto.Cash[value];
            var amountToSpawn = (int) MathF.Floor((float) (amountRemaining / value));
            if (amountToSpawn <= 0)
                continue;

            var ents = _stack.SpawnMultiple(cashId, amountToSpawn, coordinates);
            if (ents.FirstOrDefault() is {} ent)
                _hands.PickupOrDrop(user, ent);

            amountRemaining -= value * amountToSpawn;
        }

        courier.Balance -= msg.Amount;
        UpdateUi(uid, component, user, courier);
    }

    private void AddFreeMailToAllCouriers()
    {
        var couriers = EntityQueryEnumerator<CourierComponent>();
        while (couriers.MoveNext(out _, out var courier))
        {
            courier.FreeMailsCount++;
        }
    }

    private void OnLetterEquipped(EntityUid uid, LetterComponent component, GotEquippedHandEvent args)
    {
        if (!HasComp<CourierComponent>(args.User))
            return;

        SetLastCourierHeld(uid, args.User);
    }

    private void OnLetterInventoryEquipped(EntityUid uid, LetterComponent component, GotEquippedEvent args)
    {
        if (!HasComp<CourierComponent>(args.Equipment))
            return;

        SetLastCourierHeld(uid, args.Equipment);
    }

    private void OnLetterUseInHand(EntityUid uid, LetterComponent component, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (HasComp<CourierComponent>(args.User))
        {
            OpenLetterRecipientUi(uid, component, args.User);
            args.Handled = true;
            return;
        }

        if (component.Recipient is not { } recipient || recipient != args.User)
            return;

        GrantReward(component);
        OpenLetter(uid, component);

        RemComp(uid, component);
        args.Handled = true;
    }

    private void OnLetterDestructionAttempt(EntityUid uid, LetterComponent component, ref DestructionAttemptEvent args)
    {
        if (!component.IsBox)
            return;

        args.Cancel();
        _audio.PlayPvs(component.OpenSound, uid);
        TurnLetterIntoTrash(component, "courier-letter-trash-description-box-destroyed");
    }

    private void OnLetterExamined(EntityUid uid, LetterComponent component, ExaminedEvent args)
    {
        var recipientName = component.RecipientData?.Profile.Name ??
            Loc.GetString("courier-letter-examine-recipient-unknown");

        args.PushMarkup(Loc.GetString("courier-letter-examine-recipient", ("recipient", recipientName)));
        args.PushMarkup(Loc.GetString("courier-letter-examine-reward", ("reward", component.BalanceReward)));

        if (!component.IsUrgent)
            return;

        var remainingTime = component.UrgentTimer - _timing.CurTime;
        var remainingMinutes = Math.Max(0, (int) Math.Ceiling(remainingTime.TotalMinutes));

        args.PushMarkup(Loc.GetString("courier-letter-examine-time-left", ("minutes", remainingMinutes)));
    }

    private void OpenLetterRecipientUi(EntityUid uid, LetterComponent component, EntityUid user)
    {
        var state = new LetterRecipientBoundUserInterfaceState(GetLetterRecipientData(component));
        _ui.SetUiState(uid, LetterRecipientUiKey.Key, state);
        _ui.OpenUi(uid, LetterRecipientUiKey.Key, user);
    }

    private RecipientData? GetLetterRecipientData(LetterComponent letter)
    {
        return letter.Recipient is null ? null : letter.RecipientData;
    }

    private void SetLastCourierHeld(EntityUid uid, EntityUid courierUid)
    {
        if (!TryComp<LetterComponent>(uid, out var component))
            return;

        component.LastCourierHeld = courierUid;
    }

    public void ReturnBuyBack(LetterComponent component)
    {
        if (component.LastCourierHeld is { } courierUid &&
            TryComp<CourierComponent>(courierUid, out var courier))
        {
            courier.Balance += component.BalanceBuyBack;
            courier.DeliveryPoints += component.DeliveryPointsBuyBack;
            courier.FreeMailsCount += component.FreeMailBuyBack;
        }

        TurnLetterIntoTrash(component, "courier-letter-trash-description-recipient-missing");
    }

    private void TurnLetterIntoTrash(LetterComponent component, string descriptionLocId)
    {
        var letterUid = component.Owner;
        SetLetterSpriteState(letterUid, component.TrashSpriteState);
        EntityManager.System<MetaDataSystem>().SetEntityDescription(letterUid, Loc.GetString(descriptionLocId));
        RemComp(letterUid, component);
    }

    private void SetLetterSpriteState(EntityUid letterUid, string state)
    {
        if (string.IsNullOrWhiteSpace(state))
            return;

        _appearance.SetData(letterUid, CourierLetterVisuals.State, state);
    }

    public void GrantReward(LetterComponent component)
    {
        if (component.LastCourierHeld is not { } courierUid)
            return;

        if (!TryComp<CourierComponent>(courierUid, out var courier))
            return;

        courier.Balance += component.BalanceReward;
        courier.DeliveryPoints += component.DeliveryPointsReward;
    }

    private void OpenLetter(EntityUid uid, LetterComponent component)
    {
        SetLetterSpriteState(uid, component.OpenedSpriteState);

        var spawnItems = EnsureComp<SpawnItemsOnUseComponent>(uid);
        spawnItems.Items.Clear();
        spawnItems.Uses = 1;
        spawnItems.Sound = component.OpenSound;

        if (component.LetterContents is not { } contents)
            return;

        spawnItems.Items.Add(new EntitySpawnEntry
        {
            PrototypeId = contents,
        });
    }

    private void InitializePurchasedLetter(
        EntityUid letterUid,
        LetterComponent letter,
        CourierTradeOffer offer,
        EntityUid buyerUid,
        EntityUid recipient,
        RecipientData recipientData,
        CourierPitComponent pit)
    {
        letter.FreeMailBuyBack = offer.FreeMailsCost;
        letter.DeliveryPointsBuyBack = offer.DeliveryPointsCost;
        letter.BalanceBuyBack = offer.BalanceCost;

        letter.DeliveryPointsReward = GetRandomInRangeInclusive(
            pit.MinDeliveryPointsReward,
            pit.MaxDeliveryPointsReward);

        var balanceReward = GetRandomInRangeInclusive(
            pit.MinBalanceReward,
            pit.MaxBalanceReward);

        if (letter.IsUrgent)
            balanceReward *= pit.UrgentBalanceRewardMultiplier;

        if (letter.IsBox)
            balanceReward = balanceReward * pit.BoxBalanceRewardMultiplier + letter.BalanceBuyBack;

        letter.BalanceReward = balanceReward;

        SetLastCourierHeld(letterUid, buyerUid);
        letter.Recipient = recipient;
        letter.RecipientData = recipientData;

        if (letter.IsUrgent)
        {
            var urgentMinutes = GetRandomInRangeInclusive(
                pit.MinUrgentMinutes,
                pit.MaxUrgentMinutes);
            letter.UrgentTimer = _timing.CurTime + TimeSpan.FromMinutes(urgentMinutes);
        }

        var tableId = letter.IsBox ? pit.BoxMailLootTable : pit.MailLootTable;
        letter.LetterContents = null;
        if (_prototype.HasIndex(tableId))
        {
            var table = _prototype.Index(tableId);
            var spawns = EntityManager.System<EntityTableSystem>().GetSpawns(table).ToList();
            if (spawns.Count > 0)
                letter.LetterContents = _random.Pick(spawns);
        }
    }

    private int GetRandomInRangeInclusive(int min, int max)
    {
        if (max < min)
            (min, max) = (max, min);

        return _random.Next(min, max + 1);
    }

    private void ProcessExpiredUrgentLetters()
    {
        var letters = EntityQueryEnumerator<LetterComponent>();
        while (letters.MoveNext(out var letterUid, out var letter))
        {
            if (!letter.IsUrgent || _timing.CurTime <= letter.UrgentTimer)
                continue;

            if (letter.LastCourierHeld is { } courierUid &&
                TryComp<CourierComponent>(courierUid, out var courier))
            {
                var penalty = (int) Math.Ceiling(letter.BalanceReward * 0.75f);
                courier.Balance -= penalty;
            }

            letter.IsUrgent = false;
            _popup.PopupEntity(Loc.GetString("courier-letter-expired-popup"), letterUid, Filter.Pvs(letterUid), true, PopupType.MediumCaution);
        }
    }

    private void ProcessUnavailableRecipients(CourierPitComponent pit)
    {
        var activeCandidates = GetActiveRecipientCandidates();

        var letters = EntityQueryEnumerator<LetterComponent>();
        while (letters.MoveNext(out _, out var letter))
        {
            if (letter.Recipient is { } recipient && activeCandidates.Contains(recipient))
                continue;

            ReturnBuyBack(letter);
        }

        foreach (var candidate in pit.Weight.Keys.ToList())
        {
            if (!activeCandidates.Contains(candidate))
                CandidateLeave(pit, candidate);
        }
    }

    private (EntityUid Entity, RecipientData Data)? AssignRecipient(CourierPitComponent pit)
    {
        var activeCandidates = GetActiveRecipientCandidates();

        foreach (var candidate in activeCandidates)
            CandidateJoin(pit, candidate);

        foreach (var candidate in pit.Weight.Keys.ToList())
        {
            if (!activeCandidates.Contains(candidate))
                CandidateLeave(pit, candidate);
        }

        if (pit.Weight.Count == 0)
            return null;

        var winner = WeightedRandomPick(pit);
        var dropAmount = pit.Weight.Count;

        foreach (var candidate in pit.Weight.Keys.ToList())
        {
            if (candidate == winner)
                pit.Weight[candidate] = Math.Max(1, pit.Weight[candidate] - dropAmount);
            else
                pit.Weight[candidate] = Math.Max(1, pit.Weight[candidate] + 1);
        }

        var data = _recipientData.GetRecipientData(winner);
        return data == null ? null : (winner, data);
    }

    private HashSet<EntityUid> GetActiveRecipientCandidates()
    {
        var activeCandidates = new HashSet<EntityUid>();

        foreach (var session in _player.Sessions)
        {
            if (session.AttachedEntity is not { Valid: true } attached ||
                !IsEligibleRecipientCandidate(attached, session))
            {
                continue;
            }

            activeCandidates.Add(attached);
        }

        return activeCandidates;
    }

    private void CandidateJoin(CourierPitComponent pit, EntityUid candidate)
    {
        if (pit.Weight.ContainsKey(candidate))
            return;

        var initialWeight = pit.Weight.Count == 0
            ? 1
            : Math.Max(1, (int) Math.Round(pit.Weight.Values.Average()));

        pit.Weight[candidate] = initialWeight;
    }

    private void CandidateLeave(CourierPitComponent pit, EntityUid candidate)
    {
        pit.Weight.Remove(candidate);
    }

    private EntityUid WeightedRandomPick(CourierPitComponent pit)
    {
        var totalWeight = 0;
        foreach (var weight in pit.Weight.Values)
        {
            totalWeight += Math.Max(1, weight);
        }

        if (totalWeight <= 0)
            return pit.Weight.First().Key;

        var roll = _random.Next(1, totalWeight + 1);
        var cumulative = 0;

        foreach (var (candidate, weight) in pit.Weight)
        {
            cumulative += Math.Max(1, weight);
            if (roll <= cumulative)
                return candidate;
        }

        return pit.Weight.First().Key;
    }

    private bool IsEligibleRecipientCandidate(EntityUid candidate, ICommonSession session)
    {
        if (HasComp<CourierComponent>(candidate) ||
            HasComp<GhostComponent>(candidate) ||
            HasComp<GhostRoleComponent>(candidate) ||
            !HasComp<HumanoidAppearanceComponent>(candidate) ||
            !_mind.TryGetMind(session, out _, out var mind) ||
            mind.OwnedEntity != candidate ||
            !HasRoundJob(session))
        {
            return false;
        }

        foreach (var role in mind.MindRoleContainer.ContainedEntities)
        {
            if (HasComp<GhostRoleMarkerRoleComponent>(role))
                return false;
        }

        return true;
    }

    private bool HasRoundJob(ICommonSession session)
    {
        foreach (var station in _station.GetStationsSet())
        {
            if (_stationJobs.TryGetPlayerJobs(station, session.UserId, out var jobs) &&
                jobs.Count > 0)
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateUi(EntityUid pitUid, CourierPitComponent pit, EntityUid user, CourierComponent courier)
    {
        var state = new CourierUpdateState(
            courier.Balance,
            courier.DeliveryPoints,
            courier.FreeMailsCount,
            pit.GuildName,
            pit.Offers.Select(offer => new CourierTradeOffer
            {
                ProductEntity = offer.ProductEntity,
                DescriptionLoc = offer.DescriptionLoc,
                BalanceCost = offer.BalanceCost,
                DeliveryPointsCost = offer.DeliveryPointsCost,
                FreeMailsCost = offer.FreeMailsCost,
            }).ToList(),
            pit.Currency);
        _ui.SetUiState(pitUid, courierUiKey.key, state);
    }

    private void EnsureNextRewardTime(EntityUid uid, CourierPitComponent pit)
    {
        if (pit.NextRewardTime > TimeSpan.Zero)
            return;

        pit.NextRewardTime = GetNextRewardTime(pit);
    }

    private TimeSpan GetNextRewardTime(CourierPitComponent pit)
    {
        var min = Math.Max(1, pit.MinRewardMinutes);
        var max = Math.Max(min, pit.MaxRewardMinutes);
        var delayMinutes = _random.Next(min, max + 1);
        return _timing.CurTime + TimeSpan.FromMinutes(delayMinutes);
    }
}
