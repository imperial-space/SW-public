using Content.Server.Popups;
using Content.Shared.Imperial.BellMap.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Imperial.BellMap.Systems;

public sealed partial class BellMapSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BellMapComponent, InteractHandEvent>(OnUse);
    }
    public void OnUse(EntityUid uid, BellMapComponent comp, InteractHandEvent ev)
    {
        var mapId = _transformSystem.GetMapId(uid);
        var query = EntityQueryEnumerator<TransformComponent>();
        while (query.MoveNext(out var uidd, out var _))
        {
            if (mapId == _transformSystem.GetMapId(uidd))
            {
                _popupSystem.PopupEntity(Loc.GetString("bell-map-popup"), uidd, PopupType.MediumCaution);
                _audioSystem.PlayPvs(comp.Sound, uidd, comp.Params);
            }
        }
    }
}
