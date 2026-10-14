/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared._CP14.Workbench;
using Robust.Shared.Timing;

namespace Content.Server._CP14.Workbench;

public sealed partial class CP14WorkbenchSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    // imperial medieval - open windows pick up what was put on the workbench
    private static readonly TimeSpan OpenUiRefreshInterval = TimeSpan.FromSeconds(1);
    private TimeSpan _nextOpenUiRefresh;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextOpenUiRefresh)
            return;

        _nextOpenUiRefresh = _timing.CurTime + OpenUiRefreshInterval;

        var query = EntityQueryEnumerator<CP14WorkbenchComponent, ActiveUserInterfaceComponent>();
        while (query.MoveNext(out var uid, out var workbench, out _))
        {
            if (!_userInterface.IsUiOpen(uid, CP14WorkbenchUiKey.Key))
                continue;

            UpdateUIRecipes((uid, workbench));
        }
    }

    private void OnCraft(Entity<CP14WorkbenchComponent> entity, ref CP14WorkbenchUiCraftMessage args)
    {
        if (!entity.Comp.Recipes.Contains(args.Recipe))
            return;

        if (!_proto.TryIndex(args.Recipe, out var prototype))
            return;

        StartCraft(entity, args.Actor, prototype);
    }

    private void UpdateUIRecipes(Entity<CP14WorkbenchComponent> entity)
    {
        // imperial medieval - Uncontained like the craft itself, items in a bag on the workbench don't count
        var placedEntities = _lookup.GetEntitiesInRange(Transform(entity).Coordinates, WorkbenchRadius, LookupFlags.Uncontained);

        var recipes = new List<CP14WorkbenchUiRecipesEntry>();
        foreach (var recipeId in entity.Comp.Recipes)
        {
            // imperial medieval - TryIndex, this now runs every second
            if (!_proto.TryIndex(recipeId, out var recipe))
                continue;

            var entry = new CP14WorkbenchUiRecipesEntry(recipeId, CanCraftRecipe(recipe, placedEntities));

            recipes.Add(entry);
        }

        // imperial medieval - material counts for "have / need"
        var (placed, stacks) = CountPlaced(entity.Comp, placedEntities);

        _userInterface.SetUiState(entity.Owner,
            CP14WorkbenchUiKey.Key,
            new CP14WorkbenchUiRecipesState(recipes, placed, stacks, entity.Comp.UiTheme));
    }

    /// <summary>
    /// imperial medieval - counted like <see cref="CanCraftRecipe"/>, only what this workbench's recipes use.
    /// </summary>
    private (Dictionary<string, int> Entities, Dictionary<string, int> Stacks) CountPlaced(
        CP14WorkbenchComponent workbench,
        HashSet<EntityUid> placedEntities)
    {
        var wantedEntities = new HashSet<string>();
        var wantedStacks = new HashSet<string>();
        foreach (var recipeId in workbench.Recipes)
        {
            if (!_proto.TryIndex(recipeId, out var recipe))
                continue;

            foreach (var key in recipe.Entities.Keys)
                wantedEntities.Add(key);

            foreach (var key in recipe.Stacks.Keys)
                wantedStacks.Add(key);
        }

        var entities = new Dictionary<string, int>();
        foreach (var (protoId, count) in IndexIngredients(placedEntities))
        {
            if (wantedEntities.Contains(protoId))
                entities[protoId] = count;
        }

        var stacks = new Dictionary<string, int>();
        foreach (var ent in placedEntities)
        {
            if (!_stackQuery.TryGetComponent(ent, out var stack) || !wantedStacks.Contains(stack.StackTypeId))
                continue;

            stacks[stack.StackTypeId] = stacks.GetValueOrDefault(stack.StackTypeId) + stack.Count;
        }

        return (entities, stacks);
    }
}
