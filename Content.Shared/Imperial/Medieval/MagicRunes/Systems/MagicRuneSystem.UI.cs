using System.Linq;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.MagicRunes.Components;
using Content.Shared.Imperial.Medieval.MagicRunes.Data;
using Content.Shared.Random.Helpers;
using Content.Shared.Stacks;
using Content.Shared.UserInterface;
using Microsoft.CodeAnalysis.CSharp.Syntax;

//=========================================================================
// MagicRuneSystem.UI.cs
//=========================================================================
// Purpose: User interface handling for magic scroll interactions
// Author: rhailrake, edited by Bladefire5
//=========================================================================

namespace Content.Shared.Imperial.Medieval.MagicRunes.Systems;

public partial class MagicRuneSystem
{
    private readonly (string Id, string Effect, int Min, int Max)[] _rewards = new[]
    {
        ("MagicMedievalLight", "SunstrikeSpellCastEffectMiddle", 7, 15),
        ("MagicMedievalFire", "FireWallSpellCastEffectMiddle", 7, 15),
        ("MagicMedievalEarth", "SpikesSpellCastEffectBeginner", 7, 15),
        ("MagicMedievalVodka", "IceDaggerSpellCastEffectBeginner", 7, 15),
        ("MagicMedievalDarkness", "TentaclesSpellCastEffectBeginner", 1, 2)
    };

    [Dependency] private readonly SharedStackSystem _stacks = default!;
    public void InitializeUI()
    {
        SubscribeLocalEvent<MagicScrollComponent, ActivatableUIOpenAttemptEvent>(UIOpenAttempt);
        SubscribeLocalEvent<MagicScrollComponent, BeforeActivatableUIOpenEvent>(BeforeUIOpen);
        SubscribeLocalEvent<MagicScrollComponent, MagicScrollRuneUnlockedMessage>(OnRuneUnlocked);
        SubscribeLocalEvent<MagicScrollComponent, MagicScrollRunePairUnlockedMessage>(OnRunePairUnlocked);
        SubscribeLocalEvent<MagicScrollComponent, MagicScrollExplosionMessage>(OnScrollExplosion);
        //fix for restarts and being able to throw the scrolls to avoid fail.
        SubscribeLocalEvent<MagicScrollComponent, MagicScrollPairRestartUsedMessage>(OnPairRestartUsed);
        SubscribeLocalEvent<MagicScrollComponent, MagicScrollMinigameStartedMessage>(OnMinigameStarted);
        SubscribeLocalEvent<MagicScrollComponent, MagicScrollMinigameFinishedMessage>(OnMinigameFinished);

        Subs.BuiEvents<MagicScrollComponent>(MagicScrollUiKey.Key, subs =>
        {
            subs.Event<BoundUIClosedEvent>(OnScrollUiClosed);
        });
    }

    private void UIOpenAttempt(EntityUid uid, MagicScrollComponent component, ActivatableUIOpenAttemptEvent args)
    {
        if (component.DebugBypassMinigameRequirements)
            return;

        if (!HasComp<MagicRuneKnowledgeComponent>(args.User))
            args.Cancel();
    }

    private void BeforeUIOpen(EntityUid uid, MagicScrollComponent component, BeforeActivatableUIOpenEvent args)
    {
        if (TryComp<MagicRuneKnowledgeComponent>(args.User, out var knowledge))
        {
            SendScrollState(uid, component, knowledge, args.User);
            return;
        }

        if (component.DebugBypassMinigameRequirements)
            SendScrollState(uid, component, null, args.User);
    }

    private void OnRuneUnlocked(EntityUid uid, MagicScrollComponent component, MagicScrollRuneUnlockedMessage args)
    {
        if (component.RequiresRunePairs)
            return;

        TryComp<MagicRuneKnowledgeComponent>(args.Actor, out var knowledge);

        if (!component.DebugBypassMinigameRequirements &&
            (knowledge == null || !knowledge.KnownRunes.Contains(args.Rune)))
            return;

        if (!component.EncryptedRunes.Contains(args.Rune) || component.DecodedRunes.Contains(args.Rune))
            return;

        component.ActiveMinigameUsers.Remove(args.Actor);

        component.DecodedRunes.Add(args.Rune);

        RecalculateScrollPower(uid, component);
        SendScrollState(uid, component, knowledge, args.Actor);
        Dirty(uid, component);

        GetPlayerEssence(args.Actor, component);

        if (component.ConvertScroll &&
            component.EncryptedRunes.Count > 0 &&
            component.DecodedRunes.Count >= component.EncryptedRunes.Count)
        {
            ConvertToDecodedNormalScroll(uid, component, args.Actor);
        }
    }

