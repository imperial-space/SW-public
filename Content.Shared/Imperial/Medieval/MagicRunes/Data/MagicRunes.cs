using Robust.Shared.Serialization;

//=========================================================================
// MagicRunes.cs
//=========================================================================
// Purpose: Defines magic rune types, symbols, meanings, and rune pairs
// Author: rhailrake, edited by Bladefire5
//=========================================================================

namespace Content.Shared.Imperial.Medieval.MagicRunes.Data;

[Serializable, NetSerializable]
public enum MagicRune : byte
{
    Kael  = 0,
    Ryn   = 1,
    Vel   = 2,
    Oth   = 3,
    Thar  = 4,
    Lun   = 5,
    Seth  = 6,
    Mira  = 7,
}

[Serializable, NetSerializable]
public sealed class MagicRunePair(MagicRune first, MagicRune second)
{
    public MagicRune First = first;
    public MagicRune Second = second;
}

public static class MagicRuneData
{
    private static readonly Dictionary<MagicRune, string> RuneSymbols = new()
    {
        { MagicRune.Kael, "☥" },
        { MagicRune.Ryn, "☯" },
        { MagicRune.Vel, "⚶" },
        { MagicRune.Oth, "☊" },
        { MagicRune.Thar, "⚚" },
        { MagicRune.Lun, "⛧" },
        { MagicRune.Seth, "☡" },
        { MagicRune.Mira, "⚵" }
    };

    private static readonly Dictionary<MagicRune, string> RuneMeanings = new()
    {
        { MagicRune.Kael, "Огонь / разрушение" },
        { MagicRune.Ryn, "Вода / адаптация" },
        { MagicRune.Vel, "Тень / наведение" },
        { MagicRune.Oth, "Иллюзия / магия" },
        { MagicRune.Thar, "Пустота / ничто" },
        { MagicRune.Lun, "Тьма / звезда смерти" },
        { MagicRune.Seth, "Яд / проклятие" },
        { MagicRune.Mira, "Защита / барьер" }
    };

    // Every unique two-rune combination. With 8 runes this gives 28 pairs.
    public static List<MagicRunePair> GetAllPairs()
    {
        var runes = Enum.GetValues<MagicRune>();
        var pairs = new List<MagicRunePair>();

        for (var i = 0; i < runes.Length; i++)
        {
            for (var j = i + 1; j < runes.Length; j++)
            {
                pairs.Add(new MagicRunePair(runes[i], runes[j]));
            }
        }

        return pairs;
    }

    public static string GetSymbol(MagicRune rune) =>
        RuneSymbols.GetValueOrDefault(rune, "?");

    public static string GetMeaning(MagicRune rune) =>
        RuneMeanings.GetValueOrDefault(rune, "???");

    public static string GetPairDisplay(MagicRunePair pair) =>
        $"{GetSymbol(pair.First)} + {GetSymbol(pair.Second)}";
}
