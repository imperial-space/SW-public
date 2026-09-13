using System;
using Content.Shared.Imperial.Medieval.Calendar;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Localization;
using Robust.Shared.Maths;

namespace Content.Client.Imperial.Medieval.Calendar.Board.Elements;

public sealed class AnnouncementEntry : PanelContainer
{
    public Action<Guid>? OnDelete;

    public AnnouncementEntry(AnnouncementData data, bool canDelete)
    {
        Margin = new Thickness(0, 0, 0, 10);
        HorizontalExpand = true;

        MaxWidth = 1020;

        var vBox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(10)
        };

        var headerBox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true
        };

        var titleBox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true
        };

        var titleLabel = new Label
        {
            Text = data.Title,
            FontColorOverride = Color.White
        };

        var authorLabel = new Label
        {
            Text = $"{data.Author} ({data.CycleTime})",
            FontColorOverride = Color.DarkGray
        };

        titleBox.AddChild(titleLabel);
        titleBox.AddChild(authorLabel);

        var deleteButton = new Button
        {
            Text = Loc.GetString("calendar-board-announcement-delete"),
            StyleClasses = { "Caution" },
            VerticalAlignment = VAlignment.Top,
            Visible = canDelete
        };
        deleteButton.OnPressed += _ => OnDelete?.Invoke(data.Id);

        headerBox.AddChild(titleBox);
        headerBox.AddChild(deleteButton);

        vBox.AddChild(headerBox);

        var descLabel = new RichTextLabel
        {
            HorizontalExpand = true,
            Margin = new Thickness(0, 10, 0, 0)
        };
        descLabel.SetMessage(data.Text);

        vBox.AddChild(descLabel);

        AddChild(vBox);

        PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#1A1A1A"),
            BorderColor = Color.DarkGray,
            BorderThickness = new Thickness(1)
        };
    }
}
