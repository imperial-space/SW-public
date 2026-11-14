using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Localization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.Boss;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause(Dirty = true)]
public sealed partial class BossCastBarComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public bool Casting;
    [AutoNetworkedField] public bool Preparing;
    [AutoNetworkedField] public bool Visible = true;

    [AutoNetworkedField] public EntityUid? Anchor;

    [ViewVariables, AutoNetworkedField]
    public string CastName = string.Empty;

    [ViewVariables, AutoNetworkedField]
    public string CastNameKey = string.Empty;

    public string GetLocalizedCastName()
        => string.IsNullOrEmpty(CastNameKey) ? CastName : Loc.GetString(CastNameKey);

    public void SetCastName(string nameOrKey)
    {
        CastNameKey = Loc.TryGetString(nameOrKey, out var localized) ? nameOrKey : string.Empty;
        CastName = string.IsNullOrEmpty(CastNameKey) ? nameOrKey : localized ?? nameOrKey;
    }

    public void ClearCastName()
    {
        Preparing = false;
        CastName = string.Empty;
        CastNameKey = string.Empty;
    }

    [ViewVariables, AutoNetworkedField, AutoPausedField]
    public TimeSpan StartTime;

    [ViewVariables, AutoNetworkedField, AutoPausedField]
    public TimeSpan EndTime;

    [DataField, AutoNetworkedField]
    public Vector2 Size = new(96f, 6f);

    [DataField, AutoNetworkedField]
    public Vector2 Offset = new(0f, 90f);

    [DataField, AutoNetworkedField]
    public float BorderWidth = 1f;

    [DataField, AutoNetworkedField]
    public Color BackgroundColor = new(0.08f, 0.04f, 0.08f, 0.95f);

    [DataField, AutoNetworkedField]
    public Color FillColor = new(0.85f, 0.65f, 0.2f);

    [DataField, AutoNetworkedField]
    public Color BorderColor = new(0.7f, 0.57f, 0.34f);

    [DataField, AutoNetworkedField]
    public Color TextColor = Color.White;

    [DataField, AutoNetworkedField]
    public Color TextShadowColor = new(0f, 0f, 0f, 0.95f);

    [DataField, AutoNetworkedField]
    public ResPath? BackgroundTexture;

    [DataField, AutoNetworkedField]
    public ResPath? FillTexture;

    [DataField, AutoNetworkedField]
    public ResPath? FrameTexture;
}
