using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.BookAbilities;

/// <summary>The target and performer are kept on the server, never accepted from the reply.</summary>
[Serializable, NetSerializable]
public sealed class BookVoicePromptEvent(int requestId, string targetName, int maxLength) : EntityEventArgs
{
    public int RequestId = requestId;
    public string TargetName = targetName;
    public int MaxLength = maxLength;
}

/// <summary>An empty phrase cancels this single-use request.</summary>
[Serializable, NetSerializable]
public sealed class BookVoiceSubmittedEvent(int requestId, string text) : EntityEventArgs
{
    public int RequestId = requestId;
    public string Text = text;
}
