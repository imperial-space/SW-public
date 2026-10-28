using System.Numerics;
using Content.Shared.Imperial.Medieval.Weapons;
using Content.Shared.Imperial.Medieval.Items;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Imperial;

public static class MeleeSwingTestData
{
    public static readonly Vector2 Grip = new(37, 43);
    public static readonly Vector2 Blade = new(16.5f, 19.5f);
    public static readonly Vector2 ShaftTop = new(41, 17);
    public const float Extension = 12;
    public static readonly ItemNameColorComponent NameStyle = new()
    {
        Color = Color.FromHex("#FF8B23"), PulseColor = Color.FromHex("#FFB74F"), PulseSeconds = 3.2f
    };
    public static readonly MeleeSwingStylePrototype Style = new()
    {
        Texture = new ResPath("/Textures/Imperial/Medieval/Melee/revenant_scythe_swipe.rsi"),
        LeftState = "left-to-right", RightState = "right-to-left", FrameCount = 35, FrameSeconds = 0.02f,
        ReferenceSwingSeconds = 0.45f, PlaybackSpeed = 1.15f, Arc = Angle.FromDegrees(210),
        AnchorPixels = new(112, 112), ReferenceRadiusPixels = 96, ReachScale = 1.2823217627252834f,
        HandOffset = new(-0.015625f, -0.125f), WeaponTexture = new ResPath("/Textures/Imperial/Medieval/Melee/revenant_scythe.rsi"),
        WeaponState = "item", WeaponPivotPixels = new(35.17531378895271f, 54.860460371807406f), WeaponAngle = Angle.FromDegrees(117.84037725505172), FadeStart = 0.405f,
        Motion = new() { new() { Time = 0, Progress = 0 }, new() { Time = 0.135f, Progress = 0.5f }, new() { Time = 0.405f, Progress = 1 } },
        WieldedFront = "wielded-front", WieldedRear = "wielded-rear", LeftFront = "carry-front-left", LeftRear = "carry-rear-left",
        RightFront = "carry-front-right", RightRear = "carry-rear-right"
    };
}
