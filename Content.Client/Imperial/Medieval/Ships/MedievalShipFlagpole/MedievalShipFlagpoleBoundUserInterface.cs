using Content.Client.UserInterface.Controls;
using Content.Shared.Imperial.Medieval.Ships;
using Content.Shared.Imperial.Medieval.Ships.Flagpole;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.Сlient.Imperial.Medieval.Ships.Flagpole;

public sealed class MedievalShipFlagpoleBoundUserInterface : BoundUserInterface
{
    private SimpleRadialMenu? _menu;

    public MedievalShipFlagpoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<SimpleRadialMenu>();
        _menu.SetButtons([
            new RadialMenuActionOption<MedievalShipFlagpoleMenuAction>(SendAction, MedievalShipFlagpoleMenuAction.Black)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(
                    new ResPath("/Textures/Imperial/Medieval/Decor/flagpole.rsi"), "blackflag")),
                ToolTip = Loc.GetString("ship-flagpole-color-black"),
            },
            new RadialMenuActionOption<MedievalShipFlagpoleMenuAction>(SendAction, MedievalShipFlagpoleMenuAction.Red)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(
                    new ResPath("/Textures/Imperial/Medieval/Decor/flagpole.rsi"), "redflag")),
                ToolTip = Loc.GetString("ship-flagpole-color-red"),
            },
            new RadialMenuActionOption<MedievalShipFlagpoleMenuAction>(SendAction, MedievalShipFlagpoleMenuAction.White)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(
                    new ResPath("/Textures/Imperial/Medieval/Decor/flagpole.rsi"), "whiteflag")),
                ToolTip = Loc.GetString("ship-flagpole-color-white"),
            },
            new RadialMenuActionOption<MedievalShipFlagpoleMenuAction>(SendAction, MedievalShipFlagpoleMenuAction.Brown)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(
                    new ResPath("/Textures/Imperial/Medieval/Decor/flagpole.rsi"), "brownflag")),
                ToolTip = Loc.GetString("ship-flagpole-color-brown"),
            },
            new RadialMenuActionOption<MedievalShipFlagpoleMenuAction>(SendAction, MedievalShipFlagpoleMenuAction.Cyan)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(
                    new ResPath("/Textures/Imperial/Medieval/Decor/flagpole.rsi"), "cyanflag")),
                ToolTip = Loc.GetString("ship-flagpole-color-cyan"),
            },
            new RadialMenuActionOption<MedievalShipFlagpoleMenuAction>(SendAction, MedievalShipFlagpoleMenuAction.DarkRed)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(
                    new ResPath("/Textures/Imperial/Medieval/Decor/flagpole.rsi"), "darkredflag")),
                ToolTip = Loc.GetString("ship-flagpole-color-darkred"),
            },
            new RadialMenuActionOption<MedievalShipFlagpoleMenuAction>(SendAction, MedievalShipFlagpoleMenuAction.Gray)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(
                    new ResPath("/Textures/Imperial/Medieval/Decor/flagpole.rsi"), "grayflag")),
                ToolTip = Loc.GetString("ship-flagpole-color-gray"),
            },
            new RadialMenuActionOption<MedievalShipFlagpoleMenuAction>(SendAction, MedievalShipFlagpoleMenuAction.Green)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(
                    new ResPath("/Textures/Imperial/Medieval/Decor/flagpole.rsi"), "greenflag")),
                ToolTip = Loc.GetString("ship-flagpole-color-green"),
            },
            new RadialMenuActionOption<MedievalShipFlagpoleMenuAction>(SendAction, MedievalShipFlagpoleMenuAction.Orange)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(
                    new ResPath("/Textures/Imperial/Medieval/Decor/flagpole.rsi"), "orangeflag")),
                ToolTip = Loc.GetString("ship-flagpole-color-orange"),
            },
            new RadialMenuActionOption<MedievalShipFlagpoleMenuAction>(SendAction, MedievalShipFlagpoleMenuAction.Pink)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(
                    new ResPath("/Textures/Imperial/Medieval/Decor/flagpole.rsi"), "pinkflag")),
                ToolTip = Loc.GetString("ship-flagpole-color-pink"),
            },
            new RadialMenuActionOption<MedievalShipFlagpoleMenuAction>(SendAction, MedievalShipFlagpoleMenuAction.Purple)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(
                    new ResPath("/Textures/Imperial/Medieval/Decor/flagpole.rsi"), "purpleflag")),
                ToolTip = Loc.GetString("ship-flagpole-color-purple"),
            },
            new RadialMenuActionOption<MedievalShipFlagpoleMenuAction>(SendAction, MedievalShipFlagpoleMenuAction.Yellow)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(
                    new ResPath("/Textures/Imperial/Medieval/Decor/flagpole.rsi"), "yellowflag")),
                ToolTip = Loc.GetString("ship-flagpole-color-yellow"),
            },
            new RadialMenuActionOption<MedievalShipFlagpoleMenuAction>(SendAction, MedievalShipFlagpoleMenuAction.Blue)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(
                    new ResPath("/Textures/Imperial/Medieval/Decor/flagpole.rsi"), "blueflag")),
                ToolTip = Loc.GetString("ship-flagpole-color-blue"),
            },
            new RadialMenuActionOption<MedievalShipFlagpoleMenuAction>(SendAction, MedievalShipFlagpoleMenuAction.None)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(
                    new ResPath("/Textures/Imperial/Medieval/Decor/flagpole.rsi"), "transparent")),
                ToolTip = Loc.GetString("ship-flagpole-color-none"),
            }
        ]);
        _menu.OpenOverMouseScreenPosition();
    }

    private void SendAction(MedievalShipFlagpoleMenuAction action)
    {
        SendMessage(new MedievalShipFlagpoleSelectedMessage(action));
    }
}
