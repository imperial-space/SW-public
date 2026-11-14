using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Content.Shared.Imperial.Medieval.Boss;

namespace Content.Server.Imperial.Medieval.Boss;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class BossArenaMusicComponent : Component
{
    public EntityUid Boss;
    public EntityUid? Stream;
    public EntityUid? PendingLoop;
    public ProtoId<BossSoundtrackPrototype>? SelectedSoundtrack;
    public ProtoId<BossSoundtrackPrototype>? ActiveSoundtrack;
    public SoundSpecifier Loop = default!;
    public BossMusicPhase Phase;
    public TimeSpan LoopLength;
    [AutoPausedField]
    public TimeSpan LoopStartedAt;
    [AutoPausedField]
    public TimeSpan? TransitionAt;
}
