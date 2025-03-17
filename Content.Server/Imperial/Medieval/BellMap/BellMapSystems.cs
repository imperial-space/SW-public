using Content.Server.Mind;
using Content.Server.Popups;
using Content.Shared.Coordinates;
using Content.Shared.Imperial.BellMap.Components;
using Content.Shared.Interaction;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Server.Imperial.BellMap.Systems;

public sealed partial class BellMapSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BellMapComponent, InteractHandEvent>(OnUse);
    }
    public void OnUse(EntityUid uid, BellMapComponent comp, InteractHandEvent ev)
    {
        var mapId = _transformSystem.GetMapId(ev.Target);
        var all = _mindSystem.GetAliveHumans();
        foreach (var mind in all)
        {
            if (mind.Comp.CurrentEntity == null) return;
            var uidd = mind.Comp.CurrentEntity.Value;
            if (mapId == _transformSystem.GetMapId(uidd.ToCoordinates()))
            {
                _popupSystem.PopupEntity(Loc.GetString(comp.Locale), uidd, uidd);
                _audioSystem.PlayEntity(comp.Sound, Filter.Entities(uidd), uidd, false);
            }
        }
    }
}
