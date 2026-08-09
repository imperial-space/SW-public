using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using System.Numerics;

namespace Content.Shared.Imperial.Medieval.FactionFlag.Prototypes;

[Prototype]
public sealed class MedievalFactionMapPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public SpriteSpecifier NeutralPointIcon = default!;

    [DataField(required: true)]
    public Dictionary<ProtoId<MedievalFactionPrototype>, SpriteSpecifier> FactionPointIcons = [];

    [DataField]
    public SpriteSpecifier MapTexture = new SpriteSpecifier.Texture(new("/Textures/Imperial/Medieval/Misc/map_tabletop/tabletopRU.png"));

    [DataField]
    public Vector2 MapSize = new(1600, 1600);
}