    private void OnRunePairUnlocked(
    EntityUid uid,
    MagicScrollComponent component,
    MagicScrollRunePairUnlockedMessage args)
    {
        if (!component.RequiresRunePairs)
            return;

        TryComp<MagicRuneKnowledgeComponent>(args.Actor, out var knowledge);

        if (!component.DebugBypassMinigameRequirements &&
        (knowledge == null ||
         !knowledge.KnownRunes.Contains(args.First) ||
         !knowledge.KnownRunes.Contains(args.Second)))
            return;

        var pairIndex = -1;

        for (var i = 0; i < component.EncryptedPairs.Count; i++)
        {
            var pair = component.EncryptedPairs[i];

            if (pair.First == args.First && pair.Second == args.Second ||
            pair.First == args.Second && pair.Second == args.First)

            {
                pairIndex = i;
                break;
            }
        }

        if (pairIndex < 0 || component.DecodedPairs.Contains(pairIndex))
            return;

        component.ActiveMinigameUsers.Remove(args.Actor);

        component.DecodedPairs.Add(pairIndex);
        component.DecodedRunes.Add(args.First);
        component.DecodedRunes.Add(args.Second);

        RecalculateScrollPower(uid, component);
        SendScrollState(uid, component, knowledge, args.Actor);
        Dirty(uid, component);

        GetPlayerEssence(args.Actor, component);
    }

    private void OnScrollExplosion(EntityUid uid, MagicScrollComponent component, MagicScrollExplosionMessage args)
    {
        {
            component.ActiveMinigameUsers.Remove(args.Actor);

            HandleScrollFailure(uid, component);
        }
    }

    private void HandleScrollFailure(
    EntityUid uid,
    MagicScrollComponent component)
    {
        if (component.MaxFails > 1)
        {
            component.MaxFails--;
            return;
        }

        _boomSystem.TriggerExplosive(uid);
    }

    private void OnScrollUiClosed(
    Entity<MagicScrollComponent> entity,
    ref BoundUIClosedEvent args)
    {
        if (!entity.Comp.ActiveMinigameUsers.Remove(args.Actor))
            return;

        HandleScrollFailure(entity.Owner, entity.Comp);
    }

    private void ConvertToDecodedNormalScroll(EntityUid uid, MagicScrollComponent component, EntityUid user)
    {
        // The crafting system uses a separate prototype for a fully decoded normal scroll.
        // It keeps the same visible presentation, while preventing an incomplete scroll from
        // being accepted by the overclocked recipe.
        var coordinates = Transform(uid).Coordinates;
        var decodedScroll = Spawn("MedievalScrollBarrierDecoded", coordinates);

        if (TryComp<MagicScrollComponent>(decodedScroll, out var decodedComponent))
        {
            decodedComponent.BasicPower = component.Power;
            decodedComponent.Power = component.Power;
            decodedComponent.EncryptedRunes.Clear();
            decodedComponent.EncryptedRunes.AddRange(component.EncryptedRunes);
            decodedComponent.DecodedRunes.Clear();
            foreach (var rune in component.DecodedRunes)
                decodedComponent.DecodedRunes.Add(rune);
            Dirty(decodedScroll, decodedComponent);
        }

        _hands.PickupOrDrop(user, decodedScroll, checkActionBlocker: false, animate: false, dropNear: true);

        QueueDel(uid);
    }

