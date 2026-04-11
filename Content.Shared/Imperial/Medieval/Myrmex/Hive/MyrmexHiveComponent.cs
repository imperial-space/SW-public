using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared.Myrmex.Hive;

[RegisterComponent]
public sealed partial class MyrmexHiveComponent : Component
{
    [DataField]
    public int MaxAltars = 3;

    [DataField]
    public int MaxLifeSources = 3;

    [DataField]
    public int BaseMaxBuffs = 10;

    [DataField]
    public int MaxBuffs = 10;

    [DataField]
    public float BaseHealthMultiplier = 1.0f;

    [DataField]
    public float HealthMultiplier = 1.0f;

    [DataField]
    public float AltarHealthMultiplierStep = 0.2f;

    [DataField]
    public int ActiveAltars = 0;

    [DataField]
    public int ActiveLifeSources = 0;
}
