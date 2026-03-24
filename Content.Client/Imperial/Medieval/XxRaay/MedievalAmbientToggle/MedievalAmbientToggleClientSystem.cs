using Content.Shared.Imperial.Medieval.XxRaay.MedievalAmbientToggle;

namespace Content.Client.Imperial.Medieval.XxRaay.MedievalAmbientToggle;

/// <summary>
/// Receives medieval ambient on/off state from server and exposes it for ambient music selection.
/// </summary>
public sealed class MedievalAmbientToggleClientSystem : EntitySystem
{
    private bool _medievalAmbientEnabled = true;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<MedievalAmbientToggledEvent>(OnMedievalAmbientToggled);
    }

    /// <summary>
    /// True when medieval ambient music is allowed to play; false when disabled by admin.
    /// </summary>
    public bool IsMedievalAmbientEnabled => _medievalAmbientEnabled;

    private void OnMedievalAmbientToggled(MedievalAmbientToggledEvent ev, EntitySessionEventArgs args)
    {
        _medievalAmbientEnabled = ev.Enabled;
    }
}
