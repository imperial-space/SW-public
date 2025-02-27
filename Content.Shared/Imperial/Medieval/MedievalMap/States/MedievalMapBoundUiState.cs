using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.MedievalMap;


[Serializable, NetSerializable]
public sealed class MedievalMapBoundUiState : BoundUserInterfaceState
{
    public Vector2 Size = new Vector2(790, 790);

    public string MapTexturePath = "";
}
