using Robust.Shared.Audio;

namespace Content.Shared.Imperial.Medieval.Ships.Flagpole;

[RegisterComponent]
public sealed partial class MedievalShipFlagpoleComponent : Component
{
    [DataField]
    public float DoAfterTime = 5f;

    [DataField]
    public float Scale = 0.5f;

    [DataField]
    public bool IsZoomingScale = false;

    [DataField]
    public string RsiPath = "/Textures/Imperial/Medieval/Decor/flagpole.rsi";

    [DataField]
    public SoundPathSpecifier RaiseFlagSound = new SoundPathSpecifier("/Audio/Imperial/Medieval/Ships/sail_open.ogg");

    [DataField]
    public SoundPathSpecifier DownFlagSound = new SoundPathSpecifier("/Audio/Imperial/Medieval/Ships/sail_close.ogg");

    public EntityUid? User;
}
