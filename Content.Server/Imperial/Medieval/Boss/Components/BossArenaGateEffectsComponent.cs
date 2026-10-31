using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Boss;

[RegisterComponent]
public sealed partial class BossArenaGateEffectsComponent : Component
{
    [DataField]
    public EntProtoId FlameLight = "BossArenaGateFlameLight";

    [DataField]
    public Vector2 LeftFlameOffset = new(-1f, 0.95f);

    [DataField]
    public Vector2 RightFlameOffset = new(1f, 0.95f);

    [DataField]
    public EntProtoId ConeLight = "BossArenaGateConeLight";

    [DataField]
    public Vector2 ConeOffset = new(0, -0.45f);

    [DataField]
    public EntProtoId? OpeningEffect = null;

    [DataField]
    public Vector2 OpeningEffectOffset = new(0, 0.6f);

    [DataField]
    public SoundSpecifier? OpenSound = new SoundPathSpecifier("/Audio/Imperial/Medieval/Teleport_app.ogg");

    [ViewVariables]
    public EntityUid? LeftFlameEntity;

    [ViewVariables]
    public EntityUid? RightFlameEntity;

    [ViewVariables]
    public EntityUid? ConeEntity;
}
