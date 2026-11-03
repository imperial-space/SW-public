using System.Diagnostics.CodeAnalysis;
using Content.Client.Guidebook.Richtext;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Medieval.Alchemy;

[UsedImplicitly]
public sealed class GuideLocalizedText : BoxContainer, IDocumentTag
{
    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        control = null;
        if (!args.TryGetValue("Key", out var key))
            return false;

        var text = Loc.GetString(key);
        if (args.TryGetValue("Style", out var style))
        {
            AddChild(new Label { Text = text, StyleClasses = { style } });
        }
        else
        {
            var label = new RichTextLabel
            {
                HorizontalExpand = true,
                Margin = new Thickness(0, 0, 0, 15),
            };
            var message = new FormattedMessage();
            message.PushColor(Color.White);
            message.AddMarkupOrThrow(text);
            message.Pop();
            label.SetMessage(message);
            AddChild(label);
        }

        HorizontalExpand = true;
        control = this;
        return true;
    }
}
