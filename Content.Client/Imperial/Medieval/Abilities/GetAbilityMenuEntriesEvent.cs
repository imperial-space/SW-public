using Robust.Shared.Utility;

namespace Content.Client.Imperial.Medieval.Abilities;

/// <summary>
/// Raised on the local player when the menu is refreshed. Providers may add passive entries
/// or replace the presentation of their own active entries; action ownership is checked by the menu.
/// Names and descriptions are already localized.
/// </summary>
public sealed class GetAbilityMenuEntriesEvent : EntityEventArgs
{
    public readonly Dictionary<EntityUid, AbilityMenuEntry> Actions = new();
    public readonly List<AbilityMenuEntry> Passives = new();
}

public sealed record AbilityMenuEntry(string Name, string Description, SpriteSpecifier? Icon = null, int Order = 0);
