using Content.Shared.Eui;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.PlayerCreations;

[Serializable, NetSerializable]
public sealed class CreationPaintingMessage : IEquatable<CreationPaintingMessage>
{
    public NetEntity Painting { get; }
    public string Name { get; }
    public string Author { get; }
    public NetUserId? SenderUserId { get; }

    public CreationPaintingMessage(NetEntity painting, string name, string author, NetUserId? senderUserId)
    {
        Painting = painting;
        Name = name;
        Author = author;
        SenderUserId = senderUserId;
    }

    public override bool Equals(object? obj) => Equals(obj as CreationPaintingMessage);

    public bool Equals(CreationPaintingMessage? other)
    {
        if (other is null)
            return false;

        return Painting == other.Painting;
    }

    public override int GetHashCode() => Painting.GetHashCode();

    public static bool operator ==(CreationPaintingMessage? left, CreationPaintingMessage? right)
    {
        if (ReferenceEquals(left, right))
            return true;
        if (left is null || right is null)
            return false;
        return left.Equals(right);
    }

    public static bool operator !=(CreationPaintingMessage? left, CreationPaintingMessage? right) => !(left == right);

}


[Serializable, NetSerializable]
public sealed class RequestIncomingCreationPaintingsMessage : EuiMessageBase
{
}

[Serializable, NetSerializable]
public sealed class ResponseIncomingCreationPaintingsMessage : EuiMessageBase
{
    public List<CreationPaintingMessage> Paintings;

    public ResponseIncomingCreationPaintingsMessage(List<CreationPaintingMessage> paintings)
    {
        Paintings = paintings;
    }
}

[Serializable, NetSerializable]
public sealed class NewIncomingCreationPaintingMessage : EuiMessageBase
{
    public CreationPaintingMessage Painting;

    public NewIncomingCreationPaintingMessage(CreationPaintingMessage painting)
    {
        Painting = painting;
    }
}

[Serializable, NetSerializable]
public sealed class RemoveIncomingCreationPaintingMessage : EuiMessageBase
{
    public CreationPaintingMessage Painting;

    public RemoveIncomingCreationPaintingMessage(CreationPaintingMessage painting)
    {
        Painting = painting;
    }
}

[Serializable, NetSerializable]
public sealed class AcceptIncomingCreationPaintingMessage : EuiMessageBase
{
    public CreationPaintingMessage Painting;

    public AcceptIncomingCreationPaintingMessage(CreationPaintingMessage painting)
    {
        Painting = painting;
    }
}

[Serializable]
public sealed class SendCreationPaintingEvent : EntityEventArgs
{
    public NetEntity Painting;
    public string Name;
    public string Author;
    public NetUserId? SenderPlayer;

    public SendCreationPaintingEvent(NetEntity painting, string name, string author, NetUserId? senderPlayer)
    {
        Painting = painting;
        Name = name;
        Author = author;
        SenderPlayer = senderPlayer;
    }
}
