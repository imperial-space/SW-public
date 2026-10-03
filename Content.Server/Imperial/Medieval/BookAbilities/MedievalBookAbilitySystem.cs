using System.Linq;
using Content.Server._CP14.Workbench;
using Content.Server.Botany.Components;
using Content.Server.Imperial.Medieval.Knowledge;
using Content.Server.SpikeTrap.Components;
using Content.Shared.Actions;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage;
using Content.Shared.Dice;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.GameTicking;
using Content.Shared.Imperial.Medieval.BookAbilities;
using Content.Shared.Imperial.Medieval.Knowledge;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Content.Shared.Storage.Components;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.BookAbilities;

/// <summary>World interactions learned from books. Delayed work keeps server-owned input identities.</summary>
public sealed partial class MedievalBookAbilitySystem : EntitySystem
{
    [Dependency] private readonly MedievalKnowledgeSystem _knowledge = default!;
    [Dependency] private readonly MedievalCompanionSystem _companions = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly SharedCuffableSystem _cuffs = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private sealed record PendingWork(EntityUid User, EntityUid Target, string Knowledge, Func<bool> Validate, Action Complete);
    private readonly Dictionary<string, PendingWork> _pending = new();
    private readonly Dictionary<EntityUid, EntityUid> _loosenedCuffs = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<LearnedKnowledgeComponent, BookAbilityDoAfterEvent>(OnWork);
        SubscribeLocalEvent<LearnedKnowledgeComponent, BookEscapeBondsActionEvent>(OnEscapeBonds);
        InitializeCombat();
        InitializeSpeechAndMagic();
        InitializeActivation();
        InitializePacking();
        InitializeTaming();
        InitializeCartography();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ =>
        {
            CleanupTaming();
            _pending.Clear();
            _choices.Clear();
            _loosenedCuffs.Clear();
            _voices.Clear();
            _signatures.Clear();
            _ripostes.Clear();
            _guardBreak.Clear();
            _piercingShot.Clear();
        });
    }

    private bool Knows(EntityUid uid, string knowledge) => _knowledge.HasKnowledge(uid, knowledge);

    private bool Start(EntityUid user, EntityUid target, string knowledge, float seconds, Func<bool> validate, Action complete)
    {
        if (!Knows(user, knowledge) || !validate() || !_interaction.InRangeUnobstructed(user, target)) return false;
        var token = Guid.NewGuid().ToString();
        _pending[token] = new(user, target, knowledge, validate, complete);
        if (_doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, seconds,
                new BookAbilityDoAfterEvent { Ability = knowledge, Option = token }, user, target)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                NeedHand = false,
                Hidden = knowledge == "BookEscapeBonds",
                RequireCanInteract = knowledge != "BookEscapeBonds"
            })) return true;
        _pending.Remove(token);
        return false;
    }

    private void OnWork(EntityUid uid, LearnedKnowledgeComponent comp, BookAbilityDoAfterEvent args)
    {
        if (args.Handled || !_pending.Remove(args.Option, out var pending)) return;
        args.Handled = true;
        if (args.Cancelled || pending.User != uid || !Exists(pending.Target) ||
            !Knows(uid, pending.Knowledge) || !pending.Validate() ||
            !_interaction.InRangeUnobstructed(uid, pending.Target)) return;
        pending.Complete();
    }

    private bool Loosen(EntityUid user)
    {
        if (!TryComp<CuffableComponent>(user, out var cuff) || cuff.CuffedHandCount == 0) return false;
        var restraint = cuff.LastAddedCuffs;
        return Start(user, user, "BookEscapeBonds", 10,
            () => cuff.Container.ContainedEntities.Contains(restraint), () =>
            {
                _loosenedCuffs[user] = restraint;
                _popup.PopupEntity(Loc.GetString("book-ability-bonds-ready"), user, user);
            });
    }

    private void OnEscapeBonds(EntityUid uid, LearnedKnowledgeComponent comp, BookEscapeBondsActionEvent args)
    {
        if (args.Handled || !Knows(uid, "BookEscapeBonds")) return;
        if (_loosenedCuffs.Remove(uid, out var restraint) && TryComp<CuffableComponent>(uid, out var cuff) &&
            cuff.Container.ContainedEntities.Contains(restraint))
        {
            _cuffs.Uncuff(uid, uid, restraint, cuff);
            args.Handled = true;
        }
        else args.Handled = Loosen(uid);
    }

    public bool TryExtract(EntityUid user, EntityUid source, EntityUid receiver, ReagentId reagent)
    {
        if (!Knows(user, "BookExtractReagent") || source == receiver ||
            !_hands.IsHolding(user, source, out _) || !_hands.IsHolding(user, receiver, out _) ||
            !IsOpenContainer(source) || !IsOpenContainer(receiver) ||
            !_interaction.InRangeUnobstructed(user, source) ||
            !_solutions.TryGetDrainableSolution(source, out var sourceSol, out var solution) ||
            !_solutions.TryGetRefillableSolution(receiver, out var destination, out var destinationMixture) ||
            destinationMixture.Volume != 0) return false;
        var amount = solution.GetReagentQuantity(reagent);
        if (amount <= 0 || destinationMixture.AvailableVolume < amount) return false;
        var separated = new Solution { Temperature = solution.Temperature };
        separated.AddReagent(reagent, amount);
        _solutions.RemoveReagent(sourceSol.Value, reagent, amount);
        if (!_solutions.TryAddSolution(destination.Value, separated))
        {
            _solutions.TryAddSolution(sourceSol.Value, separated);
            return false;
        }
        return true;
    }

    private bool IsOpenContainer(EntityUid uid) => !TryComp<OpenableComponent>(uid, out var openable) || openable.Opened;

}
