using Content.Shared.DoAfter;

using Robust.Shared.Serialization;


namespace Content.Shared.Imperial.Medieval.Ships.Anchor;

/// <summary>
/// Ивент для поднятия якоря
/// </summary>
[Serializable, NetSerializable]
public sealed partial class UseAnchorEvent : SimpleDoAfterEvent
{
}



