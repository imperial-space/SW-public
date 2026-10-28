using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Boss;

[Serializable, NetSerializable]
public enum BossArenaSpectatorUiKey : byte { Key }

[Serializable, NetSerializable]
public sealed class BossArenaSpectatorUiState(NetEntity camera) : BoundUserInterfaceState
{
    public NetEntity Camera { get; } = camera;
}
