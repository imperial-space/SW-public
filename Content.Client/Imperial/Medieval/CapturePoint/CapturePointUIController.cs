using Content.Client.Gameplay;
using Content.Shared.Imperial.Medieval.CapturePoint;
using Robust.Client.UserInterface.Controllers;

namespace Content.Client.Imperial.Medieval.CapturePoint;

public sealed class CapturePointUIController : UIController
{
    public void OpenMessengerWindow(CapturePointMessengerEvent ev)
    {
        var window = new CapturePointMessengerWindow(ev);
        window.OpenCentered();
    }

    public void ShowResultPopup(CapturePointResultEvent ev)
    {
        var window = new CapturePointResultWindow(ev);
        window.OpenCentered();
    }
}
