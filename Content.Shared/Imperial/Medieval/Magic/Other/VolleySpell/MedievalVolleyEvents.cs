using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Magic.VolleySpell;


/// <summary>
/// Sent by the client when the caster selects a volley spell, to summon its projectiles
/// </summary>
[Serializable, NetSerializable]
public sealed class MedievalVolleySummonEvent : EntityEventArgs
{
    public NetEntity Action;
}

/// <summary>
/// Sent by the client when the caster stops aiming a volley spell, to remove the projectiles that were not launched
/// </summary>
[Serializable, NetSerializable]
public sealed class MedievalVolleyDismissEvent : EntityEventArgs
{
    public NetEntity Action;
}
