namespace Content.Server.Imperial.Medieval.Courier;

[RegisterComponent, ComponentProtoName("Courier")]
public sealed partial class CourierComponent : Component
{
    [DataField]
    public int Balance;

    [DataField]
    public int DeliveryPoints;

    [DataField]
    public int FreeMailsCount = 3;
}
