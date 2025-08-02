using Robust.Shared.Utility;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Shared.Imperial.Medieval.PlayerCreations.Paintings;

using Content.Shared.Containers.ItemSlots;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CanvasComponent : Component
{
    [DataField, AutoNetworkedField]
    public Color[] Texture = new Color[900];
    public Action? TextureChanged;
}

[Serializable, NetSerializable]
public sealed partial class CanvasTextureChangedEvent : EntityEventArgs
{
    public NetEntity Canvas;
    public Color[] Texture;

    public CanvasTextureChangedEvent(NetEntity canvas, Color[] texture)
    {
        Canvas = canvas;
        Texture = texture;
    }
}
