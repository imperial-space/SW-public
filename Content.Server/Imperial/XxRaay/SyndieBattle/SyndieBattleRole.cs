using Content.Shared.Roles;

namespace Content.Server.Imperial.XxRaay.SyndieBattle;

/// <summary>
/// Компонент роли для игроков, участвующих в режиме SyndieBattle
/// </summary>
[RegisterComponent]
public sealed partial class SyndieBattleRole : BaseMindRoleComponent
{
    /// <summary>
    /// Имя роли
    /// </summary>
    public string Name => Loc.GetString("syndie-battle-role-name");
}