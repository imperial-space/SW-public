using Content.Shared.DoAfter;
using Robust.Shared.Audio;

namespace Content.Server.Imperial.Medieval.Boss;

[RegisterComponent]
public sealed partial class BossSummoningTriggerComponent : Component
{
    public bool Activating;
    public DoAfterId? Cast;
    [DataField] public TimeSpan CastDuration = TimeSpan.FromSeconds(5);
    [DataField] public SoundSpecifier BreakSound = new SoundCollectionSpecifier("GlassBreak");
    [DataField] public Robust.Shared.Prototypes.EntProtoId? SpawnEffect;
    [DataField] public List<Robust.Shared.Prototypes.EntProtoId> AdditionalSpawnEffects = new();
    [DataField] public bool BreakToSummon;
    [DataField] public float Health = 300;
    [DataField] public float CrackedFraction = 0.5f;
    [DataField] public SoundSpecifier HitSound = new SoundCollectionSpecifier("GlassCrack");
    [DataField] public float SpawnCameraKick;
}
