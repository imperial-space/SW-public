using System.Numerics;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class SpawAtPosition : BossAttackAction
{
    [DataField(required: true)]
    public string[] Prototypes;

    [DataField]
    public int RandomDistance = 0;

    [DataField]
    public int SpawnCount = 0;

    public override void Execute(EntityUid boss, IEnumerable<EntityUid> targets, IEntityManager entMan)
    {
        var random = IoCManager.Resolve<IRobustRandom>();
        var map = entMan.System<MapSystem>();

        foreach (var target in targets)
        {
            var xform = entMan.GetComponent<TransformComponent>(target);
            var targetCoords = xform.Coordinates;
            for (var i = 0; i < SpawnCount; i++)
            {
                for (var j = 0; i < 10; j++)
                {
                    var result = targetCoords + new EntityCoordinates(xform.Coordinates.EntityId, random.Next(-RandomDistance, RandomDistance), random.Next(-RandomDistance, RandomDistance));
                    if (xform.GridUid.HasValue && RandomDistance != 0 && map.AnchoredEntityCount(xform.GridUid.Value, entMan.GetComponent<MapGridComponent>(xform.GridUid.Value), (Vector2i)result.Position) == 0)
                    {
                        targetCoords = result;
                        break;
                    }
                }
                entMan.SpawnAtPosition(random.Pick(Prototypes), targetCoords);
                var comp = entMan.EnsureComponent<BossAttackComponent>(target);
                comp.Boss = boss;
            }
        }
    }
}
