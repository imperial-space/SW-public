using Robust.Shared.Audio;
using Content.Shared.Advertise.Systems;
using Content.Shared.Dataset;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.SpeechInFight;

[RegisterComponent]
public sealed partial class MedievalSpeechInFightComponent : Component
{
    [DataField(required: true)]
    public ProtoId<LocalizedDatasetPrototype> Pack { get; private set; }

    [DataField]
    public bool Enabled = true;

    [DataField]
    public float Chanse = 0.35f;

    [DataField]
    public int TotalAtacksCooldown = 3;

    [DataField]
    public int CurrentAtacksCooldown = 0;
}
