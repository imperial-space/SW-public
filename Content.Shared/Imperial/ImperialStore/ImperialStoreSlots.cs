using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.ImperialStore;

/// <summary>
/// imperial medieval - essence slots of a grimoire. A slot is an entry of <see cref="ImperialStoreComponent.Bonuses"/>
/// holding one unit of a school currency, every balance update adds the sum of all slots to the balance.
/// </summary>
public static class ImperialStoreSlots
{
    /// <summary>
    /// How long putting a school into a slot takes.
    /// </summary>
    public const float ChangeSeconds = 3f;

    /// <summary>
    /// Currencies that count as magic schools, only these can be put into a slot. They are the currencies
    /// that have <see cref="ImperialCurrencyPrototype.School"/> set, in the order of <see cref="ImperialSchoolData.Order"/>.
    /// </summary>
    public static List<(string Id, ImperialSchoolData Data)> GetSchools(IPrototypeManager prototype)
    {
        var schools = new List<(string Id, ImperialSchoolData Data)>();

        foreach (var currency in prototype.EnumeratePrototypes<ImperialCurrencyPrototype>())
        {
            if (currency.School is { } data)
                schools.Add((currency.ID, data));
        }

        schools.Sort((a, b) => a.Data.Order.CompareTo(b.Data.Order));
        return schools;
    }

    public static bool TryGetSchool(IPrototypeManager prototype, string? currency, [NotNullWhen(true)] out ImperialSchoolData? data)
    {
        data = null;

        if (currency == null || !prototype.TryIndex<ImperialCurrencyPrototype>(currency, out var proto))
            return false;

        data = proto.School;
        return data != null;
    }

    public static bool IsSchool(IPrototypeManager prototype, string? currency)
    {
        return TryGetSchool(prototype, currency, out _);
    }
}
