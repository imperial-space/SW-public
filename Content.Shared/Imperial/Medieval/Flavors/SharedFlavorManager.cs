using Content.Shared.DetailExaminable;
using Content.Shared.Imperial.ICCVar;
using Robust.Shared.Configuration;

namespace Content.Shared.Imperial.Medieval.Flavors
{
    public abstract class SharedFlavorManager
    {
        [Dependency] protected readonly IEntityManager EntityManager = default!;
        [Dependency] private readonly IConfigurationManager _cfg = default!;
        public bool ImageAllowed(int width, int height)
        {
            if (width != _cfg.GetCVar(ICCVars.SetWidthFlavorImages) || height != _cfg.GetCVar(ICCVars.SetHeightFlavorImages))
                return false;

            return true;
        }
        public abstract bool TryExamine(EntityUid user, Entity<DetailExaminableComponent> ent);
    }
}
