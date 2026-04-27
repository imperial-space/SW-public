using Content.Shared.Fishing.Enums;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Fishing.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishingRodComponent : Component
{
    [DataField]
    public EntProtoId BobberPrototype = "FishingBobber";

    [DataField]
    public EntProtoId RiverWaterPrototype = "FloorWaterEntity";

    [DataField]
    public EntProtoId RiverWaterNoSoundPrototype = "FloorWaterEntityNoSound";

    [DataField]
    public EntProtoId SeaWaterPrototype = "MedievalWaterDeepDarkEntity";

    [DataField]
    public SoundSpecifier CastFloatSplashSound = new SoundPathSpecifier("/Audio/Imperial/Medieval/Fishing/cast_float_splash.ogg");

    [DataField]
    public SoundSpecifier MinigameBitePullSound = new SoundPathSpecifier("/Audio/Imperial/Medieval/Fishing/minigame_bite_pull.ogg");

    [DataField]
    public SoundSpecifier MinigameFishOutSound = new SoundPathSpecifier("/Audio/Imperial/Medieval/Fishing/minigame_fish_out.ogg");

    [DataField]
    public int BaseFishingChancePercent = 1;

    [DataField]
    public float InitialDoAfterTime = 1f;

    [DataField]
    public float LoopDoAfterTime = 2f;

    [DataField]
    public float BaseAsyncTImeStep = 1f;

    [DataField]
    public float AfterInteractDistanceThreshold = 2f;

    [DataField]
    public float DoAfterDistanceThreshold = 3f;

    [DataField]
    public Dictionary<EntProtoId, float> FishWeights = new() // weight-based choose of the fish being fished
    {
        ["FishPlotva"] = 1f,
        ["FishLin"] = 0.5f,
        ["FishSterlyad"] = 0.2f,
        ["FishOkun"] = 1f,
        ["FishZherekh"] = 0.5f,
        ["FishSom"] = 0.2f,
        ["FishKefal"] = 1f,
        ["FishKambalaTurbo"] = 0.5f,
        ["FishGoldSpar"] = 0.2f,
        ["FishStavrida"] = 1f,
        ["FishZubatka"] = 0.5f,
        ["FishTuna"] = 0.2f,
    };

    [DataField, AutoNetworkedField]
    public int Level = 1;

    [DataField, AutoNetworkedField]
    public int IncrementChance = 1;

    [DataField, AutoNetworkedField]
    public int MaxChance = 5;

    [DataField, AutoNetworkedField]
    public FishingLocationType LastClickedWater = FishingLocationType.River;

    [DataField, AutoNetworkedField]
    public EntProtoId? CurrentFish;

    [DataField, AutoNetworkedField]
    public EntityUid? Bait;

    [DataField]
    public float Tension = 0f;

    [DataField]
    public float Progress = 0f;

    [DataField]
    public float TensionAcceleration = 0f;

    [DataField]
    public bool MinigameActive = false;

    [DataField]
    public bool IsHoldingLmb = false;

    [DataField]
    public EntityUid? MinigameUser;

    [DataField]
    public EntityUid? CurrentBobber;

    [DataField]
    public float MinigameMovementThreshold = 0.3f;

    public EntityCoordinates MinigameStartPosition = EntityCoordinates.Invalid;
}
