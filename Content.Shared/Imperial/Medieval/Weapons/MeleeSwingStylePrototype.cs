using System.Numerics;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.Weapons;

[Prototype("meleeSwingStyle")]
public sealed partial class MeleeSwingStylePrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public ResPath Texture;
    [DataField] public string LeftState = "swing";
    [DataField] public string RightState = "swing";
    [DataField] public int FrameCount = 1;
    [DataField] public float FrameSeconds = 0.1f;                                // time per animation frame
    [DataField] public float ReferenceSwingSeconds = 0.45f;                      // base timing used to sync the effect
    [DataField] public float PlaybackSpeed = 1;
    [DataField] public Angle Arc = Angle.FromDegrees(180);
    [DataField] public Vector2? AnchorPixels;                                    // effect pivot in pixels
    [DataField] public float ReferenceRadiusPixels = 32;
    [DataField] public float ReachScale = 1;
    [DataField] public Vector2 HandOffset;                                       // swing center relative to the player
    [DataField] public ResPath? WeaponTexture;
    [DataField] public string WeaponState = "icon";
    [DataField] public Vector2 WeaponPivotPixels;                                // weapon grip point in pixels
    [DataField] public Angle WeaponAngle;
    [DataField] public float WeaponScale = 1;
    [DataField] public float FadeStart = 0.45f;
    [DataField] public List<MeleeSwingKeyFrame> Motion = new();
    [DataField] public string? WieldedFront;
    [DataField] public string? WieldedRear;
    [DataField] public string? LeftFront;
    [DataField] public string? LeftRear;
    [DataField] public string? RightFront;
    [DataField] public string? RightRear;

    public float Duration => FrameCount * FrameSeconds;
}

[DataDefinition]
public sealed partial class MeleeSwingKeyFrame
{
    [DataField] public float Time;
    [DataField] public float Progress;
}
