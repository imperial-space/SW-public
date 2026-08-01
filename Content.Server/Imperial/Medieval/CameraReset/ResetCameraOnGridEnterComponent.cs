namespace Content.Server.Imperial.Medieval.CameraReset;

[RegisterComponent]
public sealed partial class ResetCameraOnGridEnterComponent : Component
{
    [DataField]
    public TimeSpan ResetDelay = TimeSpan.FromSeconds(1);

    public uint TimerGeneration;
}
