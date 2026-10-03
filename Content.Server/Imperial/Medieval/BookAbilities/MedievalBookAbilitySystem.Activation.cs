using System.Globalization;
using System.Linq;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Dice;
using Content.Shared.Hands.Components;
using Content.Shared.Imperial.Medieval.BookAbilities;
using Content.Shared.Imperial.Medieval.Knowledge;
using Content.Shared.Imperial.Medieval.Magic;
using Content.Shared.Paper;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.BookAbilities;

public sealed partial class MedievalBookAbilitySystem
{
    [Dependency] private readonly ActionBlockerSystem _abilityBlocker = default!;

    private sealed record PendingChoice(int Id, string Knowledge, TimeSpan Until,
        Dictionary<string, Func<bool>> Options);
    private readonly Dictionary<EntityUid, PendingChoice> _choices = new();
    private int _nextChoiceId;

    private void InitializeActivation()
    {
        SubscribeLocalEvent<LearnedKnowledgeComponent, BookDiceCheatActionEvent>(OnDiceAbility);
        SubscribeLocalEvent<LearnedKnowledgeComponent, BookExtractReagentActionEvent>(OnExtractAbility);
        SubscribeLocalEvent<LearnedKnowledgeComponent, BookSpellScribingActionEvent>(OnScribeAbility);
        SubscribeNetworkEvent<BookAbilityChoiceSelectedEvent>(OnChoiceSelected);
    }

    private IEnumerable<EntityUid> HeldRightFirst(EntityUid user)
    {
        if (!TryComp<HandsComponent>(user, out var hands)) yield break;
        foreach (var hand in hands.Hands.OrderBy(h => h.Value.Location == HandLocation.Right ? 0 : 1)
                     .ThenBy(h => h.Key, StringComparer.Ordinal))
        {
            if (_hands.TryGetHeldItem((user, hands), hand.Key, out var held))
                yield return held.Value;
        }
    }

    public EntityUid? FindHeldDie(EntityUid user)
    {
        foreach (var held in HeldRightFirst(user))
            if (HasComp<DiceComponent>(held)) return held;
        return null;
    }

    public bool TryPrepareHeldDie(EntityUid user, int side)
    {
        if (!Knows(user, "BookDiceCheat") || FindHeldDie(user) is not { } die ||
            !TryComp<DiceComponent>(die, out var dice) || side < 1 || side > dice.Sides) return false;
        var loaded = EnsureComp<BookLoadedDiceComponent>(die);
        loaded.User = user;
        loaded.Side = side;
        Dirty(die, loaded);
        return true;
    }

    private void OnDiceAbility(EntityUid uid, LearnedKnowledgeComponent comp, BookDiceCheatActionEvent args)
    {
        if (args.Handled || !Knows(uid, "BookDiceCheat")) return;
        if (FindHeldDie(uid) is not { } die || !TryComp<DiceComponent>(die, out var dice))
        {
            AbilityError(uid, "book-activation-need-die");
            return;
        }
        var options = new List<(string Label, Func<bool> Select)>();
        for (var i = 1; i <= dice.Sides; i++)
        {
            var side = i;
            options.Add((Loc.GetString("book-ability-loaded-die", ("value", (side - dice.Offset) * dice.Multiplier)),
                () => FindHeldDie(uid) == die && TryPrepareHeldDie(uid, side)));
        }
        args.Handled = OpenAbilityChoice(uid, "BookDiceCheat", "book-activation-die-title", options);
    }

    /// <summary>The nonempty source and empty receiver are inferred from the hands, independently of the active hand.</summary>
    public bool TryGetExtractionPair(EntityUid user, out EntityUid source, out EntityUid receiver)
    {
        source = receiver = default;
        var held = HeldRightFirst(user).ToArray();
        foreach (var candidate in held)
        {
            if (!IsOpenContainer(candidate) ||
                !_solutions.TryGetDrainableSolution(candidate, out _, out var mixture) || mixture.Volume <= 0) continue;
            foreach (var destination in held)
            {
                if (destination == candidate || !IsOpenContainer(destination) ||
                    !_solutions.TryGetRefillableSolution(destination, out _, out var empty) || empty.Volume != 0) continue;
                source = candidate;
                receiver = destination;
                return true;
            }
        }
        return false;
    }

    private void OnExtractAbility(EntityUid uid, LearnedKnowledgeComponent comp, BookExtractReagentActionEvent args)
    {
        if (args.Handled || !Knows(uid, "BookExtractReagent")) return;
        if (!TryGetExtractionPair(uid, out var source, out var receiver) ||
            !_solutions.TryGetDrainableSolution(source, out _, out var mixture) ||
            !_solutions.TryGetRefillableSolution(receiver, out _, out var empty))
        {
            AbilityError(uid, "book-activation-need-vessels");
            return;
        }
        var options = new List<(string Label, Func<bool> Select)>();
        foreach (var content in mixture.Contents.ToArray())
        {
            var reagent = content.Reagent;
            if (empty.AvailableVolume < content.Quantity) continue;
            var label = _prototypes.Index<ReagentPrototype>(reagent.Prototype).LocalizedName;
            options.Add((Loc.GetString("book-ability-extract", ("reagent", label)),
                () => BeginExtraction(uid, source, receiver, reagent)));
        }
        if (options.Count == 0)
        {
            AbilityError(uid, "book-activation-small-vessel");
            return;
        }
        args.Handled = OpenAbilityChoice(uid, "BookExtractReagent", "book-activation-extract-title", options);
    }

