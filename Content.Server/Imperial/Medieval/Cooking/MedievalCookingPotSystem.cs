using System.Linq;
using Content.Server.Destructible;
using Content.Server.Players.RateLimiting;
using Content.Server.Popups;
using Content.Shared.ActionBlocker;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.CCVar;
using Content.Shared.Imperial.Medieval.Cooking;
using Content.Shared.Interaction;
using Content.Shared.Players.RateLimiting;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Cooking;

public sealed class MedievalCookingPotSystem : EntitySystem
{
    private const float TickInterval = 1f;
    private const string MessKey = "mess";
    private const string Water = "Water";
    private const string BowlSolution = "food";
    private const float SimmerVolume = -4f;
    private const string TakeRateLimitKey = "MedievalPotTake";

    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly PlayerRateLimitManager _rateLimit = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly MedievalHearthSystem _hearth = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly MedievalPotDishSystem _dishes = default!;

    // reused for recipe matching, never holds anything between calls
    private readonly List<EntityUid> _free = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalCookingPotComponent, ComponentInit>(OnInit);
        // fuel is tried first, so a log feeds the fire instead of landing in the pot
        SubscribeLocalEvent<MedievalCookingPotComponent, InteractUsingEvent>(OnInteractUsing, after: new[] { typeof(MedievalHearthSystem) });
        SubscribeLocalEvent<MedievalCookingPotComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<MedievalCookingPotComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<MedievalCookingPotComponent, DestructionEventArgs>(OnDestroyed);
        SubscribeLocalEvent<MedievalCookingPotComponent, SolutionContainerChangedEvent>(OnSolutionChanged);
        SubscribeLocalEvent<MedievalCookingPotComponent, MedievalPotEmptyDoAfterEvent>(OnEmptyDoAfter);
        SubscribeNetworkEvent<MedievalPotTakeRequestEvent>(OnTakeRequest);

