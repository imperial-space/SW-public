using Robust.Shared.Audio;

namespace Content.Server.Cult.Components;


[RegisterComponent]
public sealed partial class CultBrushComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField, AutoNetworkedField]
    public SoundSpecifier Sound { get; set; } = new SoundPathSpecifier("/Audio/Imperial/Medieval/Cult/chalk2.ogg")
    {
        Params = AudioParams.Default.WithVolume(-2f).WithVariation(0.015f),
    };

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField, AutoNetworkedField]
    public SoundSpecifier DelSound { get; set; } = new SoundPathSpecifier("/Audio/Imperial/Medieval/Cult/erasing2.ogg")
    {
        Params = AudioParams.Default.WithVolume(-2f).WithVariation(0.015f),
    };

}
