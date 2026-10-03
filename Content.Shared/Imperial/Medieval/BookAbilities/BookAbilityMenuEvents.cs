using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.BookAbilities;

public sealed partial class BookOpenAbilitiesMenuEvent : InstantActionEvent;
public sealed partial class BookDiceCheatActionEvent : InstantActionEvent;
public sealed partial class BookExtractReagentActionEvent : InstantActionEvent;
public sealed partial class BookPorterActionEvent : EntityTargetActionEvent;
public sealed partial class BookPackWorkbenchActionEvent : EntityTargetActionEvent;
public sealed partial class BookTrapDisarmActionEvent : EntityTargetActionEvent;
public sealed partial class BookTransplantActionEvent : EntityTargetActionEvent;
public sealed partial class BookTamingActionEvent : EntityTargetActionEvent;
public sealed partial class BookCancelTamingActionEvent : InstantActionEvent;
public sealed partial class BookSpellScribingActionEvent : EntityTargetActionEvent;

/// <summary>An option prepared by the server for one particular ability use.</summary>
[Serializable, NetSerializable]
public sealed class BookAbilityChoice(string id, string label)
{
    public string Id = id;
    public string Label = label;
}

/// <summary>Only the performer receives this; its request identifies server-held target context.</summary>
[Serializable, NetSerializable]
public sealed class BookAbilityChoicesEvent(int requestId, string title, List<BookAbilityChoice> options) : EntityEventArgs
{
    public int RequestId = requestId;
    public string Title = title;
    public List<BookAbilityChoice> Options = options;
}

/// <summary>The server must revalidate the performer, request and current ability conditions.</summary>
[Serializable, NetSerializable]
public sealed class BookAbilityChoiceSelectedEvent(int requestId, string choiceId) : EntityEventArgs
{
    public int RequestId = requestId;
    /// <summary>An empty choice cancels the pending request.</summary>
    public string ChoiceId = choiceId;
}
