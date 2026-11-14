using System.Numerics;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.Boss;

[RegisterComponent]
public sealed partial class BossArenaContentDefinitionComponent : Component
{
    [DataField(required: true)] public string ArenaId = string.Empty;
    [DataField(required: true)] public string Role = string.Empty;               // content slot, such as brazier or coffer
    [DataField] public int Priority;                                    // higher wins
    [DataField] public EntProtoId? Prototype;
    [DataField] public ResPath? SceneryFile;                                     // map file for the outer scenery grid
    [DataField] public List<BossArenaDecoration> Decorations = new();
    [DataField] public List<BossArenaFloorTile> Tiles = new();
    [DataField] public ComponentRegistry DefeatComponents = new();               // components added when this boss is defeated
}

[DataDefinition]
public sealed partial class BossArenaDecoration
{
    [DataField(required: true)] public EntProtoId Prototype;
    [DataField] public Vector2 Position;                                // arena x, y
    [DataField] public bool Scenery;                                    // outer grid
}

[DataDefinition]
public sealed partial class BossArenaFloorTile
{
    [DataField(required: true)] public string Tile = string.Empty;
    [DataField] public Vector2i Position;
}

[RegisterComponent]
public sealed partial class BossArenaContentStateComponent : Component
{
    [DataField] public EntityUid? Scenery;
    [DataField] public HashSet<string> Roles = new();                            // content slots already spawned in this arena
}
