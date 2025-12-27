using System.Linq;
using Robust.Shared.Containers;
using Robust.Shared.Random;
using Content.Shared.Imperial.Medieval.FullContainer.Components;
using Content.Shared.Storage;

namespace Content.Server.Imperial.Medieval.FullContainer.Systems;

public sealed partial class FullContainerSystems : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FullContainerComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (!_containerSystem.GetAllContainers(uid).Any()) return;
            var cont = _containerSystem.GetAllContainers(uid).First();
            var sp = EntitySpawnCollection.GetSpawns(component.Fullable);
            var needsp = sp[_random.Next(0, sp.Count)]; // minValue - included, maxValue - excluded
            var ent = Spawn(needsp);
            if (_containerSystem.CanInsert(ent, cont))
            {
                _containerSystem.Insert(ent, cont);
                continue;
            }
            QueueDel(ent);
            RemComp<FullContainerComponent>(uid);
        }
    }
}
