using Content.Shared.DetailExaminable;

namespace Content.Shared.Imperial.Medieval.Flavors
{
    public abstract class SharedFlavorManager
    {
        [Dependency] protected readonly IEntityManager EntityManager = default!;
        public bool ImageAllowed(int width, int height)
        {
            if (width != 256 || height != 256)
                return false;

            return true;
        }
        public abstract bool TryExamine(EntityUid user, Entity<DetailExaminableComponent> ent);
    }
}
