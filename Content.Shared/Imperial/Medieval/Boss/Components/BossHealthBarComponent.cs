using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.Boss;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BossHealthBarComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Active = true;

    [DataField, AutoNetworkedField]
    public float CurrentHp = 100f;

    [DataField, AutoNetworkedField]
    public float MaxHp = 100f;

    [DataField, AutoNetworkedField]
    public Color Color = Color.Red;

    [DataField, AutoNetworkedField]
    public bool ShowOverhead;

    [ViewVariables, AutoNetworkedField]
    public bool Visible = true;

    [DataField, AutoNetworkedField]
    public Vector2 Size = new(96f, 8f);

    [DataField, AutoNetworkedField]
    public Vector2 Offset = new(0f, 104f);

    [DataField, AutoNetworkedField]
    public float BorderWidth = 1f;

    [DataField, AutoNetworkedField]
    public Color BackgroundColor = new(0.08f, 0.04f, 0.08f, 0.95f);

    [DataField, AutoNetworkedField]
    public Color BorderColor = new(0.7f, 0.57f, 0.34f);

    [DataField, AutoNetworkedField]
    public ResPath? BackgroundTexture;

    [DataField, AutoNetworkedField]
    public ResPath? FillTexture;

    [DataField, AutoNetworkedField]
    public ResPath? FrameTexture;
}
