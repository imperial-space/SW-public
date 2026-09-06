namespace Content.Server.Imperial.Medieval.Magic.BindStoreOnEquip;

[RegisterComponent]
public sealed partial class BindStoreOnEquipComponent : Component
{
    [ViewVariables]
    public EntityUid? OwnerUid;
}
