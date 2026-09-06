using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Nocturn.Components;

[RegisterComponent]
public sealed partial class AncientNocturneMindConnectionComponent : Component
{
    public HashSet<EntityUid> Tralls = new();

    public EntityUid? ActiveEntity;

    public bool HasConvertedTrall;
}

[RegisterComponent]
public sealed partial class AncientNocturneTrallMindConnectionComponent : Component
{
    public EntityUid Master;

    public bool IsMasterRelay;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AncientNocturneMindChatComponent : Component
{
    [DataField, AutoNetworkedField]
    public string ChatPrefix = ":е";

    [DataField, AutoNetworkedField]
    public string AlternateChatPrefix = ":o";

    [DataField, AutoNetworkedField]
    public Color ChatColor = Color.FromHex("#A060E8");
}

[Serializable, NetSerializable]
public sealed class AncientNocturneMindChatMessageEvent : EntityEventArgs
{
    public string Message = string.Empty;

    public AncientNocturneMindChatMessageEvent(string message)
    {
        Message = message;
    }

    public AncientNocturneMindChatMessageEvent()
    {
    }
}

[Serializable, NetSerializable]
public sealed class AncientNocturneConversionNotificationEvent : EntityEventArgs
{
    public AncientNocturneConversionNotification Type;

    public AncientNocturneConversionNotificationEvent(AncientNocturneConversionNotification type)
    {
        Type = type;
    }

    public AncientNocturneConversionNotificationEvent()
    {
    }
}

[Serializable, NetSerializable]
public enum AncientNocturneConversionNotification : byte
{
    FirstTrall,
    Converted
}
