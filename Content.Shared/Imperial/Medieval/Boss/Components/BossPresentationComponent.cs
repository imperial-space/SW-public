using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;
using Robust.Shared.Serialization.TypeSerializers.Implementations;

namespace Content.Shared.Imperial.Medieval.Boss;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause(Dirty = true)]
public sealed partial class BossPresentationComponent : Component
{
    [DataField, AutoNetworkedField]
    public SpriteSpecifier.Rsi? Body;                                          // optional body art, the base sprite works without it
    [DataField, AutoNetworkedField]
    public SpriteSpecifier.Rsi? CastEffect;
    [DataField, AutoNetworkedField]
    public SpriteSpecifier.Rsi? PreparedCastEffect;
    [DataField, AutoNetworkedField]
    public float CastEffectScale = 1;                                           // Cast effect size
    [DataField, AutoNetworkedField]
    public Color CastEffectColor = Color.White;                                 // Cast effect color
    [DataField, AutoNetworkedField]
    public Color PreparedCastEffectColor = Color.White;                         // Prepared effect color
    [AutoNetworkedField]
    public bool Hidden;
    [DataField, AutoNetworkedField]
    public float BobSpeedMultiplier = 0.5f;
    [DataField, AutoNetworkedField]
    public bool AnimateLight = true;
    [DataField]
    public float LightEnergy = 3;
    [DataField]
    public float LightFadeDuration = 0.75f;                                     // Light fade time in seconds
    [AutoNetworkedField]
    public float LightFrom = 3;
    [AutoNetworkedField]
    public float LightTo = 3;
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan LightStart;
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan LightEnd;
    public SpriteSpecifier.Rsi? GetCastEffect(bool preparing)
    {
        var effect = preparing ? PreparedCastEffect : CastEffect;
        return effect == null ? null : new SpriteSpecifier.Rsi(SpriteSpecifierSerializer.TextureRoot / effect.RsiPath, effect.RsiState);
    }

    public static float BobOffset(TimeSpan time, float distance = 0.2f) => distance * 0.5f * (1f - MathF.Cos((float)time.TotalSeconds * 4f));
}
