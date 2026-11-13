using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Imperial.Medieval.Magic.SpellCastEffect;

[RegisterComponent, NetworkedComponent]
public sealed partial class SpellCastEffectComponent : Component
{
    [DataField(required: true)]

    public EntProtoId EffectProto = string.Empty;

    /// <summary>
    /// Optional second effect that appears in front of the caster (for example a growing fireball).
    /// </summary>
    [DataField]
    public EntProtoId? ForwardEffectProto;

    /// <summary>
    /// Distance in tiles in front of the caster where <see cref="ForwardEffectProto"/> appears.
    /// </summary>
    [DataField]
    public float ForwardEffectOffset = 1f;

    /// <summary>
    /// The forward effect of the cast in progress, removed when the cast ends or fails
    /// </summary>
    [ViewVariables]
    public EntityUid? ForwardEffect;
}
