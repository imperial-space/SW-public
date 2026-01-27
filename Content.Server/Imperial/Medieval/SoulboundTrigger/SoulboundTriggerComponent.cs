namespace Content.Server.Imperial.Medieval.SoulboundTrigger;
/// <summary>
/// Компонент, который привязываезывается к UID сущности, что наденет этот предмет.
/// Если другой пользователь попытается его надеть, сработает триггер с ключом KeyOut.
/// </summary>
/// <remarks>
/// Если одеть "пустую" одежду через агост, то одежда не привяжется ни к кому. Так работало на локалке.
/// Но если одеть "привязанную" одежду через агост, то вызовется триггер, если это был не владелец.
///
/// Одевание одежды другим игроком так же привязывает её к новому владельцу.
/// </remarks>
[RegisterComponent]
public sealed partial class SoulboundTriggerComponent : Component
{
    [ViewVariables]
    public EntityUid? User;

    [DataField]
    public string? KeyOut = "soulboundtrigger";
}
