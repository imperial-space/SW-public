using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using System.Linq;

namespace Content.Shared.Imperial.XxRaay.FlagSystem;

/// <summary>
/// Вспомогательный класс для работы с фракциями
/// </summary>
public static class FactionHelper
{
    /// <summary>
    /// Определяет фракцию флага по его прототипу
    /// </summary>
    /// <param name="prototypeId">ID прототипа флага</param>
    /// <returns>Название фракции</returns>
    public static string GetFlagFaction(string prototypeId)
    {
        return prototypeId switch
        {
            "ImperialGreenFlag" => "GreenFaction",
            "ImperialYellowFlag" => "YellowFaction",
            "ImperialRedFlag" => "RedFaction",
            "ImperialBlueFlag" => "BlueFaction",
            "ImperialNTFlag" => "NTFaction",
            "ImperialUSSPFlag" => "USSPFaction",
            "ImperialSindiFlag" => "SindiFaction",
            "ImperialWhiteFlag" => "NeutralFaction",
            _ => "NeutralFaction" // По умолчанию
        };
    }

    /// <summary>
    /// Определяет прототип флага по фракции
    /// </summary>
    /// <param name="faction">Название фракции</param>
    /// <returns>ID прототипа флага</returns>
    public static string GetFactionFlagPrototype(string faction)
    {
        return faction switch
        {
            "GreenFaction" => "ImperialGreenFlag",
            "YellowFaction" => "ImperialYellowFlag",
            "RedFaction" => "ImperialRedFlag",
            "BlueFaction" => "ImperialBlueFlag",
            "NTFaction" => "ImperialNTFlag",
            "USSPFaction" => "ImperialUSSPFlag",
            "SindiFaction" => "ImperialSindiFlag",
            _ => "ImperialWhiteFlag" // По умолчанию
        };
    }

    /// <summary>
    /// Определяет ID валюты по фракции
    /// </summary>
    /// <param name="faction">Название фракции</param>
    /// <returns>ID валюты или null</returns>
    public static string? GetCurrencyIdForFaction(string faction)
    {
        return faction switch
        {
            "NTFaction" => "NTFactionPoints",
            "SindiFaction" => "SindiFactionPoints",
            "GreenFaction" => "GreenFactionPoints",
            "YellowFaction" => "YellowFactionPoints",
            "RedFaction" => "RedFactionPoints",
            "BlueFaction" => "BlueFactionPoints",
            "USSPFaction" => "USSPFactionPoints",
            _ => null
        };
    }

    /// <summary>
    /// Маппинг фракций из компонента NpcFactionMember в внутренние названия
    /// </summary>
    /// <param name="factionComponentValue">Значение фракции из компонента</param>
    /// <returns>Внутреннее название фракции</returns>
    public static string MapFactionFromComponent(string factionComponentValue)
    {
        return factionComponentValue switch
        {
            "NanoTrasen" => "NTFaction",
            "Syndicate" => "SindiFaction",
            "GreenFaction" => "GreenFaction",
            "YellowFaction" => "YellowFaction",
            "RedFaction" => "RedFaction",
            "BlueFaction" => "BlueFaction",
            "USSPFaction" => "USSPFaction",
            _ => factionComponentValue // Если не знаем, возвращаем как есть
        };
    }
}
