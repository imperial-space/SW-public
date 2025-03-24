using Content.Server.Mind;
using Content.Server.Popups;
using Content.Shared.Coordinates;
using Content.Shared.Imperial.BellMap.Components;
using Content.Shared.Interaction;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.BellMap.Systems;

public sealed partial class BellMapSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BellMapComponent, InteractHandEvent>(OnUse);
    }
    public void OnUse(EntityUid uid, BellMapComponent comp, InteractHandEvent ev)
    {
        // Check if the bell has been rung recently.
        if (comp.LastRingTime != null)
        {
            if (_gameTiming.CurTime - comp.LastRingTime < TimeSpan.FromSeconds(comp.Cooldown))
            {
                // Optionally, display a popup to the user indicating they must wait.
                _popupSystem.PopupEntity(Loc.GetString("bell-map-too-soon"), ev.User, ev.User);
                return; // Prevent ringing the bell.
            }
        }

        var mapId = _transformSystem.GetMapId(ev.Target);
        var all = _mindSystem.GetAliveHumans();
        foreach (var mind in all)
        {
            if (mind.Comp.CurrentEntity == null) continue; // Skip if mind doesn't have an entity.
            var uidd = mind.Comp.CurrentEntity.Value;
            if (mapId == _transformSystem.GetMapId(uidd.ToCoordinates()))
            {
                _popupSystem.PopupEntity(Loc.GetString(comp.Locale), uidd, uidd);
                _audioSystem.PlayEntity(comp.Sound, Filter.Entities(uidd), uidd, false);
            }
        }

        // Update the last ring time for the bell.
        comp.LastRingTime = _gameTiming.CurTime;
    }
}
