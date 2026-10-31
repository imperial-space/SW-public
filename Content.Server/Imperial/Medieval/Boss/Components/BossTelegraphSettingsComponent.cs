using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.Prototypes;
using Robust.Shared.Audio;

namespace Content.Server.Imperial.Medieval.Boss;

[RegisterComponent]
public sealed partial class BossTelegraphSettingsComponent : Component
{
    [DataField]
    public bool FixedCardinals;                                                  // keeps patterns aligned to the arena grid
    [DataField]
    public EntProtoId TelegraphPrototype = "BossEncounterTelegraph";
    [DataField]
    public EntProtoId TankMarkerPrototype = "BossEncounterTankMarker";
    [DataField]
    public EntProtoId TankBusterMarkerPrototype = "BossEncounterTankBusterMarker";
    [DataField]
    public EntProtoId SpreadMarkerPrototype = "BossEncounterSpreadMarker";
    [DataField]
    public EntProtoId StackMarkerPrototype = "BossEncounterStackMarker";
    [DataField]
    public EntProtoId ImpactPrototype = "BossEncounterImpact";
    [DataField]
    public EntProtoId? TileImpactPrototype;
    [DataField]
    public EntProtoId? BlueEmitterPrototype;
    [DataField]
    public EntProtoId? PurpleEmitterPrototype;
    [DataField]
    public ProtoId<BossTelegraphStylePrototype> Style = "DefaultBossTelegraph";  // fallback warning and impact style
    [DataField]
    public string CaptionPrefix = "Boss_Encounter_";
    [DataField]
    public Dictionary<BossTelegraphPattern, string> CastNames = new();
    [DataField]
    public Dictionary<BossTelegraphPattern, string> PreparedCastNames = new();
    [DataField]
    public SoundSpecifier? SlashSound;
    [DataField]
    public SoundSpecifier? TankBusterImpactSound;
    [DataField]
    public float? TankBusterImpactSoundVolume;
    [DataField]
    public bool ExecutionLine;
    [DataField]
    public float ExecutionLineWidth = BossExecutionLineVisuals.Width;
    [DataField]
    public EntProtoId? AutoProjectilePrototype;
    [DataField]
    public SoundSpecifier? MagicImpactSound;
    [DataField]
    public SoundSpecifier? CrystalImpactSound;
    [DataField]
    public float ImpactSoundVolume;
    [DataField]
    public EntProtoId? FallingCrystalEffect;
    [DataField]
    public EntProtoId? CrystalShatterEffect;
}