    public bool BeginExtraction(EntityUid user, EntityUid source, EntityUid receiver, ReagentId reagent)
    {
        bool Ready() => TryGetExtractionPair(user, out var currentSource, out var currentReceiver) &&
                        currentSource == source && currentReceiver == receiver &&
                        _solutions.TryGetDrainableSolution(source, out _, out var mixture) &&
                        mixture.GetReagentQuantity(reagent) > 0 &&
                        _solutions.TryGetRefillableSolution(receiver, out _, out var empty) &&
                        empty.AvailableVolume >= mixture.GetReagentQuantity(reagent);
        return Start(user, source, "BookExtractReagent", 10, Ready,
            () => TryExtract(user, source, receiver, reagent));
    }

    private void OnScribeAbility(EntityUid uid, LearnedKnowledgeComponent comp, BookSpellScribingActionEvent args)
    {
        if (args.Handled || !Knows(uid, "BookSpellScribing")) return;
        var sheet = args.Target;
        if (!CanScribeOn(uid, sheet))
        {
            AbilityError(uid, "book-activation-need-paper-pen");
            return;
        }
        var options = new List<(string Label, Func<bool> Select)>();
        foreach (var action in _actions.GetActions(uid))
        {
            if (MetaData(action).EntityPrototype?.ID is not { } id ||
                !TryComp<WorldTargetActionComponent>(action, out var target) ||
                target.Event is not MedievalProjectileSpellEvent || HasComp<BookScrollActionComponent>(action)) continue;
            var spell = action.Owner;
            options.Add((Loc.GetString("book-ability-scribe", ("spell", Name(spell))),
                () => Start(uid, sheet, "BookSpellScribing", 60,
                    () => CanScribeOn(uid, sheet) && Exists(spell) &&
                          _actions.GetActions(uid).Any(a => a.Owner == spell) && HasScribingMana(uid, spell),
                    () => Scribe(uid, sheet, spell, id))));
        }
        if (options.Count == 0)
        {
            AbilityError(uid, "book-activation-no-spells");
            return;
        }
        args.Handled = OpenAbilityChoice(uid, "BookSpellScribing", "book-activation-scribe-title", options);
    }

    private bool CanScribeOn(EntityUid user, EntityUid sheet) =>
        Exists(sheet) && HasPen(user) && TryComp<PaperComponent>(sheet, out var paper) &&
        !paper.EditingDisabled && string.IsNullOrWhiteSpace(paper.Content) &&
        !HasComp<BookSpellScrollComponent>(sheet) && !HasComp<LearnableBookComponent>(sheet) &&
        _interaction.InRangeUnobstructed(user, sheet) && _interaction.CanAccess(user, sheet);

    private bool OpenAbilityChoice(EntityUid user, string knowledge, string title,
        List<(string Label, Func<bool> Select)> options)
    {
        if (!TryComp<ActorComponent>(user, out var actor) || !_abilityBlocker.CanInteract(user, null)) return false;
        var id = ++_nextChoiceId;
        var callbacks = new Dictionary<string, Func<bool>>();
        var choices = new List<BookAbilityChoice>();
        for (var i = 0; i < options.Count; i++)
        {
            var key = i.ToString(CultureInfo.InvariantCulture);
            callbacks.Add(key, options[i].Select);
            choices.Add(new BookAbilityChoice(key, options[i].Label));
        }
        _choices[user] = new(id, knowledge, _timing.CurTime + TimeSpan.FromSeconds(30), callbacks);
        RaiseNetworkEvent(new BookAbilityChoicesEvent(id, Loc.GetString(title), choices), actor.PlayerSession);
        return true;
    }

    private void OnChoiceSelected(BookAbilityChoiceSelectedEvent args, EntitySessionEventArgs session)
    {
        if (session.SenderSession.AttachedEntity is not { } user ||
            !_choices.TryGetValue(user, out var choice) || choice.Id != args.RequestId) return;
        _choices.Remove(user);
        if (string.IsNullOrEmpty(args.ChoiceId)) return;
        if (choice.Until < _timing.CurTime || !Knows(user, choice.Knowledge) ||
            !_abilityBlocker.CanInteract(user, null) || !choice.Options.TryGetValue(args.ChoiceId, out var selected) || !selected())
            AbilityError(user, "book-activation-choice-expired");
    }

    private void AbilityError(EntityUid user, string key) => _popup.PopupEntity(Loc.GetString(key), user, user);
}
