/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared._CP14.Workbench.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._CP14.Workbench;

[Serializable, NetSerializable]
public enum CP14WorkbenchUiKey
{
    Key,
}

[Serializable, NetSerializable]
public sealed class CP14WorkbenchUiCraftMessage : BoundUserInterfaceMessage
{
    public readonly ProtoId<CP14WorkbenchRecipePrototype> Recipe;

    public CP14WorkbenchUiCraftMessage(ProtoId<CP14WorkbenchRecipePrototype> recipe)
    {
        Recipe = recipe;
    }
}


[Serializable, NetSerializable]
public sealed class CP14WorkbenchUiRecipesState : BoundUserInterfaceState, IEquatable<CP14WorkbenchUiRecipesState>
{
    // It's list (not hashset) BECAUSE CP14WorkbenchComponent contains list of recipes (WHY???)
    public readonly List<CP14WorkbenchUiRecipesEntry> Recipes;

    // imperial medieval start
    /// <summary>Entities on the workbench by prototype id.</summary>
    public readonly Dictionary<string, int> PlacedEntities;

    /// <summary>Stack counts on the workbench by stack type.</summary>
    public readonly Dictionary<string, int> PlacedStacks;

    /// <summary>Window look, see uiTheme on CP14WorkbenchComponent.</summary>
    public readonly string Theme;

    public CP14WorkbenchUiRecipesState(List<CP14WorkbenchUiRecipesEntry> recipes,
        Dictionary<string, int>? placedEntities = null,
        Dictionary<string, int>? placedStacks = null,
        string theme = "")
    {
        Recipes = recipes;
        PlacedEntities = placedEntities ?? new();
        PlacedStacks = placedStacks ?? new();
        Theme = theme;
    }

    // lets SetUiState skip resending an unchanged state
    public bool Equals(CP14WorkbenchUiRecipesState? other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        if (Theme != other.Theme || Recipes.Count != other.Recipes.Count)
            return false;

        for (var i = 0; i < Recipes.Count; i++)
        {
            // entry Equals ignores Craftable
            if (Recipes[i].ProtoId.Id != other.Recipes[i].ProtoId.Id || Recipes[i].Craftable != other.Recipes[i].Craftable)
                return false;
        }

        return SameCounts(PlacedEntities, other.PlacedEntities) && SameCounts(PlacedStacks, other.PlacedStacks);
    }

    public override bool Equals(object? obj)
    {
        return obj is CP14WorkbenchUiRecipesState other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Recipes.Count, PlacedEntities.Count, PlacedStacks.Count, Theme);
    }

    private static bool SameCounts(Dictionary<string, int> a, Dictionary<string, int> b)
    {
        if (a.Count != b.Count)
            return false;

        foreach (var (key, value) in a)
        {
            if (!b.TryGetValue(key, out var other) || other != value)
                return false;
        }

        return true;
    }
    // imperial medieval end
}

[Serializable, NetSerializable]
public readonly struct CP14WorkbenchUiRecipesEntry : IEquatable<CP14WorkbenchUiRecipesEntry>
{
    public readonly ProtoId<CP14WorkbenchRecipePrototype> ProtoId;
    public readonly bool Craftable;

    public CP14WorkbenchUiRecipesEntry(ProtoId<CP14WorkbenchRecipePrototype> protoId, bool craftable)
    {
        ProtoId = protoId;
        Craftable = craftable;
    }

    public override bool Equals(object? obj)
    {
        return obj is CP14WorkbenchUiRecipesEntry other && Equals(other);
    }

    public bool Equals(CP14WorkbenchUiRecipesEntry other)
    {
        return ProtoId.Id == other.ProtoId.Id;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(ProtoId, Craftable);
    }
}
