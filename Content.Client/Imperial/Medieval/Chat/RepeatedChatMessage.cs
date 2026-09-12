using Content.Shared.Chat;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Medieval.Chat;

/// <summary>
///     imperial medieval - a chat line folded with its repeats, showing "xN" instead of
///     printing the same text again. Client-side only.
/// </summary>
public sealed class RepeatedChatMessage
{
    /// <summary>
    ///     Index of this line in the chat OutputPanel. Invalid once the panel is cleared.
    /// </summary>
    public readonly int Index;

    /// <summary>
    ///     The formatted line without any "xN" suffix.
    /// </summary>
    public readonly FormattedMessage Original;

    /// <summary>
    ///     Raw unwrapped text, used for comparing messages.
    /// </summary>
    public readonly string Text;

    public readonly ChatChannel Channel;

    public int Count = 1;

    public RepeatedChatMessage(int index, FormattedMessage original, string text, ChatChannel channel)
    {
        Index = index;
        Original = original;
        Text = text;
        Channel = channel;
    }
}
