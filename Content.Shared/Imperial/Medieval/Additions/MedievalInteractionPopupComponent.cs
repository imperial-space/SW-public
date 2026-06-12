using Robust.Shared.Audio;

namespace Content.Shared.Interaction.Components;

[RegisterComponent]
public sealed partial class MedievalInteractionPopupComponent : Component
{
    [DataField("allowed")]
    public HashSet<string> AllowedUserIds = new();

    [DataField("popup")]
    public string? PopupMessage;

    [DataField("sound")]
    public SoundSpecifier? Sound;

    [DataField("delay")]
    public TimeSpan InteractDelay = TimeSpan.FromSeconds(1.0);

    public TimeSpan LastInteractTime;
}