    private void SendScrollState(EntityUid scrollUid, MagicScrollComponent scroll, MagicRuneKnowledgeComponent? knowledge, EntityUid user)
    {
        var intelligence = ComponentDebugIntelligence(scroll, user);
        var knownRunes = knowledge?.KnownRunes ?? new HashSet<MagicRune>();

        if (scroll.DebugBypassMinigameRequirements)
            knownRunes = scroll.EncryptedRunes.ToHashSet();

        var state = new MagicScrollBoundUserInterfaceState(
            scrollPower: scroll.Power,
            encryptedRunes: scroll.EncryptedRunes,
            decodedRunes: scroll.DecodedRunes,
            knownRunes: knownRunes,
            playerIntelligence: intelligence,
            requiredIntelligence: scroll.RequiredIntelligence,
            gridSize: scroll.GridSize,
            totalMines: scroll.TotalMines,
            requiresRunePairs: scroll.RequiresRunePairs,
            encryptedPairs: scroll.EncryptedPairs,
            decodedPairs: scroll.DecodedPairs,
            moveTimeSeconds: scroll.MoveTimeSeconds,
            minimumMoveDelaySeconds: scroll.MinimumMoveDelaySeconds,
            tipsAvailable: scroll.TipsAvailable,
            maxRestarts: scroll.MaxRestarts,
            pairRestartsRemaining: scroll.PairRestartsRemaining,
            isPractice: scroll.IsPractice,
            isUnstable: scroll.IsUnstable,
            debugBypassMinigameRequirements: scroll.DebugBypassMinigameRequirements
        );

        _uiSystem.SetUiState(scrollUid, MagicScrollUiKey.Key, state);
    }

    private int ComponentDebugIntelligence(MagicScrollComponent scroll, EntityUid user)
    {
        return scroll.DebugBypassMinigameRequirements ? 10 : GetIntelligence(user);
    }

    private void GetPlayerEssence(EntityUid user, MagicScrollComponent component)
    {
        if (_net.IsClient)
            return;

        if (_rewards.Length == 0)
            return;

        var reward = _rewards[_random.Next(_rewards.Length)];

        var baseCount = _random.Next(reward.Min, reward.Max + 1);

        int count;

        if (component.IsPractice)
        {
            count = Math.Max(1, baseCount / 4);
        }
        else if (component.IsUnstable)
        {
            var multiplier = _random.NextFloat(2f, 3f);
            count = (int)MathF.Round(baseCount * multiplier);
        }
        else if (component.RequiresRunePairs)
        {
            count = (int)MathF.Round(baseCount * 2.5f);
        }
        else
        {
            count = baseCount;
        }

        var coords = Transform(user).Coordinates;

        var essence = Spawn(reward.Id, coords);
        _stacks.SetCount(essence, count);

        Spawn(reward.Effect, coords);
    }

    private void OnMinigameStarted(
    EntityUid uid,
    MagicScrollComponent component,
    MagicScrollMinigameStartedMessage args)
    {
        component.ActiveMinigameUsers.Add(args.Actor);
    }

    private void OnPairRestartUsed(
    EntityUid uid,
    MagicScrollComponent component,
    MagicScrollPairRestartUsedMessage args)
    {
        if (!component.RequiresRunePairs)
            return;

        var pairIndex = -1;

        for (var i = 0; i < component.EncryptedPairs.Count; i++)
        {
            var pair = component.EncryptedPairs[i];

            if (pair.First == args.First && pair.Second == args.Second ||
            pair.First == args.Second && pair.Second == args.First)
            {
                pairIndex = i;
                break;
            }
        }

        if (pairIndex < 0 ||
        pairIndex >= component.PairRestartsRemaining.Count)
            return;

        var remaining = component.PairRestartsRemaining[pairIndex];

        if (remaining < 0)
            return;


        if (remaining == 0)
            return;

        component.PairRestartsRemaining[pairIndex]--;

        TryComp<MagicRuneKnowledgeComponent>(args.Actor, out var knowledge);

        SendScrollState(uid, component, knowledge, args.Actor);
    }

    private void OnMinigameFinished(
    EntityUid uid,
    MagicScrollComponent component,
    MagicScrollMinigameFinishedMessage args)
    {
        component.ActiveMinigameUsers.Remove(args.Actor);
    }

}