        _rateLimit.Register(TakeRateLimitKey,
            new RateLimitRegistration(MedievalCCVars.PotTakeRateLimitPeriod, MedievalCCVars.PotTakeRateLimitCount, null));
    }

    private void OnInit(EntityUid uid, MedievalCookingPotComponent pot, ComponentInit args)
    {
        pot.Ingredients = _container.EnsureContainer<Container>(uid, pot.ContainerId);
    }

    private void OnSolutionChanged(EntityUid uid, MedievalCookingPotComponent pot, ref SolutionContainerChangedEvent args)
    {
        ContentsChanged(uid, pot, false);
    }

    private void OnDestroyed(EntityUid uid, MedievalCookingPotComponent pot, DestructionEventArgs args)
    {
        _container.EmptyContainer(pot.Ingredients, force: true, destination: _transform.GetMoverCoordinates(uid));
    }

    private void OnInteractUsing(EntityUid uid, MedievalCookingPotComponent pot, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (_whitelist.IsWhitelistPass(pot.BowlWhitelist, args.Used))
        {
            // a bowl with something in it is for pouring in, which another system handles
            var done = pot.State is MedievalPotState.Ready or MedievalPotState.Burnt;
            if (!done && !IsBowlEmpty(args.Used))
                return;

            args.Handled = true;
            Serve(uid, pot, args.User, args.Used);
            return;
        }

        if (_whitelist.IsWhitelistPass(pot.IngredientWhitelist, args.Used)
            && !_whitelist.IsBlacklistPass(pot.IngredientBlacklist, args.Used))
        {
            args.Handled = true;
            AddIngredient(uid, pot, args.User, args.Used);
        }
    }

    private void OnGetVerbs(EntityUid uid, MedievalCookingPotComponent pot, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || (pot.State == MedievalPotState.Empty && !HasContents(uid, pot)))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("medieval-pot-verb-empty"),
            Act = () => StartEmptying(uid, pot, user)
        });
    }

    private void StartEmptying(EntityUid uid, MedievalCookingPotComponent pot, EntityUid user)
    {
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, pot.EmptyDelay, new MedievalPotEmptyDoAfterEvent(), uid, target: uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
        });
    }

    private void OnEmptyDoAfter(EntityUid uid, MedievalCookingPotComponent pot, MedievalPotEmptyDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        Reset(uid, pot);
        _popup.PopupEntity(Loc.GetString("medieval-pot-emptied"), uid, args.User);
    }

    private void OnExamined(EntityUid uid, MedievalCookingPotComponent pot, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString(pot.State switch
        {
            MedievalPotState.Cooking => "medieval-pot-examine-cooking",
            MedievalPotState.Ready => "medieval-pot-examine-ready",
            MedievalPotState.Burnt => "medieval-pot-examine-burnt",
            _ => "medieval-pot-examine-idle"
        }));

        if (pot.State is MedievalPotState.Ready or MedievalPotState.Burnt)
        {
            args.PushMarkup(Loc.GetString("medieval-pot-examine-portions", ("count", pot.Portions)));
            return;
        }

        args.PushMarkup(Loc.GetString("medieval-pot-examine-water",
            ("count", GetWaterMeasures(uid, pot)), ("max", GetMaxWaterMeasures(uid, pot))));

        if (pot.Ingredients.ContainedEntities.Count == 0)
            return;

        var names = pot.Ingredients.ContainedEntities.Select(item => MetaData(item).EntityName);
        args.PushMarkup(Loc.GetString("medieval-pot-examine-contents", ("items", string.Join(", ", names))));
    }

    private void AddIngredient(EntityUid uid, MedievalCookingPotComponent pot, EntityUid user, EntityUid item)
    {
        if (pot.State is MedievalPotState.Ready or MedievalPotState.Burnt)
        {
            _popup.PopupEntity(Loc.GetString("medieval-pot-has-dish"), uid, user);
            return;
        }

        if (pot.Ingredients.ContainedEntities.Count >= pot.MaxIngredients)
        {
            _popup.PopupEntity(Loc.GetString("medieval-pot-full"), uid, user);
            return;
        }

        if (!_hands.TryDropIntoContainer(user, item, pot.Ingredients))
            return;

        _popup.PopupEntity(Loc.GetString("medieval-pot-added", ("item", MetaData(item).EntityName)), uid, user);
        _audio.PlayPvs(pot.AddSound, uid);
        ContentsChanged(uid, pot, true);
    }

    private void OnTakeRequest(MedievalPotTakeRequestEvent msg, EntitySessionEventArgs args)
    {
        // the pot and the item are whatever the client sent: only the sender's own body is trusted
        if (args.SenderSession.AttachedEntity is not { } user)
            return;

        if (_rateLimit.CountAction(args.SenderSession, TakeRateLimitKey) != RateLimitStatus.Allowed)
            return;

        if (!TryGetEntity(msg.Pot, out var potUid) || !TryGetEntity(msg.Item, out var itemUid))
            return;

        var uid = potUid.Value;
        var item = itemUid.Value;
        if (!TryComp<MedievalCookingPotComponent>(uid, out var pot) || TerminatingOrDeleted(item) || !pot.Ingredients.Contains(item))
            return;

        if (!_actionBlocker.CanInteract(user, uid) || !_interaction.InRangeUnobstructed(user, uid))
            return;

        if (pot.State is MedievalPotState.Ready or MedievalPotState.Burnt)
            return;

        // with no free hand the item would only fall next to the pot
        if (!_hands.TryGetEmptyHand(user, out _) || !_container.Remove(item, pot.Ingredients))
            return;

        _hands.PickupOrDrop(user, item);
        ContentsChanged(uid, pot, true);
    }

    /// <summary>
    ///     What the icons above the pot show. It rides on the appearance, so everyone who sees the pot has it.
    /// </summary>
    private void UpdateVisuals(EntityUid uid, MedievalCookingPotComponent pot)
    {
        if (TerminatingOrDeleted(uid))
            return;

        _appearance.SetData(uid, MedievalPotVisuals.Result, pot.Result?.Id ?? string.Empty);
        _appearance.SetData(uid, MedievalPotVisuals.Portions, pot.Portions);
        _appearance.SetData(uid, MedievalPotVisuals.Water, GetWaterMeasures(uid, pot));
        _appearance.SetData(uid, MedievalPotVisuals.MaxWater, GetMaxWaterMeasures(uid, pot));
    }

    private void Serve(EntityUid uid, MedievalCookingPotComponent pot, EntityUid user, EntityUid bowl)
    {
        if (pot.State is not (MedievalPotState.Ready or MedievalPotState.Burnt) || pot.Result is not { } result)
        {
            var key = pot.State == MedievalPotState.Cooking ? "medieval-pot-busy" : "medieval-pot-nothing-to-serve";
            _popup.PopupEntity(Loc.GetString(key), uid, user);
            return;
        }

        if (!IsBowlEmpty(bowl))
        {
            _popup.PopupEntity(Loc.GetString("medieval-pot-bowl-full"), uid, user);
            return;
        }

        // the bowl is swapped for a filled one, so the hand it was in is free again
        var bowlId = MetaData(bowl).EntityPrototype?.ID;
        var sturdy = TryComp<DestructibleComponent>(bowl, out var destructible) && destructible.Thresholds.Count == 0;
        Del(bowl);
        var portion = Spawn(result, Transform(user).Coordinates);
        if (bowlId != null)
            _dishes.SetBowl(portion, bowlId);
        // a bowl that can't break doesn't start breaking because there is food in it
        if (sturdy)
            RemComp<DestructibleComponent>(portion);
        _hands.PickupOrDrop(user, portion);
        var serveSound = GetServeSound(pot);
        if (serveSound != null)
            _audio.PlayPvs(serveSound, uid);

        pot.Portions--;
        if (pot.Portions <= 0)
            ClearDish(uid, pot);
        else
            UpdateVisuals(uid, pot);
    }

    private bool IsBowlEmpty(EntityUid bowl)
    {
        return !_solutions.TryGetSolution(bowl, BowlSolution, out _, out var contents)
            || contents.Volume <= FixedPoint2.Zero;
    }

    private SoundSpecifier? GetServeSound(MedievalCookingPotComponent pot)
    {
        if (pot.State is MedievalPotState.Ready or MedievalPotState.Burnt && pot.Recipe != null
            && _prototypes.TryIndex<MedievalCookRecipePrototype>(pot.Recipe, out var recipe))
            return recipe.SilentServe ? null : recipe.ServeSound ?? pot.ServeSound;

        return pot.ServeSound;
    }

    /// <summary>
    ///     Throws away everything in the pot: ingredients, water and the finished dish.
    /// </summary>
    private void Reset(EntityUid uid, MedievalCookingPotComponent pot)
    {
        _container.CleanContainer(pot.Ingredients);

        if (_solutions.TryGetSolution(uid, pot.SolutionId, out var solution, out _))
            _solutions.RemoveAllSolution(solution.Value);

        ClearDish(uid, pot);
    }

    /// <summary>
    ///     Forgets the finished dish only; water poured in while it waited stays.
    /// </summary>
    private void ClearDish(EntityUid uid, MedievalCookingPotComponent pot)
    {
        pot.Portions = 0;
        pot.Result = null;
        pot.Recipe = null;
        pot.Progress = 0f;
        pot.ReadyTimer = 0f;
        pot.Warned = false;
        SetState(uid, pot, MedievalPotState.Empty);
        ContentsChanged(uid, pot, true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<MedievalCookingPotComponent>();
        while (query.MoveNext(out var uid, out var pot))
        {
            // nothing to cook and nothing to burn: the pot is only looked at again when its contents change
            if (pot.State == MedievalPotState.Burnt || (pot.State != MedievalPotState.Ready && pot.Recipe == null))
                continue;

            pot.TickTimer += frameTime;
            if (pot.TickTimer < TickInterval)
                continue;

            var elapsed = pot.TickTimer;
            pot.TickTimer = 0f;

            if (pot.State == MedievalPotState.Ready)
                TickReady(uid, pot, elapsed);
            else
                TickCooking(uid, pot, elapsed);
        }
    }

    private void TickCooking(EntityUid uid, MedievalCookingPotComponent pot, float elapsed)
    {
        if (pot.Recipe == null)
        {
            SetState(uid, pot, HasContents(uid, pot) ? MedievalPotState.Idle : MedievalPotState.Empty);
            return;
        }

        if (!IsHeated(uid))
        {
            SetState(uid, pot, MedievalPotState.Idle);
            return;
        }

        SetState(uid, pot, MedievalPotState.Cooking);
        pot.Progress += elapsed;

        _prototypes.TryIndex<MedievalCookRecipePrototype>(pot.Recipe, out var recipe);
        if (pot.Progress >= (recipe?.CookTime ?? pot.MessCookTime))
            Finish(uid, pot, recipe);
    }

    private void TickReady(EntityUid uid, MedievalCookingPotComponent pot, float elapsed)
    {
        if (!IsHeated(uid))
            return;

        pot.ReadyTimer += elapsed;

        if (!pot.Warned && pot.ReadyTimer >= pot.ReadyWindow - pot.WarnBefore)
        {
            pot.Warned = true;
            _popup.PopupEntity(Loc.GetString("medieval-pot-smell"), uid);
            _audio.PlayPvs(pot.WarnSound, uid);
        }

        if (pot.ReadyTimer < pot.ReadyWindow)
            return;

        pot.Result = pot.BurntResult;
        SetState(uid, pot, MedievalPotState.Burnt);
        _popup.PopupEntity(Loc.GetString("medieval-pot-burnt"), uid);
    }

    private void Finish(EntityUid uid, MedievalCookingPotComponent pot, MedievalCookRecipePrototype? recipe)
    {
        _container.CleanContainer(pot.Ingredients);

        pot.Result = recipe?.Result ?? pot.MessResult;
        pot.Portions = recipe?.Portions ?? pot.MessPortions;
        pot.Recipe = recipe?.ID;
        pot.Progress = 0f;
        pot.ReadyTimer = 0f;
        pot.Warned = false;

        SetState(uid, pot, MedievalPotState.Ready);
        // drained after the state change, so the solution event doesn't look for a recipe in a finished pot
        if (_solutions.TryGetSolution(uid, pot.SolutionId, out var solution, out _))
            _solutions.RemoveAllSolution(solution.Value);

        _popup.PopupEntity(Loc.GetString("medieval-pot-ready"), uid);
        _audio.PlayPvs(pot.ReadySound, uid);
    }

    /// <summary>
    ///     The recipe is only looked up here, when ingredients or liquid change. Adding or taking out an ingredient
    ///     also starts the cooking over, so a dish is never finished half way through being put together.
    /// </summary>
    private void ContentsChanged(EntityUid uid, MedievalCookingPotComponent pot, bool restart)
    {
        if (pot.State is MedievalPotState.Ready or MedievalPotState.Burnt)
        {
            UpdateVisuals(uid, pot);
            return;
        }

        var key = FindRecipe(uid, pot)?.ID ?? (IsMess(uid, pot) && !FitsAnyRecipe(pot) ? MessKey : null);
        if (restart || key != pot.Recipe)
        {
            pot.Recipe = key;
            pot.Progress = 0f;
        }

        UpdateVisuals(uid, pot);
        TickCooking(uid, pot, 0f);
    }

    private bool IsHeated(EntityUid uid)
    {
        return TryComp<MedievalHearthComponent>(uid, out var hearth) && _hearth.IsLit(hearth);
    }

    private bool HasContents(EntityUid uid, MedievalCookingPotComponent pot)
    {
        // any liquid counts, not only water: otherwise a pot of beer could never be poured out
        return pot.Ingredients.ContainedEntities.Count > 0
            || _solutions.TryGetSolution(uid, pot.SolutionId, out _, out var solution) && solution.Volume > FixedPoint2.Zero;
    }

    private int GetWaterMeasures(EntityUid uid, MedievalCookingPotComponent pot)
    {
        return (int) (GetWater(uid) / Math.Max(1, pot.WaterMeasure));
    }

    private int GetMaxWaterMeasures(EntityUid uid, MedievalCookingPotComponent pot)
    {
        if (!_solutions.TryGetSolution(uid, pot.SolutionId, out _, out var solution))
            return 0;

        return (int) (solution.MaxVolume / Math.Max(1, pot.WaterMeasure));
    }

    private FixedPoint2 GetWater(EntityUid uid)
    {
        return _solutions.GetTotalPrototypeQuantity(uid, Water);
    }

    /// <summary>
    ///     Anything edible that fits no recipe still boils into something, as long as there is water.
    /// </summary>
    private bool IsMess(EntityUid uid, MedievalCookingPotComponent pot)
    {
        return pot.Ingredients.ContainedEntities.Count >= pot.MessMinIngredients && GetWater(uid) >= pot.MessWater;
    }

    /// <summary>
    ///     True when the ingredients already match a recipe and only the water is missing,
    ///     so the pot waits for water instead of boiling a mess.
    /// </summary>
    private bool FitsAnyRecipe(MedievalCookingPotComponent pot)
    {
        foreach (var recipe in _prototypes.EnumeratePrototypes<MedievalCookRecipePrototype>())
        {
            if (Matches(recipe, pot.Ingredients.ContainedEntities))
                return true;
        }

        return false;
    }

    private MedievalCookRecipePrototype? FindRecipe(EntityUid uid, MedievalCookingPotComponent pot)
    {
        var items = pot.Ingredients.ContainedEntities;
        if (items.Count == 0)
            return null;

        var water = GetWater(uid);
        foreach (var recipe in _prototypes.EnumeratePrototypes<MedievalCookRecipePrototype>())
        {
            if (water >= recipe.Water && Matches(recipe, items))
                return recipe;
        }

        return null;
    }

    private bool Matches(MedievalCookRecipePrototype recipe, IReadOnlyList<EntityUid> items)
    {
        _free.Clear();
        _free.AddRange(items);

        // exact prototypes first, so a tag line can't swallow an item another line is waiting for
        foreach (var line in recipe.Ingredients)
        {
            if (line.Id != null && !TakeMatching(line, items: _free))
                return false;
        }

        foreach (var line in recipe.Ingredients)
        {
            if (line.Id == null && !TakeMatching(line, items: _free))
                return false;
        }

        return _free.Count == 0;
    }

    private bool TakeMatching(MedievalCookIngredient line, List<EntityUid> items)
    {
        for (var taken = 0; taken < line.Count; taken++)
        {
            var index = items.FindIndex(item => IsIngredient(line, item));
            if (index < 0)
                return false;

            items.RemoveAt(index);
        }

        return true;
    }

    private bool IsIngredient(MedievalCookIngredient line, EntityUid item)
    {
        if (line.Id is { } id)
            return MetaData(item).EntityPrototype?.ID == id.Id;

        return line.Tag is { } tag && _tag.HasTag(item, tag) && !_tag.HasAnyTag(item, line.ExcludeTags);
    }

    private void SetState(EntityUid uid, MedievalCookingPotComponent pot, MedievalPotState state)
    {
        if (pot.State == state)
            return;

        pot.State = state;
        _appearance.SetData(uid, MedievalPotVisuals.State, state);
        UpdateSimmer(uid, pot);
        UpdateVisuals(uid, pot);
    }

    private void UpdateSimmer(EntityUid uid, MedievalCookingPotComponent pot)
    {
        if (pot.State != MedievalPotState.Cooking)
        {
            pot.SimmerStream = _audio.Stop(pot.SimmerStream);
            return;
        }

        if (pot.SimmerStream == null && pot.SimmerSound != null)
            pot.SimmerStream = _audio.PlayPvs(pot.SimmerSound, uid, AudioParams.Default.WithLoop(true).WithVolume(SimmerVolume))?.Entity;
    }
}
