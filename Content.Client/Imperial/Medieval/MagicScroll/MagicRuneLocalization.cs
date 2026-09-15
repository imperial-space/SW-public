using Content.Shared.Imperial.Medieval.MagicRunes.Data;

namespace Content.Client.Imperial.Medieval.MagicScroll;

internal static class MagicRuneLocalization
{
    public static string GetMeaning(MagicRune rune) =>
        MagicRuneData.GetMeaningLocKey(rune) is { } key ? Loc.GetString(key) : "???";

    public static string GetPairMeaning(MagicRunePair pair) =>
        Loc.GetString("magic-rune-pair-meaning",
            ("first", GetMeaning(pair.First)),
            ("second", GetMeaning(pair.Second)));
}
