using Robust.Shared.GameStates;
using Robust.Shared.Utility;


namespace Content.Shared.Imperial.DarkMage.Follower;

[RegisterComponent, NetworkedComponent]
public sealed partial class MedievalFollowerComponent : Component
{
    public int Layer;
    public SpriteSpecifier Sprite = new SpriteSpecifier.Rsi(new ResPath("Imperial/Medieval/Mobs/dark_mage.rsi"), "flame");
}
