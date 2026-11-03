using System;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Calendar;

[Serializable, NetSerializable]
public sealed class AnnouncementData
{
    public Guid Id;
    public string Title = string.Empty;
    public string Author = string.Empty;
    public NetEntity? AuthorId;
    public NetUserId? AuthorUserId;
    public string Text = string.Empty;
    public string CycleTime = string.Empty;
    public string? AdminInfo;
}

[Serializable, NetSerializable]
public sealed class CalendarBoardCreateAnnouncementMessage : BoundUserInterfaceMessage
{
    public string Title;
    public string Text;
    public string Author;

    public CalendarBoardCreateAnnouncementMessage(string title, string text, string author)
    {
        Title = title;
        Text = text;
        Author = author;
    }
}

[Serializable, NetSerializable]
public sealed class CalendarBoardDeleteAnnouncementMessage : BoundUserInterfaceMessage
{
    public Guid Id;
    public CalendarBoardDeleteAnnouncementMessage(Guid id)
    {
        Id = id;
    }
}
