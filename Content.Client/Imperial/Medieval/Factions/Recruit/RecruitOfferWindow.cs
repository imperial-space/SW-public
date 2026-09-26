using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Localization;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.Imperial.Medieval.Factions.Recruit;

public sealed class RecruitOfferWindow : DefaultWindow
{
    public readonly Button DeclineButton;
    public readonly Button AcceptButton;

    public RecruitOfferWindow(string recruiterName, string factionName)
    {
        Title = Loc.GetString("recruit-offer-window-title");

        Contents.AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Children =
            {
                new Label
                {
                    Text = Loc.GetString("recruit-offer-window-prompt", ("recruiter", recruiterName), ("faction", factionName)),
                },
                new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                    Align = AlignMode.Center,
                    Children =
                    {
                        (AcceptButton = new Button
                        {
                            Text = Loc.GetString("recruit-offer-window-accept-button"),
                        }),

                        new Control
                        {
                            MinSize = new Vector2(20, 0)
                        },

                        (DeclineButton = new Button
                        {
                            Text = Loc.GetString("recruit-offer-window-decline-button"),
                        })
                    }
                },
            }
        });
    }
}
