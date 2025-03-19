
using Robust.Shared.Audio;

namespace Content.Shared.Imperial.BellMap.Components;

[RegisterComponent]
public sealed partial class BellMapComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Announcements/attention.ogg")
    {
        Params = AudioParams.Default.WithVolume(5f)
    };
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public LocId Locale = "bell-map-popup";
}
