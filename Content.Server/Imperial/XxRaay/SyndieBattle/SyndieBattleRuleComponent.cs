using Content.Server.GameTicking.Rules.Components;

namespace Content.Server.Imperial.XxRaay.SyndieBattle;

/// <summary>
/// Компонент для правила игры SyndieBattle
/// </summary>
[RegisterComponent]
public sealed partial class SyndieBattleRuleComponent : Component
{
    /// <summary>
    /// Выдавать ли аплинк предателям
    /// </summary>
    [DataField]
    public bool GiveUplink = true;

    /// <summary>
    /// Выдавать ли кодовые слова предателям
    /// </summary>
    [DataField]
    public bool GiveCodewords = true;

    /// <summary>
    /// Выдавать ли брифинг предателям
    /// </summary>
    [DataField]
    public bool GiveBriefing = true;

    /// <summary>
    /// Активно ли правило в данный момент
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public bool Active;

    /// <summary>
    /// Список предательских разумов
    /// </summary>
    public readonly List<EntityUid> TraitorMinds = new();
}
