using Robust.Shared.Serialization;

namespace Content.Shared.Forged;

[Serializable, NetSerializable]
public sealed class ForgedExecuteAbilityEvent : EntityEventArgs
{
    public NetEntity ForgedUid;
    public NetEntity ModuleUid;
    public string AbilityId = string.Empty;
}
