using System.Linq;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;

namespace Content.Shared.Imperial.Medieval.MagicDungeon;

public abstract partial class SharedDungeonPortalSystem
{
    private void InitEscape()
    {
        SubscribeLocalEvent<MobStateComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(EntityUid uid, MobStateComponent mobStateComponent, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        var map = Transform(uid).MapID;
        var query = EntityQueryEnumerator<DungeonPortalFrameComponent>();
        Entity<DungeonPortalFrameComponent>? portalFrame = null;

        while (query.MoveNext(out var portal, out var comp))
        {
            if (map == comp.DungeonMap)
            {
                portalFrame = (portal, comp);
                break;
            }
        }

        if (portalFrame == null || CheckPortalShouldBeDeactivated(portalFrame.Value))
            return;

        DeactivatePortal(portalFrame.Value);
    }
}
