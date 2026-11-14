using System.Numerics;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.Boss;

[Prototype("bossTelegraphStyle")]
public sealed partial class BossTelegraphStylePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public Color DangerColor = Color.FromHex("#FF862F");                        // Danger color
    [DataField]
    public Color SentencingColor = Color.White;
    [DataField]
    public Color SafeColor = Color.FromHex("#57EAB8");                          // Safe color
    [DataField]
    public Color BlueColor = Color.FromHex("#4DBDFF");                          // Blue phase color
    [DataField]
    public Color PurpleColor = Color.FromHex("#BC66FF");                        // Purple phase color
    [DataField]
    public Color TankColor = Color.FromHex("#48DEFF");
    [DataField]
    public Color TankBusterColor = Color.FromHex("#FF4026");
    [DataField]
    public float TankBusterBobDistance = 0.5f;
    [DataField]
    public Vector2 TankBusterMarkerSize = new(0.5f, 0.5f);
    [DataField]
    public Color SpreadColor = Color.FromHex("#FF668D");
    [DataField]
    public Color StackColor = Color.FromHex("#F7D968");
    [DataField]
    public Color SpreadHighlightColor = Color.FromHex("#4DBDFF");
    [DataField]
    public Color StackHighlightColor = Color.FromHex("#FFAA33");
    [DataField]
    public Vector2 MarkerOffset = new(0, 1.3f);                                 // Marker position above the player
    [DataField]
    public Vector2? TankMarkerOffset;
    [DataField]
    public Vector2 TankMarkerSize = new(0.35f, 0.35f);
    [DataField]
    public Vector2? SpreadMarkerOffset;
    [DataField]
    public Vector2 MarkerSize = new(0.6f, 0.6f);                                // Marker size in tiles
    [DataField]
    public ResPath? DangerTexture;
    [DataField]
    public int WarningOverflowTiles;                                            // Warning extension beyond the floor
    [DataField]
    public bool WarningAreaOutline;
    [DataField]
    public ResPath? SentencingTexture;
    [DataField]
    public ResPath? SafeTexture;
    [DataField]
    public ResPath? BlueTexture;
    [DataField]
    public ResPath? PurpleTexture;
    [DataField]
    public ResPath? MovementArrowTexture;
    [DataField]
    public ResPath? MovementDestinationTexture;
    [DataField]
    public ResPath? TankTexture;
    [DataField]
    public Vector2i? TankTextureContentSize;
    [DataField]
    public ResPath? TankBusterTexture;
    [DataField]
    public ResPath? ExecutionEyesTexture;
    [DataField]
    public ResPath? ExecutionTipTexture;
    [DataField]
    public ResPath? ExecutionTrailTexture;
    [DataField]
    public ResPath? ExecutionWarningTexture;
    [DataField]
    public ResPath? SpreadTexture;
    [DataField]
    public ResPath? SpreadSoulTexture;
    [DataField]
    public ResPath? StackTexture;
    [DataField]
    public ResPath? CommunionLanternTexture;
    [DataField]
    public ResPath? CommunionFogTexture;
    [DataField]
    public ResPath? CommunionEdgeTexture;
    [DataField]
    public ResPath? VigilMarkTexture;
    [DataField]
    public ResPath? VigilAshTexture;
    [DataField]
    public ResPath? EmitterTexture;
    [DataField]
    public bool CleaveLight;
    [DataField]
    public ResPath? CleaveLightTexture;
    [DataField]
    public Vector2 CleaveLightAnchorPixels;
    [DataField]
    public float CleaveLightRadiusPixels = 1f;
    [DataField]
    public float CleaveLightScale = 1f;
    [DataField]
    public float CleaveLightMovementScale = 1f;
    [DataField]
    public float CleaveLightWidth = 3f;
    [DataField]
    public float CleaveLightArc = BossCleaveVisuals.ArcRadians;
    [DataField]
    public float CleaveLightWaveWidth = 0.24f;
    [DataField]
    public float CleaveLightWaveArc = BossCleaveVisuals.ArcRadians;
    [DataField]
    public Color CleaveLightBody = Color.FromHex("#8837DA");
    [DataField]
    public Color CleaveLightCore = Color.FromHex("#F2D9FF");
    [DataField]
    public ResPath? ConeRiverNorthTexture;
    [DataField]
    public ResPath? ConeRiverEastTexture;
    [DataField]
    public ResPath? ConeRiverSouthTexture;
    [DataField]
    public ResPath? ConeRiverWestTexture;
    [DataField]
    public BossBeamAnimationData? ConstellationBeam;
    [DataField]
    public ResPath? CrossRiverTexture;
    [DataField]
    public ResPath? DivideRiverTexture;
    [DataField]
    public ResPath? CrossRiverNorthTexture;
    [DataField]
    public ResPath? CrossRiverEastTexture;
    [DataField]
    public ResPath? CrossRiverSouthTexture;
    [DataField]
    public ResPath? CrossRiverWestTexture;
    [DataField]
    public ResPath? SmallCrystalWarningTexture;
    [DataField]
    public ResPath? SmallCrystalShatterTexture;
    [DataField]
    public ResPath? LargeCrystalWarningTexture;
    [DataField]
    public ResPath? LargeCrystalShatterTexture;
    [DataField]
    public ResPath? CloseSweepTexture;
    [DataField]
    public Vector2 CloseSweepAnchorPixels;
    [DataField]
    public float CloseSweepRadiusPixels = 1f;
    [DataField]
    public ResPath? DonutSweepTexture;
    [DataField]
    public Vector2 DonutSweepAnchorPixels;
    [DataField]
    public float DonutSweepRadiusPixels = 1f;
}
