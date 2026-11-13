using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Medieval.Magic;

/// <summary>
/// The server requests a completed cast's cursor sample; the client returns the sample used by its preview.
/// The response supplies only aim. The pending server cast supplies the projectile and all gameplay parameters.
/// </summary>
[Serializable, NetSerializable]
public sealed class MedievalProjectileTargetEvent : EntityEventArgs
{
    public NetEntity Action;
    public GameTick InputTick;
    public NetCoordinates? Coordinates;
}
