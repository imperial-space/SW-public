using Content.Shared.Myrmex.Hive;
using Robust.Shared.GameObjects;

namespace Content.Server.Myrmex.Structures;

public sealed partial class MyrmexHiveSystem : EntitySystem
{
    public bool TryGetHive(out Entity<MyrmexHiveComponent>? hive)
    {
        var query = EntityQueryEnumerator<MyrmexHiveComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            hive = (uid, comp);
            return true;
        }

        hive = null;
        return false;
    }
}
