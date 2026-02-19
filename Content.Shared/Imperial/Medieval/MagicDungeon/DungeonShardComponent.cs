using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.MagicDungeon;

[RegisterComponent, NetworkedComponent]
public sealed partial class DungeonShardComponent : Component
{
    [ViewVariables]
    public Vector2i SizeModifier = (0, 0);

    [DataField]
    public int ColorInt;
}
