namespace Content.Server.Imperial.Medieval.SoulboundTrigger;
/// <summary>
/// Компонент, который привязываезывается к UID сущности, что наденет этот предмет.
/// Если другой пользователь попытается надеть предмет, сработает триггер с ключом KeyOut.
/// </summary>
/// <remarks>
/// Если одеть непривязанную одежду на кого-то, то она не привяжется.
/// Но если одеть привязанную одежду, то всё сработает нормально.
/// </remarks>
[RegisterComponent]
public sealed partial class SoulboundTriggerComponent : Component
{
    [ViewVariables]
    public EntityUid? User;

    [DataField]
    public string? KeyOut = "soulboundtrigger";
}
