using System.Numerics;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.Boss;

[DataDefinition]
public sealed partial class BossBeamAnimationData
{
    [DataField(required: true)]
    public ResPath Sprite;

    [DataField]
    public Vector2 AnchorPixels = new(30, 32);

    [DataField]
    public float CoreLengthPixels = 196;

    [DataField]
    public float PixelsPerTile = 16;
}
